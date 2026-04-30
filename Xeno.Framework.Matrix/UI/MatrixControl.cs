using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xeno.Framework.Matrix.Core;

namespace Xeno.Framework.Matrix.UI
{
    /// <summary>
    /// Self-contained UserControl that drives an <see cref="IMatrixService"/>: renders an input×output
    /// switching grid, batch buttons (All / PTP / Refresh), connection LED, and a communication log.
    /// Host applications supply an <see cref="IMatrixService"/> instance via <see cref="AttachService"/>.
    /// </summary>
    public partial class MatrixControl : UserControl
    {
        private const int MaxLogLines = 1000;
        private const int CellWidth = 80;
        private const int CellHeight = 36;
        private const int HeaderCellWidth = 70;
        private static readonly Color SelectedCellColor = Color.FromArgb(102, 178, 255);
        private static readonly Color UnselectedCellColor = SystemColors.Control;

        private const int WM_SETREDRAW = 0x000B;
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);

        private readonly Queue<string> _logBuffer = new Queue<string>();
        private IMatrixService _service;
        private Button[,] _cells;
        private int _inputCount = 8;
        private int _outputCount = 8;
        private bool _busy;

        public MatrixControl()
        {
            InitializeComponent();
            WireOwnEvents();
            BuildGrid(_inputCount, _outputCount);
            PopulateInputCombo(_inputCount);
            UpdateConnectionUi(false, false, null);
        }

        /// <summary>
        /// Bind the control to a matrix service. Any previously attached service is detached
        /// (but not disposed — callers own the service lifetime).
        /// </summary>
        public void AttachService(IMatrixService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            DetachService();

            _service = service;
            _service.Logged += OnServiceLogged;
            _service.ConnectionStateChanged += OnServiceConnectionChanged;
            _service.StateUpdated += OnServiceStateUpdated;
            _service.DeviceInfoChanged += OnServiceDeviceInfoChanged;

            AppendLog(new LogEntry(LogDirection.Info, string.Format(
                "AttachService: {0} Max={1}×{2} Mode={3}",
                service.Device?.Model ?? "?", service.MaxInputs, service.MaxOutputs, service.Mode)));

            RebuildForDevice(service.Device);
            UpdateConnectionUi(service.IsConnected, service.IsReconnecting, service.Device);
        }

        /// <summary>Detach the current service. Safe to call multiple times.</summary>
        public void DetachService()
        {
            if (_service == null) return;
            _service.Logged -= OnServiceLogged;
            _service.ConnectionStateChanged -= OnServiceConnectionChanged;
            _service.StateUpdated -= OnServiceStateUpdated;
            _service.DeviceInfoChanged -= OnServiceDeviceInfoChanged;
            _service = null;
        }

        /// <summary>Currently attached service, or null.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IMatrixService Service { get { return _service; } }

        // ---------------------- grid construction ----------------------

        private void WireOwnEvents()
        {
            _btnApplyAll.Click += async (s, e) => await SafeRunAsync(OnApplyAllClick);
            _btnPtp.Click += async (s, e) => await SafeRunAsync(OnPtpClick);
            _btnRefresh.Click += async (s, e) => await SafeRunAsync(OnRefreshClick);
            _btnClearLog.Click += (s, e) => { _logBuffer.Clear(); _txtLog.Clear(); };
        }

        private void RebuildForDevice(DeviceInfo device)
        {
            int inputs = 8;
            int outputs = 8;
            if (device != null)
            {
                if (device.InputCount > 0) inputs = device.InputCount;
                if (device.OutputCount > 0) outputs = device.OutputCount;
            }
            bool dimsChanged = inputs != _inputCount || outputs != _outputCount;
            bool notBuilt = _grid.Controls.Count == 0;
            if (dimsChanged || notBuilt)
            {
                var sw = Stopwatch.StartNew();
                BuildGrid(inputs, outputs);
                PopulateInputCombo(inputs);
                sw.Stop();
                AppendLog(new LogEntry(LogDirection.Info, string.Format(
                    "grid (re)built {0}×{1} in {2}ms", inputs, outputs, sw.ElapsedMilliseconds)));
            }
            // silent when dimensions unchanged
        }

