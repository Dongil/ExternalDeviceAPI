using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Transports
{
    /// <summary>
    /// Singleton shared UDP listener for all VISCA-over-IP camera services in the process.
    ///
    /// Why this exists:
    /// Sony/Canon/FR/SRG VISCA-IP firmwares route replies to **destination port 52381 of the
    /// source IP**, ignoring the source port of the original request. Therefore on a host with
    /// multiple cameras, only one socket can bind to local 52381 and ALL camera replies arrive
    /// there. The hub demultiplexes by source IP into per-camera handlers, so each
    /// `UdpViscaTransport` instance still gets only its own camera's traffic.
    ///
    /// Lifecycle:
    /// - Lazy: started on first Subscribe(), stopped when last subscriber unsubscribes.
    /// - Bind preference: local port 52381; falls back to ephemeral if unavailable (in which
    ///   case some camera firmwares may not deliver replies — but the unconnected pattern
    ///   still works for those that respect source port).
    /// </summary>
    internal sealed class ViscaUdpHub
    {
        private static ViscaUdpHub _instance;
        private static readonly object _instanceLock = new object();

        public static ViscaUdpHub Shared
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null) _instance = new ViscaUdpHub();
                    return _instance;
                }
            }
        }

        private readonly object _lock = new object();
        private readonly Dictionary<IPAddress, Action<byte[], int>> _subscribers
            = new Dictionary<IPAddress, Action<byte[], int>>();
        private UdpClient _client;
        private int _boundPort;
        private CancellationTokenSource _cts;
        private Task _rxTask;
        // Latest log callback (rotates as subscribers come and go). Hub-level events are rare,
        // this is fine for diagnostics.
        private Action<string> _log;

        private ViscaUdpHub() { }

        /// <summary>The local UDP port the hub is bound to (0 if not started).</summary>
        public int LocalPort { get { lock (_lock) { return _boundPort; } } }

        public bool IsStarted { get { lock (_lock) { return _client != null; } } }

        /// <summary>
        /// Subscribe a handler for datagrams arriving from <paramref name="remoteIp"/>.
        /// Handler receives (datagram bytes, source port). Called from background thread.
        /// </summary>
        public void Subscribe(IPAddress remoteIp, Action<byte[], int> handler, Action<string> log)
        {
            if (remoteIp == null) throw new ArgumentNullException(nameof(remoteIp));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                _log = log;
                if (_client == null) StartListenerLocked();
                _subscribers[remoteIp] = handler;
                Log("hub: subscribed " + remoteIp + " (total " + _subscribers.Count + ")");
            }
        }

        public void Unsubscribe(IPAddress remoteIp)
        {
            if (remoteIp == null) return;
            lock (_lock)
            {
                if (_subscribers.Remove(remoteIp))
                    Log("hub: unsubscribed " + remoteIp + " (remaining " + _subscribers.Count + ")");
                if (_subscribers.Count == 0) StopListenerLocked();
            }
        }

        /// <summary>Send a datagram to the specified remote endpoint via the shared socket.</summary>
        public void Send(byte[] packet, IPEndPoint remote)
        {
            UdpClient c;
            lock (_lock) { c = _client; }
            if (c == null) throw new InvalidOperationException("hub not started");
            c.Send(packet, packet.Length, remote);
        }

        public Task SendAsync(byte[] packet, IPEndPoint remote)
        {
            UdpClient c;
            lock (_lock) { c = _client; }
            if (c == null) throw new InvalidOperationException("hub not started");
            return c.SendAsync(packet, packet.Length, remote);
        }

        // -------- internal --------

        private void StartListenerLocked()
        {
            try
            {
                _client = new UdpClient(new IPEndPoint(IPAddress.Any, 52381));
                _boundPort = 52381;
            }
            catch (SocketException ex)
            {
                Log("hub: local port 52381 unavailable (" + ex.SocketErrorCode + "), using ephemeral");
                _client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
                _boundPort = ((IPEndPoint)_client.Client.LocalEndPoint).Port;
            }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var client = _client;
            _rxTask = Task.Run(() => RxLoop(client, token));
            Log("hub: listening on local :" + _boundPort);
        }

        private void StopListenerLocked()
        {
            try { _cts?.Cancel(); } catch { }
            try { _client?.Close(); } catch { }
            _client = null;
            _cts = null;
            _rxTask = null;
            _boundPort = 0;
            Log("hub: listener stopped");
        }

        private async Task RxLoop(UdpClient client, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await client.ReceiveAsync().ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;

                    Action<byte[], int> handler = null;
                    lock (_lock)
                    {
                        _subscribers.TryGetValue(result.RemoteEndPoint.Address, out handler);
                    }
                    if (handler != null)
                    {
                        try { handler(result.Buffer, result.RemoteEndPoint.Port); }
                        catch (Exception ex) { Log("hub: subscriber handler threw: " + ex.Message); }
                    }
                    else
                    {
                        Log("hub: dropped (no subscriber for " + result.RemoteEndPoint.Address + ")");
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (Exception) { /* keep looping */ }
            }
        }

        private void Log(string msg)
        {
            var h = _log;
            if (h != null) { try { h(msg); } catch { } }
        }
    }
}
