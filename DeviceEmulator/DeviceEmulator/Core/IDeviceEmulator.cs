using System;

namespace DeviceEmulator.Core
{
    public interface IDeviceEmulator : IDisposable
    {
        string DeviceType { get; }
        string Brand { get; }
        string Model { get; }
        string Description { get; }
        EmulatorOptions Options { get; }

        bool IsListening { get; }
        event EventHandler<string> Logged;

        void Configure(EmulatorConfig cfg);
        void Start();
        void Stop();
    }

    public sealed class EmulatorConfig
    {
        public string TransportKind { get; set; }
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public string LocalIpFilter { get; set; }
        public int LocalPort { get; set; }
        public bool SonyReplyConvention { get; set; } = true;
    }
}
