using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Xeno.Framework.Camera.Core;
using Xeno.Framework.Camera.Protocols.Visca;
using Xeno.Framework.Camera.Transports;

namespace Xeno.Framework.Camera.Services
{
    /// <summary>
    /// Sony EVI-H100 PTZ camera (RS-232/RS-422 VISCA, default 9600 8N1).
    /// </summary>
    public sealed class EviH100CameraService : CameraServiceBase
    {
        private SerialViscaTransport _serial;

        public EviH100CameraService()
            : base(
                new CameraInfo { Brand = "Sony", Model = "EVI-H100", Description = "Sony EVI-H100 PTZ" },
                CameraCapabilities.EviH100())
        {
        }

        public override async Task ConnectAsync()
        {
            EnsureNotDisposed();
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_serial != null) { _serial.Dispose(); _serial = null; }
                _serial = new SerialViscaTransport
                {
                    PortName = ComPort,
                    BaudRate = BaudRate <= 0 ? Capabilities.DefaultBaudRate : BaudRate,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One
                };
                _serial.Open();
                SetConnected(true, ComPort + "@" + _serial.BaudRate);
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
                if (_serial != null) { _serial.Dispose(); _serial = null; }
                SetConnected(false, "disconnected");
            }
            finally { _gate.Release(); }
        }

        // ----- PTZ -----
        public override Task PanTiltDriveAsync(PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            int p = Clamp(panSpeed, Capabilities.PanSpeedMin, Capabilities.PanSpeedMax);
            int t = Clamp(tiltSpeed, Capabilities.TiltSpeedMin, Capabilities.TiltSpeedMax);
            return SendAsync(ViscaCodec.PanTiltDrive(Address, dir, p, t), "PT Drive " + dir);
        }

        public override Task PanTiltStopAsync()
        {
            return SendAsync(ViscaCodec.PanTiltStop(Address), "PT Stop");
        }

        public override Task ZoomDriveAsync(ZoomDirection dir, int speed)
        {
            int s = Clamp(speed, Capabilities.ZoomSpeedMin, Capabilities.ZoomSpeedMax);
            return SendAsync(ViscaCodec.ZoomDrive(Address, dir, s), "Zoom " + dir);
        }

        public override Task ZoomStopAsync()
        {
            return SendAsync(ViscaCodec.ZoomStop(Address), "Zoom Stop");
        }

        public override Task FocusDriveAsync(FocusDirection dir, int speed)
        {
            int s = Clamp(speed, Capabilities.FocusSpeedMin, Capabilities.FocusSpeedMax);
            return SendAsync(ViscaCodec.FocusDrive(Address, dir, s), "Focus " + dir);
        }

        public override Task FocusStopAsync()
        {
            return SendAsync(ViscaCodec.FocusStop(Address), "Focus Stop");
        }

        // ----- Presets -----
        public override Task RecallPresetAsync(int n)
        {
            int p = Clamp(n, 1, Capabilities.PresetCount);
            return SendAsync(ViscaCodec.PresetRecall(Address, p), "Preset Recall " + p);
        }

        public override Task SetPresetAsync(int n)
        {
            int p = Clamp(n, 1, Capabilities.PresetCount);
            return SendAsync(ViscaCodec.PresetSet(Address, p), "Preset Set " + p);
        }

        // ----- OSD -----
        public override Task OsdOnAsync()     { return SendAsync(ViscaCodec.OsdOn(Address),     "OSD On"); }
        public override Task OsdOffAsync()    { return SendAsync(ViscaCodec.OsdOff(Address),    "OSD Off"); }
        public override Task OsdSelectAsync() { return SendAsync(ViscaCodec.OsdSelect(Address), "OSD Select"); }
        public override Task OsdBackAsync()   { return SendAsync(ViscaCodec.OsdBack(Address),   "OSD Back"); }

        // ----- common send pipeline -----
        private async Task SendAsync(byte[] payload, string label)
        {
            EnsureNotDisposed();
            if (_serial == null || !_serial.IsOpen)
            {
                RaiseError(label + " : not connected");
                return;
            }
            await _gate.WaitAsync().ConfigureAwait(false);
            _busy = true;
            try
            {
                Log(LogDirection.Tx, label + " : " + ToHex(payload));
                byte[] reply = await _serial.SendAsync(payload).ConfigureAwait(false);
                if (reply != null && reply.Length > 0)
                {
                    Log(LogDirection.Rx, ToHex(reply));
                    HandleReply(reply, label);
                }
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

        private void HandleReply(byte[] reply, string label)
        {
            byte err;
            var kind = ViscaResponseParser.Classify(reply, out err);
            if (kind == ViscaResponseParser.ReplyKind.Error)
                RaiseError(label + " : VISCA " + ViscaResponseParser.DescribeError(err));
        }

        public override void Dispose()
        {
            try { if (_serial != null) _serial.Dispose(); } catch { }
            _serial = null;
            base.Dispose();
        }
    }
}
