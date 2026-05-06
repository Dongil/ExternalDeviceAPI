using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Pelco
{
    /// <summary>
    /// Pelco-D 7-byte packet builder.
    /// Format: [Sync 0xFF] [Address 1-255] [Cmd1] [Cmd2] [Pan-Speed] [Tilt-Speed] [Checksum]
    /// Checksum = (Address + Cmd1 + Cmd2 + Pan-Speed + Tilt-Speed) mod 256
    /// </summary>
    internal static class PelcoDCodec
    {
        // ----- Pan / Tilt -----
        public static byte[] PanTiltDrive(int address, PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            byte cmd2 = 0;
            switch (dir)
            {
                case PanTiltDirection.Up:        cmd2 = PelcoConstants.Cmd2_TiltUp; break;
                case PanTiltDirection.Down:      cmd2 = PelcoConstants.Cmd2_TiltDown; break;
                case PanTiltDirection.Left:      cmd2 = PelcoConstants.Cmd2_PanLeft; break;
                case PanTiltDirection.Right:     cmd2 = PelcoConstants.Cmd2_PanRight; break;
                case PanTiltDirection.UpLeft:    cmd2 = (byte)(PelcoConstants.Cmd2_TiltUp | PelcoConstants.Cmd2_PanLeft); break;
                case PanTiltDirection.UpRight:   cmd2 = (byte)(PelcoConstants.Cmd2_TiltUp | PelcoConstants.Cmd2_PanRight); break;
                case PanTiltDirection.DownLeft:  cmd2 = (byte)(PelcoConstants.Cmd2_TiltDown | PelcoConstants.Cmd2_PanLeft); break;
                case PanTiltDirection.DownRight: cmd2 = (byte)(PelcoConstants.Cmd2_TiltDown | PelcoConstants.Cmd2_PanRight); break;
                default:                         cmd2 = 0; break;
            }
            return Build(address, 0x00, cmd2, (byte)(panSpeed & 0xFF), (byte)(tiltSpeed & 0xFF));
        }

        public static byte[] PanTiltStop(int address)
        {
            return Build(address, 0x00, 0x00, 0x00, 0x00);
        }

        // ----- Zoom (Pelco-D has no separate zoom-speed byte) -----
        public static byte[] ZoomDrive(int address, ZoomDirection dir, int speed)
        {
            byte cmd2;
            switch (dir)
            {
                case ZoomDirection.Tele: cmd2 = PelcoConstants.Cmd2_ZoomTele; break;
                case ZoomDirection.Wide: cmd2 = PelcoConstants.Cmd2_ZoomWide; break;
                default:                 cmd2 = 0; break;
            }
            return Build(address, 0x00, cmd2, 0x00, 0x00);
        }

        public static byte[] ZoomStop(int address)
        {
            return PanTiltStop(address);
        }

        // ----- Focus -----
        public static byte[] FocusDrive(int address, FocusDirection dir, int speed)
        {
            byte cmd1 = 0, cmd2 = 0;
            switch (dir)
            {
                case FocusDirection.Far:  cmd2 = PelcoConstants.Cmd2_FocusFar; break;
                case FocusDirection.Near: cmd1 = PelcoConstants.Cmd1_FocusNear; break;
                default: break;
            }
            return Build(address, cmd1, cmd2, 0x00, 0x00);
        }

        public static byte[] FocusStop(int address)
        {
            return PanTiltStop(address);
        }

        // ----- Preset (Memory) -----
        public static byte[] PresetSet(int address, int presetNumber)
        {
            return Build(address, 0x00, PelcoConstants.ExtCmd2_SetPreset, 0x00, (byte)(presetNumber & 0xFF));
        }

        public static byte[] PresetRecall(int address, int presetNumber)
        {
            return Build(address, 0x00, PelcoConstants.ExtCmd2_CallPreset, 0x00, (byte)(presetNumber & 0xFF));
        }

        public static byte[] PresetClear(int address, int presetNumber)
        {
            return Build(address, 0x00, PelcoConstants.ExtCmd2_ClearPreset, 0x00, (byte)(presetNumber & 0xFF));
        }

        // ----- Internal: build 7-byte packet with checksum -----
        private static byte[] Build(int address, byte cmd1, byte cmd2, byte data1, byte data2)
        {
            byte addr = (byte)(address < 1 ? 1 : (address > 255 ? 255 : address));
            byte csum = (byte)((addr + cmd1 + cmd2 + data1 + data2) % 256);
            return new byte[]
            {
                PelcoConstants.Sync,
                addr,
                cmd1,
                cmd2,
                data1,
                data2,
                csum
            };
        }
    }
}
