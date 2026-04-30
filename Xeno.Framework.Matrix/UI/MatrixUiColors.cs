using System.Drawing;
using System.Windows.Forms;

namespace Xeno.Framework.Matrix.UI
{
    /// <summary>
    /// Shared color palette for matrix control visuals. Public so that external solutions
    /// can inspect or align their own UI.
    /// </summary>
    public static class MatrixUiColors
    {
        // Cell selection states
        public static readonly Color InputSelected    = Color.FromArgb(144, 238, 144); // #90EE90 LightGreen
        public static readonly Color OutputConnected  = Color.FromArgb(255, 179, 71);  // #FFB347 Pastel Orange
        public static readonly Color OutputPending    = Color.FromArgb(255, 215, 0);   // #FFD700 Gold
        public static readonly Color CellDefault      = Color.White;
        public static readonly Color CellDisabled     = SystemColors.ControlDark;

        // Row backgrounds
        public static readonly Color NoRowBackground      = SystemColors.ControlLight;
        public static readonly Color ConnectRowBackground = Color.FromArgb(176, 224, 230); // #B0E0E6 PowderBlue
        public static readonly Color RowLabelBackground   = SystemColors.ControlLight;

        // Borders
        public static readonly Color CellBorder       = Color.Silver;
    }
}
