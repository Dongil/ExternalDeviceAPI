using System.Collections.Generic;

namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Per-model hardware capabilities. UI uses this to dynamically configure
    /// speed slider ranges, baud rate dropdowns, transport defaults, preset count, etc.
    /// Adding a new camera model = create one CameraCapabilities + one ICameraService.
    /// </summary>
    public sealed class CameraCapabilities
    {
        // ----- speed ranges (from manufacturer manual) -----
        public int PanSpeedMin { get; set; }
        public int PanSpeedMax { get; set; }
        public int PanSpeedDefault { get; set; }

        public int TiltSpeedMin { get; set; }
        public int TiltSpeedMax { get; set; }
        public int TiltSpeedDefault { get; set; }

        public int ZoomSpeedMin { get; set; }
        public int ZoomSpeedMax { get; set; }
        public int ZoomSpeedDefault { get; set; }

        public int FocusSpeedMin { get; set; }
        public int FocusSpeedMax { get; set; }
        public int FocusSpeedDefault { get; set; }

        // ----- connection defaults -----
        public CameraTransportKind DefaultTransport { get; set; }
        public IList<int> SupportedBaudRates { get; set; }   // serial models only
        public int DefaultBaudRate { get; set; }
        public int DefaultIpPort { get; set; }                // IP models only
        public int DefaultAddress { get; set; }               // VISCA address (1..7)

        // ----- features -----
        public int PresetCount { get; set; }                  // 1..N (model-dependent)
        public bool OsdSupported { get; set; }

        /// <summary>
        /// Maximum VISCA address (default 7 = VISCA single-controller range).
        /// Pelco-D supports 1-255 — set higher for Pelco-D models.
        /// </summary>
        public int MaxAddress { get; set; } = 7;

        /// <summary>
        /// True (default) = camera replies to commands with VISCA ACK/Completion;
        /// transport waits for the reply per command.
        /// False = fire-and-forget mode for cameras (often OEM/Chinese firmware) that
        /// execute commands but never reply. Avoids per-command timeout latency.
        /// </summary>
        public bool ExpectReply { get; set; } = true;

        public CameraCapabilities Clone()
        {
            return new CameraCapabilities
            {
                PanSpeedMin = PanSpeedMin, PanSpeedMax = PanSpeedMax, PanSpeedDefault = PanSpeedDefault,
                TiltSpeedMin = TiltSpeedMin, TiltSpeedMax = TiltSpeedMax, TiltSpeedDefault = TiltSpeedDefault,
                ZoomSpeedMin = ZoomSpeedMin, ZoomSpeedMax = ZoomSpeedMax, ZoomSpeedDefault = ZoomSpeedDefault,
                FocusSpeedMin = FocusSpeedMin, FocusSpeedMax = FocusSpeedMax, FocusSpeedDefault = FocusSpeedDefault,
                DefaultTransport = DefaultTransport,
                SupportedBaudRates = SupportedBaudRates == null ? null : new List<int>(SupportedBaudRates),
                DefaultBaudRate = DefaultBaudRate,
                DefaultIpPort = DefaultIpPort,
                DefaultAddress = DefaultAddress,
                PresetCount = PresetCount,
                OsdSupported = OsdSupported,
                ExpectReply = ExpectReply,
                MaxAddress = MaxAddress
            };
        }

        // ----- model presets -----
        // Values follow public Sony VISCA conventions; verify exact numbers against the
        // device manual when adding/changing models (see asset/*.pdf).

        public static CameraCapabilities EviH100()
        {
            return new CameraCapabilities
            {
                PanSpeedMin = 0x01, PanSpeedMax = 0x18, PanSpeedDefault = 0x08,
                TiltSpeedMin = 0x01, TiltSpeedMax = 0x14, TiltSpeedDefault = 0x08,
                ZoomSpeedMin = 0, ZoomSpeedMax = 7, ZoomSpeedDefault = 4,
                FocusSpeedMin = 0, FocusSpeedMax = 7, FocusSpeedDefault = 4,
                DefaultTransport = CameraTransportKind.Rs232,
                SupportedBaudRates = new List<int> { 9600, 19200, 38400 },
                DefaultBaudRate = 9600,
                DefaultIpPort = 0,
                DefaultAddress = 1,
                PresetCount = 6,
                OsdSupported = true
            };
        }

        public static CameraCapabilities SrgIp()
        {
            return new CameraCapabilities
            {
                PanSpeedMin = 0x01, PanSpeedMax = 0x18, PanSpeedDefault = 0x10,
                TiltSpeedMin = 0x01, TiltSpeedMax = 0x14, TiltSpeedDefault = 0x10,
                ZoomSpeedMin = 0, ZoomSpeedMax = 7, ZoomSpeedDefault = 4,
                FocusSpeedMin = 0, FocusSpeedMax = 7, FocusSpeedDefault = 4,
                DefaultTransport = CameraTransportKind.UdpVisca,
                SupportedBaudRates = null,
                DefaultBaudRate = 0,
                DefaultIpPort = 52381,
                DefaultAddress = 1,
                PresetCount = 16,
                OsdSupported = true,
                ExpectReply = true
            };
        }

        /// <summary>
        /// FR-H50SN: VISCA-over-IP camera with OEM (Chinese) firmware. UDP commands execute
        /// but emit no ACK/Completion; TCP path may reply (verify in the camera web UI).
        /// Defaults assume TCP transport with replies enabled — switch the row to UDP and
        /// untick replies (via model swap to a no-reply variant) if your firmware is silent.
        /// </summary>
        public static CameraCapabilities FrH50Sn()
        {
            var caps = SrgIp();
            caps.DefaultTransport = CameraTransportKind.TcpVisca;
            caps.DefaultIpPort = 5678;            // Third-party raw VISCA-over-TCP convention (PTZOptics et al.)
            caps.PresetCount = 16;
            caps.ExpectReply = true;              // TCP path replies normally on this firmware
            return caps;
        }

        /// <summary>
        /// Canon CR-N300 — VISCA-over-IP. Default port 52381 (Sony standard, Canon adopts same).
        /// Confirmed at user site (2026-05-06): UDP fix (unconnected socket pattern) required for
        /// reply reception. Speed/preset values follow Sony VISCA convention pending Canon manual.
        /// </summary>
        public static CameraCapabilities CanonCrN300()
        {
            var caps = SrgIp();
            caps.DefaultTransport = CameraTransportKind.UdpVisca;
            caps.DefaultIpPort = 52381;
            caps.PresetCount = 16;
            caps.ExpectReply = true;
            return caps;
        }

        /// <summary>
        /// Generic Pelco-D camera (vendor-agnostic). Wire format = 7-byte packet:
        /// [Sync 0xFF] [Addr 1-255] [Cmd1] [Cmd2] [Pan-Speed] [Tilt-Speed] [Checksum].
        /// Pelco-D is fire-and-forget by spec — most cameras do not emit ACKs.
        /// </summary>
        public static CameraCapabilities PelcoDGeneric()
        {
            return new CameraCapabilities
            {
                // Pelco-D Pan/Tilt speed range 0x00-0x3F (63), 0xFF = Turbo (별도 처리)
                PanSpeedMin = 0x00, PanSpeedMax = 0x3F, PanSpeedDefault = 0x20,
                TiltSpeedMin = 0x00, TiltSpeedMax = 0x3F, TiltSpeedDefault = 0x20,
                // Zoom/Focus 속도 별도 없음 — UI 편의상 동일 범위 노출 (codec 무시)
                ZoomSpeedMin = 0, ZoomSpeedMax = 7, ZoomSpeedDefault = 4,
                FocusSpeedMin = 0, FocusSpeedMax = 7, FocusSpeedDefault = 4,
                DefaultTransport = CameraTransportKind.Rs232,
                SupportedBaudRates = new List<int> { 2400, 4800, 9600, 19200, 38400 },
                DefaultBaudRate = 9600,                  // Q4 결정 (사용자 환경 표준)
                DefaultIpPort = 4001,                    // Pelco-D over UDP/TCP vendor 컨벤션
                DefaultAddress = 1,
                PresetCount = 64,                        // Pelco-D vendor 별 32-256, 보수적 64
                OsdSupported = false,                    // Pelco-D 는 OSD 표준 명령 없음
                ExpectReply = false,                     // Q3 결정 — fire-and-forget
                MaxAddress = 255                         // Pelco-D 주소 1-255
            };
        }
    }
}
