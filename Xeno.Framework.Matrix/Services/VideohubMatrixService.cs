using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xeno.Framework.Matrix.Core;

namespace Xeno.Framework.Matrix.Services
{
    /// <summary>
    /// Drives a Blackmagic Videohub over TCP/9990 using the Blackmagic Videohub Ethernet Protocol v2.x.
    /// Persistent mode keeps a live reader to receive asynchronous state updates.
    /// PerCommand mode opens a fresh connection per call with best-effort ACK handling.
    /// </summary>
    public sealed class VideohubMatrixService : MatrixServiceBase
    {
        public const int DefaultPort = 9990;

        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly object _pendingLock = new object();
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private Task _readerTask;
        private TaskCompletionSource<bool> _pendingAck;
        private bool _disposed;

        public int ConnectTimeoutMs { get; set; } = 2500;
        public int AckTimeoutMs { get; set; } = 2500;
        public int InitialDumpWaitMs { get; set; } = 800;

        public VideohubMatrixService() : base(new DeviceInfo
        {
            Kind = DeviceKind.BlackmagicVideohub,
            Model = "Videohub",
            InputCount = 16,
            OutputCount = 16,
            IsPresent = false
        })
        {
            Port = DefaultPort;
        }

        // ----------------------- IMatrixService -----------------------

        public override async Task ConnectAsync()
        {
            EnsureNotDisposed();

            if (Mode == ConnectionMode.PerCommand)
            {
                await RefreshAsync().ConfigureAwait(false);
                return;
            }

            var sw = Stopwatch.StartNew();
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                CloseSocket();
                _cts = new CancellationTokenSource();
                await OpenPersistentAsync(_cts.Token).ConfigureAwait(false);
                StartReader(_cts.Token);
            }
            finally
            {
                _writeLock.Release();
            }
            sw.Stop();
            Log(LogDirection.Info, string.Format("ConnectAsync complete in {0}ms (reader started)", sw.ElapsedMilliseconds));
        }

