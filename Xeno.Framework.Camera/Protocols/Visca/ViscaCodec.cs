using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// Builds VISCA command byte sequences (without IP transport header).
    /// Output is the raw VISCA payload starting with the address header byte (8x) and
    /// ending with the terminator FF. Both Serial and UDP transports send the identical payload.
    /// </summary>
    internal static class ViscaCodec
    {
        // ----- Pan/Tilt Drive / Stop -----
        // Format: 8x 01 06 01 VV WW pp tt FF
        //   VV = pan speed, WW = tilt speed
        //   pp: 01=Left  02=Right 03=Stop
        //   tt: 01=Up    02=Down  03=Stop

        public static byte[] PanTiltDrive(int address, PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            byte pp, tt;
            ResolveDirection(dir, out pp, out tt);
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x01,
                (byte)(panSpeed & 0xFF),
                (byte)(tiltSpeed & 0xFF),
                pp, tt,
                ViscaConstants.Terminator
            };
        }

        public static byte[] PanTiltStop(int address)
        {
            // Speed bytes must be non-zero per spec; use minimum 0x01.
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x01,
                0x01, 0x01,
                0x03, 0x03,
                ViscaConstants.Terminator
            };
        }

        private static void ResolveDirection(PanTiltDirection dir, out byte pp, out byte tt)
        {
            switch (dir)
            {
                case PanTiltDirection.Up:        pp = 0x03; tt = 0x01; break;
                case PanTiltDirection.Down:      pp = 0x03; tt = 0x02; break;
                case PanTiltDirection.Left:      pp = 0x01; tt = 0x03; break;
                case PanTiltDirection.Right:     pp = 0x02; tt = 0x03; break;
                case PanTiltDirection.UpLeft:    pp = 0x01; tt = 0x01; break;
                case PanTiltDirection.UpRight:   pp = 0x02; tt = 0x01; break;
                case PanTiltDirection.DownLeft:  pp = 0x01; tt = 0x02; break;
                case PanTiltDirection.DownRight: pp = 0x02; tt = 0x02; break;
                default:                         pp = 0x03; tt = 0x03; break;
            }
        }

        // ----- Zoom -----
        // 8x 01 04 07 0p FF
        //   p = 0:Stop, 2:Tele(std), 3:Wide(std), 2p:Tele variable (p=0..7), 3p:Wide variable

        public static byte[] ZoomDrive(int address, ZoomDirection dir, int speed)
        {
            byte p;
            switch (dir)
            {
                case ZoomDirection.Tele:
                    p = (byte)(speed < 0 ? 0x02 : (0x20 | (speed & 0x07)));
                    break;
                case ZoomDirection.Wide:
                    p = (byte)(speed < 0 ? 0x03 : (0x30 | (speed & 0x07)));
                    break;
                default:
                    p = 0x00;
                    break;
            }
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x04, 0x07, p,
                ViscaConstants.Terminator
            };
        }

        public static byte[] ZoomStop(int address)
        {
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x04, 0x07, 0x00,
                ViscaConstants.Terminator
            };
        }

        // ----- Focus -----
        // 8x 01 04 08 0p FF (same shape as Zoom)
        //   p = 0:Stop, 2:Far(std), 3:Near(std), 2p:Far variable, 3p:Near variable

        public static byte[] FocusDrive(int address, FocusDirection dir, int speed)
        {
            byte p;
            switch (dir)
            {
                case FocusDirection.Far:
                    p = (byte)(speed < 0 ? 0x02 : (0x20 | (speed & 0x07)));
                    break;
                case FocusDirection.Near:
                    p = (byte)(speed < 0 ? 0x03 : (0x30 | (speed & 0x07)));
                    break;
                default:
                    p = 0x00;
                    break;
            }
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x04, 0x08, p,
                ViscaConstants.Terminator
            };
        }

        public static byte[] FocusStop(int address)
        {
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x04, 0x08, 0x00,
                ViscaConstants.Terminator
            };
        }

        // ----- Memory (Preset) -----
        // 8x 01 04 3F 02 0p FF (Recall)
        // 8x 01 04 3F 01 0p FF (Set)
        // 8x 01 04 3F 00 0p FF (Reset)

        public static byte[] PresetRecall(int address, int presetNumber)
        {
            int p = presetNumber & 0x7F;
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x04, 0x3F, 0x02, (byte)p,
                ViscaConstants.Terminator
            };
        }

        public static byte[] PresetSet(int address, int presetNumber)
        {
            int p = presetNumber & 0x7F;
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x04, 0x3F, 0x01, (byte)p,
                ViscaConstants.Terminator
            };
        }

        // ----- OSD / Menu -----
        // NOTE: OSD command bytes vary by model. Sequences below are the common Sony convention
        // (Menu Display + IR-emulated Up/Down/Enter for navigation). Verify against the
        // device manual when adding/changing models. Unsupported menu actions should be
        // marked OsdSupported=false in CameraCapabilities and the buttons disabled in UI.
        //
        // Display On/Off: 8x 01 06 06 02 FF (On) / 8x 01 06 06 03 FF (Off)
        // Menu navigate via IR remote emulation (8x 01 06 1E ... FF) — values below are placeholders.

        public static byte[] OsdOn(int address)
        {
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x06, 0x02,
                ViscaConstants.Terminator
            };
        }

        public static byte[] OsdOff(int address)
        {
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x06, 0x03,
                ViscaConstants.Terminator
            };
        }

        public static byte[] OsdSelect(int address)
        {
            // Placeholder: send "Enter" as IR-emulated key. Confirm against EVI-H100 / SRG-300H manuals.
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x06, 0x05,
                ViscaConstants.Terminator
            };
        }

        public static byte[] OsdBack(int address)
        {
            // Placeholder: send "Back" as IR-emulated key. Confirm against device manuals.
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x06, 0x04,
                ViscaConstants.Terminator
            };
        }
    }
}
