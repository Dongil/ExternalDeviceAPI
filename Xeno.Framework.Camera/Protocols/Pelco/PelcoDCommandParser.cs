using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Pelco
{
    /// <summary>
    /// Inverse of <see cref="PelcoDCodec"/>: parses raw 7-byte Pelco-D frames sent by a
    /// controller into structured <see cref="PelcoDCommand"/> objects.
    /// </summary>
    public static class PelcoDCommandParser
    {
        public static PelcoDCommand Parse(byte[] data)
        {
            var cmd = new PelcoDCommand { RawBytes = data };
            if (data == null || data.Length != 7 || data[0] != 0xFF)
            {
                cmd.Kind = PelcoDCommandKind.Unknown;
                return cmd;
            }
            // Verify checksum
            byte expected = (byte)((data[1] + data[2] + data[3] + data[4] + data[5]) % 256);
            if (expected != data[6])
            {
                cmd.Kind = PelcoDCommandKind.Unknown;
                return cmd;
            }
            cmd.Address = data[1];
            byte cmd1 = data[2], cmd2 = data[3];
            cmd.PanSpeed = data[4];
            cmd.TiltSpeed = data[5];

            // Stop = all zero after addr
            if (cmd1 == 0 && cmd2 == 0 && data[4] == 0 && data[5] == 0)
            {
                cmd.Kind = PelcoDCommandKind.Stop;
                return cmd;
            }
            // Extended: cmd1=0x00, cmd2 in {0x03,0x05,0x07}, data2 = preset#
            if (cmd1 == 0x00 && (cmd2 == 0x03 || cmd2 == 0x05 || cmd2 == 0x07))
            {
                cmd.PresetNumber = data[5];
                if (cmd2 == 0x03) cmd.Kind = PelcoDCommandKind.PresetSet;
                else if (cmd2 == 0x05) cmd.Kind = PelcoDCommandKind.PresetClear;
                else cmd.Kind = PelcoDCommandKind.PresetRecall;
                return cmd;
            }
            // Iris
            if ((cmd1 & PelcoConstants.Cmd1_IrisOpen) != 0)  { cmd.Kind = PelcoDCommandKind.IrisOpen; return cmd; }
            if ((cmd1 & PelcoConstants.Cmd1_IrisClose) != 0) { cmd.Kind = PelcoDCommandKind.IrisClose; return cmd; }
            // Focus
            if ((cmd1 & PelcoConstants.Cmd1_FocusNear) != 0) { cmd.Kind = PelcoDCommandKind.FocusDrive; cmd.FocusDir = FocusDirection.Near; return cmd; }
            if ((cmd2 & PelcoConstants.Cmd2_FocusFar) != 0)  { cmd.Kind = PelcoDCommandKind.FocusDrive; cmd.FocusDir = FocusDirection.Far;  return cmd; }
            // Zoom
            if ((cmd2 & PelcoConstants.Cmd2_ZoomTele) != 0)  { cmd.Kind = PelcoDCommandKind.ZoomDrive; cmd.ZoomDir = ZoomDirection.Tele; return cmd; }
            if ((cmd2 & PelcoConstants.Cmd2_ZoomWide) != 0)  { cmd.Kind = PelcoDCommandKind.ZoomDrive; cmd.ZoomDir = ZoomDirection.Wide; return cmd; }
            // Pan/Tilt
            bool U = (cmd2 & PelcoConstants.Cmd2_TiltUp) != 0;
            bool D = (cmd2 & PelcoConstants.Cmd2_TiltDown) != 0;
            bool L = (cmd2 & PelcoConstants.Cmd2_PanLeft) != 0;
            bool R = (cmd2 & PelcoConstants.Cmd2_PanRight) != 0;
            if (U || D || L || R)
            {
                cmd.Kind = PelcoDCommandKind.PanTiltDrive;
                if (U && L)      cmd.PanTiltDir = PanTiltDirection.UpLeft;
                else if (U && R) cmd.PanTiltDir = PanTiltDirection.UpRight;
                else if (D && L) cmd.PanTiltDir = PanTiltDirection.DownLeft;
                else if (D && R) cmd.PanTiltDir = PanTiltDirection.DownRight;
                else if (U)      cmd.PanTiltDir = PanTiltDirection.Up;
                else if (D)      cmd.PanTiltDir = PanTiltDirection.Down;
                else if (L)      cmd.PanTiltDir = PanTiltDirection.Left;
                else if (R)      cmd.PanTiltDir = PanTiltDirection.Right;
                return cmd;
            }
            cmd.Kind = PelcoDCommandKind.Unknown;
            return cmd;
        }
    }
}
