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
    /// Alias-based 4-row matrix switching panel: 입력별칭 / No. / 연결상태 / 출력별칭.
    /// Input selection highlights currently-connected outputs; additional output clicks stage
    /// pending routes; Take (or Auto Take) flushes them via <see cref="IMatrixService.RouteBatchAsync"/>.
    /// Includes batch shortcuts (All / PTP / Refresh) and a shared communication log.
    /// </summary>
    public partial class AliasMatrixControl : UserControl
    {
        // ----- Win32 SuspendDrawing -----
        private const int WM_SETREDRAW = 0x000B;
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);

        // ----- Fixed cell sizing -----
        private const int CellWidth = 80;
        private const int CellHeight = 36;
        private const int RowLabelWidth = 60;
        private const int MaxLogLines = 1000;

        // ----- Row indices in _cells[row, col] -----
        private const int RowInput = 0;
        private const int RowNo = 1;
        private const int RowConnect = 2;
        private const int RowOutput = 3;

        // ----- State -----
        private readonly Queue<string> _logBuffer = new Queue<string>();
        private IMatrixService _service;
        private AliasSettings _aliases;
        private readonly Dictionary<int, int> _pendingRoutes = new Dictionary<int, int>();
        private int? _selectedInput;
        private Label[,] _cells;
        private TextBox _editOverlay;
        private Label _editingLabel;
        private bool _editingIsInput;
        private int _editingIndex;
        private bool _editingIsInputBeforeEnd;
        private int _editingIndexBeforeEnd;
        private bool _busy;
        private int _columns;
        private int _inputCount;
        private int _outputCount;

        public AliasMatrixControl()
        {
            InitializeComponent();
            InitializeEditOverlay();
            WireOwnEvents();
            _aliases = AliasSettings.CreateDefault(8, 8);
            BuildGrid();
        }

        // ------------------ Public API ------------------

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IMatrixService Service { get { return _service; } }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AliasSettings Aliases
        {
            get { return _aliases; }
            set
            {
                _aliases = value ?? AliasSettings.CreateDefault(_inputCount, _outputCount);
                if (_aliases.InputAliases == null || _aliases.InputAliases.Length < _inputCount
                    || _aliases.OutputAliases == null || _aliases.OutputAliases.Length < _outputCount)
                {
                    _aliases.Resize(_inputCount, _outputCount);
                }
                PaintAll();
            }
        }

        [DefaultValue(false)]
        public bool AutoTake
        {
            get { return _chkAutoTake.Checked; }
            set { _chkAutoTake.Checked = value; }
        }

        public event EventHandler<AliasChangedEventArgs> AliasChanged;
        public event EventHandler TakeCompleted;
        public event EventHandler<int> PendingCountChanged;

        public void AttachService(IMatrixService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            DetachService();
            _service = service;
            _service.StateUpdated += OnServiceStateUpdated;
            _service.DeviceInfoChanged += OnServiceDeviceInfoChanged;
            _service.ConnectionStateChanged += OnServiceConnectionChanged;
            _service.Logged += OnServiceLogged;

            int inputs = service.Device?.InputCount ?? 0;
            int outputs = service.Device?.OutputCount ?? 0;
            if (inputs > 0 || outputs > 0)
            {
                if (_aliases == null) _aliases = AliasSettings.CreateDefault(inputs, outputs);
                else _aliases.Resize(inputs, outputs);
                _inputCount = inputs;
                _outputCount = outputs;
            }
            BuildGrid();

            AppendLog(new LogEntry(LogDirection.Info, string.Format(
                "AliasMatrixControl attached: {0} Max={1}×{2}",
                service.Device?.Model ?? "?", service.MaxInputs, service.MaxOutputs)));
        }

        public void DetachService()
        {
            if (_service == null) return;
            _service.StateUpdated -= OnServiceStateUpdated;
            _service.DeviceInfoChanged -= OnServiceDeviceInfoChanged;
            _service.ConnectionStateChanged -= OnServiceConnectionChanged;
            _service.Logged -= OnServiceLogged;
            _service = null;
            _selectedInput = null;
            _pendingRoutes.Clear();
            UpdateTakeButtonState();
            PaintAll();
        }

        // ------------------ Grid construction ------------------

        private void InitializeEditOverlay()
        {
            _editOverlay = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = AliasSettings.MaxAliasLength,
                Visible = false
            };
            _editOverlay.KeyDown += OnEditKeyDown;
            _editOverlay.Leave += OnEditLeave;
            _grid.Controls.Add(_editOverlay);
            _editOverlay.BringToFront();
        }

        private void WireOwnEvents()
        {
            _btnTake.Click += async (s, e) => await SafeRunAsync(TakeAsync);
            _chkAutoTake.CheckedChanged += OnAutoTakeChanged;
            _btnApplyAll.Click += async (s, e) => await SafeRunAsync(OnApplyAllClick);
            _btnPtp.Click += async (s, e) => await SafeRunAsync(OnPtpClick);
            _btnRefresh.Click += async (s, e) => await SafeRunAsync(OnRefreshClick);
            _btnClearLog.Click += (s, e) => { _logBuffer.Clear(); _txtLog.Clear(); };
        }

        private void BuildGrid()
        {
            int inputs = _service?.Device?.InputCount ?? _aliases?.InputAliases?.Length ?? 8;
            int outputs = _service?.Device?.OutputCount ?? _aliases?.OutputAliases?.Length ?? 8;
            if (inputs < 1) inputs = 8;
            if (outputs < 1) outputs = 8;

            _inputCount = inputs;
            _outputCount = outputs;
            _columns = Math.Max(inputs, outputs);
            _aliases?.Resize(inputs, outputs);

            SuspendDrawing(_gridHost);
            _grid.SuspendLayout();
            try
            {
                _grid.Controls.Clear();
                _grid.ColumnStyles.Clear();
                _grid.RowStyles.Clear();

                int totalCols = _columns + 1;
                _grid.ColumnCount = totalCols;
                _grid.RowCount = 4;

                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RowLabelWidth));
                for (int c = 1; c < totalCols; c++)
                    _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, CellWidth));

                for (int r = 0; r < 4; r++)
                    _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, CellHeight));

                _grid.Controls.Add(MakeRowLabel("입 력"), 0, RowInput);
                _grid.Controls.Add(MakeRowLabel("No."), 0, RowNo);
                _grid.Controls.Add(MakeRowLabel("연 결"), 0, RowConnect);
                _grid.Controls.Add(MakeRowLabel("출 력"), 0, RowOutput);

                _cells = new Label[4, _columns + 1];
                for (int c = 1; c <= _columns; c++)
                {
                    _cells[RowInput, c] = BuildAliasCell(c, isInput: true);
                    _cells[RowNo, c] = BuildNoCell(c);
                    _cells[RowConnect, c] = BuildConnectCell(c);
                    _cells[RowOutput, c] = BuildAliasCell(c, isInput: false);

                    _grid.Controls.Add(_cells[RowInput, c], c, RowInput);
                    _grid.Controls.Add(_cells[RowNo, c], c, RowNo);
                    _grid.Controls.Add(_cells[RowConnect, c], c, RowConnect);
                    _grid.Controls.Add(_cells[RowOutput, c], c, RowOutput);
                }

                _grid.Controls.Add(_editOverlay);
                _editOverlay.Visible = false;
                _editOverlay.BringToFront();

                PaintAll();
            }
            finally
            {
                _grid.ResumeLayout(false);
                _grid.PerformLayout();
                ResumeDrawing(_gridHost);
            }
        }

        private Label MakeRowLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font, FontStyle.Bold),
                BackColor = MatrixUiColors.RowLabelBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0)
            };
        }

        private Label BuildAliasCell(int col, bool isInput)
        {
            var lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                BackColor = MatrixUiColors.CellDefault,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0),
                Tag = new CellTag { IsInput = isInput, Column = col }
            };
            lbl.Click += OnAliasCellClick;
            lbl.DoubleClick += OnAliasCellDoubleClick;
            return lbl;
        }

        private Label BuildNoCell(int col)
        {
            return new Label
            {
                Text = col.ToString(),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = MatrixUiColors.NoRowBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0)
            };
        }

        private Label BuildConnectCell(int col)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                BackColor = MatrixUiColors.ConnectRowBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0),
                Tag = new CellTag { IsInput = false, Column = col }
            };
        }

        private sealed class CellTag
        {
            public bool IsInput;
            public int Column;
        }

        // ------------------ Paint ------------------

        private void PaintAll()
        {
            if (_cells == null) return;
            SuspendDrawing(_grid);
            try
            {
                for (int c = 1; c <= _columns; c++)
                {
                    PaintInputCell(c);
                    PaintConnectCell(c);
                    PaintOutputCell(c);
                }
            }
            finally
            {
                ResumeDrawing(_grid);
            }
        }

        private void PaintInputCell(int c)
        {
            var cell = _cells[RowInput, c];
            if (cell == null) return;
            if (c > _inputCount)
            {
                cell.Enabled = false;
                cell.BackColor = MatrixUiColors.CellDisabled;
                cell.Text = string.Empty;
                return;
            }
            cell.Enabled = true;
            cell.Text = _aliases != null ? _aliases.GetInputAlias(c) : c.ToString();
            cell.BackColor = (_selectedInput == c) ? MatrixUiColors.InputSelected : MatrixUiColors.CellDefault;
        }

        private void PaintOutputCell(int c)
        {
            var cell = _cells[RowOutput, c];
            if (cell == null) return;
            if (c > _outputCount)
            {
                cell.Enabled = false;
                cell.BackColor = MatrixUiColors.CellDisabled;
                cell.Text = string.Empty;
                return;
            }
            cell.Enabled = true;
            cell.Text = _aliases != null ? _aliases.GetOutputAlias(c) : c.ToString();

            if (_pendingRoutes.ContainsKey(c))
                cell.BackColor = MatrixUiColors.OutputPending;
            else if (_selectedInput.HasValue && _service != null && _service.GetInputFor(c) == _selectedInput.Value)
                cell.BackColor = MatrixUiColors.OutputConnected;
            else
                cell.BackColor = MatrixUiColors.CellDefault;
        }

        private void PaintConnectCell(int c)
        {
            var cell = _cells[RowConnect, c];
            if (cell == null) return;
            if (c > _outputCount)
            {
                cell.Enabled = false;
                cell.BackColor = MatrixUiColors.CellDisabled;
                cell.Text = string.Empty;
                return;
            }
            int input = _service?.GetInputFor(c) ?? 0;
            string display = input >= 1 && _aliases != null ? _aliases.GetInputAlias(input) : (input >= 1 ? input.ToString() : string.Empty);
            cell.Text = display;
            cell.BackColor = MatrixUiColors.ConnectRowBackground;
        }

        // ------------------ Cell interaction ------------------

        private void OnAliasCellClick(object sender, EventArgs e)
        {
            if (_busy) return;
            var tag = (sender as Label)?.Tag as CellTag;
            if (tag == null) return;

            if (_editingLabel != null) CommitEdit();

            if (tag.IsInput)
                HandleInputClick(tag.Column);
            else
                HandleOutputClick(tag.Column);
        }

        private void HandleInputClick(int col)
        {
            if (col > _inputCount) return;

            if (_selectedInput == col)
            {
                _selectedInput = null;
            }
            else
            {
                _selectedInput = col;
                if (_pendingRoutes.Count > 0)
                {
                    _pendingRoutes.Clear();
                    RaisePendingCountChanged();
                }
            }
            PaintAll();
            UpdateTakeButtonState();
        }

        private void HandleOutputClick(int col)
        {
            if (col > _outputCount) return;
            if (!_selectedInput.HasValue) return;

            int inputToConnect = _selectedInput.Value;

            int currentInput = _service?.GetInputFor(col) ?? 0;
            if (currentInput == inputToConnect) return;

            int existing;
            if (_pendingRoutes.TryGetValue(col, out existing) && existing == inputToConnect) return;

            _pendingRoutes[col] = inputToConnect;
            PaintOutputCell(col);
            UpdateTakeButtonState();
            RaisePendingCountChanged();

            if (AutoTake)
            {
                _ = SafeRunAsync(TakeAsync);
            }
        }

        private void OnAliasCellDoubleClick(object sender, EventArgs e)
        {
            if (_busy) return;
            var lbl = sender as Label;
            var tag = lbl?.Tag as CellTag;
            if (tag == null) return;
            if (tag.IsInput && tag.Column > _inputCount) return;
            if (!tag.IsInput && tag.Column > _outputCount) return;

            BeginEdit(lbl, tag.IsInput, tag.Column);
        }

        // ------------------ Edit UX ------------------

        private void BeginEdit(Label targetCell, bool isInput, int column)
        {
            if (_editingLabel != null) CommitEdit();

            _editingLabel = targetCell;
            _editingIsInput = isInput;
            _editingIndex = column;

            _editOverlay.Bounds = targetCell.Bounds;
            _editOverlay.Text = isInput
                ? (_aliases?.GetInputAlias(column) ?? column.ToString())
                : (_aliases?.GetOutputAlias(column) ?? column.ToString());
            _editOverlay.Visible = true;
            _editOverlay.BringToFront();
            _editOverlay.Focus();
            _editOverlay.SelectAll();

            targetCell.Visible = false;
        }

        private void OnEditKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitEdit();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CancelEdit();
            }
        }

        private void OnEditLeave(object sender, EventArgs e)
        {
            if (_editingLabel != null) CommitEdit();
        }

        private void CommitEdit()
        {
            if (_editingLabel == null) return;

            string oldValue = _editingIsInput
                ? (_aliases?.InputAliases != null && _editingIndex <= _aliases.InputAliases.Length ? _aliases.InputAliases[_editingIndex - 1] : null)
                : (_aliases?.OutputAliases != null && _editingIndex <= _aliases.OutputAliases.Length ? _aliases.OutputAliases[_editingIndex - 1] : null);

            bool changed = _editingIsInput
                ? (_aliases?.SetInputAlias(_editingIndex, _editOverlay.Text) ?? false)
                : (_aliases?.SetOutputAlias(_editingIndex, _editOverlay.Text) ?? false);

            string newValue = _editingIsInput
                ? (_aliases?.InputAliases != null && _editingIndex <= _aliases.InputAliases.Length ? _aliases.InputAliases[_editingIndex - 1] : null)
                : (_aliases?.OutputAliases != null && _editingIndex <= _aliases.OutputAliases.Length ? _aliases.OutputAliases[_editingIndex - 1] : null);

            EndEdit();

            if (changed)
            {
                AliasChanged?.Invoke(this, new AliasChangedEventArgs(_editingIsInputBeforeEnd, _editingIndexBeforeEnd, oldValue, newValue));
                PaintAll();
            }
        }

        private void CancelEdit() { EndEdit(); }

        private void EndEdit()
        {
            if (_editingLabel == null) return;
            _editingIsInputBeforeEnd = _editingIsInput;
            _editingIndexBeforeEnd = _editingIndex;
            _editingLabel.Visible = true;
            _editingLabel = null;
            _editOverlay.Visible = false;
        }

        // ------------------ Batch actions ------------------

        private async Task OnApplyAllClick()
        {
            if (_service == null) { AppendLog(new LogEntry(LogDirection.Info, "서비스가 연결되어 있지 않습니다.")); return; }
            if (!_selectedInput.HasValue)
            {
                AppendLog(new LogEntry(LogDirection.Info, "입력 셀을 먼저 선택하세요."));
                return;
            }
            int input = _selectedInput.Value;
            var sw = Stopwatch.StartNew();
            await _service.RouteAllAsync(input);
            sw.Stop();
            AppendLog(new LogEntry(LogDirection.Info, string.Format(
                "RouteAll in={0} 완료 {1}ms", input, sw.ElapsedMilliseconds)));
            _pendingRoutes.Clear();
            _selectedInput = null;
            PaintAll();
            UpdateTakeButtonState();
            RaisePendingCountChanged();
        }

        private async Task OnPtpClick()
        {
            if (_service == null) { AppendLog(new LogEntry(LogDirection.Info, "서비스가 연결되어 있지 않습니다.")); return; }
            var sw = Stopwatch.StartNew();
            await _service.PtpAsync();
            sw.Stop();
            AppendLog(new LogEntry(LogDirection.Info, string.Format("PTP 완료 {0}ms", sw.ElapsedMilliseconds)));
            _pendingRoutes.Clear();
            _selectedInput = null;
            PaintAll();
            UpdateTakeButtonState();
            RaisePendingCountChanged();
        }

        private async Task OnRefreshClick()
        {
            if (_service == null) { AppendLog(new LogEntry(LogDirection.Info, "서비스가 연결되어 있지 않습니다.")); return; }
            var sw = Stopwatch.StartNew();
            await _service.RefreshAsync();
            sw.Stop();
            AppendLog(new LogEntry(LogDirection.Info, string.Format("Refresh 완료 {0}ms", sw.ElapsedMilliseconds)));
        }

        // ------------------ Take / AutoTake ------------------

        private async Task TakeAsync()
        {
            if (_pendingRoutes.Count == 0) return;
            if (_service == null || !_service.IsConnected)
            {
                AppendLog(new LogEntry(LogDirection.Info, "서비스 미연결. Take 중단."));
                return;
            }

            var effective = new Dictionary<int, int>();
            foreach (var kvp in _pendingRoutes)
            {
                if (_service.GetInputFor(kvp.Key) != kvp.Value)
                    effective[kvp.Key] = kvp.Value;
            }

            if (effective.Count == 0)
            {
                AppendLog(new LogEntry(LogDirection.Info, "모든 pending 항목이 이미 연결된 상태와 동일합니다. 전송 생략."));
                _pendingRoutes.Clear();
                _selectedInput = null;
                PaintAll();
                UpdateTakeButtonState();
                RaisePendingCountChanged();
                return;
            }

            _busy = true;
            SetUiBusy(true);
            var sw = Stopwatch.StartNew();
            try
            {
                await _service.RouteBatchAsync(effective).ConfigureAwait(true);
                sw.Stop();
                AppendLog(new LogEntry(LogDirection.Info, string.Format(
                    "Take: {0}건 전송 완료 {1}ms", effective.Count, sw.ElapsedMilliseconds)));
                TakeCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppendLog(new LogEntry(LogDirection.Error, "Take failed: " + ex.Message));
            }
            finally
            {
                _pendingRoutes.Clear();
                _selectedInput = null;
                _busy = false;
                SetUiBusy(false);
                PaintAll();
                UpdateTakeButtonState();
                RaisePendingCountChanged();
            }
        }

        private void OnAutoTakeChanged(object sender, EventArgs e)
        {
            if (AutoTake && _pendingRoutes.Count > 0)
            {
                _ = SafeRunAsync(TakeAsync);
            }
            UpdateTakeButtonState();
        }

        private void UpdateTakeButtonState()
        {
            _btnTake.Enabled = !_busy && _pendingRoutes.Count > 0 && !AutoTake;
            _lblPending.Text = "Pending: " + _pendingRoutes.Count;
        }

        private void RaisePendingCountChanged()
        {
            var h = PendingCountChanged;
            if (h != null) h(this, _pendingRoutes.Count);
        }

        // ------------------ Service event handlers ------------------

        private void OnServiceLogged(object sender, LogEntry entry)
        {
            InvokeIfRequired(() => AppendLog(entry));
        }

        private void OnServiceStateUpdated(object sender, EventArgs e)
        {
            InvokeIfRequired(PaintAll);
        }

        private void OnServiceDeviceInfoChanged(object sender, EventArgs e)
        {
            var svc = _service;
            InvokeIfRequired(() =>
            {
                if (svc == null) return;
                int newIn = svc.Device.InputCount;
                int newOut = svc.Device.OutputCount;
                if (newIn != _inputCount || newOut != _outputCount)
                {
                    if (_editingLabel != null) CancelEdit();
                    if (_aliases == null) _aliases = AliasSettings.CreateDefault(newIn, newOut);
                    else _aliases.Resize(newIn, newOut);
                    _inputCount = newIn;
                    _outputCount = newOut;
                    BuildGrid();
                }
                else
                {
                    PaintAll();
                }
            });
        }

        private void OnServiceConnectionChanged(object sender, bool connected)
        {
            InvokeIfRequired(() =>
            {
                if (!connected)
                {
                    _pendingRoutes.Clear();
                    _selectedInput = null;
                    UpdateTakeButtonState();
                    RaisePendingCountChanged();
                    PaintAll();
                }
            });
        }

        // ------------------ Log ------------------

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

        // ------------------ Helpers ------------------

        private void SetUiBusy(bool busy)
        {
            _btnTake.Enabled = !busy && _pendingRoutes.Count > 0 && !AutoTake;
            _chkAutoTake.Enabled = !busy;
            _btnApplyAll.Enabled = !busy;
            _btnPtp.Enabled = !busy;
            _btnRefresh.Enabled = !busy;
            _grid.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

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
            try { await action(); }
            catch (Exception ex) { AppendLog(new LogEntry(LogDirection.Error, ex.Message)); }
        }

        private void InvokeIfRequired(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(action); } catch { /* form closing */ } }
            else action();
        }
    }
}