        public override async Task DisconnectAsync()
        {
            try { _cts?.Cancel(); } catch { }
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                CloseSocket();
                SetConnected(false, "disconnected");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public override async Task RouteAsync(int input, int output)
        {
            ValidateInput(input);
            ValidateOutput(output);
            bool ok = await SendBlockAsync("VIDEO OUTPUT ROUTING",
                new[] { string.Format("{0} {1}", output - 1, input - 1) }).ConfigureAwait(false);
            if (!ok) Log(LogDirection.Error, "Videohub: NAK / timeout");
            if (Mode == ConnectionMode.PerCommand)
            {
                ApplyRoute(input, output);
                RaiseStateUpdated();
            }
        }

        public override async Task RouteAllAsync(int input)
        {
            ValidateInput(input);
            int outputs = Math.Max(1, Device.OutputCount);
            var lines = new List<string>(outputs);
            for (int o = 1; o <= outputs; o++)
                lines.Add(string.Format("{0} {1}", o - 1, input - 1));
            bool ok = await SendBlockAsync("VIDEO OUTPUT ROUTING", lines).ConfigureAwait(false);
            if (!ok) Log(LogDirection.Error, "Videohub: NAK / timeout");
            if (Mode == ConnectionMode.PerCommand)
            {
                for (int o = 1; o <= outputs; o++) ApplyRoute(input, o);
                RaiseStateUpdated();
            }
        }

        public override async Task PtpAsync()
        {
            int n = Math.Min(Math.Max(1, Device.InputCount), Math.Max(1, Device.OutputCount));
            var lines = new List<string>(n);
            for (int i = 1; i <= n; i++)
                lines.Add(string.Format("{0} {1}", i - 1, i - 1));
            bool ok = await SendBlockAsync("VIDEO OUTPUT ROUTING", lines).ConfigureAwait(false);
            if (!ok) Log(LogDirection.Error, "Videohub: NAK / timeout");
            if (Mode == ConnectionMode.PerCommand)
            {
                for (int i = 1; i <= n; i++) ApplyRoute(i, i);
                RaiseStateUpdated();
            }
        }

        public override async Task RouteBatchAsync(IDictionary<int, int> outputToInput)
        {
            if (outputToInput == null || outputToInput.Count == 0) return;
            var lines = new List<string>(outputToInput.Count);
            foreach (var kvp in outputToInput)
            {
                lines.Add(string.Format("{0} {1}", kvp.Key - 1, kvp.Value - 1));
            }
            bool ok = await SendBlockAsync("VIDEO OUTPUT ROUTING", lines).ConfigureAwait(false);
            if (!ok) Log(LogDirection.Error, "Videohub batch: NAK / timeout");
            if (Mode == ConnectionMode.PerCommand)
            {
                foreach (var kvp in outputToInput) ApplyRoute(kvp.Value, kvp.Key);
                RaiseStateUpdated();
            }
        }

        public override async Task RefreshAsync()
        {
            if (Mode == ConnectionMode.Persistent)
            {
                // Request a routing dump; reader parses the incoming block.
                await SendBlockAsync("VIDEO OUTPUT ROUTING", null, requestDump: true).ConfigureAwait(false);
                return;
            }

            // PerCommand: open a fresh connection, let it stream the initial dump, parse it, close.
            using (var client = new TcpClient())
            {
                await ConnectWithTimeoutAsync(client).ConfigureAwait(false);
                using (var stream = client.GetStream())
                {
                    await ReadAndProcessAsync(stream, InitialDumpWaitMs, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cts?.Cancel(); } catch { }
            CloseSocket();
            try { _writeLock.Dispose(); } catch { }
            base.Dispose();
        }

        // ----------------------- connection -----------------------

        private async Task OpenPersistentAsync(CancellationToken token)
        {
            var client = new TcpClient();
            try
            {
                await ConnectWithTimeoutAsync(client).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try { client.Close(); } catch { }
                Log(LogDirection.Error, "connect failed: " + ex.Message);
                throw;
            }
            _client = client;
            _stream = client.GetStream();
            SetConnected(true, string.Format("{0}:{1}", Host, Port));
        }

        private async Task ConnectWithTimeoutAsync(TcpClient client)
        {
            var connectTask = client.ConnectAsync(Host, Port);
            var delayTask = Task.Delay(ConnectTimeoutMs);
            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completed == delayTask)
            {
                try { client.Close(); } catch { }
                throw new TimeoutException(string.Format("connect timeout to {0}:{1}", Host, Port));
            }
            await connectTask.ConfigureAwait(false);
        }

        private void CloseSocket()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        private void StartReader(CancellationToken token)
        {
            var stream = _stream;
            _readerTask = Task.Run(async () =>
            {
                try
                {
                    await ReadAndProcessAsync(stream, -1, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log(LogDirection.Error, "reader failed: " + ex.Message);
                }
                finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        SetConnected(false, "reader ended");
                        if (AutoReconnect) _ = TryReconnectAsync(token);
                    }
                }
            });
        }

        private async Task TryReconnectAsync(CancellationToken token)
        {
            SetReconnecting(true);
            try
            {
                int[] delays = new int[] { 500, 1000, 2000, 5000, 5000 };
                for (int i = 0; i < delays.Length; i++)
                {
                    if (token.IsCancellationRequested || !AutoReconnect) return;
                    try
                    {
                        Log(LogDirection.Info, string.Format("reconnecting in {0}ms (attempt {1})...", delays[i], i + 1));
                        await Task.Delay(delays[i], token).ConfigureAwait(false);
                        await _writeLock.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            CloseSocket();
                            await OpenPersistentAsync(token).ConfigureAwait(false);
                            StartReader(token);
                            return;
                        }
                        finally { _writeLock.Release(); }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex) { Log(LogDirection.Error, "reconnect failed: " + ex.Message); }
                }
                Log(LogDirection.Error, "reconnect attempts exhausted");
            }
            finally
            {
                SetReconnecting(false);
            }
        }

        // ----------------------- reader / parser -----------------------

        /// <summary>
        /// Reads lines from <paramref name="stream"/> and processes blocks. If <paramref name="quietWindowMs"/>
        /// is positive, returns after that many ms of silence once at least one block was seen (used by
        /// PerCommand to consume the initial dump and exit). If -1, runs until cancelled or stream ends.
        /// </summary>
        private async Task ReadAndProcessAsync(NetworkStream stream, int quietWindowMs, CancellationToken token)
        {
            if (stream == null) return;

            var buf = new byte[4096];
            var sb = new StringBuilder();
            var blockLines = new List<string>();
            string blockHeader = null;
            bool inBlock = false;
            bool anyBlock = false;
            DateTime lastData = DateTime.UtcNow;

            while (true)
            {
                if (token.IsCancellationRequested) return;
                if (quietWindowMs > 0 && anyBlock && !inBlock &&
                    (DateTime.UtcNow - lastData).TotalMilliseconds >= quietWindowMs)
                {
                    return;
                }

                if (!stream.DataAvailable)
                {
                    await Task.Delay(20).ConfigureAwait(false);
                    if (quietWindowMs > 0 && (DateTime.UtcNow - lastData).TotalMilliseconds >= quietWindowMs * 3)
                    {
                        return; // extended silence; bail out to avoid hanging PerCommand flow
                    }
                    continue;
                }

                int n;
                try { n = await stream.ReadAsync(buf, 0, buf.Length, token).ConfigureAwait(false); }
                catch (IOException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (OperationCanceledException) { return; }
                if (n <= 0) return;

                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                lastData = DateTime.UtcNow;

                int idx;
                while ((idx = IndexOfNewline(sb)) >= 0)
                {
                    var line = sb.ToString(0, idx).TrimEnd('\r');
                    sb.Remove(0, idx + 1);

                    if (!inBlock)
                    {
                        if (line.Length == 0) continue;
                        blockHeader = line.TrimEnd(':').Trim();
                        blockLines.Clear();
                        inBlock = true;
                        // ACK / NAK are single-line blocks but still terminated by blank line.
                    }
                    else
                    {
                        if (line.Length == 0)
                        {
                            ProcessBlock(blockHeader, blockLines);
                            inBlock = false;
                            blockHeader = null;
                            blockLines.Clear();
                            anyBlock = true;
                        }
                        else
                        {
                            blockLines.Add(line);
                        }
                    }
                }
            }
        }

        private static int IndexOfNewline(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length; i++) if (sb[i] == '\n') return i;
            return -1;
        }

