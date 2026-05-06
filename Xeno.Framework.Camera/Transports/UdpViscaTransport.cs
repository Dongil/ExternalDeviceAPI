using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xeno.Framework.Camera.Protocols.Visca;

namespace Xeno.Framework.Camera.Transports
{
    /// <summary>
    /// Sony VISCA over IP (UDP) transport.
    /// Wire packet (non-Raw): [Payload Type 2B BE] [Payload Length 2B BE] [Sequence 4B BE] [VISCA payload N bytes]
    /// Default port 52381.
    ///
    /// v1.2.2 architecture:
    /// - VISCA-IP mode (RawMode=false): uses singleton <see cref="ViscaUdpHub"/> sharing local
    ///   port 52381 across all camera services in the process. Required because Sony/Canon/FR
    ///   firmwares route replies to dest port 52381 of source IP regardless of source port.
    /// - RawMode=true (Pelco-D over UDP): keeps own UdpClient bound to ephemeral port,
    ///   fire-and-forget, no hub.
    /// </summary>
    internal sealed class UdpViscaTransport : IViscaIpTransport
    {
        private readonly object _lock = new object();
        private bool _opened;
        private uint _sequence;
        private IPEndPoint _remoteEndpoint;
        private IPAddress _expectedRemoteIp;
        private BlockingCollection<byte[]> _rxQueue;

        // Raw-mode owns its own UdpClient (Pelco-D over UDP, fire-and-forget).
        private UdpClient _rawClient;
        private CancellationTokenSource _rawRxCts;
        private Task _rawRxTask;

        public string Host { get; set; }
        public int Port { get; set; } = 52381;
        public int ReceiveTimeoutMs { get; set; } = 300;
        public bool WaitForReply { get; set; } = true;
        public bool RawMode { get; set; } = false;
        public Action<string> OnLog { get; set; }

        public bool IsOpen { get { lock (_lock) { return _opened; } } }

        public void Open()
        {
            lock (_lock)
            {
                CloseInternal();
                if (string.IsNullOrEmpty(Host))
                    throw new InvalidOperationException("Host not set");

                _remoteEndpoint = new IPEndPoint(IPAddress.Parse(Host), Port);
                _expectedRemoteIp = _remoteEndpoint.Address;
                _sequence = 1;
                _rxQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());

                if (RawMode)
                {
                    OpenRawLocked();
                }
                else
                {
                    OpenSharedHubLocked();
                }

                _opened = true;
            }
        }

        private void OpenSharedHubLocked()
        {
            // Subscribe to the singleton hub. Hub starts on first subscription, dispatches
            // datagrams from our remote IP to our handler.
            var hub = ViscaUdpHub.Shared;
            // Capture the queue ref locally so the handler closure isn't re-resolving every call.
            var queue = _rxQueue;
            var address = _expectedRemoteIp;
            hub.Subscribe(address, (data, srcPort) =>
            {
                Log("RX from :" + srcPort + " raw=" + ToHex(data));
                try { queue?.Add(data); } catch { /* queue closed */ }
            }, msg => Log(msg));

            Log("UDP open " + Host + ":" + Port + " (hub local :" + hub.LocalPort + ")");
            SendControlReset();   // sync sequence (Sony VISCA-IP convention)
        }

        private void OpenRawLocked()
        {
            // RawMode: own ephemeral socket, no hub (Pelco-D over UDP and similar).
            try
            {
                _rawClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException("UDP open failed: " + ex.Message, ex);
            }
            int local = ((IPEndPoint)_rawClient.Client.LocalEndPoint).Port;
            _rawRxCts = new CancellationTokenSource();
            var token = _rawRxCts.Token;
            var client = _rawClient;
            _rawRxTask = Task.Run(() => RawRxLoop(client, token));
            Log("UDP open " + Host + ":" + Port + " (raw, local :" + local + ")");
        }

