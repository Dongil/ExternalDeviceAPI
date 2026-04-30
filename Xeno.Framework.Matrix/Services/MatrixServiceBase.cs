using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xeno.Framework.Matrix.Core;

namespace Xeno.Framework.Matrix.Services
{
    /// <summary>
    /// Shared plumbing for <see cref="IMatrixService"/> implementations: property bag, events,
    /// state cache. Derived classes implement the wire protocol.
    /// </summary>
    public abstract class MatrixServiceBase : IMatrixService
    {
        private readonly object _stateLock = new object();
        private readonly Dictionary<int, int> _outputToInput = new Dictionary<int, int>();
        private DeviceInfo _device;
        private volatile bool _connected;
        private volatile bool _reconnecting;

        protected MatrixServiceBase(DeviceInfo initialDevice)
        {
            _device = initialDevice ?? new DeviceInfo();
        }

        public string Host { get; set; } = "192.168.1.100";
        public int Port { get; set; }
        public ConnectionMode Mode { get; set; } = ConnectionMode.Persistent;
        public bool AutoReconnect { get; set; } = true;

        /// <summary>User-supplied floor for input count. 0 = not set (device value wins).</summary>
        protected int UserMaxInputs { get; private set; }
        /// <summary>User-supplied floor for output count. 0 = not set (device value wins).</summary>
        protected int UserMaxOutputs { get; private set; }

        /// <summary>
        /// Host-supplied max input count. Seeds <see cref="DeviceInfo.InputCount"/> and acts as a
        /// floor when the wire protocol later reports a different value. Concrete services may
        /// override (fixed-size devices ignore).
        /// </summary>
        public virtual int MaxInputs
        {
            get { lock (_stateLock) { return _device.InputCount; } }
            set
            {
                if (value >= 1)
                {
                    UserMaxInputs = value;
                    UpdateDeviceInfo(d => d.InputCount = value);
                }
            }
        }

        public virtual int MaxOutputs
        {
            get { lock (_stateLock) { return _device.OutputCount; } }
            set
            {
                if (value >= 1)
                {
                    UserMaxOutputs = value;
                    UpdateDeviceInfo(d => d.OutputCount = value);
                }
            }
        }

        public DeviceInfo Device
        {
            get { lock (_stateLock) { return _device.Clone(); } }
        }

        public bool IsConnected { get { return _connected; } }
        public bool IsReconnecting { get { return _reconnecting; } }

        public event EventHandler<bool> ConnectionStateChanged;
        public event EventHandler<LogEntry> Logged;
        public event EventHandler DeviceInfoChanged;
        public event EventHandler StateUpdated;

        public abstract Task ConnectAsync();
        public abstract Task DisconnectAsync();
        public abstract Task RouteAsync(int input, int output);
        public abstract Task RouteAllAsync(int input);
        public abstract Task PtpAsync();
        public abstract Task RefreshAsync();

        /// <summary>
        /// Default implementation: sequential RouteAsync calls. Services with batch protocol
        /// support (Videohub) override for atomic transactions.
        /// </summary>
        public virtual async Task RouteBatchAsync(IDictionary<int, int> outputToInput)
        {
            if (outputToInput == null || outputToInput.Count == 0) return;
            foreach (var kvp in outputToInput)
            {
                await RouteAsync(kvp.Value, kvp.Key).ConfigureAwait(false);
            }
        }

        public int GetInputFor(int output)
        {
            lock (_stateLock)
            {
                int value;
                return _outputToInput.TryGetValue(output, out value) ? value : 0;
            }
        }

        public virtual void Dispose() { }

        // ---------- protected helpers ----------

        protected void SetConnected(bool value, string reason)
        {
            if (_connected == value) return;
            _connected = value;
            string prefix = value ? "connected" : "disconnected";
            string msg = prefix;
            if (!string.IsNullOrEmpty(reason) && !string.Equals(reason, prefix, StringComparison.OrdinalIgnoreCase))
            {
                msg = prefix + " (" + reason + ")";
            }
            Log(LogDirection.Info, msg);
            var h = ConnectionStateChanged;
            if (h != null) h(this, value);
        }

        protected void SetReconnecting(bool value) { _reconnecting = value; }

        protected void Log(LogDirection direction, string text)
        {
            var h = Logged;
            if (h != null) h(this, new LogEntry(direction, text));
        }

        protected void UpdateDeviceInfo(Action<DeviceInfo> mutator)
        {
            if (mutator == null) return;
            bool changed;
            lock (_stateLock)
            {
                var before = _device.Clone();
                mutator(_device);
                changed = !DeviceInfoEquals(before, _device);
            }
            if (changed)
            {
                var h = DeviceInfoChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private static bool DeviceInfoEquals(DeviceInfo a, DeviceInfo b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.Kind == b.Kind
                && string.Equals(a.Model, b.Model, StringComparison.Ordinal)
                && a.InputCount == b.InputCount
                && a.OutputCount == b.OutputCount
                && a.IsPresent == b.IsPresent;
        }

        protected void ApplyRoute(int input, int output)
        {
            lock (_stateLock) { _outputToInput[output] = input; }
        }

        protected void ApplyRoutes(IDictionary<int, int> outputToInput)
        {
            if (outputToInput == null) return;
            lock (_stateLock)
            {
                foreach (var kvp in outputToInput) _outputToInput[kvp.Key] = kvp.Value;
            }
        }

        protected void RaiseStateUpdated()
        {
            var h = StateUpdated;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected Dictionary<int, int> SnapshotRoutes()
        {
            lock (_stateLock) { return new Dictionary<int, int>(_outputToInput); }
        }
    }
}
