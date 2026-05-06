using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.UI
{
    /// <summary>
    /// Per-slot configuration captured from <see cref="CameraControl"/> rows.
    /// Hosts use this to persist/restore the user's last-entered values.
    /// </summary>
    public sealed class CameraSlotConfig
    {
        public string Model { get; set; }
        public CameraTransportKind Transport { get; set; }
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public int Address { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public int HomePreset { get; set; }   // 0 = none, otherwise preset number to recall on connect

        public CameraSlotConfig Clone()
        {
            return new CameraSlotConfig
            {
                Model = Model,
                Transport = Transport,
                ComPort = ComPort,
                BaudRate = BaudRate,
                Address = Address,
                Host = Host,
                Port = Port,
                HomePreset = HomePreset
            };
        }
    }
}
