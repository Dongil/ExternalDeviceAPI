using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Shared plumbing for <see cref="ICameraService"/> implementations: state, events,
    /// send-gate semaphore. Concrete services implement transport open/close + SendAsync.
    /// </summary>
    public abstract class CameraServiceBase : ICameraService
    {
        protected readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        protected volatile bool _disposed;
        protected volatile bool _connected;
        protected volatile bool _busy;
        private string _lastErrorMessage;

        protected CameraServiceBase(CameraInfo info, CameraCapabilities caps)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (caps == null) throw new ArgumentNullException(nameof(caps));
            Info = info;
            Capabilities = caps;
            Transport = caps.DefaultTransport;
            BaudRate = caps.DefaultBaudRate;
            Port = caps.DefaultIpPort;
            Address = caps.DefaultAddress;
        }

        public CameraInfo Info { get; }
        public CameraCapabilities Capabilities { get; }

        public CameraTransportKind Transport { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public int Address { get; set; }
        public bool AutoReconnect { get; set; }

        public bool IsConnected { get { return _connected; } }
        public bool IsBusy { get { return _busy; } }
        public string LastErrorMessage { get { return _lastErrorMessage; } }

        public event EventHandler<bool> ConnectionStateChanged;
        public event EventHandler<LogEntry> Logged;
        public event EventHandler ErrorRaised;

        public abstract Task ConnectAsync();
        public abstract Task DisconnectAsync();
        public abstract Task PanTiltDriveAsync(PanTiltDirection direction, int panSpeed, int tiltSpeed);
        public abstract Task PanTiltStopAsync();
        public abstract Task ZoomDriveAsync(ZoomDirection direction, int speed);
        public abstract Task ZoomStopAsync();
        public abstract Task FocusDriveAsync(FocusDirection direction, int speed);
        public abstract Task FocusStopAsync();
        public abstract Task RecallPresetAsync(int presetNumber);
        public abstract Task SetPresetAsync(int presetNumber);
        public abstract Task OsdOnAsync();
        public abstract Task OsdOffAsync();
        public abstract Task OsdSelectAsync();
        public abstract Task OsdBackAsync();

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _gate.Dispose(); } catch { }
        }

        // ----- protected helpers -----

        protected void SetConnected(bool value, string reason)
        {
            if (_connected == value) return;
            _connected = value;
            string prefix = value ? "connected" : "disconnected";
            string msg = string.IsNullOrEmpty(reason) || string.Equals(reason, prefix, StringComparison.OrdinalIgnoreCase)
                ? prefix
                : prefix + " (" + reason + ")";
            Log(LogDirection.Info, msg);
            var h = ConnectionStateChanged;
            if (h != null) h(this, value);
        }

        protected void Log(LogDirection direction, string text)
        {
            var h = Logged;
            if (h != null) h(this, new LogEntry(direction, text));
        }

        protected void RaiseError(string message)
        {
            _lastErrorMessage = message;
            Log(LogDirection.Error, message);
            var h = ErrorRaised;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }

        protected static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        protected static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return "(empty)";
            var sb = new System.Text.StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
