using System;
using System.Windows.Forms;
using PN8080Controller.UI;

namespace PN8080Controller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.ThreadException += (sender, e) =>
                MessageBox.Show(e.Exception.ToString(), "예상치 못한 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                MessageBox.Show(e.ExceptionObject?.ToString(), "치명적 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
