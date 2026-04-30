using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xeno.Framework.Matrix.Core;
using Xeno.Framework.Matrix.Services;

namespace PN8080Controller.UI
{
    public partial class MainForm : Form
    {
        private UserSettings _settings;
        private IMatrixService _service;
        private bool _suppressDeviceAdjust;

        public MainForm()
        {
            InitializeComponent();
            _settings = UserSettings.Load();
            ApplySettingsToUi();
            WireEvents();
            AdjustMaxFieldsForDevice();
            ApplyUiMode();
        }

        private void WireEvents()
        {
            _btnConnect.Click += async (s, e) => await SafeRunAsync(OnConnectClick);
            _btnDisconnect.Click += async (s, e) => await SafeRunAsync(OnDisconnectClick);
            _rbPn8080.CheckedChanged += OnDeviceChanged;
            _rbVideohub.CheckedChanged += OnDeviceChanged;
            _rbPersistent.CheckedChanged += OnModeChanged;
            _rbPerCommand.CheckedChanged += OnModeChanged;
            _rbGridUi.CheckedChanged += OnUiModeChanged;
            _rbAliasUi.CheckedChanged += OnUiModeChanged;
            _aliasMatrixControl.AliasChanged += OnAliasChanged;
            FormClosing += (s, e) =>
            {
                _settings.AutoTake = _aliasMatrixControl.AutoTake;
                _settings.UiMode = _rbAliasUi.Checked ? UiMode.AliasPanel : UiMode.Grid;
                PersistSettings();
                DetachAndDispose();
            };
        }

        private void ApplySettingsToUi()
        {
            _suppressDeviceAdjust = true;
            try
            {
                _rbPn8080.Checked = _settings.Device == DeviceKind.Pn8080;
                _rbVideohub.Checked = _settings.Device == DeviceKind.BlackmagicVideohub;
                _txtHost.Text = _settings.Host;
                _txtPort.Text = _settings.Port.ToString();
                _rbPersistent.Checked = _settings.Mode == ConnectionMode.Persistent;
                _rbPerCommand.Checked = _settings.Mode == ConnectionMode.PerCommand;
                _chkAutoReconnect.Checked = _settings.AutoReconnect;
                _numMaxInputs.Value = Clamp(_settings.MaxInputs, (int)_numMaxInputs.Minimum, (int)_numMaxInputs.Maximum);
                _numMaxOutputs.Value = Clamp(_settings.MaxOutputs, (int)_numMaxOutputs.Minimum, (int)_numMaxOutputs.Maximum);
                _aliasMatrixControl.AutoTake = _settings.AutoTake;
                _rbGridUi.Checked = _settings.UiMode == UiMode.Grid;
                _rbAliasUi.Checked = _settings.UiMode == UiMode.AliasPanel;
            }
            finally
            {
                _suppressDeviceAdjust = false;
            }
        }

        private void OnDeviceChanged(object sender, EventArgs e)
        {
            if (_suppressDeviceAdjust) return;
            if (!(sender is RadioButton rb) || !rb.Checked) return;
            AdjustMaxFieldsForDevice();

            int currentPort;
            int.TryParse(_txtPort.Text, out currentPort);
            if (_rbPn8080.Checked && (currentPort == 9990 || currentPort == 0))
                _txtPort.Text = "8000";
            else if (_rbVideohub.Checked && (currentPort == 8000 || currentPort == 0))
                _txtPort.Text = "9990";
        }

        private void AdjustMaxFieldsForDevice()
        {
            if (_rbPn8080.Checked)
            {
                _numMaxInputs.Value = 8;
                _numMaxOutputs.Value = 8;
                _numMaxInputs.Enabled = false;
                _numMaxOutputs.Enabled = false;
            }
            else
            {
                _numMaxInputs.Enabled = true;
                _numMaxOutputs.Enabled = true;
                if (_numMaxInputs.Value < 2) _numMaxInputs.Value = 16;
                if (_numMaxOutputs.Value < 2) _numMaxOutputs.Value = 16;
            }
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            if (_service != null)
                _service.Mode = _rbPersistent.Checked ? ConnectionMode.Persistent : ConnectionMode.PerCommand;
        }

