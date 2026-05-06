using System;
using System.Threading.Tasks;
using Xeno.Framework.Camera.Core;
using Xeno.Framework.Camera.Protocols.Visca;
using Xeno.Framework.Camera.Transports;

namespace Xeno.Framework.Camera.Services
{
    /// <summary>
    /// Generic VISCA over IP (UDP, default port 52381) camera service.
    /// Used for any camera whose wire protocol is Sony VISCA over IP — Sony SRG / BRC series,
    /// PTZOptics, FR-H50SN, and other OEM clones. Behavioural variations (e.g. cameras that
    /// don't emit replies) are expressed via <see cref="CameraCapabilities.ExpectReply"/>.
    /// </summary>
    public sealed class ViscaIpCameraService : CameraServiceBase
    {
        private IViscaIpTransport _transport;
        private string _activeKind;   // "UDP" or "TCP"

        /// <summary>Construct with explicit identity and capabilities.</summary>
        public ViscaIpCameraService(CameraInfo info, CameraCapabilities caps)
            : base(info, caps)
        {
        }

        public override async Task ConnectAsync()
        {
            EnsureNotDisposed();
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_transport != null) { _transport.Dispose(); _transport = null; }

                IViscaIpTransport t;
                if (Transport == CameraTransportKind.TcpVisca)
                {
                    _activeKind = "TCP";
                    t = new TcpViscaTransport();
                }
                else
                {
                    _activeKind = "UDP";
                    t = new UdpViscaTransport();
                }

                t.Host = Host;
                t.Port = Port <= 0 ? Capabilities.DefaultIpPort : Port;
                t.WaitForReply = Capabilities.ExpectReply;
                string tag = "[" + _activeKind + "] ";
                t.OnLog = msg => Log(LogDirection.Info, tag + msg);
                t.Open();
                _transport = t;

                SetConnected(true, _activeKind + " " + Host + ":" + t.Port
                    + (Capabilities.ExpectReply ? "" : " (no-reply mode)"));
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
                if (_transport != null) { _transport.Dispose(); _transport = null; }
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

        public override Task OsdOnAsync()     { return SendAsync(ViscaCodec.OsdOn(Address),     "OSD On"); }
        public override Task OsdOffAsync()    { return SendAsync(ViscaCodec.OsdOff(Address),    "OSD Off"); }
        public override Task OsdSelectAsync() { return SendAsync(ViscaCodec.OsdSelect(Address), "OSD Select"); }
        public override Task OsdBackAsync()   { return SendAsync(ViscaCodec.OsdBack(Address),   "OSD Back"); }

        private async Task SendAsync(byte[] payload, string label)
        {
            EnsureNotDisposed();
            if (_transport == null || !_transport.IsOpen)
            {
                RaiseError(label + " : not connected");
                return;
            }
            await _gate.WaitAsync().ConfigureAwait(false);
            _busy = true;
            try
            {
                Log(LogDirection.Tx, label + " : " + ToHex(payload));
                byte[] reply = await _transport.SendCommandAsync(payload).ConfigureAwait(false);
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
            try { if (_transport != null) _transport.Dispose(); } catch { }
            _transport = null;
            base.Dispose();
        }
    }
}
