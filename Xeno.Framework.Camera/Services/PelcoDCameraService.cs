using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Xeno.Framework.Camera.Core;
using Xeno.Framework.Camera.Protocols.Pelco;
using Xeno.Framework.Camera.Transports;

namespace Xeno.Framework.Camera.Services
{
    /// <summary>
    /// Generic Pelco-D camera service. Codec is PelcoDCodec; transport is dispatched at
    /// ConnectAsync based on the <see cref="CameraTransportKind"/> value (RS-232/422/485 →
    /// SerialViscaTransport; UDP → UdpViscaTransport in RawMode; TCP → TcpViscaTransport).
    /// All paths are fire-and-forget (no reply expected).
    /// </summary>
    public sealed class PelcoDCameraService : CameraServiceBase
    {
        private SerialViscaTransport _serial;
        private IViscaIpTransport _ipTransport;

        public PelcoDCameraService()
            : base(
                new CameraInfo { Brand = "Pelco", Model = "Pelco-D Generic", Description = "Generic Pelco-D PTZ camera" },
                CameraCapabilities.PelcoDGeneric())
        {
        }

        public override async Task ConnectAsync()
        {
            EnsureNotDisposed();
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                DisposeTransports();

                switch (Transport)
                {
                    case CameraTransportKind.Rs232:
                    case CameraTransportKind.Rs422:
                    case CameraTransportKind.Rs485:
                        _serial = new SerialViscaTransport
                        {
                            PortName = ComPort,
                            BaudRate = BaudRate <= 0 ? Capabilities.DefaultBaudRate : BaudRate,
                            Parity = Parity.None,
                            DataBits = 8,
                            StopBits = StopBits.One,
                            WaitForReply = false       // Pelco-D fire-and-forget
                        };
                        _serial.Open();
                        SetConnected(true, "Serial " + ComPort + "@" + _serial.BaudRate + " (Pelco-D)");
                        break;

                    case CameraTransportKind.UdpVisca:
                    {
                        var udp = new UdpViscaTransport
                        {
                            Host = Host,
                            Port = Port <= 0 ? Capabilities.DefaultIpPort : Port,
                            WaitForReply = false,
                            RawMode = true              // Pelco-D over UDP: no Sony 8-byte header
                        };
                        udp.OnLog = msg => Log(LogDirection.Info, "[UDP-Pelco] " + msg);
                        udp.Open();
                        _ipTransport = udp;
                        SetConnected(true, "UDP " + Host + ":" + udp.Port + " (Pelco-D raw)");
                        break;
                    }

                    case CameraTransportKind.TcpVisca:
                    {
                        var tcp = new TcpViscaTransport
                        {
                            Host = Host,
                            Port = Port <= 0 ? Capabilities.DefaultIpPort : Port,
                            WaitForReply = false
                        };
                        tcp.OnLog = msg => Log(LogDirection.Info, "[TCP-Pelco] " + msg);
                        tcp.Open();
                        _ipTransport = tcp;
                        SetConnected(true, "TCP " + Host + ":" + tcp.Port + " (Pelco-D raw)");
                        break;
                    }

                    default:
                        throw new NotSupportedException("Pelco-D unsupported transport: " + Transport);
                }
            }
            catch (Exception ex)
            {
                RaiseError("connect failed: " + ex.Message);
                SetConnected(false, "connect failed");
                throw;
            }
            finally { _gate.Release(); }
        }

        public override async Task DisconnectAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                DisposeTransports();
                SetConnected(false, "disconnected");
            }
            finally { _gate.Release(); }
        }

        // ----- PTZ -----
        public override Task PanTiltDriveAsync(PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            int p = Clamp(panSpeed, Capabilities.PanSpeedMin, Capabilities.PanSpeedMax);
            int t = Clamp(tiltSpeed, Capabilities.TiltSpeedMin, Capabilities.TiltSpeedMax);
            return SendAsync(PelcoDCodec.PanTiltDrive(Address, dir, p, t), "PT Drive " + dir);
        }

        public override Task PanTiltStopAsync()
        {
            return SendAsync(PelcoDCodec.PanTiltStop(Address), "PT Stop");
        }

        public override Task ZoomDriveAsync(ZoomDirection dir, int speed)
        {
            return SendAsync(PelcoDCodec.ZoomDrive(Address, dir, speed), "Zoom " + dir);
        }

        public override Task ZoomStopAsync()
        {
            return SendAsync(PelcoDCodec.ZoomStop(Address), "Zoom Stop");
        }

        public override Task FocusDriveAsync(FocusDirection dir, int speed)
        {
            return SendAsync(PelcoDCodec.FocusDrive(Address, dir, speed), "Focus " + dir);
        }

        public override Task FocusStopAsync()
        {
            return SendAsync(PelcoDCodec.FocusStop(Address), "Focus Stop");
        }

        public override Task RecallPresetAsync(int n)
        {
            int p = Clamp(n, 1, Capabilities.PresetCount);
            return SendAsync(PelcoDCodec.PresetRecall(Address, p), "Preset Recall " + p);
        }

        public override Task SetPresetAsync(int n)
        {
            int p = Clamp(n, 1, Capabilities.PresetCount);
            return SendAsync(PelcoDCodec.PresetSet(Address, p), "Preset Set " + p);
        }

        // OSD: Pelco-D 표준 미지원 — 로그만 남기고 무시
        public override Task OsdOnAsync()     { Log(LogDirection.Info, "OSD On not supported by Pelco-D");     return Task.CompletedTask; }
        public override Task OsdOffAsync()    { Log(LogDirection.Info, "OSD Off not supported by Pelco-D");    return Task.CompletedTask; }
        public override Task OsdSelectAsync() { Log(LogDirection.Info, "OSD Select not supported by Pelco-D"); return Task.CompletedTask; }
        public override Task OsdBackAsync()   { Log(LogDirection.Info, "OSD Back not supported by Pelco-D");   return Task.CompletedTask; }

        private async Task SendAsync(byte[] payload, string label)
        {
            EnsureNotDisposed();
            bool serialOk = _serial != null && _serial.IsOpen;
            bool ipOk = _ipTransport != null && _ipTransport.IsOpen;
            if (!serialOk && !ipOk) { RaiseError(label + " : not connected"); return; }

            await _gate.WaitAsync().ConfigureAwait(false);
            _busy = true;
            try
            {
                Log(LogDirection.Tx, label + " : " + ToHex(payload));
                if (serialOk)
                {
                    await _serial.SendAsync(payload).ConfigureAwait(false);
                }
                else
                {
                    await _ipTransport.SendCommandAsync(payload).ConfigureAwait(false);
                }
                // Fire-and-forget: no reply processing
            }
            catch (Exception ex)
            {
                RaiseError(label + " failed: " + ex.Message);
            }
            finally
            {
                _busy = false;
                _gate.Release();
            }
        }

        private void DisposeTransports()
        {
            try { if (_serial != null) _serial.Dispose(); } catch { }
            try { if (_ipTransport != null) _ipTransport.Dispose(); } catch { }
            _serial = null;
            _ipTransport = null;
        }

        public override void Dispose()
        {
            DisposeTransports();
            base.Dispose();
        }
    }
}
