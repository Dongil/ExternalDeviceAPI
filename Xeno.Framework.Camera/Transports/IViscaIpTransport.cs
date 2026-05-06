using System;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Transports
{
    /// <summary>
    /// Common abstraction for IP-based VISCA transports (UDP and TCP variants).
    /// Both share the same VISCA byte payload; only the wire framing differs.
    /// </summary>
    internal interface IViscaIpTransport : IDisposable
    {
        string Host { get; set; }
        int Port { get; set; }
        int ReceiveTimeoutMs { get; set; }
        bool WaitForReply { get; set; }
        Action<string> OnLog { get; set; }
        bool IsOpen { get; }

        void Open();
        void Close();
        Task<byte[]> SendCommandAsync(byte[] viscaPayload);
    }
}
