using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceEmulator.Transports
{
    /// <summary>
    /// Server-side UDP transport.
    /// Reply mode SonyConvention sends back to dest port 52381 of source IP (Sony/Canon/FR firmware behavior),
    /// SourcePortMirror sends back to the actual source port (permissive, for legacy controllers).
    /// </summary>
    internal sealed class UdpServerTransport : IDisposable
    {
        public enum ReplyMode { SonyConvention = 0, SourcePortMirror = 1 }

        private UdpClient _client;
        private CancellationTokenSource _cts;
        private Task _rxTask;
        private readonly object _lock = new object();

        public int LocalPort { get; set; } = 52381;
        public ReplyMode Reply { get; set; } = ReplyMode.SonyConvention;
        public int SonyReplyPort { get; set; } = 52381;

        public Action<byte[], IPEndPoint> OnPacketReceived { get; set; }
        public Action<string> OnLog { get; set; }

        public bool IsOpen { get { lock (_lock) { return _client != null; } } }

        public void Start()
        {
            lock (_lock)
            {
                Stop();
                _client = new UdpClient(new IPEndPoint(IPAddress.Any, LocalPort));
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                var c = _client;
                _rxTask = Task.Run(() => RxLoop(c, token));
                Log("UDP listen on 0.0.0.0:" + LocalPort + " (reply " + Reply + ")");
            }
        }

        public void Stop()
        {
            CancellationTokenSource cts;
            UdpClient c;
            lock (_lock) { cts = _cts; c = _client; _cts = null; _client = null; }
            try { if (cts != null) cts.Cancel(); } catch { }
            try { if (c != null) c.Close(); } catch { }
            try { if (cts != null) cts.Dispose(); } catch { }
        }

        public void SendReply(byte[] data, IPEndPoint sourceEndpoint)
        {
            UdpClient c;
            lock (_lock) { c = _client; }
            if (c == null || sourceEndpoint == null) return;
            try
            {
                IPEndPoint target = (Reply == ReplyMode.SonyConvention)
                    ? new IPEndPoint(sourceEndpoint.Address, SonyReplyPort)
                    : sourceEndpoint;
                c.Send(data, data.Length, target);
            }
            catch (Exception ex) { Log("UDP send failed: " + ex.Message); }
        }

        private async Task RxLoop(UdpClient client, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await client.ReceiveAsync().ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;
                    try { var h = OnPacketReceived; if (h != null) h(result.Buffer, result.RemoteEndPoint); } catch { }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (Exception ex) { Log("UDP RX loop: " + ex.Message); }
            }
        }

        private void Log(string msg) { var h = OnLog; if (h != null) try { h(msg); } catch { } }
        public void Dispose() { Stop(); }
    }
}