        private void BuildGrid(int inputs, int outputs)
        {
            _inputCount = inputs;
            _outputCount = outputs;

            SuspendDrawing(_gridHost);
            _grid.SuspendLayout();
            try
            {
                _grid.Controls.Clear();
                _grid.RowStyles.Clear();
                _grid.ColumnStyles.Clear();

                int cols = outputs + 1;
                int rows = inputs + 1;
                _grid.ColumnCount = cols;
                _grid.RowCount = rows;

                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, HeaderCellWidth));
                for (int c = 1; c < cols; c++)
                    _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, CellWidth));

                _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, CellHeight));
                for (int r = 1; r < rows; r++)
                    _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, CellHeight));

                _cells = new Button[inputs + 1, outputs + 1];

                var headerFont = new Font(Font, FontStyle.Bold);
                var headerBg = SystemColors.ControlLight;

                _grid.Controls.Add(MakeHeader(string.Empty, headerFont, headerBg), 0, 0);

                for (int c = 1; c <= outputs; c++)
                    _grid.Controls.Add(MakeHeader("출력 " + c, headerFont, headerBg), c, 0);

                for (int r = 1; r <= inputs; r++)
                {
                    _grid.Controls.Add(MakeHeader("입력 " + r, headerFont, headerBg), 0, r);
                    for (int c = 1; c <= outputs; c++)
                    {
                        var btn = new Button
                        {
                            Dock = DockStyle.Fill,
                            Margin = new Padding(0),
                            FlatStyle = FlatStyle.Flat,
                            UseVisualStyleBackColor = false,
                            BackColor = UnselectedCellColor,
                            Text = string.Empty,
                            TabStop = false,
                            Tag = new CellTag(r, c)
                        };
                        btn.FlatAppearance.BorderSize = 1;
                        btn.FlatAppearance.BorderColor = Color.Silver;
                        btn.Click += OnCellClick;
                        _cells[r, c] = btn;
                        _grid.Controls.Add(btn, c, r);
                    }
                }
            }
            finally
            {
                _grid.ResumeLayout(false);
                _grid.PerformLayout();
                ResumeDrawing(_gridHost);
            }
        }

        private static Label MakeHeader(string text, Font font, Color bg)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = font,
                BackColor = bg
            };
        }

        private void PopulateInputCombo(int count)
        {
            _cboInput.BeginUpdate();
            try
            {
                _cboInput.Items.Clear();
                for (int i = 1; i <= count; i++) _cboInput.Items.Add(i);
                if (_cboInput.Items.Count > 0) _cboInput.SelectedIndex = 0;
            }
            finally
            {
                _cboInput.EndUpdate();
            }
        }

        private sealed class CellTag
        {
            public int Input { get; }
            public int Output { get; }
            public CellTag(int input, int output) { Input = input; Output = output; }
        }

        // ---------------------- service event handlers ----------------------

        private void OnServiceLogged(object sender, LogEntry entry)
        {
            InvokeIfRequired(() => AppendLog(entry));
        }

        private void OnServiceConnectionChanged(object sender, bool connected)
        {
            var svc = _service;
            InvokeIfRequired(() => UpdateConnectionUi(connected, svc != null && svc.IsReconnecting, svc?.Device));
        }

        private void OnServiceStateUpdated(object sender, EventArgs e)
        {
            InvokeIfRequired(PaintAllCells);
        }

        private void OnServiceDeviceInfoChanged(object sender, EventArgs e)
        {
            var svc = _service;
            InvokeIfRequired(() =>
            {
                if (svc != null) RebuildForDevice(svc.Device);
                PaintAllCells();
                UpdateConnectionUi(svc != null && svc.IsConnected, svc != null && svc.IsReconnecting, svc?.Device);
            });
        }

        // ---------------------- UI event handlers ----------------------

        private async void OnCellClick(object sender, EventArgs e)
        {
            if (_service == null) { AppendLog(new LogEntry(LogDirection.Info, "서비스가 연결되어 있지 않습니다.")); return; }
            var tag = (sender as Button)?.Tag as CellTag;
            if (tag == null) return;
            await SafeRunAsync(async () =>
            {
                var sw = Stopwatch.StartNew();
                await _service.RouteAsync(tag.Input, tag.Output);
                sw.Stop();
                AppendLog(new LogEntry(LogDirection.Info, string.Format(
                    "Route in={0} out={1} 완료 {2}ms", tag.Input, tag.Output, sw.ElapsedMilliseconds)));
            });
        }

        private async Task OnApplyAllClick()
        {
            if (_service == null) { AppendLog(new LogEntry(LogDirection.Info, "서비스가 연결되어 있지 않습니다.")); return; }
            if (!(_cboInput.SelectedItem is int input)) return;
            var sw = Stopwatch.StartNew();
            await _service.RouteAllAsync(input);
            sw.Stop();
            AppendLog(new LogEntry(LogDirection.Info, string.Format(
                "RouteAll in={0} 완료 {1}ms", input, sw.ElapsedMilliseconds)));
        }

        private async Task OnPtpClick()
        {
            if (_service == null) { AppendLog(new LogEntry(LogDirection.Info, "서비스가 연결되어 있지 않습니다.")); return; }
            var sw = Stopwatch.StartNew();
            await _service.PtpAsync();
            sw.Stop();
            AppendLog(new LogEntry(LogDirection.Info, string.Format(
                "PTP 완료 {0}ms", sw.ElapsedMilliseconds)));
        }

        private async Task OnRefreshClick()
        {
            if (_service == null) { AppendLog(new LogEntry(LogDirection.Info, "서비스가 연결되어 있지 않습니다.")); return; }
            var sw = Stopwatch.StartNew();
            await _service.RefreshAsync();
            sw.Stop();
            AppendLog(new LogEntry(LogDirection.Info, string.Format(
                "Refresh 완료 {0}ms", sw.ElapsedMilliseconds)));
        }

        // ---------------------- UI rendering ----------------------

        private void UpdateConnectionUi(bool connected, bool reconnecting, DeviceInfo device)
        {
            if (_service == null)
            {
                _ledPanel.BackColor = Color.Gray;
                _lblStatus.Text = "서비스 미연결";
                return;
            }

            if (_service.Mode == ConnectionMode.PerCommand && !reconnecting)
            {
                _ledPanel.BackColor = Color.LightSlateGray;
                _lblStatus.Text = "단발 모드 (명령 시마다 연결) - " + (device?.Model ?? "Unknown");
                return;
            }

            if (reconnecting)
            {
                _ledPanel.BackColor = Color.Gold;
                _lblStatus.Text = "재연결 시도 중... (" + (device?.Model ?? "?") + ")";
            }
            else if (connected)
            {
                _ledPanel.BackColor = Color.LimeGreen;
                _lblStatus.Text = "연결됨 " + _service.Host + ":" + _service.Port + "  [" + (device?.Model ?? "?") + "]";
            }
            else
            {
                _ledPanel.BackColor = Color.Firebrick;
                _lblStatus.Text = "끊김";
            }
        }

        private void PaintAllCells()
        {
            if (_service == null || _cells == null) return;
            var sw = Stopwatch.StartNew();
            SuspendDrawing(_grid);
            try
            {
                for (int output = 1; output <= _outputCount; output++) PaintOutput(output);
            }
            finally
            {
                ResumeDrawing(_grid);
            }
            sw.Stop();
            if (sw.ElapsedMilliseconds >= 5)
            {
                AppendLog(new LogEntry(LogDirection.Info, string.Format(
                    "paint {0}×{1} in {2}ms", _inputCount, _outputCount, sw.ElapsedMilliseconds)));
            }
        }

        private void PaintOutput(int output)
        {
            if (_cells == null || output < 1 || output > _outputCount) return;
            int selectedInput = _service != null ? _service.GetInputFor(output) : 0;
            for (int r = 1; r <= _inputCount; r++)
            {
                var btn = _cells[r, output];
                if (btn == null) continue;
                bool selected = r == selectedInput;
                btn.BackColor = selected ? SelectedCellColor : UnselectedCellColor;
                btn.Text = selected ? "●" : string.Empty;
            }
        }

        private void AppendLog(LogEntry entry)
        {
            string line = entry.ToString();
            _logBuffer.Enqueue(line);
            while (_logBuffer.Count > MaxLogLines) _logBuffer.Dequeue();

            if (_logBuffer.Count == MaxLogLines)
            {
                _txtLog.Text = string.Join(Environment.NewLine, _logBuffer.ToArray());
                _txtLog.SelectionStart = _txtLog.TextLength;
                _txtLog.ScrollToCaret();
            }
            else
            {
                _txtLog.AppendText(line + Environment.NewLine);
            }
        }

        // ---------------------- helpers ----------------------

        private static void SuspendDrawing(Control c)
        {
            if (c == null || !c.IsHandleCreated) return;
            SendMessage(c.Handle, WM_SETREDRAW, false, 0);
        }

        private static void ResumeDrawing(Control c)
        {
            if (c == null || !c.IsHandleCreated) return;
            SendMessage(c.Handle, WM_SETREDRAW, true, 0);
            c.Invalidate(true);
        }

        private async Task SafeRunAsync(Func<Task> action)
        {
            if (_busy) return; // 중복 재입력 무시
            _busy = true;
            SetUiBusy(true);
            try { await action(); }
            catch (Exception ex)
            {
                AppendLog(new LogEntry(LogDirection.Error, ex.Message));
            }
            finally
            {
                _busy = false;
                SetUiBusy(false);
            }
        }

        private void SetUiBusy(bool busy)
        {
            _btnApplyAll.Enabled = !busy;
            _btnPtp.Enabled = !busy;
            _btnRefresh.Enabled = !busy;
            _cboInput.Enabled = !busy;
            _grid.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void InvokeIfRequired(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(action); } catch { /* form closing */ } }
            else action();
        }
    }
}