        public void Close()
        {
            BlockingCollection<byte[]> q;
            CancellationTokenSource cts;
            UdpClient raw;
            IPAddress expected;
            bool wasOpen, wasRaw;
            lock (_lock)
            {
                wasOpen = _opened;
                wasRaw = _rawClient != null;
                q = _rxQueue;
                cts = _rawRxCts;
                raw = _rawClient;
                expected = _expectedRemoteIp;
                _rxQueue = null;
                _rawRxCts = null;
                _rawClient = null;
                _opened = false;
            }
            if (!wasOpen) return;

            try { cts?.Cancel(); } catch { }
            try { raw?.Close(); } catch { }
            try { q?.CompleteAdding(); } catch { }
            try { cts?.Dispose(); } catch { }
            try { q?.Dispose(); } catch { }

            if (!wasRaw && expected != null)
            {
                try { ViscaUdpHub.Shared.Unsubscribe(expected); } catch { }
            }
        }

        private void CloseInternal()
        {
            if (!_opened) return;
            // Mirror Close() but called within lock — release subscriber + raw socket.
            try
            {
                if (_rawClient != null) { _rawClient.Close(); _rawClient = null; }
                if (_rawRxCts != null) { _rawRxCts.Cancel(); _rawRxCts.Dispose(); _rawRxCts = null; }
                if (_rxQueue != null) { _rxQueue.CompleteAdding(); _rxQueue.Dispose(); _rxQueue = null; }
                if (_expectedRemoteIp != null && !RawMode)
                    ViscaUdpHub.Shared.Unsubscribe(_expectedRemoteIp);
            }
            catch { }
            _opened = false;
        }

        public Task<byte[]> SendCommandAsync(byte[] viscaPayload)
        {
            return SendInternalAsync(0x0100, viscaPayload);
        }

        private async Task<byte[]> SendInternalAsync(ushort payloadType, byte[] viscaPayload)
        {
            BlockingCollection<byte[]> q;
            uint seq;
            bool raw;
            UdpClient rawClient;
            IPEndPoint remote;
            lock (_lock)
            {
                if (!_opened) throw new InvalidOperationException("UDP not open");
                q = _rxQueue;
                seq = _sequence++;
                raw = RawMode;
                rawClient = _rawClient;
                remote = _remoteEndpoint;
            }

            // Drain queued datagrams from previous commands (late Completion etc.).
            byte[] stale;
            while (q.TryTake(out stale))
            {
                if (raw)
                {
                    Log("DRAIN raw : " + ToHex(stale));
                }
                else
                {
                    byte[] inner = stale != null && stale.Length >= 8
                        ? Slice(stale, 8, stale.Length - 8)
                        : stale;
                    Log("DRAIN late " + ViscaResponseParser.DescribeReply(inner) + " : " + ToHex(stale));
                }
            }

            // Build packet (VISCA-IP wraps 8-byte header; raw passes through).
            byte[] packet = raw ? viscaPayload : BuildPacket(payloadType, seq, viscaPayload);
            if (raw)
                Log("TX raw seq=" + seq + " : " + ToHex(packet));
            else
                Log("TX seq=" + seq + " type=0x" + payloadType.ToString("X4")
                    + " len=" + viscaPayload.Length + " : " + ToHex(packet));

            try
            {
                if (raw)
                    await rawClient.SendAsync(packet, packet.Length, remote).ConfigureAwait(false);
                else
                    await ViscaUdpHub.Shared.SendAsync(packet, remote).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log("TX failed seq=" + seq + " : " + ex.Message);
                return new byte[0];
            }

            if (!WaitForReply) return new byte[0];

            // Wait for the matching reply with late-Completion auto-skip (max 1 retry).
            byte[] reply = null;
            int maxAttempts = raw ? 1 : 2;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
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

                if (raw) break;

                byte[] inner = reply != null && reply.Length >= 8 ? Slice(reply, 8, reply.Length - 8) : reply;
                byte _err;
                var kind = ViscaResponseParser.Classify(inner, out _err);
                if (kind == ViscaResponseParser.ReplyKind.Completion && attempt == 0)
                {
                    Log("RX late Completion (skipped) : " + ToHex(reply));
                    continue;
                }
                break;
            }

