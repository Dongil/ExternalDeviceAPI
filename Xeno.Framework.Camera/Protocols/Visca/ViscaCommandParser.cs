using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// Inverse of <see cref="ViscaCodec"/>: parses raw VISCA frames (8x ... FF) sent by a
    /// controller into structured <see cref="ViscaCommand"/> objects. Used by emulators to
    /// recognize incoming commands and prepare appropriate replies.
    /// </summary>
    public static class ViscaCommandParser
    {
        public static ViscaCommand Parse(byte[] data)
        {
            var cmd = new ViscaCommand { RawBytes = data };
            if (data == null || data.Length < 3) { cmd.Kind = ViscaCommandKind.Unknown; return cmd; }
            if ((data[0] & 0xF0) != 0x80) { cmd.Kind = ViscaCommandKind.Unknown; return cmd; }
            if (data[data.Length - 1] != 0xFF) { cmd.Kind = ViscaCommandKind.Unknown; return cmd; }

            cmd.Address = data[0] & 0x0F;

            // 8x 01 06 01 VV WW pp tt FF — Pan/Tilt Drive (or Stop when pp=tt=03)
            if (data.Length == 9 && data[1] == 0x01 && data[2] == 0x06 && data[3] == 0x01)
            {
                cmd.PanSpeed = data[4];
                cmd.TiltSpeed = data[5];
                byte pp = data[6], tt = data[7];
                if (pp == 0x03 && tt == 0x03) { cmd.Kind = ViscaCommandKind.PanTiltStop; return cmd; }
                cmd.Kind = ViscaCommandKind.PanTiltDrive;
                cmd.PanTiltDir = ResolvePtDir(pp, tt);
                return cmd;
            }
            // 8x 01 04 07 0p FF — Zoom
            if (data.Length == 6 && data[1] == 0x01 && data[2] == 0x04 && data[3] == 0x07)
            {
                byte p = data[4];
                if (p == 0x00) { cmd.Kind = ViscaCommandKind.ZoomStop; return cmd; }
                cmd.Kind = ViscaCommandKind.ZoomDrive;
                if ((p & 0xF0) == 0x20)      { cmd.ZoomDir = ZoomDirection.Tele; cmd.ZoomSpeed = p & 0x07; }
                else if ((p & 0xF0) == 0x30) { cmd.ZoomDir = ZoomDirection.Wide; cmd.ZoomSpeed = p & 0x07; }
                else if (p == 0x02)          { cmd.ZoomDir = ZoomDirection.Tele; cmd.ZoomSpeed = -1; }
                else if (p == 0x03)          { cmd.ZoomDir = ZoomDirection.Wide; cmd.ZoomSpeed = -1; }
                return cmd;
            }
            // 8x 01 04 08 0p FF — Focus (same shape as Zoom)
            if (data.Length == 6 && data[1] == 0x01 && data[2] == 0x04 && data[3] == 0x08)
            {
                byte p = data[4];
                if (p == 0x00) { cmd.Kind = ViscaCommandKind.FocusStop; return cmd; }
                cmd.Kind = ViscaCommandKind.FocusDrive;
                if ((p & 0xF0) == 0x20)      { cmd.FocusDir = FocusDirection.Far;  cmd.FocusSpeed = p & 0x07; }
                else if ((p & 0xF0) == 0x30) { cmd.FocusDir = FocusDirection.Near; cmd.FocusSpeed = p & 0x07; }
                else if (p == 0x02)          { cmd.FocusDir = FocusDirection.Far;  cmd.FocusSpeed = -1; }
                else if (p == 0x03)          { cmd.FocusDir = FocusDirection.Near; cmd.FocusSpeed = -1; }
                return cmd;
            }
            // 8x 01 04 3F 0[12] 0p FF — Memory Set/Recall/Reset
            if (data.Length == 7 && data[1] == 0x01 && data[2] == 0x04 && data[3] == 0x3F)
            {
                byte action = data[4];
                cmd.PresetNumber = data[5] & 0x7F;
                if (action == 0x00) cmd.Kind = ViscaCommandKind.PresetReset;
                else if (action == 0x01) cmd.Kind = ViscaCommandKind.PresetSet;
                else if (action == 0x02) cmd.Kind = ViscaCommandKind.PresetRecall;
                return cmd;
            }
            // 8x 01 06 06 0X FF — OSD (placeholder per ViscaCodec)
            if (data.Length == 6 && data[1] == 0x01 && data[2] == 0x06 && data[3] == 0x06)
            {
                switch (data[4])
                {
                    case 0x02: cmd.Kind = ViscaCommandKind.OsdOn; break;
                    case 0x03: cmd.Kind = ViscaCommandKind.OsdOff; break;
                    case 0x04: cmd.Kind = ViscaCommandKind.OsdBack; break;
                    case 0x05: cmd.Kind = ViscaCommandKind.OsdSelect; break;
                    default:   cmd.Kind = ViscaCommandKind.Unknown; break;
                }
                return cmd;
            }
            // Inquiry: 8x 09 06 23 FF (PT status) / 8x 09 04 47 FF (Zoom pos)
            if (data.Length == 5 && data[1] == 0x09)
            {
                if (data[2] == 0x06 && data[3] == 0x23) cmd.Kind = ViscaCommandKind.InquiryPanTiltStatus;
                else if (data[2] == 0x04 && data[3] == 0x47) cmd.Kind = ViscaCommandKind.InquiryZoomPosition;
                return cmd;
            }

            cmd.Kind = ViscaCommandKind.Unknown;
            return cmd;
        }

        private static PanTiltDirection ResolvePtDir(byte pp, byte tt)
        {
            // pp: 01=Left 02=Right 03=Stop ; tt: 01=Up 02=Down 03=Stop
            bool L = pp == 0x01, R = pp == 0x02;
            bool U = tt == 0x01, D = tt == 0x02;
            if (U && L) return PanTiltDirection.UpLeft;
            if (U && R) return PanTiltDirection.UpRight;
            if (D && L) return PanTiltDirection.DownLeft;
            if (D && R) return PanTiltDirection.DownRight;
            if (U) return PanTiltDirection.Up;
            if (D) return PanTiltDirection.Down;
            if (L) return PanTiltDirection.Left;
            if (R) return PanTiltDirection.Right;
            return PanTiltDirection.Stop;
        }
    }
}
