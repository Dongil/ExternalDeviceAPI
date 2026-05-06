using System;
using DeviceEmulator.Core;
using DeviceEmulator.Transports;
using Xeno.Framework.Camera.Protocols.Pelco;

namespace DeviceEmulator.Emulators
{
    public sealed class PelcoDGenericEmulator : DeviceEmulatorBase
    {
        private SerialServerTransport _serial;
        private UdpServerTransport _udp;
        private TcpServerTransport _tcp;

        public PelcoDGenericEmulator()
        {
            DeviceType = "Camera"; Brand = "Pelco"; Model = "Pelco-D Generic";
            Description = "Generic Pelco-D";
        }

        public override void Configure(EmulatorConfig cfg) { _cfg = cfg; }

        public override void Start()
        {
            if (_cfg == null) throw new InvalidOperationException("not configured");
            string kind = (_cfg.TransportKind ?? "").ToLowerInvariant();
            switch (kind)
            {
                case "serial":
                    _serial = new SerialServerTransport
                    {
                        PortName = _cfg.ComPort,
                        BaudRate = _cfg.BaudRate <= 0 ? 9600 : _cfg.BaudRate,
                        Framing = SerialServerTransport.FrameMode.PelcoFixed
                    };
                    _serial.OnLog = msg => Log("[Serial] " + msg);
                    _serial.OnPacketReceived = OnPelco;
                    _serial.Start();
                    break;
                case "udp":
                    _udp = new UdpServerTransport
                    {
                        LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 4001,
                        Reply = UdpServerTransport.ReplyMode.SourcePortMirror
                    };
                    _udp.OnLog = msg => Log("[UDP] " + msg);
                    _udp.OnPacketReceived = (data, src) => OnPelco(data);
                    _udp.Start();
                    break;
                case "tcp":
                    _tcp = new TcpServerTransport
                    {
                        LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 4001,
                        Framing = TcpServerTransport.FrameMode.PelcoFixed
                    };
                    _tcp.OnLog = msg => Log("[TCP] " + msg);
                    _tcp.OnPacketReceived = OnPelco;
                    _tcp.Start();
                    break;
                default:
                    throw new NotSupportedException("Transport: " + _cfg.TransportKind);
            }
            _listening = true;
        }

        public override void Stop()
        {
            _listening = false;
            try { if (_serial != null) _serial.Dispose(); } catch { }
            try { if (_udp != null) _udp.Dispose(); } catch { }
            try { if (_tcp != null) _tcp.Dispose(); } catch { }
            _serial = null; _udp = null; _tcp = null;
        }

        private void OnPelco(byte[] data)
        {
            var cmd = PelcoDCommandParser.Parse(data);
            Log("RX " + ToHex(data) + "  | " + cmd.Describe());
            // Pelco-D fire-and-forget — no reply by spec.
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
