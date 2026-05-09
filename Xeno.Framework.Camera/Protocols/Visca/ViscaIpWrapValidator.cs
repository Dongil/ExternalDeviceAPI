using System.Collections.Generic;
using System.Net;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    public enum ValidationSeverity { Info, Warning }

    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }

        public ValidationIssue(ValidationSeverity sev, string code, string message)
        {
            Severity = sev; Code = code; Message = message;
        }

        public string Format()
        {
            string icon = Severity == ValidationSeverity.Warning ? "[WARN]" : "[INFO]";
            return icon + " " + Code + ": " + Message;
        }
    }

    /// <summary>
    /// Stateful per-source-IP VISCA-over-IP wrap header validator.
    /// Detects sequence regression/duplicate, length mismatch, unknown payload type,
    /// and inner VISCA frame anomalies (invalid header byte, missing terminator).
    /// Thread-safe (single lock — call frequency bounded by network packet rate).
    /// Reset() clears per-source state on emulator stop.
    /// </summary>
    public sealed class ViscaIpWrapValidator
    {
        private readonly object _lock = new object();
        private readonly Dictionary<IPAddress, uint> _lastSeqBySource =
            new Dictionary<IPAddress, uint>();

        private const ushort Type_ViscaCommand   = 0x0100;
        private const ushort Type_ViscaInquiry   = 0x0110;
        private const ushort Type_ViscaReply     = 0x0111;
        private const ushort Type_ControlCommand = 0x0200;
        private const ushort Type_ControlReply   = 0x0201;

        public IReadOnlyList<ValidationIssue> ValidateWrap(byte[] data, IPAddress source)
        {
            var issues = new List<ValidationIssue>();
            if (data == null || data.Length < 8)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "MALFORMED",
                    "Packet too short (<8 bytes) for VISCA-IP wrap"));
                return issues;
            }

            ushort type        = (ushort)((data[0] << 8) | data[1]);
            ushort declaredLen = (ushort)((data[2] << 8) | data[3]);
            uint   seq         = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
            int    actualInnerLen = data.Length - 8;

            if (type != Type_ViscaCommand && type != Type_ViscaInquiry
                && type != Type_ViscaReply  && type != Type_ControlCommand
                && type != Type_ControlReply)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Info, "UNKNOWN_TYPE",
                    "PayloadType=0x" + type.ToString("X4") + " (not in standard set)"));
            }

            if (declaredLen != actualInnerLen)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "LENGTH_MISMATCH",
                    "declared=" + declaredLen + " bytes, actual inner=" + actualInnerLen + " bytes"));
            }

            if (actualInnerLen > 0
                && (type == Type_ViscaCommand || type == Type_ViscaInquiry || type == Type_ViscaReply))
            {
                byte first = data[8];
                byte last  = data[data.Length - 1];
                if ((first & 0xF0) != 0x80 && (first & 0xF0) != 0x90)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "INVALID_VISCA_HEADER",
                        "inner first byte 0x" + first.ToString("X2")
                        + " - expected 0x8x (cmd) or 0x9x (reply)"));
                }
                if (last != 0xFF)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "MISSING_VISCA_TERMINATOR",
                        "inner last byte 0x" + last.ToString("X2") + " - expected 0xFF"));
                }
            }

            if (source != null)
            {
                lock (_lock)
                {
                    if (_lastSeqBySource.TryGetValue(source, out uint lastSeq))
                    {
                        if (seq < lastSeq)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "SEQ_REGRESSION",
                                "last=" + lastSeq + ", got=" + seq + " (controller reset sequence?)"));
                        }
                        else if (seq == lastSeq)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "SEQ_DUPLICATE",
                                "seq=" + seq + " repeated (replay or controller bug)"));
                        }
                    }
                    _lastSeqBySource[source] = seq;
                }
            }

            return issues;
        }

        public void Reset()
        {
            lock (_lock) { _lastSeqBySource.Clear(); }
        }
    }
}
