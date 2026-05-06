using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Transports
{
    /// <summary>
    /// Serial transport for VISCA payloads (RS-232, RS-422, RS-485 share the same wire format).
    /// Sends the raw payload and reads bytes until a terminator (0xFF) or timeout.
    /// </summary>
    internal sealed class SerialViscaTransport : IDisposable
    {
        private readonly object _lock = new object();
        private SerialPort _port;

        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = Parity.None;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public int ReadTimeoutMs { get; set; } = 500;
        public int WriteTimeoutMs { get; set; } = 500;

        /// <summary>
        /// True (default) = read until 0xFF terminator or timeout (VISCA reply).
        /// False = fire-and-forget; SendAsync returns immediately after Write
        /// (used by Pelco-D and other one-way serial protocols).
        /// </summary>
        public bool WaitForReply { get; set; } = true;

        public bool IsOpen
        {
            get { lock (_lock) { return _port != null && _port.IsOpen; } }
        }

        public void Open()
        {
            lock (_lock)
            {
                CloseInternal();
                if (string.IsNullOrEmpty(PortName))
                    throw new InvalidOperationException("PortName not set");

                _port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    ReadTimeout = ReadTimeoutMs,
                    WriteTimeout = WriteTimeoutMs,
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false
                };
                _port.Open();
                try { _port.DiscardInBuffer(); _port.DiscardOutBuffer(); } catch { }
            }
        }

        public void Close()
        {
            lock (_lock) { CloseInternal(); }
        }

        private void CloseInternal()
        {
            try { if (_port != null && _port.IsOpen) _port.Close(); } catch { }
            try { if (_port != null) _port.Dispose(); } catch { }
            _port = null;
        }

        public Task<byte[]> SendAsync(byte[] payload)
        {
            return Task.Run(new Func<byte[]>(() => SendCore(payload)));
        }

        private byte[] SendCore(byte[] payload)
        {
            SerialPort p;
            lock (_lock) { p = _port; }
            if (p == null || !p.IsOpen) throw new InvalidOperationException("serial port not open");

            try { p.DiscardInBuffer(); } catch { }
            p.Write(payload, 0, payload.Length);

            // Pelco-D and other fire-and-forget protocols: skip the read.
            if (!WaitForReply) return new byte[0];

            // Read until terminator FF or timeout (VISCA reply pattern)
            var buf = new List<byte>(16);
            var start = Environment.TickCount;
            while (Environment.TickCount - start < ReadTimeoutMs)
            {
                try
                {
                    int b = p.ReadByte();
                    if (b < 0) break;
                    buf.Add((byte)b);
                    if ((byte)b == 0xFF) break;
                }
                catch (TimeoutException) { break; }
                catch (System.IO.IOException) { break; }
            }
            return buf.ToArray();
        }

        public void Dispose() { Close(); }
    }
}
