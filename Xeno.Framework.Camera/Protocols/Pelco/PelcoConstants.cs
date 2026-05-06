namespace Xeno.Framework.Camera.Protocols.Pelco
{
    internal static class PelcoConstants
    {
        public const byte Sync = 0xFF;

        // Command 1 byte bit definitions
        public const byte Cmd1_Sense          = 0x80;   // rarely used by software controllers
        public const byte Cmd1_AutoManualScan = 0x10;
        public const byte Cmd1_CameraOnOff    = 0x08;
        public const byte Cmd1_IrisClose      = 0x04;
        public const byte Cmd1_IrisOpen       = 0x02;
        public const byte Cmd1_FocusNear      = 0x01;

        // Command 2 byte bit definitions
        public const byte Cmd2_FocusFar  = 0x80;
        public const byte Cmd2_ZoomWide  = 0x40;
        public const byte Cmd2_ZoomTele  = 0x20;
        public const byte Cmd2_TiltDown  = 0x10;
        public const byte Cmd2_TiltUp    = 0x08;
        public const byte Cmd2_PanLeft   = 0x04;
        public const byte Cmd2_PanRight  = 0x02;
        // bit0 of Cmd2 is always 0 (reserved)

        // Extended command 2 codes — Pan/Tilt speed bytes are repurposed as data1/data2
        public const byte ExtCmd2_SetPreset    = 0x03;
        public const byte ExtCmd2_ClearPreset  = 0x05;
        public const byte ExtCmd2_CallPreset   = 0x07;

        // Address range
        public const int MinAddress = 1;
        public const int MaxAddress = 255;

        // Speed range (0-63 typical, 0xFF for Turbo special)
        public const int MinSpeed = 0x00;
        public const int MaxSpeed = 0x3F;
    }
}
