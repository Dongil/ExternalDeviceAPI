using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace DeviceEmulator.Transports
{
    internal sealed class SerialServerTransport : IDisposable
    {
        public enum FrameMode { ViscaTerminator, PelcoFixed }

        private SerialPort _port;
        private readonly object _lock = new object();
        private readonly List<byte> _accumulator = new List<byte>(16);

        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = Parity.None;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public FrameMode Framing { get; set; } = FrameMode.ViscaTerminator;

        public Action<byte[]> OnPacketReceived { get; set; }
        public Action<string> OnLog { get; set; }

        public bool IsOpen { get { lock (_lock) { return _port != null && _port.IsOpen; } } }

        public void Start()
        {
            lock (_lock)
            {
                Stop();
                _port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false
                };
                _port.DataReceived += OnDataReceived;
                _port.Open();
                Log("Serial listen on " + PortName + "@" + BaudRate + " (" + Framing + ")");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                try { if (_port != null) { _port.DataReceived -= OnDataReceived; _port.Close(); _port.Dispose(); } } catch { }
                _port = null;
                _accumulator.Clear();
            }
        }

        public void SendReply(byte[] data)
        {
            SerialPort p;
            lock (_lock) { p = _port; }
            if (p == null || !p.IsOpen) return;
            try { p.Write(data, 0, data.Length); } catch (Exception ex) { Log("Serial send failed: " + ex.Message); }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort p;
            lock (_lock) { p = _port; }
            if (p == null) return;
            try
            {
                int n = p.BytesToRead;
                if (n <= 0) return;
                var buf = new byte[n];
                p.Read(buf, 0, n);
                lock (_lock)
                {
                    for (int i = 0; i < buf.Length; i++)
                    {
                        _accumulator.Add(buf[i]);
                        bool packetReady = false;
                        if (Framing == FrameMode.ViscaTerminator && buf[i] == 0xFF) packetReady = true;
                        else if (Framing == FrameMode.PelcoFixed && _accumulator.Count == 7) packetReady = true;
                        if (packetReady)
                        {
                            var packet = _accumulator.ToArray();
                            _accumulator.Clear();
                            try { var h = OnPacketReceived; if (h != null) h(packet); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { Log("Serial read failed: " + ex.Message); }
        }

        private void Log(string msg) { var h = OnLog; if (h != null) try { h(msg); } catch { } }
        public void Dispose() { Stop(); }
    }
}
