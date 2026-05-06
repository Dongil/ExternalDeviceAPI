using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.UI
{
    /// <summary>
    /// Self-contained UserControl that hosts N camera control sessions (default 3).
    /// Hosts attach via <see cref="AttachFactory"/> (control creates services from the model
    /// combo) or <see cref="AttachServices"/> (host owns the service instances).
    /// PTZ / Zoom / Focus buttons are Hold-to-Move (MouseDown drives, MouseUp/Leave stops).
    /// OSD / Preset buttons are single-shot. Preset SET toggles a save-mode for one click.
    /// </summary>
    public partial class CameraControl : UserControl
    {
        private const int DefaultSlotCount = 3;
        private const int RowHeight = 28;
        private const int HeaderHeight = 24;
        private const int CamButtonWidth = 80;
        private const int CamButtonGap = 6;

        private ICameraServiceFactory _factory;
        private readonly List<SlotState> _slots = new List<SlotState>();
        private readonly ToolTip _toolTip = new ToolTip();
        private int _activeIndex = -1;
        private int _slotCount = DefaultSlotCount;
        private bool _presetSetMode;
        private bool _busy;
        private bool _ptHoldActive;
        private bool _zoomHoldActive;
        private bool _focusHoldActive;

        public CameraControl()
        {
            InitializeComponent();
            WireOwnEvents();
            // Build initial empty rows so Designer preview shows 3 slots.
            RebuildRows();
            RebuildCamButtons();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Number of camera slots (rows + CAM buttons). Re-creates rows on change.</summary>
        [DefaultValue(DefaultSlotCount)]
        public int SlotCount
        {
            get { return _slotCount; }
            set
            {
                if (value < 1) value = 1;
                if (value == _slotCount) return;
                _slotCount = value;
                RebuildRows();
                RebuildCamButtons();
                SyncSpeedSlidersToActive();
            }
        }

        /// <summary>Currently selected slot index (0-based), or -1 if none.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ActiveIndex { get { return _activeIndex; } }

        /// <summary>
        /// Forwarded log events from any attached service. Hosts can subscribe to render
        /// transport/protocol traffic in their own log panel. Marshalled to UI thread.
        /// </summary>
        public event EventHandler<ServiceLogEventArgs> ServiceLog;

        /// <summary>Attach a factory; the control creates services from the per-row Model combo on Connect.</summary>
        public void AttachFactory(ICameraServiceFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _factory = factory;
            PopulateModelCombos();
        }

        /// <summary>Attach pre-built services. List length must equal SlotCount. Host owns service lifetime.</summary>
        public void AttachServices(IList<ICameraService> services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (services.Count != _slotCount) throw new ArgumentException(
                "services.Count must equal SlotCount (" + _slotCount + ")", nameof(services));
            for (int i = 0; i < _slotCount; i++)
            {
                AttachServiceAtSlot(i, services[i]);
            }
        }

        public void DetachAll()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                DetachServiceAtSlot(i, dispose: false);
            }
            _activeIndex = -1;
            UpdateCamButtonsAppearance();
        }

        public CameraSlotConfig[] GetSlotConfigs()
        {
            var arr = new CameraSlotConfig[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                ReadConfigFromUi(_slots[i]);
                arr[i] = _slots[i].Config.Clone();
            }
            return arr;
        }

        public void ApplySlotConfigs(CameraSlotConfig[] configs)
        {
            if (configs == null) return;
            int n = Math.Min(configs.Length, _slots.Count);
            for (int i = 0; i < n; i++)
            {
                if (configs[i] == null) continue;
                _slots[i].Config = configs[i].Clone();
                WriteConfigToUi(_slots[i]);
            }
        }

        // ------------------------------------------------------------------
        // Internal slot model
        // ------------------------------------------------------------------

        private sealed class SlotState
        {
            public CameraSlotConfig Config = new CameraSlotConfig();
            public ICameraService Service;
            public EventHandler<LogEntry> LogHandler;
            public bool ConnectFailed;
            public string LastErrorMessage;

            // Row controls
            public Label LblIndex;
            public ComboBox CmbModel;
            public ComboBox CmbTransport;
            public TextBox TxtCom;
            public ComboBox CmbBaud;
            public TextBox TxtAddress;
            public TextBox TxtHost;
            public TextBox TxtPort;
            public ComboBox CmbHomePreset;
            public Label LblStatus;

            // CAM selector button (in _camButtonsHost)
            public Button BtnCam;
        }

        // ------------------------------------------------------------------
        // Event wiring (Designer-external)
        // ------------------------------------------------------------------

        private void WireOwnEvents()
        {
            _btnConnect.Click += async (s, e) => await SafeRunAsync(OnConnectAllAsync);
            _btnDisconnect.Click += async (s, e) => await SafeRunAsync(OnDisconnectAllAsync);

            // OSD single-shot
            _btnOsdOn.Click   += async (s, e) => await SafeSingleShotAsync(svc => svc.OsdOnAsync());
            _btnOsdOff.Click  += async (s, e) => await SafeSingleShotAsync(svc => svc.OsdOffAsync());
            _btnOsdSel.Click  += async (s, e) => await SafeSingleShotAsync(svc => svc.OsdSelectAsync());
            _btnOsdBack.Click += async (s, e) => await SafeSingleShotAsync(svc => svc.OsdBackAsync());

            // PTZ Hold-to-Move
            WirePanTilt(_btnUp,         PanTiltDirection.Up);
            WirePanTilt(_btnDown,       PanTiltDirection.Down);
            WirePanTilt(_btnLeft,       PanTiltDirection.Left);
            WirePanTilt(_btnRight,      PanTiltDirection.Right);
            WirePanTilt(_btnUpLeft,     PanTiltDirection.UpLeft);
            WirePanTilt(_btnUpRight,    PanTiltDirection.UpRight);
            WirePanTilt(_btnDownLeft,   PanTiltDirection.DownLeft);
            WirePanTilt(_btnDownRight,  PanTiltDirection.DownRight);

            WireZoom(_btnZoomIn,  ZoomDirection.Tele);
            WireZoom(_btnZoomOut, ZoomDirection.Wide);
            WireFocus(_btnFocusFar,  FocusDirection.Far);
            WireFocus(_btnFocusNear, FocusDirection.Near);

            // Preset SET toggle
            _btnPresetSet.Click += (s, e) => TogglePresetSetMode();

            // Preset 1..12
            WirePreset(_btnPreset1,  1);
            WirePreset(_btnPreset2,  2);
            WirePreset(_btnPreset3,  3);
            WirePreset(_btnPreset4,  4);
            WirePreset(_btnPreset5,  5);
            WirePreset(_btnPreset6,  6);
            WirePreset(_btnPreset7,  7);
            WirePreset(_btnPreset8,  8);
            WirePreset(_btnPreset9,  9);
            WirePreset(_btnPreset10, 10);
            WirePreset(_btnPreset11, 11);
            WirePreset(_btnPreset12, 12);
        }

        private void WirePanTilt(Button btn, PanTiltDirection dir)
        {
            btn.MouseDown  += async (s, e) => { if (e.Button == MouseButtons.Left) await SafePtDownAsync(dir); };
            btn.MouseUp    += async (s, e) => { if (e.Button == MouseButtons.Left) await SafePtUpAsync(); };
            btn.MouseLeave += async (s, e) => { if (Control.MouseButtons == MouseButtons.None) await SafePtUpAsync(); };
        }

        private void WireZoom(Button btn, ZoomDirection dir)
        {
            btn.MouseDown  += async (s, e) => { if (e.Button == MouseButtons.Left) await SafeZoomDownAsync(dir); };
            btn.MouseUp    += async (s, e) => { if (e.Button == MouseButtons.Left) await SafeZoomUpAsync(); };
            btn.MouseLeave += async (s, e) => { if (Control.MouseButtons == MouseButtons.None) await SafeZoomUpAsync(); };
        }

        private void WireFocus(Button btn, FocusDirection dir)
        {
            btn.MouseDown  += async (s, e) => { if (e.Button == MouseButtons.Left) await SafeFocusDownAsync(dir); };
            btn.MouseUp    += async (s, e) => { if (e.Button == MouseButtons.Left) await SafeFocusUpAsync(); };
            btn.MouseLeave += async (s, e) => { if (Control.MouseButtons == MouseButtons.None) await SafeFocusUpAsync(); };
        }

        private void WirePreset(Button btn, int presetNumber)
        {
            btn.Click += async (s, e) => await OnPresetClickAsync(presetNumber);
        }

        // ------------------------------------------------------------------
        // Row construction
        // ------------------------------------------------------------------

        private void RebuildRows()
        {
            _rowsHost.SuspendLayout();
            try
            {
                _rowsHost.Controls.Clear();
                _slots.Clear();

                _rowsHost.Controls.Add(BuildHeaderRow(0));

                for (int i = 0; i < _slotCount; i++)
                {
                    var slot = new SlotState();
                    var row = BuildSlotRow(i, slot);
                    row.Top = HeaderHeight + i * RowHeight;
                    _rowsHost.Controls.Add(row);
                    _slots.Add(slot);
                    WriteConfigToUi(slot);
                    ApplyTransportFieldRules(slot);
                }
            }
            finally
            {
                _rowsHost.ResumeLayout(true);
            }
        }

        private Panel BuildHeaderRow(int top)
        {
            var p = new Panel
            {
                Top = top,
                Left = 0,
                Width = _rowsHost.ClientSize.Width,
                Height = HeaderHeight,
                BackColor = SystemColors.ControlLight
            };
            int x = 4;
            p.Controls.Add(MakeHeaderLabel("Cam",  x,  60)); x += 60 + 4;
            p.Controls.Add(MakeHeaderLabel("모델", x, 110)); x += 110 + 4;
            p.Controls.Add(MakeHeaderLabel("통신", x,  80)); x +=  80 + 4;
            p.Controls.Add(MakeHeaderLabel("COM",  x,  60)); x +=  60 + 4;
            p.Controls.Add(MakeHeaderLabel("Baud", x,  70)); x +=  70 + 4;
            p.Controls.Add(MakeHeaderLabel("ID",   x,  40)); x +=  40 + 4;
            p.Controls.Add(MakeHeaderLabel("IP",   x, 110)); x += 110 + 4;
            p.Controls.Add(MakeHeaderLabel("Port", x,  60)); x +=  60 + 4;
            p.Controls.Add(MakeHeaderLabel("Home", x,  60)); x +=  60 + 4;
            p.Controls.Add(MakeHeaderLabel("상태", x, 140));
            return p;
        }

        private static Label MakeHeaderLabel(string text, int x, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, 4),
                Size = new Size(width, 16),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Panel BuildSlotRow(int index, SlotState slot)
        {
            var p = new Panel
            {
                Left = 0,
                Width = _rowsHost.ClientSize.Width,
                Height = RowHeight
            };

            int x = 4;
            slot.LblIndex = new Label
            {
                Text = "Cam " + (index + 1),
                Location = new Point(x, 6),
                Size = new Size(60, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };
            p.Controls.Add(slot.LblIndex);
            x += 60 + 4;

            slot.CmbModel = new ComboBox
            {
                Location = new Point(x, 4),
                Size = new Size(110, 22),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            slot.CmbModel.SelectedIndexChanged += (s, e) => OnModelChanged(slot);
            p.Controls.Add(slot.CmbModel);
            x += 110 + 4;

            slot.CmbTransport = new ComboBox
            {
                Location = new Point(x, 4),
                Size = new Size(80, 22),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            slot.CmbTransport.Items.AddRange(new object[] { "RS-232", "RS-422", "RS-485", "UDP", "TCP" });
            slot.CmbTransport.SelectedIndexChanged += (s, e) => OnTransportChanged(slot);
            p.Controls.Add(slot.CmbTransport);
            x += 80 + 4;

            slot.TxtCom = new TextBox { Location = new Point(x, 4), Size = new Size(60, 22) };
            slot.TxtCom.Leave += (s, e) => slot.Config.ComPort = slot.TxtCom.Text;
            p.Controls.Add(slot.TxtCom);
            x += 60 + 4;

            slot.CmbBaud = new ComboBox
            {
                Location = new Point(x, 4),
                Size = new Size(70, 22),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            slot.CmbBaud.Items.AddRange(new object[] { 9600, 19200, 38400, 57600, 115200 });
            slot.CmbBaud.SelectedIndexChanged += (s, e) =>
            {
                if (slot.CmbBaud.SelectedItem is int v) slot.Config.BaudRate = v;
            };
            p.Controls.Add(slot.CmbBaud);
            x += 70 + 4;

            slot.TxtAddress = new TextBox { Location = new Point(x, 4), Size = new Size(40, 22), Text = "1" };
            slot.TxtAddress.Leave += (s, e) =>
            {
                int a;
                int parsed = int.TryParse(slot.TxtAddress.Text, out a) ? a : 1;
                // Pelco-D 는 주소 1-255 / VISCA 는 1-7 — 모델 capability 보고 동적 클램프
                int max = 7;
                if (slot.Service != null) max = slot.Service.Capabilities.MaxAddress;
                else if (!string.IsNullOrEmpty(slot.Config.Model) && _factory != null)
                    max = _factory.GetCapabilities(slot.Config.Model).MaxAddress;
                slot.Config.Address = Math.Max(1, Math.Min(parsed, max));
                if (slot.Config.Address.ToString() != slot.TxtAddress.Text)
                    slot.TxtAddress.Text = slot.Config.Address.ToString();
            };
            p.Controls.Add(slot.TxtAddress);
            x += 40 + 4;

            slot.TxtHost = new TextBox { Location = new Point(x, 4), Size = new Size(110, 22) };
            slot.TxtHost.Leave += (s, e) => slot.Config.Host = slot.TxtHost.Text;
            p.Controls.Add(slot.TxtHost);
            x += 110 + 4;

            slot.TxtPort = new TextBox { Location = new Point(x, 4), Size = new Size(60, 22) };
            slot.TxtPort.Leave += (s, e) =>
            {
                int v;
                slot.Config.Port = int.TryParse(slot.TxtPort.Text, out v) ? v : 0;
            };
            p.Controls.Add(slot.TxtPort);
            x += 60 + 4;

            slot.CmbHomePreset = new ComboBox
            {
                Location = new Point(x, 4),
                Size = new Size(60, 22),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            slot.CmbHomePreset.Items.Add("(없음)");
            for (int i = 1; i <= 16; i++) slot.CmbHomePreset.Items.Add(i);
            slot.CmbHomePreset.SelectedIndex = 0;
            slot.CmbHomePreset.SelectedIndexChanged += (s, e) =>
            {
                slot.Config.HomePreset = slot.CmbHomePreset.SelectedIndex; // 0 = none, 1..16 = preset
            };
            p.Controls.Add(slot.CmbHomePreset);
            x += 60 + 4;

            slot.LblStatus = new Label
            {
                Text = "대기",
                Location = new Point(x, 4),
                Size = new Size(140, 20),
                BackColor = CameraUiColors.StatusIdle,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter
            };
            p.Controls.Add(slot.LblStatus);

            return p;
        }

        private void RebuildCamButtons()
        {
            _camButtonsHost.SuspendLayout();
            try
            {
                _camButtonsHost.Controls.Clear();
                int x = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    var btn = new Button
                    {
                        Text = "CAM " + (i + 1),
                        Location = new Point(x, 2),
                        Size = new Size(CamButtonWidth, 28),
                        UseVisualStyleBackColor = true,
                        Tag = i,
                        Enabled = false   // disabled until successful connection
                    };
                    btn.Click += OnCamClick;
                    _slots[i].BtnCam = btn;
                    _camButtonsHost.Controls.Add(btn);
                    x += CamButtonWidth + CamButtonGap;
                }
            }
            finally { _camButtonsHost.ResumeLayout(true); }
            UpdateCamButtonsAppearance();
        }

        // ------------------------------------------------------------------
        // Model / Transport changes
        // ------------------------------------------------------------------

        private void PopulateModelCombos()
        {
            if (_factory == null) return;
            var models = _factory.GetAvailableModels();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                string previousSelection = slot.Config.Model;
                slot.CmbModel.BeginUpdate();
                try
                {
                    slot.CmbModel.Items.Clear();
                    foreach (var m in models) slot.CmbModel.Items.Add(m.Model);
                    if (!string.IsNullOrEmpty(previousSelection))
                    {
                        int idx = slot.CmbModel.Items.IndexOf(previousSelection);
                        if (idx >= 0) slot.CmbModel.SelectedIndex = idx;
                    }
                }
                finally { slot.CmbModel.EndUpdate(); }
            }
        }

        private void OnModelChanged(SlotState slot)
        {
            string model = slot.CmbModel.SelectedItem as string;
            if (string.IsNullOrEmpty(model) || _factory == null) return;
            slot.Config.Model = model;
            var caps = _factory.GetCapabilities(model);

            // Apply defaults from capabilities (but only if user hasn't already entered values)
            slot.Config.Transport = caps.DefaultTransport;
            slot.CmbTransport.SelectedIndex = TransportToIndex(caps.DefaultTransport);

            // Baud combo: replace with capability list
            slot.CmbBaud.BeginUpdate();
            try
            {
                slot.CmbBaud.Items.Clear();
                if (caps.SupportedBaudRates != null)
                {
                    foreach (var r in caps.SupportedBaudRates) slot.CmbBaud.Items.Add(r);
                }
                if (slot.CmbBaud.Items.Count > 0)
                {
                    int defaultIdx = slot.CmbBaud.Items.IndexOf(caps.DefaultBaudRate);
                    slot.CmbBaud.SelectedIndex = defaultIdx >= 0 ? defaultIdx : 0;
                }
            }
            finally { slot.CmbBaud.EndUpdate(); }

            if (string.IsNullOrEmpty(slot.TxtPort.Text) && caps.DefaultIpPort > 0)
            {
                slot.TxtPort.Text = caps.DefaultIpPort.ToString();
                slot.Config.Port = caps.DefaultIpPort;
            }
            if (string.IsNullOrEmpty(slot.TxtAddress.Text))
            {
                slot.TxtAddress.Text = caps.DefaultAddress.ToString();
                slot.Config.Address = caps.DefaultAddress;
            }

            // Home preset combo: rebuild to (none, 1..PresetCount)
            int previousHome = slot.Config.HomePreset;
            slot.CmbHomePreset.BeginUpdate();
            try
            {
                slot.CmbHomePreset.Items.Clear();
                slot.CmbHomePreset.Items.Add("(없음)");
                for (int i = 1; i <= caps.PresetCount; i++) slot.CmbHomePreset.Items.Add(i);
                int restore = previousHome;
                if (restore < 0 || restore > caps.PresetCount) restore = 0;
                slot.CmbHomePreset.SelectedIndex = restore;
            }
            finally { slot.CmbHomePreset.EndUpdate(); }

            ApplyTransportFieldRules(slot);
            // If this slot is currently active, refresh sliders too
            int idx = _slots.IndexOf(slot);
            if (idx == _activeIndex) SyncSpeedSlidersToActive();
        }

        private void OnTransportChanged(SlotState slot)
        {
            slot.Config.Transport = IndexToTransport(slot.CmbTransport.SelectedIndex);
            ApplyTransportFieldRules(slot);
        }

        private void ApplyTransportFieldRules(SlotState slot)
        {
            var t = slot.Config.Transport;
            bool serial = t == CameraTransportKind.Rs232 || t == CameraTransportKind.Rs422 || t == CameraTransportKind.Rs485;
            bool ip = t == CameraTransportKind.UdpVisca || t == CameraTransportKind.TcpVisca;

            slot.TxtCom.Enabled = serial;
            slot.CmbBaud.Enabled = serial;
            slot.TxtAddress.Enabled = serial;
            slot.TxtHost.Enabled = ip;
            slot.TxtPort.Enabled = ip;

            string serialOff = "선택된 통신 방식에서는 사용하지 않습니다";
            string ipOff = serialOff;
            string addrOff = "VISCA over IP 는 주소를 자동 처리합니다";

            _toolTip.SetToolTip(slot.TxtCom,     serial ? "" : serialOff);
            _toolTip.SetToolTip(slot.CmbBaud,    serial ? "" : serialOff);
            _toolTip.SetToolTip(slot.TxtAddress, serial ? "" : addrOff);
            _toolTip.SetToolTip(slot.TxtHost,    ip ? "" : ipOff);
            _toolTip.SetToolTip(slot.TxtPort,    ip ? "" : ipOff);
        }

        private static int TransportToIndex(CameraTransportKind t)
        {
            switch (t)
            {
                case CameraTransportKind.Rs232: return 0;
                case CameraTransportKind.Rs422: return 1;
                case CameraTransportKind.Rs485: return 2;
                case CameraTransportKind.UdpVisca: return 3;
                case CameraTransportKind.TcpVisca: return 4;
                default: return 0;
            }
        }

        private static CameraTransportKind IndexToTransport(int idx)
        {
            switch (idx)
            {
                case 0: return CameraTransportKind.Rs232;
                case 1: return CameraTransportKind.Rs422;
                case 2: return CameraTransportKind.Rs485;
                case 3: return CameraTransportKind.UdpVisca;
                case 4: return CameraTransportKind.TcpVisca;
                default: return CameraTransportKind.Rs232;
            }
        }

        // ------------------------------------------------------------------
        // Read/Write SlotConfig <-> UI
        // ------------------------------------------------------------------

        private void ReadConfigFromUi(SlotState slot)
        {
            slot.Config.Model = slot.CmbModel.SelectedItem as string;
            slot.Config.Transport = IndexToTransport(slot.CmbTransport.SelectedIndex);
            slot.Config.ComPort = slot.TxtCom.Text;
            int baud = 0;
            if (slot.CmbBaud.SelectedItem is int b) baud = b;
            slot.Config.BaudRate = baud;
            int addr;
            slot.Config.Address = int.TryParse(slot.TxtAddress.Text, out addr) ? addr : 1;
            slot.Config.Host = slot.TxtHost.Text;
            int port;
            slot.Config.Port = int.TryParse(slot.TxtPort.Text, out port) ? port : 0;
            slot.Config.HomePreset = slot.CmbHomePreset.SelectedIndex;
        }

        private void WriteConfigToUi(SlotState slot)
        {
            // Model + transport require populated combos before they can be set
            if (slot.CmbModel.Items.Count == 0 && _factory != null) PopulateModelCombos();
            if (!string.IsNullOrEmpty(slot.Config.Model))
            {
                int idx = slot.CmbModel.Items.IndexOf(slot.Config.Model);
                if (idx >= 0) slot.CmbModel.SelectedIndex = idx;
            }
            slot.CmbTransport.SelectedIndex = TransportToIndex(slot.Config.Transport);
            slot.TxtCom.Text = slot.Config.ComPort ?? string.Empty;
            if (slot.Config.BaudRate > 0)
            {
                int idx = slot.CmbBaud.Items.IndexOf(slot.Config.BaudRate);
                if (idx >= 0) slot.CmbBaud.SelectedIndex = idx;
            }
            slot.TxtAddress.Text = slot.Config.Address.ToString();
            slot.TxtHost.Text = slot.Config.Host ?? string.Empty;
            slot.TxtPort.Text = slot.Config.Port > 0 ? slot.Config.Port.ToString() : string.Empty;
            if (slot.CmbHomePreset.Items.Count > slot.Config.HomePreset)
                slot.CmbHomePreset.SelectedIndex = slot.Config.HomePreset;
        }

        // ------------------------------------------------------------------
        // Connect / Disconnect (Q2 + Q3)
        // ------------------------------------------------------------------

        private async Task OnConnectAllAsync()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                ReadConfigFromUi(slot);

                if (string.IsNullOrEmpty(slot.Config.Model))
                {
                    slot.LblStatus.Text = "모델 미선택";
                    slot.LblStatus.BackColor = CameraUiColors.StatusIdle;
                    if (slot.BtnCam != null) slot.BtnCam.Enabled = false;
                    continue;
                }

                try
                {
                    if (slot.Service == null && _factory != null)
                    {
                        AttachServiceAtSlot(i, _factory.Create(slot.Config.Model));
                    }
                    if (slot.Service == null)
                    {
                        slot.LblStatus.Text = "팩토리 미부착";
                        slot.LblStatus.BackColor = CameraUiColors.StatusError;
                        if (slot.BtnCam != null) slot.BtnCam.Enabled = false;
                        continue;
                    }

                    ApplyConfigToService(slot);
                    await slot.Service.ConnectAsync().ConfigureAwait(true);
                    slot.ConnectFailed = false;
                    slot.LastErrorMessage = null;
                    slot.LblStatus.Text = "연결됨";
                    slot.LblStatus.BackColor = CameraUiColors.StatusOk;
                    if (slot.BtnCam != null) slot.BtnCam.Enabled = true;

                    // Q3: Auto-recall home preset on successful connect
                    if (slot.Config.HomePreset >= 1)
                    {
                        try { await slot.Service.RecallPresetAsync(slot.Config.HomePreset).ConfigureAwait(true); }
                        catch (Exception ex)
                        {
                            slot.LblStatus.Text = "Home Preset 실패";
                            slot.LblStatus.BackColor = CameraUiColors.StatusWarn;
                            _toolTip.SetToolTip(slot.LblStatus, ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    slot.ConnectFailed = true;
                    slot.LastErrorMessage = ex.Message;
                    slot.LblStatus.Text = "실패";
                    slot.LblStatus.BackColor = CameraUiColors.StatusError;
                    _toolTip.SetToolTip(slot.LblStatus, ex.Message);
                    if (slot.BtnCam != null) slot.BtnCam.Enabled = false;
                }
            }
            UpdateCamButtonsAppearance();
            // Auto-select first connected slot if nothing active
            if (_activeIndex < 0)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Service != null && _slots[i].Service.IsConnected)
                    {
                        SetActiveIndex(i);
                        break;
                    }
                }
            }
        }

        private async Task OnDisconnectAllAsync()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Service != null)
                {
                    try { await slot.Service.DisconnectAsync().ConfigureAwait(true); } catch { }
                }
                slot.LblStatus.Text = "대기";
                slot.LblStatus.BackColor = CameraUiColors.StatusIdle;
                if (slot.BtnCam != null) slot.BtnCam.Enabled = false;
                _toolTip.SetToolTip(slot.LblStatus, string.Empty);
            }
            _activeIndex = -1;
            UpdateCamButtonsAppearance();
        }

        private void AttachServiceAtSlot(int index, ICameraService service)
        {
            if (index < 0 || index >= _slots.Count) return;
            DetachServiceAtSlot(index, dispose: true);
            _slots[index].Service = service;
            if (service != null)
            {
                int capturedIndex = index;
                EventHandler<LogEntry> handler = (s, entry) => OnServiceLogged(capturedIndex, entry);
                _slots[index].LogHandler = handler;
                service.Logged += handler;
            }
        }

        private void DetachServiceAtSlot(int index, bool dispose)
        {
            if (index < 0 || index >= _slots.Count) return;
            var svc = _slots[index].Service;
            if (svc == null) return;
            if (_slots[index].LogHandler != null)
            {
                try { svc.Logged -= _slots[index].LogHandler; } catch { }
                _slots[index].LogHandler = null;
            }
            if (dispose)
            {
                try { svc.Dispose(); } catch { }
            }
            _slots[index].Service = null;
        }

        private void OnServiceLogged(int slotIndex, LogEntry entry)
        {
            var h = ServiceLog;
            if (h == null) return;
            var args = new ServiceLogEventArgs(slotIndex, entry);
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => h(this, args))); } catch { }
            }
            else
            {
                h(this, args);
            }
        }

        private void ApplyConfigToService(SlotState slot)
        {
            var svc = slot.Service;
            if (svc == null) return;
            svc.Transport = slot.Config.Transport;
            svc.ComPort = slot.Config.ComPort;
            svc.BaudRate = slot.Config.BaudRate > 0 ? slot.Config.BaudRate : svc.Capabilities.DefaultBaudRate;
            svc.Address = slot.Config.Address;
            svc.Host = slot.Config.Host;
            svc.Port = slot.Config.Port > 0 ? slot.Config.Port : svc.Capabilities.DefaultIpPort;
        }

        // ------------------------------------------------------------------
        // CAM selection
        // ------------------------------------------------------------------

        private void OnCamClick(object sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null) return;
            int idx = (int)btn.Tag;
            SetActiveIndex(idx);
        }

        private void SetActiveIndex(int idx)
        {
            if (idx < 0 || idx >= _slots.Count) return;
            if (_slots[idx].BtnCam != null && !_slots[idx].BtnCam.Enabled) return;
            _activeIndex = idx;
            UpdateCamButtonsAppearance();
            SyncSpeedSlidersToActive();
        }

        private void UpdateCamButtonsAppearance()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var btn = _slots[i].BtnCam;
                if (btn == null) continue;
                if (!btn.Enabled)
                {
                    btn.BackColor = CameraUiColors.CamDisabled;
                    btn.UseVisualStyleBackColor = false;
                }
                else if (i == _activeIndex)
                {
                    btn.BackColor = CameraUiColors.CamSelected;
                    btn.UseVisualStyleBackColor = false;
                }
                else
                {
                    btn.BackColor = CameraUiColors.CamUnselected;
                    btn.UseVisualStyleBackColor = true;
                }
            }
        }

        // ------------------------------------------------------------------
        // Speed slider sync (Q1)
        // ------------------------------------------------------------------

        private void SyncSpeedSlidersToActive()
        {
            CameraCapabilities caps = null;
            if (_activeIndex >= 0 && _activeIndex < _slots.Count)
            {
                var slot = _slots[_activeIndex];
                if (slot.Service != null) caps = slot.Service.Capabilities;
                else if (!string.IsNullOrEmpty(slot.Config.Model) && _factory != null)
                    caps = _factory.GetCapabilities(slot.Config.Model);
            }
            if (caps == null) return;

            ApplyRange(_numPanSpeed,  caps.PanSpeedMin,  caps.PanSpeedMax,  caps.PanSpeedDefault);
            ApplyRange(_numTiltSpeed, caps.TiltSpeedMin, caps.TiltSpeedMax, caps.TiltSpeedDefault);
            ApplyRange(_numZoomSpeed, caps.ZoomSpeedMin, caps.ZoomSpeedMax, caps.ZoomSpeedDefault);
        }

        private static void ApplyRange(NumericUpDown nud, int min, int max, int defaultValue)
        {
            // Order matters: widen first, narrow second, to avoid invalid intermediate states.
            nud.Minimum = Math.Min(min, (int)nud.Minimum);
            nud.Maximum = Math.Max(max, (int)nud.Maximum);
            nud.Minimum = min;
            nud.Maximum = max;
            int current = (int)nud.Value;
            if (current < min || current > max) nud.Value = Math.Max(min, Math.Min(max, defaultValue));
        }

        // ------------------------------------------------------------------
        // PTZ / Zoom / Focus (Hold-to-Move)
        // ------------------------------------------------------------------

        private async Task SafePtDownAsync(PanTiltDirection dir)
        {
            var svc = ActiveService();
            if (svc == null) return;
            _ptHoldActive = true;
            int p = (int)_numPanSpeed.Value;
            int t = (int)_numTiltSpeed.Value;
            try { await svc.PanTiltDriveAsync(dir, p, t).ConfigureAwait(true); } catch { }
        }

        private async Task SafePtUpAsync()
        {
            // Only send Stop if we previously sent Drive — otherwise hovering over the button
            // would emit spurious Stop commands that block subsequent real commands behind the
            // SemaphoreSlim gate while waiting for replies.
            if (!_ptHoldActive) return;
            _ptHoldActive = false;
            var svc = ActiveService();
            if (svc == null) return;
            try { await svc.PanTiltStopAsync().ConfigureAwait(true); } catch { }
        }

        private async Task SafeZoomDownAsync(ZoomDirection dir)
        {
            var svc = ActiveService();
            if (svc == null) return;
            _zoomHoldActive = true;
            int s = (int)_numZoomSpeed.Value;
            try { await svc.ZoomDriveAsync(dir, s).ConfigureAwait(true); } catch { }
        }

        private async Task SafeZoomUpAsync()
        {
            if (!_zoomHoldActive) return;
            _zoomHoldActive = false;
            var svc = ActiveService();
            if (svc == null) return;
            try { await svc.ZoomStopAsync().ConfigureAwait(true); } catch { }
        }

        private async Task SafeFocusDownAsync(FocusDirection dir)
        {
            var svc = ActiveService();
            if (svc == null) return;
            _focusHoldActive = true;
            int s = svc.Capabilities.FocusSpeedDefault;
            try { await svc.FocusDriveAsync(dir, s).ConfigureAwait(true); } catch { }
        }

        private async Task SafeFocusUpAsync()
        {
            if (!_focusHoldActive) return;
            _focusHoldActive = false;
            var svc = ActiveService();
            if (svc == null) return;
            try { await svc.FocusStopAsync().ConfigureAwait(true); } catch { }
        }

        // ------------------------------------------------------------------
        // OSD / Preset
        // ------------------------------------------------------------------

        private async Task SafeSingleShotAsync(Func<ICameraService, Task> action)
        {
            var svc = ActiveService();
            if (svc == null) return;
            try { await action(svc).ConfigureAwait(true); } catch { }
        }

        private void TogglePresetSetMode()
        {
            _presetSetMode = !_presetSetMode;
            _btnPresetSet.BackColor = _presetSetMode ? CameraUiColors.PresetSetMode : CameraUiColors.PresetNormal;
            _btnPresetSet.UseVisualStyleBackColor = !_presetSetMode;
            ApplyPresetButtonHighlight();
        }

        private void ApplyPresetButtonHighlight()
        {
            var color = _presetSetMode ? CameraUiColors.PresetSetMode : CameraUiColors.PresetNormal;
            ApplyPresetColor(_btnPreset1,  color);
            ApplyPresetColor(_btnPreset2,  color);
            ApplyPresetColor(_btnPreset3,  color);
            ApplyPresetColor(_btnPreset4,  color);
            ApplyPresetColor(_btnPreset5,  color);
            ApplyPresetColor(_btnPreset6,  color);
            ApplyPresetColor(_btnPreset7,  color);
            ApplyPresetColor(_btnPreset8,  color);
            ApplyPresetColor(_btnPreset9,  color);
            ApplyPresetColor(_btnPreset10, color);
            ApplyPresetColor(_btnPreset11, color);
            ApplyPresetColor(_btnPreset12, color);
        }

        private static void ApplyPresetColor(Button btn, Color color)
        {
            if (color == CameraUiColors.PresetNormal)
            {
                btn.UseVisualStyleBackColor = true;
            }
            else
            {
                btn.BackColor = color;
                btn.UseVisualStyleBackColor = false;
            }
        }

        private async Task OnPresetClickAsync(int presetNumber)
        {
            var svc = ActiveService();
            if (svc == null) return;
            try
            {
                if (_presetSetMode)
                {
                    await svc.SetPresetAsync(presetNumber).ConfigureAwait(true);
                    _presetSetMode = false;
                    _btnPresetSet.BackColor = CameraUiColors.PresetNormal;
                    _btnPresetSet.UseVisualStyleBackColor = true;
                    ApplyPresetButtonHighlight();
                }
                else
                {
                    await svc.RecallPresetAsync(presetNumber).ConfigureAwait(true);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ICameraService ActiveService()
        {
            if (_activeIndex < 0 || _activeIndex >= _slots.Count) return null;
            var svc = _slots[_activeIndex].Service;
            if (svc == null || !svc.IsConnected) return null;
            return svc;
        }

        private async Task SafeRunAsync(Func<Task> action)
        {
            if (_busy) return;
            _busy = true;
            try { await action().ConfigureAwait(true); }
            finally { _busy = false; }
        }
    }
}
