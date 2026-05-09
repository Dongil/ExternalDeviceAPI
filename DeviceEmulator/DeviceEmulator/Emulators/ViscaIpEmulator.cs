using System;
using System.Net;
using DeviceEmulator.Core;
using DeviceEmulator.Transports;
using Xeno.Framework.Camera.Protocols.Visca;

namespace DeviceEmulator.Emulators
{
    /// <summary>
    /// Generic VISCA-over-IP emulator covering Sony SRG-300H, Canon CR-N300, FR-H50SN.
    /// Recognizes Sony VISCA-over-IP wrapper (8-byte header) on UDP and mirrors sequence in reply.
    /// On TCP, treats each 8x..FF run as raw VISCA payload.
    /// </summary>
    public sealed class ViscaIpEmulator : DeviceEmulatorBase
    {
        private UdpServerTransport _udp;
        private TcpServerTransport _tcp;
        private readonly ViscaIpWrapValidator _validator = new ViscaIpWrapValidator();

        public ViscaIpEmulator(string brand, string model)
        {
            DeviceType = "Camera"; Brand = brand; Model = model;
            Description = brand + " " + model + " (VISCA over IP)";
        }

        public override void Configure(EmulatorConfig cfg) { _cfg = cfg; }

        public override void Start()
        {
            if (_cfg == null) throw new InvalidOperationException("not configured");
            if (string.Equals(_cfg.TransportKind, "Udp", StringComparison.OrdinalIgnoreCase))
                StartUdp();
            else if (string.Equals(_cfg.TransportKind, "Tcp", StringComparison.OrdinalIgnoreCase))
                StartTcp();
            else throw new NotSupportedException("Transport: " + _cfg.TransportKind);
            _listening = true;
        }

        private void StartUdp()
        {
            _udp = new UdpServerTransport
            {
                LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 52381,
                Reply = _cfg.SonyReplyConvention ? UdpServerTransport.ReplyMode.SonyConvention : UdpServerTransport.ReplyMode.SourcePortMirror,
                SonyReplyPort = 52381
            };
            _udp.OnLog = msg => Log("[UDP] " + msg);
            _udp.OnPacketReceived = (data, src) => OnViscaIp(data, src, isTcp: false);
            _udp.Start();
        }

        private void StartTcp()
        {
            _tcp = new TcpServerTransport
            {
                LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 5678,
                Framing = TcpServerTransport.FrameMode.ViscaTerminator
            };
            _tcp.OnLog = msg => Log("[TCP] " + msg);
            _tcp.OnPacketReceived = data => OnViscaIp(data, null, isTcp: true);
            _tcp.Start();
        }

        public override void Stop()
        {
            _listening = false;
            try { if (_udp != null) _udp.Dispose(); } catch { }
            try { if (_tcp != null) _tcp.Dispose(); } catch { }
            _udp = null; _tcp = null;
            _validator.Reset();
        }

        private async void OnViscaIp(byte[] data, IPEndPoint src, bool isTcp)
        {
            // VISCA-over-IP wrap-level validation (UDP only — TCP raw has no wrap).
            // Surfaces controller bugs like seq regression/duplicate, length mismatch, etc.
            if (!isTcp && src != null)
            {
                var issues = _validator.ValidateWrap(data, src.Address);
                foreach (var issue in issues) Log(issue.Format());
            }

            byte[] inner;
            uint seq = 0;
            bool wrapped = false;
            if (!isTcp && data != null && data.Length >= 8 && data[0] == 0x01 && (data[1] == 0x00 || data[1] == 0x10))
            {
                // Sony VISCA-over-IP: [PayloadType 2B][Length 2B][Sequence 4B][VISCA payload]
                seq = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
                inner = new byte[data.Length - 8];
                Buffer.BlockCopy(data, 8, inner, 0, inner.Length);
                wrapped = true;
            }
            else inner = data;

            var cmd = ViscaCommandParser.Parse(inner);
            Log("RX " + (isTcp ? "" : (wrapped ? "seq=" + seq + " " : "raw ")) + ToHex(data) + "  | " + cmd.Describe());

            if (Options.InjectMalformed)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectMalformed = false;
                Log("INJECT malformed");
                await SendReplyAsync(() => SendInner(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFF }, src, isTcp, seq, wrapped));
                return;
            }
            if (Options.InjectNak)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectNak = false;
                var err = ViscaReplyBuilder.Error(cmd.Address, 0x02);
                Log("INJECT NAK");
                await SendReplyAsync(() => SendInner(err, src, isTcp, seq, wrapped));
                return;
            }

            var ack = ViscaReplyBuilder.Ack(cmd.Address);
            await SendReplyAsync(() => { SendInner(ack, src, isTcp, seq, wrapped); Log("TX ACK " + ToHex(ack)); });
            var done = ViscaReplyBuilder.Completion(cmd.Address);
            await SendReplyAsync(() => { SendInner(done, src, isTcp, seq, wrapped); Log("TX Completion " + ToHex(done)); });
        }

        private void SendInner(byte[] viscaPayload, IPEndPoint src, bool isTcp, uint seq, bool wrapped)
        {
            if (isTcp) { var t = _tcp; if (t != null) t.SendReply(viscaPayload); return; }
            var u = _udp; if (u == null || src == null) return;
            byte[] outBuf;
            if (wrapped)
            {
                int n = viscaPayload.Length;
                outBuf = new byte[8 + n];
                outBuf[0] = 0x01; outBuf[1] = 0x11;
                outBuf[2] = (byte)(n >> 8); outBuf[3] = (byte)(n & 0xFF);
                outBuf[4] = (byte)(seq >> 24); outBuf[5] = (byte)(seq >> 16);
                outBuf[6] = (byte)(seq >> 8);  outBuf[7] = (byte)(seq & 0xFF);
                Buffer.BlockCopy(viscaPayload, 0, outBuf, 8, n);
            }
            else
            {
                outBuf = viscaPayload;
            }
            u.SendReply(outBuf, src);
        }

        public override void Dispose() { Stop(); }

        private static string ToHex(byte[] d)
        {
            if (d == null) return "(null)";
            var sb = new System.Text.StringBuilder(d.Length * 3);
            for (int i = 0; i < d.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(d[i].ToString("X2")); }
            return sb.ToString();
        }
    }
}