        private void OnUiModeChanged(object sender, EventArgs e)
        {
            if (!(sender is RadioButton rb) || !rb.Checked) return;
            ApplyUiMode();
        }

        private void ApplyUiMode()
        {
            bool useAlias = _rbAliasUi.Checked;
            _aliasMatrixControl.Visible = useAlias;
            _matrixControl.Visible = !useAlias;
        }

        private void OnAliasChanged(object sender, Xeno.Framework.Matrix.Core.AliasChangedEventArgs e)
        {
            if (_service == null || _aliasMatrixControl.Aliases == null) return;
            _settings.SaveAliases(_service.Host, _service.Port, _aliasMatrixControl.Aliases);
        }

        private async Task OnConnectClick()
        {
            DetachAndDispose();

            _settings.Device = _rbPn8080.Checked ? DeviceKind.Pn8080 : DeviceKind.BlackmagicVideohub;
            _settings.Host = (_txtHost.Text ?? string.Empty).Trim();
            int port;
            int.TryParse(_txtPort.Text, out port);
            _settings.Port = port > 0 && port <= 65535 ? port : (_settings.Device == DeviceKind.Pn8080 ? 8000 : 9990);
            _settings.Mode = _rbPersistent.Checked ? ConnectionMode.Persistent : ConnectionMode.PerCommand;
            _settings.AutoReconnect = _chkAutoReconnect.Checked;
            _settings.MaxInputs = (int)_numMaxInputs.Value;
            _settings.MaxOutputs = (int)_numMaxOutputs.Value;
            _settings.AutoTake = _aliasMatrixControl.AutoTake;
            _settings.UiMode = _rbAliasUi.Checked ? UiMode.AliasPanel : UiMode.Grid;

            IMatrixService service = _settings.Device == DeviceKind.Pn8080
                ? (IMatrixService)new Pn8080MatrixService()
                : new VideohubMatrixService();
            service.Host = _settings.Host;
            service.Port = _settings.Port;
            service.Mode = _settings.Mode;
            service.AutoReconnect = _settings.AutoReconnect;
            service.MaxInputs = _settings.MaxInputs;
            service.MaxOutputs = _settings.MaxOutputs;

            _service = service;

            _matrixControl.AttachService(service);
            _aliasMatrixControl.AttachService(service);

            _aliasMatrixControl.Aliases = _settings.GetAliasesFor(
                service.Host, service.Port, service.MaxInputs, service.MaxOutputs);

            service.ConnectionStateChanged += (s, connected) =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(() =>
                {
                    bool persistent = service.Mode == ConnectionMode.Persistent;
                    _btnConnect.Enabled = !(persistent && (connected || service.IsReconnecting));
                    _btnDisconnect.Enabled = persistent && (connected || service.IsReconnecting);
                }));
            };

            _btnConnect.Enabled = false;
            try
            {
                await service.ConnectAsync();
            }
            finally
            {
                bool persistent = service.Mode == ConnectionMode.Persistent;
                _btnConnect.Enabled = !(persistent && service.IsConnected);
                _btnDisconnect.Enabled = persistent && service.IsConnected;
            }
        }

        private async Task OnDisconnectClick()
        {
            if (_service == null) return;
            await _service.DisconnectAsync();
        }

        private void PersistSettings()
        {
            try { _settings?.Save(); } catch { /* ignore */ }
        }

        private void DetachAndDispose()
        {
            if (_service == null) return;
            _matrixControl.DetachService();
            _aliasMatrixControl.DetachService();
            try { _service.Dispose(); } catch { /* ignore */ }
            _service = null;
        }

        private async Task SafeRunAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
