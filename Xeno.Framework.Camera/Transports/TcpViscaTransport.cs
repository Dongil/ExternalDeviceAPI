using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xeno.Framework.Camera.Protocols.Visca;

namespace Xeno.Framework.Camera.Transports
{
    /// <summary>
    /// VISCA over TCP transport. Sends raw VISCA bytes (8x ... FF) over a TCP stream
    /// without the Sony VISCA-over-IP 8-byte sequence header. Many third-party / OEM
    /// PTZ cameras (PTZOptics, FR-series, Aida) speak this dialect.
    /// Default port 5678 (PTZOptics convention); cameras vary — check the device web UI.
    /// </summary>
    internal sealed class TcpViscaTransport : IViscaIpTransport
    {
        private readonly object _lock = new object();
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _rxLoopCts;
        private Task _rxLoopTask;
        private BlockingCollection<byte[]> _rxQueue;

        public string Host { get; set; }
        public int Port { get; set; } = 5678;
        public int ReceiveTimeoutMs { get; set; } = 300;
        public bool WaitForReply { get; set; } = true;
        public Action<string> OnLog { get; set; }

        public bool IsOpen
        {
            get { lock (_lock) { return _client != null && _client.Connected; } }
        }

        public void Open()
        {
            lock (_lock)
            {
                CloseInternal();
                if (string.IsNullOrEmpty(Host))
                    throw new InvalidOperationException("Host not set");

                _client = new TcpClient();
                // Quick connect timeout (2s) so a wrong port doesn't hang the UI thread.
                var ar = _client.BeginConnect(Host, Port, null, null);
                bool ok = ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                if (!ok)
                {
                    try { _client.Close(); } catch { }
                    _client = null;
                    throw new TimeoutException("TCP connect to " + Host + ":" + Port + " timed out");
                }
                _client.EndConnect(ar);
                _stream = _client.GetStream();

                _rxQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
                _rxLoopCts = new CancellationTokenSource();

                Log("TCP open " + Host + ":" + Port);

                var token = _rxLoopCts.Token;
                var stream = _stream;
                _rxLoopTask = Task.Run(() => RxLoopAsync(stream, token));
            }
        }

        public void Close()
        {
            CancellationTokenSource cts;
            BlockingCollection<byte[]> q;
            lock (_lock)
            {
                cts = _rxLoopCts;
                q = _rxQueue;
                _rxLoopCts = null;
                _rxQueue = null;
                CloseInternal();
            }
            try { cts?.Cancel(); } catch { }
            try { q?.CompleteAdding(); } catch { }
            try { cts?.Dispose(); } catch { }
            try { q?.Dispose(); } catch { }
        }

        private void CloseInternal()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        public async Task<byte[]> SendCommandAsync(byte[] viscaPayload)
        {
            NetworkStream s;
            BlockingCollection<byte[]> q;
            lock (_lock)
            {
                s = _stream;
                q = _rxQueue;
                if (s == null || q == null) throw new InvalidOperationException("TCP not open");
            }

            // Drain late replies left in the queue (e.g. Completion that arrived after the
            // previous command's wait window). Classify so routine late-Completion is visible
            // as a normal/expected event rather than appearing as opaque "stale" hex.
            byte[] stale;
            while (q.TryTake(out stale))
            {
                Log("DRAIN late " + ViscaResponseParser.DescribeReply(stale) + " : " + ToHex(stale));
            }

            Log("TX raw=" + ToHex(viscaPayload));

            try
            {
                await s.WriteAsync(viscaPayload, 0, viscaPayload.Length).ConfigureAwait(false);
                await s.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log("TX failed: " + ex.Message);
                return new byte[0];
            }

            if (!WaitForReply) return new byte[0];

            // Wait for the matching ACK. If a late Completion (from a previous command) arrives
            // first, drain it and wait once more. Bounded retry prevents infinite loops if the
            // queue is flooded with completions.
            byte[] reply = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(ReceiveTimeoutMs))
                    {
                        reply = q.Take(cts.Token);
                    }
                }
                catch (OperationCanceledException) { reply = null; break; }
                catch (ObjectDisposedException) { reply = null; break; }
                catch (InvalidOperationException) { reply = null; break; }

                // If this is a leftover Completion (90 5y FF) from a previous command, treat
                // it as late and keep waiting for our ACK. If it's an ACK / Error / Unknown
                // we accept it as the reply.
                var kind = ViscaResponseParser.Classify(reply, out _);
                if (kind == ViscaResponseParser.ReplyKind.Completion && attempt == 0)
                {
                    Log("RX late Completion (skipped) : " + ToHex(reply));
                    continue;
                }
                break;
            }

            if (reply == null || reply.Length == 0)
            {
                Log("RX timeout (no reply within " + ReceiveTimeoutMs + "ms)");
                return new byte[0];
            }

            Log("RX raw=" + ToHex(reply));
            return reply;
        }

        /// <summary>
        /// Continuously reads bytes from the TCP stream and slices them into VISCA messages
        /// at 0xFF terminators. Each completed message is enqueued for SendCommandAsync.
        /// </summary>
        private async Task RxLoopAsync(NetworkStream stream, CancellationToken ct)
        {
            Log("RX loop started");
            var accumulator = new List<byte>(16);
            var buf = new byte[256];
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int n = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                    if (n <= 0)
                    {
                        Log("RX loop: stream closed by peer");
                        break;
                    }
                    var q = _rxQueue;
                    if (q == null || q.IsAddingCompleted) break;
                    for (int i = 0; i < n; i++)
                    {
                        accumulator.Add(buf[i]);
                        if (buf[i] == 0xFF)
                        {
                            var msg = accumulator.ToArray();
                            accumulator.Clear();
                            try { q.Add(msg, ct); }
                            catch (InvalidOperationException) { return; }
                            catch (OperationCanceledException) { return; }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (System.IO.IOException ex)
                {
                    if (ct.IsCancellationRequested) break;
                    Log("RX loop IOException: " + ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    Log("RX loop exception: " + ex.GetType().Name + " " + ex.Message);
                    break;
                }
            }
            Log("RX loop exited");
        }

        private void Log(string message)
        {
            var h = OnLog;
            if (h != null) h(message);
        }

        private static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return "(empty)";
            var sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public void Dispose() { Close(); }
    }
}
