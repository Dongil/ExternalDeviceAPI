using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xeno.Framework.Matrix.Core;

namespace Xeno.Framework.Matrix.Services
{
    /// <summary>
    /// Drives the PN-8080 4K30 HDMI 8x8 Seamless Matrix over TCP/8000 using the vendor ASCII
    /// protocol (commands terminated with "!"). Supports Persistent and PerCommand modes.
    /// </summary>
    public sealed class Pn8080MatrixService : MatrixServiceBase
    {
        public const int DefaultPort = 8000;
        public const int Inputs = 8;
        public const int Outputs = 8;

        private static readonly Regex RouteLine = new Regex(
            @"input\s+(?<in>[0-8])\s*->\s*output\s+(?<out>[1-8])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ErrorCode = new Regex(
            @"\bE0[0-2]\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private Task _watchdog;
        private bool _disposed;

        public int ConnectTimeoutMs { get; set; } = 2000;
        public int QuietPeriodMs { get; set; } = 100;
        public int OverallTimeoutMs { get; set; } = 2000;

        public Pn8080MatrixService() : base(new DeviceInfo
        {
            Kind = DeviceKind.Pn8080,
            Model = "PN-8080",
            InputCount = Inputs,
            OutputCount = Outputs,
            IsPresent = true
        })
        {
            Port = DefaultPort;
        }

        /// <summary>PN-8080 is a fixed 8×8 router; the setter is a no-op.</summary>
        public override int MaxInputs { get { return Inputs; } set { /* fixed 8×8 */ } }
        public override int MaxOutputs { get { return Outputs; } set { /* fixed 8×8 */ } }

        // ------------------ IMatrixService ------------------

        public override async Task ConnectAsync()
        {
            EnsureNotDisposed();
            if (Mode != ConnectionMode.Persistent)
            {
                // PerCommand: just verify connectivity by issuing a refresh
                await RefreshAsync().ConfigureAwait(false);
                return;
            }

            var sw = Stopwatch.StartNew();
            await _sendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                CloseSocket();
                _cts = new CancellationTokenSource();
                await OpenAsync(_cts.Token).ConfigureAwait(false);
                StartWatchdog();
            }
            finally
            {
                _sendGate.Release();
            }

            await RefreshAsync().ConfigureAwait(false);
            sw.Stop();
            Log(LogDirection.Info, string.Format("ConnectAsync complete in {0}ms", sw.ElapsedMilliseconds));
        }

        public override async Task DisconnectAsync()
        {
            var cts = _cts;
            if (cts != null) { try { cts.Cancel(); } catch { } }

            await _sendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                CloseSocket();
                SetConnected(false, "disconnected");
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public override async Task RouteAsync(int input, int output)
        {
            ValidateInput(input); ValidateOutput(output);
            var cmd = string.Format("s in {0} av out {1}!", input, output);
            var response = await SendCommandAsync(cmd).ConfigureAwait(false);
            ApplyResponseOrFallback(response, input, output);
        }

        public override async Task RouteAllAsync(int input)
        {
            ValidateInput(input);
            var cmd = string.Format("s in {0} av out 0!", input);
            var response = await SendCommandAsync(cmd).ConfigureAwait(false);
            ApplyResponseOrFallback(response, input, output: 0);
        }

        public override async Task PtpAsync()
        {
            for (int i = 1; i <= Outputs; i++)
                await RouteAsync(i, i).ConfigureAwait(false);
        }

        public override async Task RefreshAsync()
        {
            var response = await SendCommandAsync("r av out 0!").ConfigureAwait(false);
            var routes = ParseRoutes(response);
            if (routes.Count > 0)
            {
                ApplyRoutes(routes);
                RaiseStateUpdated();
            }
            else
            {
                Log(LogDirection.Info, "응답에서 라우팅 정보를 찾지 못했습니다.");
            }
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cts?.Cancel(); } catch { }
            CloseSocket();
            try { _sendGate.Dispose(); } catch { }
            base.Dispose();
        }

        // ------------------ internal send/receive ------------------

