using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Xeno.Framework.Camera.Core;
using Xeno.Framework.Camera.UI;

namespace CameraController.UI
{
    public partial class MainForm : Form
    {
        private const int MaxLogLines = 2000;
        private readonly Queue<string> _logBuffer = new Queue<string>();
        private readonly UserSettings _settings;
        private bool _logPaused;

        public MainForm()
        {
            InitializeComponent();
            _settings = UserSettings.Load();

            _mnuSlots3.Click += (s, e) => SetSlots(3);
            _mnuSlots4.Click += (s, e) => SetSlots(4);
            _mnuSlots5.Click += (s, e) => SetSlots(5);
            _mnuSlots6.Click += (s, e) => SetSlots(6);
            _mnuClearLog.Click += (s, e) => ClearLog();

            _btnLogCopy.Click  += (s, e) => CopyLogToClipboard();
            _btnLogClear.Click += (s, e) => ClearLog();
            _btnLogSave.Click  += (s, e) => SaveLogToFile();
            _btnLogPause.Click += (s, e) => ToggleLogPaused();

            var tt = new ToolTip();
            tt.SetToolTip(_btnLogCopy,  "전체 로그를 클립보드에 복사");
            tt.SetToolTip(_btnLogClear, "로그 화면 지우기");
            tt.SetToolTip(_btnLogSave,  "로그를 .log 파일로 저장");
            tt.SetToolTip(_btnLogPause, "수신 로그 일시정지/재개");

            _cameraControl.SlotCount = _settings.SlotCount;
            _cameraControl.AttachFactory(new CameraServiceFactory());
            _cameraControl.ServiceLog += OnServiceLog;

            if (_settings.Slots.Count > 0)
            {
                _cameraControl.ApplySlotConfigs(_settings.Slots.ToArray());
            }

            UpdateSlotMenuChecks();

            this.FormClosing += OnFormClosing;
        }

        private void OnServiceLog(object sender, ServiceLogEventArgs args)
        {
            if (_logPaused) return;
            string dirTag;
            switch (args.Entry.Direction)
            {
                case LogDirection.Tx:    dirTag = "TX   "; break;
                case LogDirection.Rx:    dirTag = "  RX "; break;
                case LogDirection.Error: dirTag = "ERR  "; break;
                default:                 dirTag = "INFO "; break;
            }
            AppendLog("[Cam " + (args.SlotIndex + 1) + "] " + dirTag + args.Entry.Text);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                var configs = _cameraControl.GetSlotConfigs();
                _settings.Slots.Clear();
                foreach (var c in configs) _settings.Slots.Add(c);
                _settings.SlotCount = _cameraControl.SlotCount;
                _settings.Save();
                _cameraControl.ServiceLog -= OnServiceLog;
                _cameraControl.DetachAll();
            }
            catch { /* best effort */ }
        }

        private void SetSlots(int n)
        {
            if (n == _cameraControl.SlotCount) return;
            var configs = _cameraControl.GetSlotConfigs();
            _cameraControl.SlotCount = n;
            _cameraControl.ApplySlotConfigs(configs);
            UpdateSlotMenuChecks();
        }

        private void UpdateSlotMenuChecks()
        {
            int n = _cameraControl.SlotCount;
            _mnuSlots3.Checked = (n == 3);
            _mnuSlots4.Checked = (n == 4);
            _mnuSlots5.Checked = (n == 5);
            _mnuSlots6.Checked = (n == 6);
        }

        // ---------------- log toolbar ----------------

        private void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _logBuffer.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line);
            while (_logBuffer.Count > MaxLogLines) _logBuffer.Dequeue();
            _txtLog.Lines = _logBuffer.ToArray();
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
        }

        private void CopyLogToClipboard()
        {
            try
            {
                if (_txtLog.TextLength == 0) return;
                Clipboard.SetText(_txtLog.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "복사 실패: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearLog()
        {
            _logBuffer.Clear();
            _txtLog.Clear();
        }

        private void SaveLogToFile()
        {
            if (_txtLog.TextLength == 0) return;
            _dlgSaveLog.FileName = "camera-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
            if (_dlgSaveLog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                File.WriteAllText(_dlgSaveLog.FileName, _txtLog.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "저장 실패: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ToggleLogPaused()
        {
            _logPaused = !_logPaused;
            _btnLogPause.Text = _logPaused ? "재  개" : "일시정지";
            // Visual cue: tinted background while paused
            _btnLogPause.BackColor = _logPaused
                ? System.Drawing.Color.FromArgb(255, 215, 0)
                : System.Drawing.SystemColors.Control;
            _btnLogPause.UseVisualStyleBackColor = !_logPaused;
        }
    }
}