        private void ProcessBlock(string header, List<string> lines)
        {
            if (string.IsNullOrEmpty(header)) return;

            var sw = Stopwatch.StartNew();
            Log(LogDirection.Rx, BuildBlockLog(header, lines));

            string key = header.ToUpperInvariant();
            switch (key)
            {
                case "ACK":
                    CompletePending(true);
                    break;
                case "NAK":
                    CompletePending(false);
                    break;
                case "PROTOCOL PREAMBLE":
                    break;
                case "VIDEOHUB DEVICE":
                    ParseDeviceBlock(lines);
                    break;
                case "VIDEO OUTPUT ROUTING":
                    ParseRoutingBlock(lines);
                    break;
                case "INPUT LABELS":
                case "OUTPUT LABELS":
                case "MONITORING OUTPUT LABELS":
                case "SERIAL PORT LABELS":
                case "FRAME LABELS":
                case "VIDEO OUTPUT LOCKS":
                case "MONITORING OUTPUT LOCKS":
                case "SERIAL PORT LOCKS":
                case "PROCESSING UNIT LOCKS":
                case "FRAME BUFFER LOCKS":
                case "VIDEO MONITORING OUTPUT ROUTING":
                case "SERIAL PORT ROUTING":
                case "PROCESSING UNIT ROUTING":
                case "FRAME BUFFER ROUTING":
                case "END PRELUDE":
                case "CONFIGURATION":
                    break; // ignored for matrix switching UI
            }
            sw.Stop();
            if (sw.ElapsedMilliseconds >= 2)
            {
                Log(LogDirection.Info, string.Format("block '{0}' processed in {1}ms", header, sw.ElapsedMilliseconds));
            }
        }

        private static string BuildBlockLog(string header, List<string> lines)
        {
            if (lines == null || lines.Count == 0) return header;
            if (lines.Count == 1) return header + " :: " + lines[0];
            return header + " (" + lines.Count + " lines)";
        }

        private void ParseDeviceBlock(List<string> lines)
        {
            bool present = false;
            string model = null;
            int inputs = -1, outputs = -1;
            foreach (var l in lines)
            {
                int colon = l.IndexOf(':');
                if (colon < 0) continue;
                var k = l.Substring(0, colon).Trim().ToLowerInvariant();
                var v = l.Substring(colon + 1).Trim();
                switch (k)
                {
                    case "device present": present = v.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case "model name": model = v; break;
                    case "video inputs": int.TryParse(v, out inputs); break;
                    case "video outputs": int.TryParse(v, out outputs); break;
                }
            }
            UpdateDeviceInfo(d =>
            {
                d.IsPresent = present;
                if (!string.IsNullOrEmpty(model)) d.Model = model;
                if (inputs > 0)
                {
                    d.InputCount = UserMaxInputs > 0 ? Math.Max(UserMaxInputs, inputs) : inputs;
                }
                if (outputs > 0)
                {
                    d.OutputCount = UserMaxOutputs > 0 ? Math.Max(UserMaxOutputs, outputs) : outputs;
                }
            });
        }

        private void ParseRoutingBlock(List<string> lines)
        {
            var updates = new Dictionary<int, int>();
            foreach (var l in lines)
            {
                var parts = l.Split(' ');
                if (parts.Length < 2) continue;
                int outIdx, inIdx;
                if (int.TryParse(parts[0], out outIdx) && int.TryParse(parts[1], out inIdx))
                {
                    if (outIdx < 0 || inIdx < 0) continue;
                    updates[outIdx + 1] = inIdx + 1;
                }
            }
            if (updates.Count > 0)
            {
                ApplyRoutes(updates);
                RaiseStateUpdated();
            }
        }

