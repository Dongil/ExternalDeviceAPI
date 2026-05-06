using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceEmulator.Transports
{
    internal sealed class TcpServerTransport : IDisposable
    {
        public enum FrameMode { ViscaTerminator, PelcoFixed }

        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private Task _acceptTask;
        private Task _rxTask;
        private readonly object _lock = new object();

        public int LocalPort { get; set; } = 5678;
        public FrameMode Framing { get; set; } = FrameMode.ViscaTerminator;

        public Action<byte[]> OnPacketReceived { get; set; }
        public Action<string> OnLog { get; set; }

        public bool IsOpen { get { lock (_lock) { return _listener != null; } } }
        public bool HasClient { get { lock (_lock) { return _client != null && _client.Connected; } } }

        public void Start()
        {
            lock (_lock)
            {
                Stop();
                _listener = new TcpListener(IPAddress.Any, LocalPort);
                _listener.Start();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _acceptTask = Task.Run(() => AcceptLoop(token));
                Log("TCP listen on 0.0.0.0:" + LocalPort + " (" + Framing + ")");
            }
        }

        public void Stop()
        {
            CancellationTokenSource cts;
            TcpListener l; TcpClient c; NetworkStream s;
            lock (_lock)
            {
                cts = _cts; l = _listener; c = _client; s = _stream;
                _cts = null; _listener = null; _client = null; _stream = null;
            }
            try { if (cts != null) cts.Cancel(); } catch { }
            try { if (s != null) s.Close(); } catch { }
            try { if (c != null) c.Close(); } catch { }
            try { if (l != null) l.Stop(); } catch { }
            try { if (cts != null) cts.Dispose(); } catch { }
        }

        public void SendReply(byte[] data)
        {
            NetworkStream s;
            lock (_lock) { s = _stream; }
            if (s == null) return;
            try { s.Write(data, 0, data.Length); s.Flush(); }
            catch (Exception ex) { Log("TCP send failed: " + ex.Message); }
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpListener l;
                lock (_lock) { l = _listener; }
                if (l == null) break;
                try
                {
                    var client = await l.AcceptTcpClientAsync().ConfigureAwait(false);
                    var ep = client.Client.RemoteEndPoint as IPEndPoint;
                    Log("TCP client connected from " + ep);
                    NetworkStream stream;
                    lock (_lock)
                    {
                        try { if (_client != null) _client.Close(); } catch { }
                        _client = client;
                        _stream = client.GetStream();
                        stream = _stream;
                        _rxTask = Task.Run(() => RxLoop(stream, ct));
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
            }
        }

        private async Task RxLoop(NetworkStream stream, CancellationToken ct)
        {
            var accumulator = new List<byte>(16);
            var buf = new byte[256];
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int n = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                    if (n <= 0) { Log("TCP client disconnected"); break; }
                    for (int i = 0; i < n; i++)
                    {
                        accumulator.Add(buf[i]);
                        bool packetReady = false;
                        if (Framing == FrameMode.ViscaTerminator && buf[i] == 0xFF) packetReady = true;
                        else if (Framing == FrameMode.PelcoFixed && accumulator.Count == 7) packetReady = true;
                        if (packetReady)
                        {
                            var packet = accumulator.ToArray();
                            accumulator.Clear();
                            try { var h = OnPacketReceived; if (h != null) h(packet); } catch { }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (System.IO.IOException) { break; }
                catch (Exception ex) { Log("TCP RX loop: " + ex.Message); break; }
            }
        }

        private void Log(string msg) { var h = OnLog; if (h != null) try { h(msg); } catch { } }
        public void Dispose() { Stop(); }
    }
}