        private async Task<string> SendCommandAsync(string cmd)
        {
            EnsureNotDisposed();
            var data = Encoding.ASCII.GetBytes(cmd);

            await _sendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Mode == ConnectionMode.Persistent)
                {
                    if (!IsConnected || _stream == null)
                        await OpenAsync(_cts?.Token ?? CancellationToken.None).ConfigureAwait(false);

                    Log(LogDirection.Tx, cmd);
                    try
                    {
                        return await WriteAndReadAsync(_stream, data).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log(LogDirection.Error, "send failed: " + ex.Message);
                        SetConnected(false, "send failed");
                        CloseSocket();
                        throw;
                    }
                }
                else
                {
                    using (var client = new TcpClient())
                    {
                        await ConnectWithTimeoutAsync(client).ConfigureAwait(false);
                        using (var stream = client.GetStream())
                        {
                            Log(LogDirection.Tx, cmd);
                            return await WriteAndReadAsync(stream, data).ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                _sendGate.Release();
            }
        }

        private async Task OpenAsync(CancellationToken token)
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

        private async Task<string> WriteAndReadAsync(NetworkStream stream, byte[] data)
        {
            DrainStream(stream);
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            try { await stream.FlushAsync().ConfigureAwait(false); } catch { }

            var sb = new StringBuilder();
            var buf = new byte[4096];
            var overallStart = DateTime.UtcNow;
            DateTime lastRx = DateTime.MinValue;
            bool hasData = false;

            while (true)
            {
                if ((DateTime.UtcNow - overallStart).TotalMilliseconds >= OverallTimeoutMs) break;

                if (stream.DataAvailable)
                {
                    int n = await stream.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                    if (n <= 0) break;
                    sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                    lastRx = DateTime.UtcNow;
                    hasData = true;
                }
                else if (hasData && (DateTime.UtcNow - lastRx).TotalMilliseconds >= QuietPeriodMs)
                {
                    break;
                }
                else
                {
                    await Task.Delay(15).ConfigureAwait(false);
                }
            }

            var response = sb.ToString().Replace("\r\n", "\n").TrimEnd('\n', '\r', ' ');
            if (response.Length > 0) Log(LogDirection.Rx, response);
            return response;
        }

        private static void DrainStream(NetworkStream s)
        {
            try
            {
                var tmp = new byte[1024];
                while (s.DataAvailable)
                {
                    int n = s.Read(tmp, 0, tmp.Length);
                    if (n <= 0) break;
                }
            }
            catch { }
        }

        private void CloseSocket()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        private void StartWatchdog()
        {
            var token = _cts.Token;
            _watchdog = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try { await Task.Delay(2000, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }

                    if (token.IsCancellationRequested) break;

                    if (IsConnected && !IsSocketAlive())
                        SetConnected(false, "socket lost");

                    if (!IsConnected && AutoReconnect && !IsReconnecting && !token.IsCancellationRequested)
                        await TryReconnectAsync(token).ConfigureAwait(false);
                }
            });
        }

        private bool IsSocketAlive()
        {
            try
            {
                var s = _client?.Client;
                if (s == null || !s.Connected) return false;
                bool pollResult = s.Poll(0, SelectMode.SelectRead);
                if (pollResult && s.Available == 0) return false;
                return true;
            }
            catch { return false; }
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
                        await _sendGate.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            CloseSocket();
                            await OpenAsync(token).ConfigureAwait(false);
                            return;
                        }
                        finally { _sendGate.Release(); }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Log(LogDirection.Error, "reconnect failed: " + ex.Message);
                    }
                }
                Log(LogDirection.Error, "reconnect attempts exhausted");
            }
            finally
            {
                SetReconnecting(false);
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Pn8080MatrixService));
        }

        private static void ValidateInput(int input)
        {
            if (input < 1 || input > Inputs) throw new ArgumentOutOfRangeException(nameof(input));
        }

        private static void ValidateOutput(int output)
        {
            if (output < 1 || output > Outputs) throw new ArgumentOutOfRangeException(nameof(output));
        }

        // ------------------ response handling ------------------

        private static Dictionary<int, int> ParseRoutes(string response)
        {
            var result = new Dictionary<int, int>();
            if (string.IsNullOrEmpty(response)) return result;
            foreach (Match m in RouteLine.Matches(response))
            {
                int input, output;
                if (int.TryParse(m.Groups["in"].Value, out input) &&
                    int.TryParse(m.Groups["out"].Value, out output) &&
                    input >= 1 && input <= Inputs && output >= 1 && output <= Outputs)
                {
                    result[output] = input;
                }
            }
            return result;
        }

        private void ApplyResponseOrFallback(string response, int input, int output)
        {
            if (!string.IsNullOrEmpty(response))
            {
                var m = ErrorCode.Match(response);
                if (m.Success)
                {
                    Log(LogDirection.Error, "device error " + m.Value.ToUpperInvariant());
                    return;
                }
            }

            var routes = ParseRoutes(response);
            if (routes.Count > 0)
            {
                ApplyRoutes(routes);
                RaiseStateUpdated();
                return;
            }

            if (output == 0)
            {
                var all = new Dictionary<int, int>();
                for (int o = 1; o <= Outputs; o++) all[o] = input;
                ApplyRoutes(all);
            }
            else
            {
                ApplyRoute(input, output);
            }
            RaiseStateUpdated();
        }
    }
}
