namespace Xeno.Framework.Camera.Protocols.Visca
{
    internal static class ViscaConstants
    {
        public const byte Terminator = 0xFF;
        public const byte BroadcastHeader = 0x88;

        // Sony VISCA over IP payload type values (16-bit big-endian)
        public const ushort PayloadType_ViscaCommand = 0x0100;
        public const ushort PayloadType_ViscaInquiry = 0x0110;
        public const ushort PayloadType_ViscaReply = 0x0111;
        public const ushort PayloadType_ControlCommand = 0x0200;
        public const ushort PayloadType_ControlReply = 0x0201;

        // Sony VISCA over IP default UDP port
        public const int DefaultUdpPort = 52381;
    }

    internal static class ViscaHeader
    {
        /// <summary>
        /// Build the VISCA header byte = 0x80 | (address &amp; 0x0F).
        /// Address is clamped to 1..7 (VISCA single-controller addressing range).
        /// </summary>
        public static byte Build(int address)
        {
            int a = address < 1 ? 1 : (address > 7 ? 7 : address);
            return (byte)(0x80 | (a & 0x0F));
        }
    }
}