        private void CompletePending(bool ack)
        {
            TaskCompletionSource<bool> tcs;
            lock (_pendingLock) { tcs = _pendingAck; _pendingAck = null; }
            if (tcs != null) tcs.TrySetResult(ack);
        }

        // ----------------------- send helpers -----------------------

        private async Task<bool> SendBlockAsync(string header, IEnumerable<string> bodyLines, bool requestDump = false)
        {
            EnsureNotDisposed();

            if (Mode == ConnectionMode.Persistent)
                return await SendBlockPersistentAsync(header, bodyLines, requestDump).ConfigureAwait(false);
            else
                return await SendBlockPerCommandAsync(header, bodyLines, requestDump).ConfigureAwait(false);
        }

        private async Task<bool> SendBlockPersistentAsync(string header, IEnumerable<string> bodyLines, bool requestDump)
        {
            if (!IsConnected || _stream == null)
                await OpenPersistentAsync(_cts?.Token ?? CancellationToken.None).ConfigureAwait(false);

            var tcs = new TaskCompletionSource<bool>();
            lock (_pendingLock) { _pendingAck = tcs; }

            string message = BuildBlock(header, bodyLines, requestDump);
            var bytes = Encoding.ASCII.GetBytes(message);

            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                Log(LogDirection.Tx, message.TrimEnd());
                try
                {
                    await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    await _stream.FlushAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log(LogDirection.Error, "send failed: " + ex.Message);
                    SetConnected(false, "send failed");
                    CloseSocket();
                    lock (_pendingLock) { if (_pendingAck == tcs) _pendingAck = null; }
                    return false;
                }
            }
            finally
            {
                _writeLock.Release();
            }

            var timeout = Task.Delay(AckTimeoutMs);
            var completed = await Task.WhenAny(tcs.Task, timeout).ConfigureAwait(false);
            if (completed == timeout)
            {
                lock (_pendingLock) { if (_pendingAck == tcs) _pendingAck = null; }
                return false;
            }
            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task<bool> SendBlockPerCommandAsync(string header, IEnumerable<string> bodyLines, bool requestDump)
        {
            using (var client = new TcpClient())
            {
                await ConnectWithTimeoutAsync(client).ConfigureAwait(false);
                using (var stream = client.GetStream())
                {
                    // Consume the initial dump briefly to avoid racing the server.
                    await ReadAndProcessAsync(stream, InitialDumpWaitMs, CancellationToken.None).ConfigureAwait(false);

                    string message = BuildBlock(header, bodyLines, requestDump);
                    var bytes = Encoding.ASCII.GetBytes(message);
                    Log(LogDirection.Tx, message.TrimEnd());

                    var tcs = new TaskCompletionSource<bool>();
                    lock (_pendingLock) { _pendingAck = tcs; }

                    await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    // Keep reading while waiting for ACK/NAK.
                    var readTask = ReadAndProcessAsync(stream, InitialDumpWaitMs, CancellationToken.None);
                    var timeout = Task.Delay(AckTimeoutMs);
                    var completed = await Task.WhenAny(tcs.Task, timeout).ConfigureAwait(false);

                    bool ok;
                    if (completed == timeout)
                    {
                        lock (_pendingLock) { if (_pendingAck == tcs) _pendingAck = null; }
                        ok = false;
                    }
                    else
                    {
                        ok = await tcs.Task.ConfigureAwait(false);
                    }

                    try { await Task.WhenAny(readTask, Task.Delay(100)).ConfigureAwait(false); } catch { }
                    return ok;
                }
            }
        }

        private static string BuildBlock(string header, IEnumerable<string> bodyLines, bool requestDump)
        {
            var sb = new StringBuilder();
            sb.Append(header);
            sb.Append(":\n");
            if (!requestDump && bodyLines != null)
            {
                foreach (var l in bodyLines) { sb.Append(l); sb.Append('\n'); }
            }
            sb.Append('\n');
            return sb.ToString();
        }

        // ----------------------- validation -----------------------

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VideohubMatrixService));
        }

        private void ValidateInput(int input)
        {
            int max = Math.Max(1, Device.InputCount);
            if (input < 1 || input > max) throw new ArgumentOutOfRangeException(nameof(input));
        }

        private void ValidateOutput(int output)
        {
            int max = Math.Max(1, Device.OutputCount);
            if (output < 1 || output > max) throw new ArgumentOutOfRangeException(nameof(output));
        }
    }
}
