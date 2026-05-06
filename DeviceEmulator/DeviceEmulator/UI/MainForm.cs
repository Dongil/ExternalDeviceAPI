using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;
using DeviceEmulator.Core;

namespace DeviceEmulator.UI
{
    public partial class MainForm : Form
    {
        private const int MaxLogLines = 2000;

        private readonly Queue<string> _logBuffer = new Queue<string>();
        private bool _logPaused;
        private IDeviceEmulator _emulator;

        public MainForm()
        {
            InitializeComponent();

            // Cascade combos
            _cmbDeviceType.SelectedIndexChanged += OnDeviceTypeChanged;
            _cmbModel.SelectedIndexChanged += OnModelChanged;
            _cmbTransport.SelectedIndexChanged += OnTransportChanged;

            _btnListen.Click += (s, e) => OnListenClick();
            _btnStop.Click += (s, e) => OnStopClick();

            _btnLogCopy.Click  += (s, e) => CopyLogToClipboard();
            _btnLogClear.Click += (s, e) => ClearLog();
            _btnLogSave.Click  += (s, e) => SaveLogToFile();
            _btnLogPause.Click += (s, e) => ToggleLogPaused();

            var tt = new ToolTip();
            tt.SetToolTip(_btnLogCopy,  "전체 로그를 클립보드에 복사");
            tt.SetToolTip(_btnLogClear, "로그 화면 지우기");
            tt.SetToolTip(_btnLogSave,  "로그를 .log 파일로 저장");
            tt.SetToolTip(_btnLogPause, "수신 로그 일시정지/재개");

            this.Load += OnFormLoad;
            this.FormClosing += OnFormClosing;
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            // Populate Device Type (distinct)
            _cmbDeviceType.Items.Clear();
            foreach (var t in EmulatorRegistry.All.Select(x => x.DeviceType).Distinct())
                _cmbDeviceType.Items.Add(t);
            if (_cmbDeviceType.Items.Count > 0) _cmbDeviceType.SelectedIndex = 0;

            // Populate baud
            _cmbBaud.Items.Clear();
            foreach (var b in new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 })
                _cmbBaud.Items.Add(b);
            _cmbBaud.SelectedItem = 9600;

            // Populate COM
            try
            {
                _cmbCom.Items.Clear();
                foreach (var p in SerialPort.GetPortNames()) _cmbCom.Items.Add(p);
                if (_cmbCom.Items.Count > 0) _cmbCom.SelectedIndex = 0;
            }
            catch { }

            UpdateButtons();
        }

        private void OnDeviceTypeChanged(object sender, EventArgs e)
        {
            string dt = (_cmbDeviceType.SelectedItem ?? "").ToString();
            _cmbModel.Items.Clear();
            foreach (var entry in EmulatorRegistry.All.Where(x => x.DeviceType == dt))
                _cmbModel.Items.Add(entry.Brand + " " + entry.Model);
            if (_cmbModel.Items.Count > 0) _cmbModel.SelectedIndex = 0;
        }

        private void OnModelChanged(object sender, EventArgs e)
        {
            var entry = SelectedEntry();
            _cmbTransport.Items.Clear();
            if (entry != null)
            {
                foreach (var t in entry.SupportedTransports) _cmbTransport.Items.Add(t);
                if (_cmbTransport.Items.Count > 0) _cmbTransport.SelectedIndex = 0;
            }
        }

        private void OnTransportChanged(object sender, EventArgs e)
        {
            string t = (_cmbTransport.SelectedItem ?? "").ToString();
            bool serial = t == "Serial";
            bool udp = t == "Udp";
            bool tcp = t == "Tcp";
            _cmbCom.Enabled = _cmbBaud.Enabled = serial;
            _txtPort.Enabled = !serial;
            _chkSonyReply.Enabled = udp;
            if (udp) _txtPort.Text = "52381";
            else if (tcp) _txtPort.Text = "5678";
        }

        private EmulatorRegistry.Entry SelectedEntry()
        {
            string dt = (_cmbDeviceType.SelectedItem ?? "").ToString();
            string m = (_cmbModel.SelectedItem ?? "").ToString();
            return EmulatorRegistry.All.FirstOrDefault(x =>
                x.DeviceType == dt && (x.Brand + " " + x.Model) == m);
        }

        private void OnListenClick()
        {
            if (_emulator != null) return;
            var entry = SelectedEntry();
            if (entry == null) { MessageBox.Show(this, "모델을 선택하세요.", "DeviceEmulator"); return; }

            try
            {
                _emulator = entry.Create();
                _emulator.Logged += (s, msg) => BeginInvoke((Action)(() => AppendLog("[" + entry.Model + "] " + msg)));
                _emulator.Options.LatencyMs = (int)_numLatency.Value;
                _emulator.Options.InjectNak = _chkInjectNak.Checked;
                _emulator.Options.InjectTimeout = _chkInjectTimeout.Checked;
                _emulator.Options.InjectMalformed = _chkInjectMalformed.Checked;
                _emulator.Options.ConsumeOnNextCommand = _chkOnce.Checked;

                int port; int.TryParse(_txtPort.Text, out port);
                int baud = 9600; if (_cmbBaud.SelectedItem != null) int.TryParse(_cmbBaud.SelectedItem.ToString(), out baud);

                var cfg = new EmulatorConfig
                {
                    TransportKind = (_cmbTransport.SelectedItem ?? "").ToString(),
                    ComPort = (_cmbCom.SelectedItem ?? _cmbCom.Text ?? "").ToString(),
                    BaudRate = baud,
                    LocalPort = port,
                    SonyReplyConvention = _chkSonyReply.Checked
                };
                _emulator.Configure(cfg);
                _emulator.Start();
                AppendLog("Listening (" + entry.Description + ", " + cfg.TransportKind + ")");
                _lblStatus.Text = "● Listening";
                _lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                AppendLog("Listen failed: " + ex.Message);
                MessageBox.Show(this, "Listen 실패: " + ex.Message, "DeviceEmulator",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                try { if (_emulator != null) _emulator.Dispose(); } catch { }
                _emulator = null;
                _lblStatus.Text = "Error";
                _lblStatus.ForeColor = Color.DarkRed;
            }
            UpdateButtons();
        }

        private void OnStopClick()
        {
            if (_emulator == null) return;
            try { _emulator.Stop(); _emulator.Dispose(); } catch { }
            _emulator = null;
            AppendLog("Stopped");
            _lblStatus.Text = "Stopped";
            _lblStatus.ForeColor = SystemColors.ControlText;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool running = _emulator != null;
            _btnListen.Enabled = !running;
            _btnStop.Enabled = running;
            _cmbDeviceType.Enabled = !running;
            _cmbModel.Enabled = !running;
            _cmbTransport.Enabled = !running;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            try { if (_emulator != null) { _emulator.Stop(); _emulator.Dispose(); } } catch { }
            _emulator = null;
        }

        private void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (_logPaused) return;
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
            _dlgSaveLog.FileName = "device-emulator-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
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
            _btnLogPause.BackColor = _logPaused
                ? Color.FromArgb(255, 215, 0)
                : SystemColors.Control;
            _btnLogPause.UseVisualStyleBackColor = !_logPaused;
        }
    }
}
