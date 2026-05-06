using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Pelco
{
    public enum PelcoDCommandKind
    {
        Unknown,
        Stop,
        PanTiltDrive,
        ZoomDrive,
        FocusDrive,
        IrisOpen,
        IrisClose,
        PresetSet,
        PresetClear,
        PresetRecall
    }

    public sealed class PelcoDCommand
    {
        public PelcoDCommandKind Kind { get; set; }
        public int Address { get; set; }
        public PanTiltDirection? PanTiltDir { get; set; }
        public ZoomDirection? ZoomDir { get; set; }
        public FocusDirection? FocusDir { get; set; }
        public int PanSpeed { get; set; }
        public int TiltSpeed { get; set; }
        public int PresetNumber { get; set; }
        public byte[] RawBytes { get; set; }

        public string Describe()
        {
            switch (Kind)
            {
                case PelcoDCommandKind.Stop: return "Pelco Stop";
                case PelcoDCommandKind.PanTiltDrive:
                    return "Pelco PT " + PanTiltDir + " (pan=0x" + PanSpeed.ToString("X2")
                         + ", tilt=0x" + TiltSpeed.ToString("X2") + ")";
                case PelcoDCommandKind.ZoomDrive:    return "Pelco Zoom " + ZoomDir;
                case PelcoDCommandKind.FocusDrive:   return "Pelco Focus " + FocusDir;
                case PelcoDCommandKind.IrisOpen:     return "Pelco Iris Open";
                case PelcoDCommandKind.IrisClose:    return "Pelco Iris Close";
                case PelcoDCommandKind.PresetSet:    return "Pelco Preset Set " + PresetNumber;
                case PelcoDCommandKind.PresetRecall: return "Pelco Preset Recall " + PresetNumber;
                case PelcoDCommandKind.PresetClear:  return "Pelco Preset Clear " + PresetNumber;
                default:                             return "Unknown Pelco-D";
            }
        }
    }
}
