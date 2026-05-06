using System;
using DeviceEmulator.Core;
using DeviceEmulator.Transports;
using Xeno.Framework.Camera.Protocols.Visca;

namespace DeviceEmulator.Emulators
{
    public sealed class EviH100Emulator : DeviceEmulatorBase
    {
        private SerialServerTransport _serial;

        public EviH100Emulator()
        {
            DeviceType = "Camera"; Brand = "Sony"; Model = "EVI-H100";
            Description = "Sony EVI-H100 PTZ (RS-232/422 VISCA)";
        }

        public override void Configure(EmulatorConfig cfg) { _cfg = cfg; }

        public override void Start()
        {
            if (_cfg == null || string.IsNullOrEmpty(_cfg.ComPort))
                throw new InvalidOperationException("ComPort not configured");
            _serial = new SerialServerTransport
            {
                PortName = _cfg.ComPort,
                BaudRate = _cfg.BaudRate <= 0 ? 9600 : _cfg.BaudRate,
                Framing = SerialServerTransport.FrameMode.ViscaTerminator
            };
            _serial.OnLog = msg => Log("[Serial] " + msg);
            _serial.OnPacketReceived = OnVisca;
            _serial.Start();
            _listening = true;
        }

        public override void Stop()
        {
            _listening = false;
            try { if (_serial != null) _serial.Dispose(); } catch { }
            _serial = null;
        }

        private async void OnVisca(byte[] data)
        {
            var cmd = ViscaCommandParser.Parse(data);
            Log("RX " + ToHex(data) + "  | " + cmd.Describe());

            if (Options.InjectMalformed)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectMalformed = false;
                Log("INJECT malformed — sending garbage");
                await SendReplyAsync(() => { var s = _serial; if (s != null) s.SendReply(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFF }); });
                return;
            }
            if (Options.InjectNak)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectNak = false;
                var err = ViscaReplyBuilder.Error(cmd.Address, 0x02);
                Log("INJECT NAK — TX " + ToHex(err));
                await SendReplyAsync(() => { var s = _serial; if (s != null) s.SendReply(err); });
                return;
            }

            var ack = ViscaReplyBuilder.Ack(cmd.Address);
            await SendReplyAsync(() => { var s = _serial; if (s != null) { s.SendReply(ack); Log("TX ACK " + ToHex(ack)); } });
            var done = ViscaReplyBuilder.Completion(cmd.Address);
            await SendReplyAsync(() => { var s = _serial; if (s != null) { s.SendReply(done); Log("TX Completion " + ToHex(done)); } });
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
