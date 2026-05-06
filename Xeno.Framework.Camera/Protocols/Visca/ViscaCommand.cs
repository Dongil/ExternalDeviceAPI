using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// Categorized VISCA command kind (parsed from controller's TX byte sequence).
    /// </summary>
    public enum ViscaCommandKind
    {
        Unknown,
        PanTiltDrive,
        PanTiltStop,
        ZoomDrive,
        ZoomStop,
        FocusDrive,
        FocusStop,
        PresetSet,
        PresetRecall,
        PresetReset,
        OsdOn,
        OsdOff,
        OsdSelect,
        OsdBack,
        InquiryPanTiltStatus,
        InquiryZoomPosition
    }

    /// <summary>Parsed VISCA command + parameters (output of <see cref="ViscaCommandParser"/>).</summary>
    public sealed class ViscaCommand
    {
        public ViscaCommandKind Kind { get; set; }
        public int Address { get; set; }
        public PanTiltDirection? PanTiltDir { get; set; }
        public ZoomDirection? ZoomDir { get; set; }
        public FocusDirection? FocusDir { get; set; }
        public int PanSpeed { get; set; }
        public int TiltSpeed { get; set; }
        public int ZoomSpeed { get; set; }
        public int FocusSpeed { get; set; }
        public int PresetNumber { get; set; }
        public byte[] RawBytes { get; set; }

        /// <summary>Human-readable description for logs.</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case ViscaCommandKind.PanTiltDrive:
                    return "PT Drive " + PanTiltDir + " (pan=0x" + PanSpeed.ToString("X2")
                         + ", tilt=0x" + TiltSpeed.ToString("X2") + ")";
                case ViscaCommandKind.PanTiltStop:        return "PT Stop";
                case ViscaCommandKind.ZoomDrive:          return "Zoom " + ZoomDir + " (speed=" + ZoomSpeed + ")";
                case ViscaCommandKind.ZoomStop:           return "Zoom Stop";
                case ViscaCommandKind.FocusDrive:         return "Focus " + FocusDir + " (speed=" + FocusSpeed + ")";
                case ViscaCommandKind.FocusStop:          return "Focus Stop";
                case ViscaCommandKind.PresetSet:          return "Preset Set " + PresetNumber;
                case ViscaCommandKind.PresetRecall:       return "Preset Recall " + PresetNumber;
                case ViscaCommandKind.PresetReset:        return "Preset Reset " + PresetNumber;
                case ViscaCommandKind.OsdOn:              return "OSD On";
                case ViscaCommandKind.OsdOff:             return "OSD Off";
                case ViscaCommandKind.OsdSelect:          return "OSD Select";
                case ViscaCommandKind.OsdBack:            return "OSD Back";
                case ViscaCommandKind.InquiryPanTiltStatus: return "Inquiry PT Status";
                case ViscaCommandKind.InquiryZoomPosition:  return "Inquiry Zoom Position";
                default:                                  return "Unknown VISCA";
            }
        }
    }
}
