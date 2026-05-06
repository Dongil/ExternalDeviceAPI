namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// Builds VISCA reply byte sequences from an emulated camera (source address).
    /// Reply header byte z = ((sourceAddress + 8) &lt;&lt; 4):
    ///   addr 1 → 0x90, addr 2 → 0xA0, ..., addr 7 → 0xF0.
    /// (Per Sony VISCA spec: source-address-MSB indicates direction camera→controller.)
    /// </summary>
    public static class ViscaReplyBuilder
    {
        private static byte ReplyHeader(int sourceAddress)
        {
            int a = sourceAddress < 1 ? 1 : (sourceAddress > 7 ? 7 : sourceAddress);
            return (byte)((a + 8) << 4);
        }

        /// <summary>ACK: z0 4y FF (y = socket 1..2, default 1).</summary>
        public static byte[] Ack(int sourceAddress, int socket = 1)
        {
            return new byte[] { ReplyHeader(sourceAddress), (byte)(0x40 | (socket & 0x0F)), 0xFF };
        }

        /// <summary>Completion: z0 5y FF.</summary>
        public static byte[] Completion(int sourceAddress, int socket = 1)
        {
            return new byte[] { ReplyHeader(sourceAddress), (byte)(0x50 | (socket & 0x0F)), 0xFF };
        }

        /// <summary>Error: z0 6y EE FF (EE = error code).</summary>
        public static byte[] Error(int sourceAddress, byte errorCode, int socket = 1)
        {
            return new byte[] { ReplyHeader(sourceAddress), (byte)(0x60 | (socket & 0x0F)), errorCode, 0xFF };
        }
    }
}
