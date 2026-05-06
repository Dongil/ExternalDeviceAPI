namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// VISCA reply categorizer.
    /// Reply formats (after header byte z0, where z = receiving address; e.g. 0x90 for source 0x81):
    ///   ACK:        z0 4y FF       (y = socket 1..2)
    ///   Completion: z0 5y FF
    ///   Error:      z0 6y EE FF    (EE = error code)
    /// </summary>
    internal static class ViscaResponseParser
    {
        public enum ReplyKind
        {
            Unknown,
            Ack,
            Completion,
            Error
        }

        public static ReplyKind Classify(byte[] reply, out byte errorCode)
        {
            errorCode = 0;
            if (reply == null || reply.Length < 3) return ReplyKind.Unknown;
            if (reply[reply.Length - 1] != 0xFF) return ReplyKind.Unknown;

            byte b1 = reply[1];
            if ((b1 & 0xF0) == 0x40) return ReplyKind.Ack;
            if ((b1 & 0xF0) == 0x50) return ReplyKind.Completion;
            if ((b1 & 0xF0) == 0x60)
            {
                if (reply.Length >= 4) errorCode = reply[2];
                return ReplyKind.Error;
            }
            return ReplyKind.Unknown;
        }

        public static string DescribeError(byte code)
        {
            switch (code)
            {
                case 0x01: return "Message length error";
                case 0x02: return "Syntax error";
                case 0x03: return "Command buffer full";
                case 0x04: return "Command cancelled";
                case 0x05: return "No socket";
                case 0x41: return "Command not executable";
                default:   return "Unknown error 0x" + code.ToString("X2");
            }
        }

        /// <summary>
        /// Short human-readable label for a VISCA reply byte sequence (ACK / Completion / Error / Unknown).
        /// Used in transport drain logging so readers immediately recognise normal late-Completion packets.
        /// </summary>
        public static string DescribeReply(byte[] data)
        {
            if (data == null || data.Length < 3) return "short(" + (data == null ? 0 : data.Length) + "B)";
            byte err;
            var kind = Classify(data, out err);
            switch (kind)
            {
                case ReplyKind.Ack:        return "ACK";
                case ReplyKind.Completion: return "Completion";
                case ReplyKind.Error:      return "Error[" + DescribeError(err) + "]";
                default:                   return "Unknown";
            }
        }
    }
}