            if (reply == null || reply.Length == 0)
            {
                Log("RX timeout seq=" + seq + " (no reply within " + ReceiveTimeoutMs + "ms)");
                return new byte[0];
            }

            if (raw)
            {
                Log("RX raw : " + ToHex(reply));
                return reply;
            }

            if (reply.Length >= 8)
            {
                ushort rxType = (ushort)((reply[0] << 8) | reply[1]);
                ushort rxLen  = (ushort)((reply[2] << 8) | reply[3]);
                uint   rxSeq  = (uint)((reply[4] << 24) | (reply[5] << 16) | (reply[6] << 8) | reply[7]);
                Log("RX seq=" + rxSeq + " type=0x" + rxType.ToString("X4") + " len=" + rxLen
                    + " full=" + ToHex(reply));
                if (rxSeq != seq)
                    Log("WARN: RX seq " + rxSeq + " != TX seq " + seq + " (stale or out-of-order)");
                var payload = new byte[reply.Length - 8];
                Buffer.BlockCopy(reply, 8, payload, 0, payload.Length);
                return payload;
            }

            Log("RX too short (" + reply.Length + " bytes) — ignoring");
            return new byte[0];
        }

        // ---- Raw-mode (Pelco-D over UDP) own receive loop ----
        private async Task RawRxLoop(UdpClient client, CancellationToken ct)
        {
            Log("raw RX loop started");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await client.ReceiveAsync().ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;
                    if (_expectedRemoteIp != null
                        && !result.RemoteEndPoint.Address.Equals(_expectedRemoteIp))
                    {
                        Log("raw RX dropped (source " + result.RemoteEndPoint
                            + " != expected " + _expectedRemoteIp + ")");
                        continue;
                    }
                    var q = _rxQueue;
                    if (q == null || q.IsAddingCompleted) break;
                    Log("raw RX from :" + result.RemoteEndPoint.Port + " raw=" + ToHex(result.Buffer));
                    try { q.Add(result.Buffer, ct); }
                    catch (InvalidOperationException) { break; }
                    catch (OperationCanceledException) { break; }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (Exception ex) { Log("raw RX loop ex: " + ex.Message); break; }
            }
            Log("raw RX loop exited");
        }

        private static byte[] BuildPacket(ushort payloadType, uint sequence, byte[] viscaPayload)
        {
            int n = viscaPayload == null ? 0 : viscaPayload.Length;
            byte[] packet = new byte[8 + n];
            packet[0] = (byte)(payloadType >> 8);
            packet[1] = (byte)(payloadType & 0xFF);
            packet[2] = (byte)(n >> 8);
            packet[3] = (byte)(n & 0xFF);
            packet[4] = (byte)(sequence >> 24);
            packet[5] = (byte)(sequence >> 16);
            packet[6] = (byte)(sequence >> 8);
            packet[7] = (byte)(sequence & 0xFF);
            if (n > 0) Buffer.BlockCopy(viscaPayload, 0, packet, 8, n);
            return packet;
        }

        private void SendControlReset()
        {
            byte[] packet = BuildPacket(0x0200, 0u, new byte[] { 0x01 });
            try
            {
                Log("TX RESET : " + ToHex(packet));
                ViscaUdpHub.Shared.Send(packet, _remoteEndpoint);
            }
            catch (Exception ex) { Log("TX RESET failed: " + ex.Message); }
            _sequence = 1;
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

        private static byte[] Slice(byte[] src, int offset, int length)
        {
            if (src == null || length <= 0 || offset < 0 || offset + length > src.Length)
                return new byte[0];
            var dst = new byte[length];
            Buffer.BlockCopy(src, offset, dst, 0, length);
            return dst;
        }

        public void Dispose() { Close(); }
    }
}
