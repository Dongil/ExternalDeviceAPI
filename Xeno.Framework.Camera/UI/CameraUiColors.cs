using System.Drawing;

namespace Xeno.Framework.Camera.UI
{
    internal static class CameraUiColors
    {
        // CAM selector buttons
        public static readonly Color CamSelected   = Color.FromArgb(102, 178, 255);  // matches Matrix selected
        public static readonly Color CamUnselected = SystemColors.Control;
        public static readonly Color CamDisabled   = Color.FromArgb(220, 220, 220);

        // Per-row connection status label
        public static readonly Color StatusIdle    = SystemColors.Control;
        public static readonly Color StatusOk      = Color.FromArgb(144, 238, 144);  // light green
        public static readonly Color StatusWarn    = Color.FromArgb(255, 179,  71);
        public static readonly Color StatusError   = Color.FromArgb(255,  99,  99);

        // Preset SET-mode highlight
        public static readonly Color PresetSetMode = Color.FromArgb(255, 215,   0);  // gold
        public static readonly Color PresetNormal  = SystemColors.Control;
    }
}
