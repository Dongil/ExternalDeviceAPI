using System;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Abstraction for a single PTZ camera control session. Implementations encapsulate
    /// brand/model-specific protocol and transport details. All async methods are safe to
    /// call from the UI thread; concrete services serialize TX/RX with a SemaphoreSlim.
    /// </summary>
    public interface ICameraService : IDisposable
    {
        // ----- identity & configuration -----
        CameraInfo Info { get; }
        CameraCapabilities Capabilities { get; }
        CameraTransportKind Transport { get; set; }

        // network params (used when Transport is Udp/Tcp)
        string Host { get; set; }
        int Port { get; set; }

        // serial params (used when Transport is RS232/RS422/RS485)
        string ComPort { get; set; }
        int BaudRate { get; set; }

        int Address { get; set; }   // VISCA camera address (1..7)

        bool AutoReconnect { get; set; }

        // ----- state -----
        bool IsConnected { get; }
        bool IsBusy { get; }
        string LastErrorMessage { get; }

        // ----- events -----
        event EventHandler<bool> ConnectionStateChanged;
        event EventHandler<LogEntry> Logged;
        event EventHandler ErrorRaised;

        // ----- lifecycle -----
        Task ConnectAsync();
        Task DisconnectAsync();

        // ----- PTZ commands -----
        Task PanTiltDriveAsync(PanTiltDirection direction, int panSpeed, int tiltSpeed);
        Task PanTiltStopAsync();

        Task ZoomDriveAsync(ZoomDirection direction, int speed);
        Task ZoomStopAsync();

        Task FocusDriveAsync(FocusDirection direction, int speed);
        Task FocusStopAsync();

        // ----- presets -----
        Task RecallPresetAsync(int presetNumber);
        Task SetPresetAsync(int presetNumber);

        // ----- OSD / Menu -----
        Task OsdOnAsync();
        Task OsdOffAsync();
        Task OsdSelectAsync();
        Task OsdBackAsync();
    }
}
