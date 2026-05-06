# Design — camera-remote-control (PTZ 카메라 통합 제어 프레임워크)

> **문서 버전**: v1.1 (2026-05-06 갱신, round-2 implementation-ahead 9건 흡수)
> **이전 버전**: v1.0 (2026-05-04 초기 Design)

## 변경 이력

| 버전 | 일자 | 변경 요약 |
|------|------|----------|
| v1.0 | 2026-05-04 | 초기 Design — Plan §11 Q1~Q5 결정 흡수, 19개 파일 명세 |
| v1.1 | 2026-05-06 | Round-2 Gap Analysis 결과 흡수: ① UDP transport 백그라운드 RX 루프 + 큐 패턴 (§5.2 재기술), ② TCP transport 신설 (§5.3), ③ `IViscaIpTransport` 공통 추상화 (§5.0), ④ `SrgIpCameraService` → `ViscaIpCameraService` 일반화 (§6.2), ⑤ `CameraCapabilities.ExpectReply` + `FrH50Sn()` 팩토리 (§3.4), ⑥ FR-H50SN 모델 등록 (§3.7), ⑦ UI 통신 콤보 5항목 (§7.4·§7.7), ⑧ `MainForm` 로그 toolbar (§8.2), ⑨ `CameraControl.ServiceLog` 이벤트 (§7.2), ⑩ Hold-to-Move 축별 hold-flag 가드 (§7.9). 모든 변경은 실측 디버깅에서 정당화된 진화 — 코드 수정 없이 문서만 동기화. |

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | camera-remote-control |
| **참조 Plan** | [camera-remote-control.plan.md](../../01-plan/features/camera-remote-control.plan.md) |
| **대상 솔루션** | `PN8080Controller.sln` |
| **신규 프로젝트** | `Xeno.Framework.Camera` (Library, GUID `{A2C3D4E5-F6B7-48C9-9A1D-2E3F4B5C6A7B}`), `CameraController` (WinExe, GUID `{B3D4E5F6-A7B8-49CA-AB2E-3F4A5B6C7D8E}`) |
| **신규 파일 (v1.1 기준)** | 라이브러리 23개 (Core 8 + Visca 3 + Transports 4 + Services 2 + UI 6) / 테스트 하네스 5개 / 솔루션 등록 |
| **수정 파일** | `PN8080Controller.sln` (프로젝트 2개 추가), `Xeno.Framework.Camera/Form1.cs / .Designer.cs / Program.cs` (제거), `Xeno.Framework.Camera.csproj` (OutputType WinExe → Library 전환) |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | Plan §11 Decisions Log 5건이 확정되었어도, 클래스 시그니처·바이트 사전·이벤트 흐름·디자이너 호환 패턴이 명세되지 않으면 구현자가 매번 결정을 다시 내리며 일관성을 잃는다. 특히 시리얼/UDP Transport 가 하나의 코덱을 공유하는 구조는 잘못 설계 시 시퀀스 동기화·에러 처리에서 누수가 발생한다. |
| **Solution** | 19개 파일 단위로 namespace·클래스·프로퍼티·메서드 시그니처·이벤트·예외 처리·바이트 시퀀스·UI 상태머신을 모두 사전 확정. `ViscaCodec` 정적 빌더가 두 Transport 의 단일 진실 공급원 역할. `CameraControl` 의 N대 동적 행 생성 알고리즘과 디자이너 호환 규약 명시. |
| **Function UX Effect** | 구현 단계에서는 본 문서 §4~§9 의 시그니처를 그대로 옮겨 쓰면 컴파일이 통과하도록 설계. 모델 추가 비용은 "1 Service + 1 Capabilities" 로 측정 가능. UI 가 모델 메타데이터에 따라 동적 변형되어 신규 모델이 즉시 사용 가능. |
| **Core Value** | Design 문서가 "구현 전 최종 기술 계약" 역할 → Do 단계 속도↑·재작업↓. `ViscaCodec`·`UdpViscaTransport` 는 향후 다른 Sony VISCA 모델(SRG-X400, BRC-X400 등) 추가 시 재사용 가능한 자산. |

---

## 1. Plan 결정사항 매핑 (Q1~Q5 → 설계 위치)

| Q | 결정 | 본 문서 위치 |
|---|------|--------------|
| Q1 | 속도는 모델별, `CameraCapabilities` 메타데이터 | §3.4 `CameraCapabilities`, §8.5 슬라이더 동기화 |
| Q2 | 모두 동시 연결 + 실패 카메라 CAM 버튼 Disable | §8.6 연결 상태머신 |
| Q3 | 연결 직후 Home Preset Auto-Recall | §8.6 연결 상태머신 마지막 단계 |
| Q4 | 모든 통신 필드 표시 + 통신 방식별 Disable | §8.4 행 레이아웃 + §8.7 필드 활성화 규칙 |
| Q5 | N대 확장 (기본 3) | §8.3 컨트롤 옵션, §8.8 동적 행 생성 알고리즘 |

---

## 2. 아키텍처 오버뷰

```
┌──────────────────────────────────────────────────────────────────────────┐
│ CameraController.exe (테스트 하네스, WinExe)                              │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │ MainForm                                                            │  │
│  │   └── _cameraControl (CameraControl)                                │  │
│  │         ├── SlotCount = 3 (디자이너 시드)                           │  │
│  │         └── AttachFactory(new CameraServiceFactory())               │  │
│  │   └── UserSettings (마지막 모델/IP/COM/ID/Baud 영속화)              │  │
│  └────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ Xeno.Framework.Camera.dll (Class Library, .NET 4.8, LangVersion 7.3)     │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐     │
│  │ Core/                                                            │     │
│  │   ICameraService (interface)                                     │     │
│  │   CameraServiceBase (abstract — 공통 상태/이벤트/SemaphoreSlim)  │     │
│  │   CameraInfo, CameraCapabilities, CameraTransportKind            │     │
│  │   PanTiltDirection, ZoomDirection, FocusDirection                │     │
│  │   PresetMode, ConnectionMode                                     │     │
│  │   LogDirection, LogEntry  (Matrix 와 동일 시그니처, 복제)        │     │
│  │   ICameraServiceFactory, CameraServiceFactory                    │     │
│  └─────────────────────────────────────────────────────────────────┘     │
│           ▲                                                               │
│           │   uses                                                        │
│  ┌────────┴─────────────────────────────────────┐  ┌──────────────────┐  │
│  │ Services/                                     │  │ Protocols/Visca/ │  │
│  │   EviH100CameraService : CameraServiceBase    │  │   ViscaCodec     │  │
│  │   SrgIpCameraService   : CameraServiceBase    │  │   ViscaResponse  │  │
│  └────────┬───────────┬──────────────────────────┘  │     Parser       │  │
│           │ uses      │ uses                         │   ViscaConstants │  │
│           ▼           ▼                              └──────────────────┘  │
│  ┌────────────────┐ ┌────────────────┐                                    │
│  │ Transports/    │ │ Transports/    │                                    │
│  │ SerialVisca    │ │ UdpVisca       │                                    │
│  │ Transport      │ │ Transport      │                                    │
│  │ (SerialPort)   │ │ (UdpClient,    │                                    │
│  │                │ │  8B 시퀀스 헤더)│                                    │
│  └────────────────┘ └────────────────┘                                    │
│                                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐     │
│  │ UI/                                                              │     │
│  │   CameraControl.cs (.Designer.cs, .resx)                         │     │
│  │     - SlotCount, AttachFactory(...) / AttachServices(...)        │     │
│  │     - 동적 N행 생성 (TableLayoutPanel)                            │     │
│  │     - Hold-to-Move (MouseDown/Up/Leave) PTZ/Zoom/Focus           │     │
│  │     - 프리셋 SET 토글, OSD 단발                                  │     │
│  │     - 모델별 슬라이더 범위 동기화                                │     │
│  │   CameraUiColors (선택/일반/경고/SET 모드 색상 상수)             │     │
│  └─────────────────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Core 계층 — 인터페이스/모델

### 3.1 `Xeno.Framework.Camera/Core/ICameraService.cs`

```csharp
using System;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Abstraction for a single PTZ camera control session.
    /// Implementations encapsulate brand/model-specific protocol & transport details.
    /// All async methods are safe to call from the UI thread; concrete services serialize TX/RX with SemaphoreSlim.
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
        string ComPort { get; set; }      // e.g. "COM3"
        int BaudRate { get; set; }        // e.g. 9600

        int Address { get; set; }         // VISCA camera address (1..7), 0 = N/A for IP single-drop

        bool AutoReconnect { get; set; }  // default false (carry-over from Matrix patterns; PTZ rarely needs)

        // ----- state -----
        bool IsConnected { get; }
        bool IsBusy { get; }
        string LastErrorMessage { get; }

        // ----- events -----
        event EventHandler<bool> ConnectionStateChanged;
        event EventHandler<LogEntry> Logged;
        event EventHandler ErrorRaised;   // LastErrorMessage 갱신 시

        // ----- lifecycle -----
        Task ConnectAsync();
        Task DisconnectAsync();

        // ----- PTZ commands -----
        Task PanTiltDriveAsync(PanTiltDirection direction, int panSpeed, int tiltSpeed);
        Task PanTiltStopAsync();

        Task ZoomDriveAsync(ZoomDirection direction, int speed);   // speed 0..7 일반 / -1 = 표준 속도
        Task ZoomStopAsync();

        Task FocusDriveAsync(FocusDirection direction, int speed); // speed 0..7
        Task FocusStopAsync();

        // ----- presets -----
        Task RecallPresetAsync(int presetNumber);  // 1..PresetCount
        Task SetPresetAsync(int presetNumber);

        // ----- OSD / Menu -----
        Task OsdOnAsync();
        Task OsdOffAsync();
        Task OsdSelectAsync();
        Task OsdBackAsync();
    }
}
```

### 3.2 `Xeno.Framework.Camera/Core/CameraInfo.cs`

```csharp
namespace Xeno.Framework.Camera.Core
{
    /// <summary>Static identity of a camera model.</summary>
    public sealed class CameraInfo
    {
        public string Brand { get; set; }     // "Sony"
        public string Model { get; set; }     // "EVI-H100", "SRG-300H"
        public string Description { get; set; }

        public CameraInfo Clone() => new CameraInfo { Brand = Brand, Model = Model, Description = Description };
    }
}
```

### 3.3 `Xeno.Framework.Camera/Core/CameraTransportKind.cs`

```csharp
namespace Xeno.Framework.Camera.Core
{
    public enum CameraTransportKind
    {
        Rs232 = 0,
        Rs422 = 1,
        Rs485 = 2,
        UdpVisca = 10,   // Sony VISCA over IP UDP 52381
        TcpVisca = 11    // (예약, v2)
    }
}
```

### 3.4 `Xeno.Framework.Camera/Core/CameraCapabilities.cs` ⭐ **Q1 핵심**

```csharp
using System.Collections.Generic;

namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Per-model hardware capabilities. Used by UI to dynamically configure
    /// speed slider ranges, baud rate dropdowns, transport defaults, etc.
    /// </summary>
    public sealed class CameraCapabilities
    {
        // 속도 범위 (제조사 매뉴얼에서 정함)
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

        // 연결 기본값
        public CameraTransportKind DefaultTransport { get; set; }
        public IList<int> SupportedBaudRates { get; set; }   // 시리얼 모델에서만 의미
        public int DefaultBaudRate { get; set; }
        public int DefaultIpPort { get; set; }               // IP 모델에서만 의미 (52381 등)
        public int DefaultAddress { get; set; }              // VISCA address 기본 1

        // 기능
        public int PresetCount { get; set; }                 // 1..N (EVI-H100=6/16, SRG-300H=16)
        public bool OsdSupported { get; set; }

        /// <summary>
        /// True (기본) = 카메라가 VISCA ACK/Completion 으로 응답함 → transport 가 응답을 기다림.
        /// False = OEM/Chinese firmware 같은 silent 카메라용 fire-and-forget 모드.
        /// `ViscaIpCameraService` 가 이 값을 transport 의 `WaitForReply` 로 forward.
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
                ExpectReply = ExpectReply
            };
        }

        // ----- 모델별 프리셋 (정적 팩토리) -----
        // 실측 매뉴얼 기반 (구현 시 PDF 재확인하여 미세 조정)
        public static CameraCapabilities EviH100()
        {
            return new CameraCapabilities
            {
                PanSpeedMin = 1, PanSpeedMax = 0x18, PanSpeedDefault = 0x08,   // VISCA: 1..18 (hex)
                TiltSpeedMin = 1, TiltSpeedMax = 0x14, TiltSpeedDefault = 0x08, // 1..14 (hex) — 매뉴얼 재확인
                ZoomSpeedMin = 0, ZoomSpeedMax = 7, ZoomSpeedDefault = 4,
                FocusSpeedMin = 0, FocusSpeedMax = 7, FocusSpeedDefault = 4,
                DefaultTransport = CameraTransportKind.Rs232,
                SupportedBaudRates = new List<int> { 9600, 19200, 38400 },     // 매뉴얼 재확인
                DefaultBaudRate = 9600,
                DefaultIpPort = 0,
                DefaultAddress = 1,
                PresetCount = 6,    // EVI-H100 — 매뉴얼 재확인 (6 또는 16)
                OsdSupported = true
            };
        }

        // Sony SRG 계열 (SRG-300H 등) — VISCA over IP UDP 기본
        public static CameraCapabilities SrgIp()
        {
            return new CameraCapabilities
            {
                PanSpeedMin = 1, PanSpeedMax = 0x18, PanSpeedDefault = 0x10,
                TiltSpeedMin = 1, TiltSpeedMax = 0x14, TiltSpeedDefault = 0x10,
                ZoomSpeedMin = 0, ZoomSpeedMax = 7, ZoomSpeedDefault = 4,
                FocusSpeedMin = 0, FocusSpeedMax = 7, FocusSpeedDefault = 4,
                DefaultTransport = CameraTransportKind.UdpVisca,
                SupportedBaudRates = null,
                DefaultBaudRate = 0,
                DefaultIpPort = 52381,                     // Sony VISCA over IP 표준 포트
                DefaultAddress = 1,
                PresetCount = 16,
                OsdSupported = true,
                ExpectReply = true
            };
        }

        /// <summary>
        /// FR-H50SN (v1.1 추가): VISCA-over-IP 호환 OEM 모델. UDP 응답 미지원 firmware 가 다수이며,
        /// raw VISCA over TCP (PTZOptics 컨벤션) 경로에서는 정상 응답. 실측 (2026-05-04, 사용자 KDI):
        /// TCP port 52381 + ACK 도착 ~5ms. 단 vendor unit 별로 TCP 포트가 5678 / 52381 / 1259 등 다양하여
        /// `DefaultIpPort = 5678` 은 plceholder 의미 — 사용자가 카메라 web UI 에서 실제 포트 확인 후
        /// UI 의 Port TextBox 에 직접 입력해야 함.
        /// </summary>
        public static CameraCapabilities FrH50Sn()
        {
            var caps = SrgIp();
            caps.DefaultTransport = CameraTransportKind.TcpVisca;
            caps.DefaultIpPort = 5678;                     // PTZOptics raw-VISCA-over-TCP 컨벤션
            caps.PresetCount = 16;
            caps.ExpectReply = true;                       // TCP 경로에서 응답 정상
            return caps;
        }
    }
}
```

> 📌 **구현 시 책임**: EVI-H100·SRG-300H 의 정확한 속도 범위·프리셋 개수는 `asset/16349-EVI-H100V-S Tech Manual.pdf`, `asset/SRG-300H Technical Manual.pdf` 의 VISCA 명령 표에서 최종 확인. 위 값은 일반 Sony VISCA 표준 기준 초기값.

### 3.5 방향 enum 들

```csharp
namespace Xeno.Framework.Camera.Core
{
    public enum PanTiltDirection
    {
        Up, Down, Left, Right,
        UpLeft, UpRight, DownLeft, DownRight,
        Stop
    }

    public enum ZoomDirection { Tele, Wide, Stop }
    public enum FocusDirection { Far, Near, Stop }
    public enum PresetMode { Recall, Set }
    public enum ConnectionMode { Persistent = 0, PerCommand = 1 }   // 시리얼은 항상 Persistent
}
```

### 3.6 `LogEntry` / `LogDirection` (Matrix 와 동일 시그니처, 복제)

```csharp
namespace Xeno.Framework.Camera.Core
{
    public enum LogDirection { Tx, Rx, Info, Error }

    public sealed class LogEntry
    {
        public LogDirection Direction { get; }
        public string Text { get; }
        public DateTime TimestampUtc { get; }
        public LogEntry(LogDirection direction, string text)
        {
            Direction = direction;
            Text = text ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }
    }
}
```

> v2 에서 `Xeno.Framework.Common` 으로 추출 검토 (현재는 의존성 분리 위해 복제).

### 3.7 `ICameraServiceFactory` / `CameraServiceFactory`

```csharp
namespace Xeno.Framework.Camera.Core
{
    public interface ICameraServiceFactory
    {
        IList<CameraInfo> GetAvailableModels();
        CameraCapabilities GetCapabilities(string model);
        ICameraService Create(string model);
    }

    public sealed class CameraServiceFactory : ICameraServiceFactory
    {
        public IList<CameraInfo> GetAvailableModels()
        {
            return new List<CameraInfo>
            {
                new CameraInfo { Brand = "Sony", Model = "EVI-H100", Description = "Sony EVI-H100 (RS-232/RS-422 VISCA)" },
                new CameraInfo { Brand = "Sony", Model = "SRG-300H", Description = "Sony SRG-300H (VISCA over IP)" },
                new CameraInfo { Brand = "FR",   Model = "FR-H50SN", Description = "FR-H50SN (VISCA over IP, OEM firmware)" }   // v1.1
            };
        }

        public CameraCapabilities GetCapabilities(string model)
        {
            switch (model)
            {
                case "EVI-H100": return CameraCapabilities.EviH100();
                case "SRG-300H": return CameraCapabilities.SrgIp();
                case "FR-H50SN": return CameraCapabilities.FrH50Sn();    // v1.1
                default: throw new NotSupportedException("Unknown model: " + model);
            }
        }

        public ICameraService Create(string model)
        {
            switch (model)
            {
                case "EVI-H100":
                    return new Services.EviH100CameraService();
                case "SRG-300H":
                    return new Services.ViscaIpCameraService(
                        new CameraInfo { Brand = "Sony", Model = "SRG-300H", Description = "Sony SRG-300H PTZ (VISCA over IP)" },
                        CameraCapabilities.SrgIp());
                case "FR-H50SN":            // v1.1 — 같은 generic Service, 다른 (Info, Caps)
                    return new Services.ViscaIpCameraService(
                        new CameraInfo { Brand = "FR", Model = "FR-H50SN", Description = "FR-H50SN PTZ (VISCA over IP, OEM firmware)" },
                        CameraCapabilities.FrH50Sn());
                default:
                    throw new NotSupportedException("Unknown model: " + model);
            }
        }
    }
}
```

> **v1.1 — 모델 추가 비용의 재정의**: round-1 가설은 "1 Service + 1 Capabilities" 였으나, `ViscaIpCameraService` 가 generic 화되면서 **0 Service + 1 Capabilities + 1 factory case** 로 추가로 줄어듦. 시리얼 경로는 여전히 `EviH100CameraService` 같은 모델별 클래스를 유지 (시리얼 파라미터가 모델별로 다양함).

### 3.8 `CameraServiceBase` (abstract)

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Core
{
    public abstract class CameraServiceBase : ICameraService
    {
        protected readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        protected volatile bool _disposed;
        protected volatile bool _connected;
        protected volatile bool _busy;

        protected CameraServiceBase(CameraInfo info, CameraCapabilities caps)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Capabilities = caps ?? throw new ArgumentNullException(nameof(caps));
            Transport = caps.DefaultTransport;
            BaudRate = caps.DefaultBaudRate;
            Port = caps.DefaultIpPort;
            Address = caps.DefaultAddress;
        }

        public CameraInfo Info { get; }
        public CameraCapabilities Capabilities { get; }

        public CameraTransportKind Transport { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public int Address { get; set; }
        public bool AutoReconnect { get; set; }

        public bool IsConnected => _connected;
        public bool IsBusy => _busy;
        public string LastErrorMessage { get; protected set; }

        public event EventHandler<bool> ConnectionStateChanged;
        public event EventHandler<LogEntry> Logged;
        public event EventHandler ErrorRaised;

        public abstract Task ConnectAsync();
        public abstract Task DisconnectAsync();
        public abstract Task PanTiltDriveAsync(PanTiltDirection direction, int panSpeed, int tiltSpeed);
        public abstract Task PanTiltStopAsync();
        public abstract Task ZoomDriveAsync(ZoomDirection direction, int speed);
        public abstract Task ZoomStopAsync();
        public abstract Task FocusDriveAsync(FocusDirection direction, int speed);
        public abstract Task FocusStopAsync();
        public abstract Task RecallPresetAsync(int presetNumber);
        public abstract Task SetPresetAsync(int presetNumber);
        public abstract Task OsdOnAsync();
        public abstract Task OsdOffAsync();
        public abstract Task OsdSelectAsync();
        public abstract Task OsdBackAsync();

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _gate.Dispose(); } catch { }
        }

        // ----- protected helpers -----
        protected void SetConnected(bool value, string reason)
        {
            if (_connected == value) return;
            _connected = value;
            Log(LogDirection.Info, (value ? "connected" : "disconnected") +
                                   (string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")"));
            ConnectionStateChanged?.Invoke(this, value);
        }

        protected void Log(LogDirection direction, string text)
        {
            Logged?.Invoke(this, new LogEntry(direction, text));
        }

        protected void RaiseError(string message)
        {
            LastErrorMessage = message;
            Log(LogDirection.Error, message);
            ErrorRaised?.Invoke(this, EventArgs.Empty);
        }

        protected void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }

        protected int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
```

---

## 4. Protocol 계층 — VISCA 코덱

### 4.1 `Protocols/Visca/ViscaConstants.cs`

```csharp
namespace Xeno.Framework.Camera.Protocols.Visca
{
    internal static class ViscaConstants
    {
        public const byte Header(int address) =>  /* C# 7.3 미지원 — 다음 메서드로 대체 */ 0;

        public const byte Terminator = 0xFF;
        public const byte BroadcastHeader = 0x88;

        // Sony VISCA over IP payload type (16-bit)
        public const ushort PayloadType_ViscaCommand = 0x0100;
        public const ushort PayloadType_ViscaInquiry = 0x0110;
        public const ushort PayloadType_ViscaReply   = 0x0111;
        public const ushort PayloadType_ControlCmd   = 0x0200;
        public const ushort PayloadType_ControlReply = 0x0201;
    }

    internal static class ViscaHeader
    {
        // VISCA 헤더 = 0x80 | (address & 0x0F)
        public static byte Build(int address)
        {
            int a = address < 1 ? 1 : (address > 7 ? 7 : address);
            return (byte)(0x80 | (a & 0x0F));
        }
    }
}
```

### 4.2 `Protocols/Visca/ViscaCodec.cs` ⭐ **공통 명령 사전**

```csharp
using System;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// Builds VISCA command byte sequences (without IP transport header).
    /// Output is the raw VISCA payload starting with header byte (8x) and ending with terminator FF.
    /// Both Serial and UDP transports send the identical payload.
    /// </summary>
    internal static class ViscaCodec
    {
        // ----- PT Drive / Stop -----
        // Format: 8x 01 06 01 VV WW pp tt FF
        //   VV = pan speed (1..0x18), WW = tilt speed (1..0x14)
        //   pp: 01=Left  02=Right 03=Stop
        //   tt: 01=Up    02=Down  03=Stop
        public static byte[] PanTiltDrive(int address, Core.PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            byte pp, tt;
            ResolveDirection(dir, out pp, out tt);
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x01,
                (byte)(panSpeed & 0xFF),
                (byte)(tiltSpeed & 0xFF),
                pp, tt,
                ViscaConstants.Terminator
            };
        }

        public static byte[] PanTiltStop(int address)
        {
            // pan/tilt speed 는 stop 시에도 0이 아닌 값 권장 (사양). 0x01 사용.
            return new byte[]
            {
                ViscaHeader.Build(address),
                0x01, 0x06, 0x01,
                0x01, 0x01,
                0x03, 0x03,
                ViscaConstants.Terminator
            };
        }

        private static void ResolveDirection(Core.PanTiltDirection dir, out byte pp, out byte tt)
        {
            switch (dir)
            {
                case Core.PanTiltDirection.Up:        pp = 0x03; tt = 0x01; break;
                case Core.PanTiltDirection.Down:      pp = 0x03; tt = 0x02; break;
                case Core.PanTiltDirection.Left:      pp = 0x01; tt = 0x03; break;
                case Core.PanTiltDirection.Right:     pp = 0x02; tt = 0x03; break;
                case Core.PanTiltDirection.UpLeft:    pp = 0x01; tt = 0x01; break;
                case Core.PanTiltDirection.UpRight:   pp = 0x02; tt = 0x01; break;
                case Core.PanTiltDirection.DownLeft:  pp = 0x01; tt = 0x02; break;
                case Core.PanTiltDirection.DownRight: pp = 0x02; tt = 0x02; break;
                default:                              pp = 0x03; tt = 0x03; break;   // Stop
            }
        }

        // ----- Zoom -----
        // 8x 01 04 07 0p FF
        //  p = 0:Stop, 2:Tele(std), 3:Wide(std), 2p:Tele variable (p=0..7), 3p:Wide variable
        public static byte[] ZoomDrive(int address, Core.ZoomDirection dir, int speed)
        {
            byte p;
            switch (dir)
            {
                case Core.ZoomDirection.Tele:
                    p = (byte)((speed < 0) ? 0x02 : (0x20 | (speed & 0x07)));
                    break;
                case Core.ZoomDirection.Wide:
                    p = (byte)((speed < 0) ? 0x03 : (0x30 | (speed & 0x07)));
                    break;
                default:
                    p = 0x00; break;   // Stop
            }
            return new byte[] { ViscaHeader.Build(address), 0x01, 0x04, 0x07, p, ViscaConstants.Terminator };
        }

        public static byte[] ZoomStop(int address) =>
            new byte[] { ViscaHeader.Build(address), 0x01, 0x04, 0x07, 0x00, ViscaConstants.Terminator };

        // ----- Focus -----
        // 8x 01 04 08 0p FF  (same shape as Zoom)
        public static byte[] FocusDrive(int address, Core.FocusDirection dir, int speed)
        {
            byte p;
            switch (dir)
            {
                case Core.FocusDirection.Far:
                    p = (byte)((speed < 0) ? 0x02 : (0x20 | (speed & 0x07)));
                    break;
                case Core.FocusDirection.Near:
                    p = (byte)((speed < 0) ? 0x03 : (0x30 | (speed & 0x07)));
                    break;
                default:
                    p = 0x00; break;
            }
            return new byte[] { ViscaHeader.Build(address), 0x01, 0x04, 0x08, p, ViscaConstants.Terminator };
        }

        public static byte[] FocusStop(int address) =>
            new byte[] { ViscaHeader.Build(address), 0x01, 0x04, 0x08, 0x00, ViscaConstants.Terminator };

        // ----- Memory (Preset) -----
        // 8x 01 04 3F 02 0p FF  (Recall)
        // 8x 01 04 3F 01 0p FF  (Set)
        // 8x 01 04 3F 00 0p FF  (Reset)
        public static byte[] PresetRecall(int address, int presetNumber)
        {
            int p = presetNumber & 0x7F;
            return new byte[] { ViscaHeader.Build(address), 0x01, 0x04, 0x3F, 0x02, (byte)p, ViscaConstants.Terminator };
        }

        public static byte[] PresetSet(int address, int presetNumber)
        {
            int p = presetNumber & 0x7F;
            return new byte[] { ViscaHeader.Build(address), 0x01, 0x04, 0x3F, 0x01, (byte)p, ViscaConstants.Terminator };
        }

        // ----- OSD / Menu -----
        // ⚠️ 모델별로 다름. 아래는 Sony 표준 가이드 기준 1차 후보. 실기 검증 필수.
        // EVI-H100/SRG-300H 는 메뉴 표시 명령이 자체 확장(0x06 0x06)에 속할 수 있음.
        // 실측 시 매뉴얼의 "On Screen Display" 또는 "Menu" 표 참조하여 PdMutate.
        public static byte[] OsdOn(int address) =>
            new byte[] { ViscaHeader.Build(address), 0x01, 0x06, 0x06, 0x02, ViscaConstants.Terminator };

        public static byte[] OsdOff(int address) =>
            new byte[] { ViscaHeader.Build(address), 0x01, 0x06, 0x06, 0x03, ViscaConstants.Terminator };

        // SEL/BACK 는 모델 의존 — 일반적으로 Joystick 형태(상하좌우+SEL)로 매핑되거나
        // 별도 Menu Enter / Menu Back 명령이 존재. 구현 시 매뉴얼 확인.
        public static byte[] OsdSelect(int address) =>
            new byte[] { ViscaHeader.Build(address), 0x01, 0x7E, 0x01, 0x02, 0x00, 0x01, ViscaConstants.Terminator };  // placeholder

        public static byte[] OsdBack(int address) =>
            new byte[] { ViscaHeader.Build(address), 0x01, 0x7E, 0x01, 0x02, 0x00, 0x02, ViscaConstants.Terminator };  // placeholder
    }
}
```

> 🛑 **OSD 4종 (On/Off/Select/Back) 은 실제 매뉴얼 검증 후 확정**. 위 바이트는 Sony VISCA 일반 패턴 기반 placeholder. Do 단계 첫 작업으로 PDF 의 Menu 섹션 발췌→교정. 미지원 명령은 로그에 명시 후 NoOp 처리.

### 4.3 `Protocols/Visca/ViscaResponseParser.cs`

```csharp
namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// VISCA reply categorizer.
    /// Reply formats (after header z0):
    ///   ACK:        z0 4y FF       (y = socket 1..2)
    ///   Completion: z0 5y FF
    ///   Error:      z0 6y EE FF    (EE = error code)
    /// where z = receiving address (0x90 for source 0x81), y = socket number.
    /// </summary>
    internal static class ViscaResponseParser
    {
        public enum ReplyKind { Unknown, Ack, Completion, Error }

        public static ReplyKind Classify(byte[] reply, out byte errorCode)
        {
            errorCode = 0;
            if (reply == null || reply.Length < 3) return ReplyKind.Unknown;
            if (reply[reply.Length - 1] != 0xFF) return ReplyKind.Unknown;

            byte b1 = reply[1];
            if ((b1 & 0xF0) == 0x40) return ReplyKind.Ack;
            if ((b1 & 0xF0) == 0x50) return ReplyKind.Completion;
            if ((b1 & 0xF0) == 0x60)
            {
                if (reply.Length >= 4) errorCode = reply[2];
                return ReplyKind.Error;
            }
            return ReplyKind.Unknown;
        }

        public static string DescribeError(byte code)
        {
            switch (code)
            {
                case 0x01: return "Message length error";
                case 0x02: return "Syntax error";
                case 0x03: return "Command buffer full";
                case 0x04: return "Command cancelled";
                case 0x05: return "No socket";
                case 0x41: return "Command not executable";
                default:   return "Unknown error 0x" + code.ToString("X2");
            }
        }
    }
}
```

---

## 5. Transport 계층

### 5.0 IP transport 공통 추상화 — `IViscaIpTransport` (v1.1)

UDP 와 TCP transport 가 동일한 시그니처를 공유하도록 인터페이스 추출. `ViscaIpCameraService` 가 `Transport` enum 보고 어느 transport 를 인스턴스화할지 결정 가능.

```
┌─────────────────────────────┐
│ ViscaIpCameraService         │   (Transport enum 보고 dispatch)
│   _transport : IViscaIpTrans │
└──────────┬──────────────────┘
           │ uses
   ┌───────┴────────┐
   ▼                ▼
┌─────────────┐  ┌─────────────┐
│ UdpVisca    │  │ TcpVisca    │
│ Transport   │  │ Transport   │
│ (Sony 8B    │  │ (raw VISCA  │
│  header)    │  │  over TCP)  │
└─────────────┘  └─────────────┘
   둘 다 IViscaIpTransport 구현
```

```csharp
namespace Xeno.Framework.Camera.Transports
{
    /// <summary>UDP/TCP 공통 추상화. Service 가 transport 종류와 무관하게 동작.</summary>
    internal interface IViscaIpTransport : IDisposable
    {
        string Host { get; set; }
        int Port { get; set; }
        int ReceiveTimeoutMs { get; set; }
        bool WaitForReply { get; set; }       // false = silent firmware 용 fire-and-forget
        Action<string> OnLog { get; set; }    // 진단 로그 콜백 (Service 가 wrap 해서 [UDP]/[TCP] prefix 부여)
        bool IsOpen { get; }

        void Open();
        void Close();
        Task<byte[]> SendCommandAsync(byte[] viscaPayload);
    }
}
```

### 5.1 `Transports/SerialViscaTransport.cs`

```csharp
using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace Xeno.Framework.Camera.Transports
{
    internal sealed class SerialViscaTransport : IDisposable
    {
        private SerialPort _port;
        private readonly object _lock = new object();

        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = Parity.None;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public int ReadTimeoutMs { get; set; } = 500;
        public int WriteTimeoutMs { get; set; } = 500;

        public bool IsOpen { get { lock (_lock) return _port != null && _port.IsOpen; } }

        public void Open()
        {
            lock (_lock)
            {
                Close();
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
            lock (_lock)
            {
                try { _port?.Close(); } catch { }
                try { _port?.Dispose(); } catch { }
                _port = null;
            }
        }

        public Task<byte[]> SendAsync(byte[] payload)
        {
            return Task.Run(() => SendCore(payload));
        }

        private byte[] SendCore(byte[] payload)
        {
            SerialPort p;
            lock (_lock) { p = _port; }
            if (p == null || !p.IsOpen) throw new InvalidOperationException("port not open");

            try { p.DiscardInBuffer(); } catch { }
            p.Write(payload, 0, payload.Length);

            // Read until terminator FF or timeout
            var buf = new System.Collections.Generic.List<byte>(16);
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
            }
            return buf.ToArray();
        }

        public void Dispose() => Close();
    }
}
```

### 5.2 `Transports/UdpViscaTransport.cs` ⭐ **Sony VISCA over IP 헤더 + 백그라운드 RX 루프** (v1.1 재기술)

#### v1.0 → v1.1 변경 사유

v1.0 의 "send 마다 `ReceiveAsync` 후 `Task.WhenAny(receive, Task.Delay)`" 패턴이 실측 (2026-05-04, 사용자 KDI) 운영에서 race 발생:
- timeout 시 background `ReceiveAsync` Task 가 cancel 되지 않고 계속 살아남아 다음 명령의 응답을 가로챔
- 결과: Reset 응답 외 모든 명령이 timeout 처럼 보임

v1.1 패턴: **Open() 시 백그라운드 receive 루프 1회 시작** → 받은 모든 datagram 을 `BlockingCollection<byte[]>` 큐에 enqueue. `SendCommandAsync` 는 send 직전 stale drain → send → 큐에서 timeout 까지 dequeue. orphan task 없음, 1:1 응답 매칭 보장.

#### 핵심 시그니처

```csharp
internal sealed class UdpViscaTransport : IViscaIpTransport
{
    private UdpClient _client;
    private uint _sequence;
    private CancellationTokenSource _rxLoopCts;
    private Task _rxLoopTask;
    private BlockingCollection<byte[]> _rxQueue;
    private readonly object _lock = new object();

    public string Host { get; set; }
    public int Port { get; set; } = 52381;
    public int ReceiveTimeoutMs { get; set; } = 300;          // v1.1: 1000 → 300 (Sony 응답 ~50ms)
    public bool WaitForReply { get; set; } = true;            // v1.1 신설
    public Action<string> OnLog { get; set; }                 // v1.1 신설

    public bool IsOpen { get { lock (_lock) { return _client != null; } } }
}
```

#### Open() — 큐 + RX 루프 시작 + Reset 송신

```csharp
public void Open()
{
    lock (_lock)
    {
        CloseInternal();
        if (string.IsNullOrEmpty(Host)) throw new InvalidOperationException("Host not set");
        _client = new UdpClient();
        _client.Connect(IPAddress.Parse(Host), Port);
        _sequence = 1;
        _rxQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        _rxLoopCts = new CancellationTokenSource();

        Log("UDP open " + Host + ":" + Port);

        // RX 루프를 Reset 송신 전에 시작 → Reset 응답도 큐에 정상 enqueue 됨
        var token = _rxLoopCts.Token;
        var client = _client;
        _rxLoopTask = Task.Run(() => RxLoopAsync(client, token));

        SendControlReset();   // VISCA over IP Reset (PayloadType 0x0200 + 0x01, seq=0)
    }
}
```

#### SendCommandAsync — drain → send → 큐 take + late-Completion auto-skip ("방식 A")

```csharp
public async Task<byte[]> SendCommandAsync(byte[] viscaPayload)
{
    UdpClient c; BlockingCollection<byte[]> q; uint seq;
    lock (_lock) { c = _client; q = _rxQueue; seq = _sequence++; }
    if (c == null || q == null) throw new InvalidOperationException("UDP not open");

    // 1. 큐에 남은 stale (이전 명령의 늦은 Completion 등) 모두 drain — 분류 라벨로 로깅
    while (q.TryTake(out byte[] stale))
    {
        byte[] inner = stale.Length >= 8 ? Slice(stale, 8, stale.Length - 8) : stale;
        Log("DRAIN late " + ViscaResponseParser.DescribeReply(inner) + " : " + ToHex(stale));
    }

    // 2. 8B 헤더 + payload 패킷 빌드 후 send
    byte[] packet = BuildPacket(0x0100, seq, viscaPayload);
    Log("TX seq=" + seq + " type=0x0100 len=" + viscaPayload.Length + " : " + ToHex(packet));
    await c.SendAsync(packet, packet.Length).ConfigureAwait(false);

    if (!WaitForReply) return new byte[0];

    // 3. 큐에서 응답 dequeue, 단 Completion 이 먼저 오면 1회 skip 후 재대기 ("방식 A")
    byte[] reply = null;
    for (int attempt = 0; attempt < 2; attempt++)
    {
        try { using (var cts = new CancellationTokenSource(ReceiveTimeoutMs)) reply = q.Take(cts.Token); }
        catch (OperationCanceledException) { reply = null; break; }
        catch (ObjectDisposedException)    { reply = null; break; }
        catch (InvalidOperationException)  { reply = null; break; }

        byte[] inner = reply != null && reply.Length >= 8 ? Slice(reply, 8, reply.Length - 8) : reply;
        var kind = ViscaResponseParser.Classify(inner, out _);
        if (kind == ViscaResponseParser.ReplyKind.Completion && attempt == 0)
        {
            Log("RX late Completion (skipped) : " + ToHex(reply));
            continue;   // 한 번만 재시도
        }
        break;
    }

    if (reply == null || reply.Length == 0)
    {
        Log("RX timeout seq=" + seq + " (no reply within " + ReceiveTimeoutMs + "ms)");
        return new byte[0];
    }
    // 4. 8B 헤더 strip + 헤더 파싱 정보 로깅 + 시퀀스 mismatch 경고
    // (생략 — 헤더에서 rxSeq 추출, rxSeq != seq 이면 WARN 로그)
    return Slice(reply, 8, reply.Length - 8);
}
```

#### 백그라운드 RX 루프

```csharp
private async Task RxLoopAsync(UdpClient client, CancellationToken ct)
{
    Log("RX loop started");
    while (!ct.IsCancellationRequested)
    {
        try
        {
            var result = await client.ReceiveAsync().ConfigureAwait(false);
            if (ct.IsCancellationRequested) break;
            var q = _rxQueue;
            if (q == null || q.IsAddingCompleted) break;
            Log("RX raw=" + ToHex(result.Buffer));
            try { q.Add(result.Buffer, ct); }
            catch (OperationCanceledException) { break; }
            catch (InvalidOperationException) { break; }
        }
        catch (ObjectDisposedException) { break; }
        catch (SocketException) { /* backoff and retry */ await Task.Delay(20, ct); }
    }
    Log("RX loop exited");
}
```

> **요약**: `Open()` ↔ `RxLoopAsync` 1:1, 큐 1개. `SendCommandAsync` 는 send 측만 담당. orphan ReceiveAsync 가 원천 차단됨.

### 5.3 `Transports/TcpViscaTransport.cs` (v1.1 신설) — raw VISCA over TCP

#### 도입 동기

PTZOptics, Aida, BirdDog, FR 같은 third-party 카메라가 사용하는 raw VISCA-over-TCP 변형 지원. Sony 의 8B 헤더 없이 VISCA 바이트 (`81 ... FF`) 를 TCP stream 으로 직접 전송. FR-H50SN 같은 OEM firmware 가 UDP 응답은 미구현이지만 TCP 응답은 정상인 사례 다수 (실측 검증됨, 2026-05-04).

#### 핵심 시그니처

```csharp
internal sealed class TcpViscaTransport : IViscaIpTransport
{
    private TcpClient _client;
    private NetworkStream _stream;
    private CancellationTokenSource _rxLoopCts;
    private Task _rxLoopTask;
    private BlockingCollection<byte[]> _rxQueue;
    private readonly object _lock = new object();

    public string Host { get; set; }
    public int Port { get; set; } = 5678;             // PTZOptics 컨벤션 (vendor 별로 1259/52381 등 다양)
    public int ReceiveTimeoutMs { get; set; } = 300;
    public bool WaitForReply { get; set; } = true;
    public Action<string> OnLog { get; set; }
    public bool IsOpen { get { lock (_lock) { return _client != null && _client.Connected; } } }
}
```

#### Open() — 2초 connect timeout (잘못된 포트 입력 시 UI hang 차단)

```csharp
public void Open()
{
    lock (_lock)
    {
        CloseInternal();
        if (string.IsNullOrEmpty(Host)) throw new InvalidOperationException("Host not set");
        _client = new TcpClient();
        var ar = _client.BeginConnect(Host, Port, null, null);
        bool ok = ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));   // 2초 hard timeout
        if (!ok)
        {
            try { _client.Close(); } catch { }
            _client = null;
            throw new TimeoutException("TCP connect to " + Host + ":" + Port + " timed out");
        }
        _client.EndConnect(ar);
        _stream = _client.GetStream();
        _rxQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        _rxLoopCts = new CancellationTokenSource();
        Log("TCP open " + Host + ":" + Port);
        var token = _rxLoopCts.Token;
        _rxLoopTask = Task.Run(() => RxLoopAsync(_stream, token));
    }
}
```

#### RX 루프 — 0xFF terminator 단위로 메시지 분리

TCP 는 stream 이라 한 read 에 여러 VISCA 메시지가 섞여 올 수 있음. Accumulator 패턴으로 0xFF 마다 잘라 큐에 push.

```csharp
private async Task RxLoopAsync(NetworkStream stream, CancellationToken ct)
{
    Log("RX loop started");
    var accumulator = new List<byte>(16);
    var buf = new byte[256];
    while (!ct.IsCancellationRequested)
    {
        try
        {
            int n = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
            if (n <= 0) { Log("RX loop: stream closed by peer"); break; }
            for (int i = 0; i < n; i++)
            {
                accumulator.Add(buf[i]);
                if (buf[i] == 0xFF)
                {
                    var msg = accumulator.ToArray();
                    accumulator.Clear();
                    try { _rxQueue?.Add(msg, ct); }
                    catch (InvalidOperationException) { return; }
                    catch (OperationCanceledException) { return; }
                }
            }
        }
        catch (OperationCanceledException) { break; }
        catch (ObjectDisposedException) { break; }
        catch (System.IO.IOException) { break; }
    }
    Log("RX loop exited");
}
```

#### SendCommandAsync — UDP 와 동일 패턴

UDP 와 동일하게: drain → send → take with timeout → late-Completion auto-skip 1회. 차이점은 **헤더 strip 불필요** (raw VISCA 그대로). 코드는 §5.2 와 거의 동일하므로 생략.

> **요약**: `IViscaIpTransport` 인터페이스 덕분에 UDP/TCP 가 Service 입장에서 동등 — 행동만 다른 substitution.

---

## 6. Service 계층

### 6.1 `Services/EviH100CameraService.cs`

```csharp
using System;
using System.Threading.Tasks;
using Xeno.Framework.Camera.Core;
using Xeno.Framework.Camera.Protocols.Visca;
using Xeno.Framework.Camera.Transports;

namespace Xeno.Framework.Camera.Services
{
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
                _serial?.Dispose();
                _serial = new SerialViscaTransport
                {
                    PortName = ComPort,
                    BaudRate = BaudRate <= 0 ? Capabilities.DefaultBaudRate : BaudRate
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
                _serial?.Dispose();
                _serial = null;
                SetConnected(false, "disconnected");
            }
            finally { _gate.Release(); }
        }

        // ----- PTZ -----
        public override Task PanTiltDriveAsync(PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            int p = Clamp(panSpeed,  Capabilities.PanSpeedMin,  Capabilities.PanSpeedMax);
            int t = Clamp(tiltSpeed, Capabilities.TiltSpeedMin, Capabilities.TiltSpeedMax);
            return SendAsync(ViscaCodec.PanTiltDrive(Address, dir, p, t), "PT Drive " + dir);
        }
        public override Task PanTiltStopAsync() => SendAsync(ViscaCodec.PanTiltStop(Address), "PT Stop");

        public override Task ZoomDriveAsync(ZoomDirection dir, int speed)
        {
            int s = Clamp(speed, Capabilities.ZoomSpeedMin, Capabilities.ZoomSpeedMax);
            return SendAsync(ViscaCodec.ZoomDrive(Address, dir, s), "Zoom " + dir);
        }
        public override Task ZoomStopAsync() => SendAsync(ViscaCodec.ZoomStop(Address), "Zoom Stop");

        public override Task FocusDriveAsync(FocusDirection dir, int speed)
        {
            int s = Clamp(speed, Capabilities.FocusSpeedMin, Capabilities.FocusSpeedMax);
            return SendAsync(ViscaCodec.FocusDrive(Address, dir, s), "Focus " + dir);
        }
        public override Task FocusStopAsync() => SendAsync(ViscaCodec.FocusStop(Address), "Focus Stop");

        // ----- Presets -----
        public override Task RecallPresetAsync(int n) =>
            SendAsync(ViscaCodec.PresetRecall(Address, Clamp(n, 1, Capabilities.PresetCount)), "Preset Recall " + n);
        public override Task SetPresetAsync(int n) =>
            SendAsync(ViscaCodec.PresetSet(Address, Clamp(n, 1, Capabilities.PresetCount)), "Preset Set " + n);

        // ----- OSD -----
        public override Task OsdOnAsync()     => SendAsync(ViscaCodec.OsdOn(Address),     "OSD On");
        public override Task OsdOffAsync()    => SendAsync(ViscaCodec.OsdOff(Address),    "OSD Off");
        public override Task OsdSelectAsync() => SendAsync(ViscaCodec.OsdSelect(Address), "OSD Select");
        public override Task OsdBackAsync()   => SendAsync(ViscaCodec.OsdBack(Address),   "OSD Back");

        // ----- common send pipeline -----
        private async Task SendAsync(byte[] payload, string label)
        {
            EnsureNotDisposed();
            if (_serial == null || !_serial.IsOpen)
            {
                RaiseError("not connected");
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

        private static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return "(empty)";
            var sb = new System.Text.StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(data[i].ToString("X2")); }
            return sb.ToString();
        }

        public override void Dispose()
        {
            _serial?.Dispose();
            base.Dispose();
        }
    }
}
```

### 6.2 `Services/ViscaIpCameraService.cs` (v1.1 — 구 `SrgIpCameraService` 일반화)

#### v1.0 → v1.1 변경 사유

v1.0 의 `SrgIpCameraService` 는 SRG-300H 에 하드코딩된 단일 모델 서비스였음. v1.1 에서 FR-H50SN 등 비-Sony VISCA-IP 모델을 추가하면서, 단순히 모델별로 클래스를 복제하는 대신 **`(CameraInfo, CameraCapabilities)` 를 생성자 인자로 받는 generic 서비스**로 일반화. UDP/TCP transport 선택은 `Transport` enum 에 따라 Connect 시점에 dispatch.

#### 핵심 시그니처

```csharp
public sealed class ViscaIpCameraService : CameraServiceBase
{
    private IViscaIpTransport _transport;
    private string _activeKind;   // "UDP" or "TCP" — 로그 prefix 용

    /// <summary>호출자가 모델 정체성과 능력을 명시. Factory 가 모델별로 다른 (Info, Caps) 주입.</summary>
    public ViscaIpCameraService(CameraInfo info, CameraCapabilities caps) : base(info, caps) { }
}
```

#### ConnectAsync — Transport enum 보고 UDP/TCP dispatch

```csharp
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
        t.WaitForReply = Capabilities.ExpectReply;          // v1.1 — silent firmware 대응
        string tag = "[" + _activeKind + "] ";
        t.OnLog = msg => Log(LogDirection.Info, tag + msg);  // 로그에 [UDP]/[TCP] prefix
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
```

#### SendAsync — transport 종류 무관

```csharp
private async Task SendAsync(byte[] payload, string label)
{
    EnsureNotDisposed();
    if (_transport == null || !_transport.IsOpen) { RaiseError(label + " : not connected"); return; }
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
    catch (Exception ex) { RaiseError(label + " failed: " + ex.Message); }
    finally { _busy = false; _gate.Release(); }
}
```

PT/Zoom/Focus/Preset/OSD 메서드들은 `EviH100CameraService` 와 동일 패턴 (`SendAsync(ViscaCodec.X(Address, ...), label)`) — 생략.

---

## 7. UI 계층 — `CameraControl`

### 7.1 파일 / Designer 호환 규약

| 파일 | 역할 |
|------|------|
| `UI/CameraControl.cs` | 상태머신·이벤트 핸들러·동적 행 빌드 |
| `UI/CameraControl.Designer.cs` | 정적 배치(컨테이너 패널, OSD/PTZ/프리셋 영역). **루프 없음, 직선형 할당만** |
| `UI/CameraControl.resx` | 표준 |
| `UI/CameraUiColors.cs` | 색상 상수 (선택, 일반, 경고, SET, Disable) |

**디자이너 안전 규약** (Matrix `MatrixControl` 패턴 그대로):
- `InitializeComponent()` 안에는 컨트롤 생성·고정 위치/크기 설정만. 루프·동적 컨트롤 생성 금지.
- 카메라 행은 `_rowsHost` (Panel) 안에 런타임에 `BuildRows(int n)` 으로 생성.
- 프리셋 12 버튼은 디자이너에 12개 직선 배치(`_btnPreset1`..`_btnPreset12`) — 동적 생성 안 함.
- 모든 이벤트 와이어링은 `WireOwnEvents()` 메서드(Designer 외부)에서 처리.

### 7.2 공개 API

```csharp
namespace Xeno.Framework.Camera.UI
{
    public partial class CameraControl : UserControl
    {
        public CameraControl();   // 디자이너용. 기본 SlotCount=3.

        /// <summary>슬롯(카메라 행) 개수. 변경 시 동적 행 재생성. 1 이상.</summary>
        [DefaultValue(3)]
        public int SlotCount { get; set; }

        /// <summary>호스트가 모델→서비스를 만들 팩토리. AttachServices 와 둘 중 하나 사용.</summary>
        public void AttachFactory(ICameraServiceFactory factory);

        /// <summary>호스트가 직접 생성한 N개 서비스를 슬롯에 부착. 서비스 수명은 호스트 책임.</summary>
        public void AttachServices(IList<ICameraService> services);

        public void DetachAll();

        // 호스트가 마지막 입력값을 저장/복원하기 위한 직렬화 API
        public CameraSlotConfig[] GetSlotConfigs();           // SlotCount 길이
        public void ApplySlotConfigs(CameraSlotConfig[] configs);

        /// <summary>
        /// (v1.1) 모든 슬롯 service 의 LogEntry 를 forward. 호스트가 이걸 구독해
        /// 자체 로그 패널에 transport/protocol 트래픽을 표시. UI 스레드 자동 marshal.
        /// </summary>
        public event EventHandler<ServiceLogEventArgs> ServiceLog;
    }

    public sealed class CameraSlotConfig
    {
        public string Model { get; set; }
        public CameraTransportKind Transport { get; set; }
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public int Address { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public int HomePreset { get; set; }   // 0 = none
    }

    /// <summary>(v1.1) ServiceLog 이벤트 인자 — 슬롯 인덱스 + 원본 LogEntry.</summary>
    public sealed class ServiceLogEventArgs : EventArgs
    {
        public int SlotIndex { get; }
        public LogEntry Entry { get; }
        public ServiceLogEventArgs(int slotIndex, LogEntry entry) { SlotIndex = slotIndex; Entry = entry; }
    }
}
```

> **v1.1 — Round-1 M2 갭 해결**: round-1 분석에서 "MainForm 의 `HookExistingServices` 가 no-op 라 로그가 안 보임" 으로 지적된 M2 minor gap 은 본 `ServiceLog` 이벤트 채널을 통해 정식 해결. MainForm 은 `_cameraControl.ServiceLog += OnServiceLog` 로 구독하면 슬롯별 TX/RX/INFO/ERR 로그가 자동으로 textbox 에 출력됨.

### 7.3 내부 상태

```csharp
private ICameraServiceFactory _factory;
private readonly List<SlotState> _slots = new List<SlotState>();   // length == SlotCount
private int _activeIndex = -1;                                      // 현재 선택된 CAM (0-based), -1 = 없음
private bool _presetSetMode = false;                                // Q3 토글

private sealed class SlotState
{
    public CameraSlotConfig Config = new CameraSlotConfig();
    public ICameraService Service;          // null 또는 factory.Create(model)
    public bool ConnectFailed;              // 마지막 시도 실패 여부
    public string LastErrorMessage;
    // 행 컨트롤 참조
    public ComboBox CmbModel, CmbTransport, CmbBaud, CmbHomePreset;
    public TextBox TxtCom, TxtAddress, TxtHost, TxtPort;
    public Button BtnCam;
    public Label LblStatus;
}
```

### 7.4 행 레이아웃 (Q4 — 모든 필드 표시)

`_rowsHost` 는 `TableLayoutPanel` (또는 직접 Panel + Anchor 계산). 헤더 행 1개 + 카메라 행 N개.

| 컬럼 | 너비 | 컨트롤 |
|------|------|--------|
| Cam # | 60 | Label "Cam 1" |
| 모델 | 110 | ComboBox (CameraInfo.Model 목록) |
| 통신 | 80 | ComboBox **5항목** (v1.1): "RS-232" / "RS-422" / "RS-485" / "UDP" / "TCP" |
| COM | 70 | TextBox |
| Baud | 80 | ComboBox (Capabilities.SupportedBaudRates) |
| ID | 50 | TextBox (1..7) |
| IP | 130 | TextBox |
| Port | 70 | TextBox |
| Home | 70 | ComboBox ("(없음)", 1..PresetCount) |
| 상태 | 140 | Label (회색=대기/녹색=연결/주황=재시도/빨강=실패+사유) |

### 7.5 모델별 슬라이더 동기화 (Q1)

선택된 CAM 인덱스 `_activeIndex` 가 바뀌거나, 해당 슬롯의 모델 콤보가 바뀔 때 호출:

```csharp
private void SyncSpeedSlidersToActive()
{
    if (_activeIndex < 0) { DisableSpeedSliders(); return; }
    var caps = _slots[_activeIndex].Service?.Capabilities
            ?? _factory?.GetCapabilities(_slots[_activeIndex].Config.Model);
    if (caps == null) return;
    _numPanSpeed.Minimum  = caps.PanSpeedMin;  _numPanSpeed.Maximum  = caps.PanSpeedMax;
    _numTiltSpeed.Minimum = caps.TiltSpeedMin; _numTiltSpeed.Maximum = caps.TiltSpeedMax;
    _numZoomSpeed.Minimum = caps.ZoomSpeedMin; _numZoomSpeed.Maximum = caps.ZoomSpeedMax;
    // 현재값이 새 범위 밖이면 클램프
    _numPanSpeed.Value  = Clamp(_numPanSpeed.Value,  caps.PanSpeedMin,  caps.PanSpeedMax);
    _numTiltSpeed.Value = Clamp(_numTiltSpeed.Value, caps.TiltSpeedMin, caps.TiltSpeedMax);
    _numZoomSpeed.Value = Clamp(_numZoomSpeed.Value, caps.ZoomSpeedMin, caps.ZoomSpeedMax);
}
```

### 7.6 연결 상태머신 (Q2 + Q3)

```
[Idle]
  └ 사용자: "연결" 클릭
      ├ for each slot in _slots:
      │   if Config.Model 비어있음 → skip
      │   try
      │     slot.Service = _factory.Create(Config.Model)
      │     ApplyConfigToService(slot)
      │     await slot.Service.ConnectAsync()
      │     slot.ConnectFailed = false; slot.LastErrorMessage = null
      │     UI: 행 상태 = 녹색 "연결됨", BtnCam.Enabled = true
      │     if Config.HomePreset > 0:
      │        await slot.Service.RecallPresetAsync(Config.HomePreset)        ◄── Q3
      │   catch (Exception ex)
      │     slot.ConnectFailed = true; slot.LastErrorMessage = ex.Message
      │     UI: 행 상태 = 빨강 "실패: ...", BtnCam.Enabled = false              ◄── Q2
      └ 첫 번째 성공한 슬롯을 자동 선택 (옵션) 또는 사용자 클릭 대기

[Connected]
  └ 사용자: CAM N 클릭
      ├ if _slots[N].ConnectFailed: 무시 (BtnCam 이미 Disable)
      └ else: _activeIndex = N; UI 강조; SyncSpeedSlidersToActive()

  └ 사용자: PTZ 버튼 MouseDown
      ├ if _activeIndex < 0: 경고 로그
      └ else: await _slots[_activeIndex].Service.PanTiltDriveAsync(...)

  └ 사용자: PTZ 버튼 MouseUp/MouseLeave
      └ await _slots[_activeIndex].Service.PanTiltStopAsync()

  └ 사용자: PRESET SET 클릭
      └ _presetSetMode = !_presetSetMode; PaintPresetButtons()

  └ 사용자: Preset N 클릭
      ├ if _presetSetMode: await Service.SetPresetAsync(N); _presetSetMode = false
      └ else:              await Service.RecallPresetAsync(N)

  └ 사용자: OSD ON/OFF/SEL/BACK 클릭
      └ await Service.OsdOn/Off/Select/BackAsync()

[Disconnect]
  └ 사용자: "연결 종료" 클릭
      └ for each slot: await Service?.DisconnectAsync(); Dispose; UI 회색
```

### 7.7 통신 방식별 필드 활성화 규칙 (Q4)

> v1.1: **TCP** 도 IP 그룹에 포함 — Host/Port 활성, COM/Baud/Address 비활성. UDP 와 동일 규칙.

```csharp
private void ApplyTransportFieldRules(SlotState slot)
{
    bool serial = slot.Config.Transport == CameraTransportKind.Rs232
               || slot.Config.Transport == CameraTransportKind.Rs422
               || slot.Config.Transport == CameraTransportKind.Rs485;
    bool ip = slot.Config.Transport == CameraTransportKind.UdpVisca
           || slot.Config.Transport == CameraTransportKind.TcpVisca;     // v1.1 TCP 포함

    slot.TxtCom.Enabled     = serial;
    slot.CmbBaud.Enabled    = serial;
    slot.TxtAddress.Enabled = serial;     // VISCA over IP 는 헤더 자동 처리
    slot.TxtHost.Enabled    = ip;
    slot.TxtPort.Enabled    = ip;

    // 비활성 필드 시각: 기본 Disable 색상 사용. ToolTip 으로 사유 안내.
    SetTooltip(slot.TxtCom,     serial ? "" : "선택된 통신 방식에서는 사용하지 않습니다");
    SetTooltip(slot.CmbBaud,    serial ? "" : "선택된 통신 방식에서는 사용하지 않습니다");
    SetTooltip(slot.TxtAddress, serial ? "" : "VISCA over IP 는 주소를 자동 처리합니다");
    SetTooltip(slot.TxtHost,    ip     ? "" : "선택된 통신 방식에서는 사용하지 않습니다");
    SetTooltip(slot.TxtPort,    ip     ? "" : "선택된 통신 방식에서는 사용하지 않습니다");
}
```

### 7.8 동적 N 행 생성 알고리즘 (Q5)

```csharp
private void RebuildRows()
{
    _rowsHost.SuspendLayout();
    try
    {
        _rowsHost.Controls.Clear();
        _slots.Clear();

        // 헤더
        var header = BuildHeaderRow();
        _rowsHost.Controls.Add(header);
        header.Top = 0; header.Left = 0;

        for (int i = 0; i < SlotCount; i++)
        {
            var slot = new SlotState();
            var rowPanel = BuildSlotRow(i, slot);                 // 모든 컨트롤 생성·이벤트 와이어
            rowPanel.Top = (i + 1) * RowHeight;
            rowPanel.Left = 0;
            _rowsHost.Controls.Add(rowPanel);
            _slots.Add(slot);
            ApplyTransportFieldRules(slot);
        }
        // CAM N 컨트롤 행은 별도 패널에서 N개 동적 생성 (BtnCam 참조 보관)
        RebuildCamButtons();
    }
    finally { _rowsHost.ResumeLayout(true); }
}
```

### 7.9 Hold-to-Move 이벤트 와이어 (v1.1 — Hold-Flag 가드 추가)

#### v1.0 → v1.1 변경 사유

v1.0 의 단순 MouseLeave 핸들러가 **마우스 호버만으로도 Stop 명령을 송신**하는 버그 발견 (실측 2026-05-04). 사용자가 버튼 위로 마우스 통과만 해도 `Stop` 이 큐에 쌓여 SemaphoreSlim 이 1초씩 막히고, 진짜 Drive 가 한참 후에야 송신되어 "버튼 누르고 한참 뒤 움직임" 증상.

#### 해결 — 축별 hold-active flag

PTZ / Zoom / Focus 각 축마다 독립 flag. **MouseDown 에서 Drive 가 실제 송신될 때만 true 로 set**, MouseUp/Leave 는 **flag 가 true 일 때만 Stop 송신** 후 false 로 reset.

```csharp
private bool _ptHoldActive;       // v1.1
private bool _zoomHoldActive;     // v1.1
private bool _focusHoldActive;    // v1.1

private void WirePanTilt(Button btn, PanTiltDirection dir)
{
    btn.MouseDown  += async (s, e) => { if (e.Button == MouseButtons.Left) await SafePtDownAsync(dir); };
    btn.MouseUp    += async (s, e) => { if (e.Button == MouseButtons.Left) await SafePtUpAsync(); };
    btn.MouseLeave += async (s, e) => { if (Control.MouseButtons == MouseButtons.None) await SafePtUpAsync(); };
}

private async Task SafePtDownAsync(PanTiltDirection dir)
{
    var svc = ActiveService();
    if (svc == null) return;
    _ptHoldActive = true;       // v1.1 — Drive 가 실제 송신될 때만 set
    int p = (int)_numPanSpeed.Value, t = (int)_numTiltSpeed.Value;
    try { await svc.PanTiltDriveAsync(dir, p, t).ConfigureAwait(true); } catch { }
}

private async Task SafePtUpAsync()
{
    if (!_ptHoldActive) return;  // v1.1 — Drive 없었으면 Stop 도 무시 (호버성 spurious 차단)
    _ptHoldActive = false;
    var svc = ActiveService();
    if (svc == null) return;
    try { await svc.PanTiltStopAsync().ConfigureAwait(true); } catch { }
}

// Zoom / Focus 도 _zoomHoldActive / _focusHoldActive 로 동일 패턴
```

### 7.10 색상 상수 — `UI/CameraUiColors.cs`

```csharp
namespace Xeno.Framework.Camera.UI
{
    internal static class CameraUiColors
    {
        public static readonly Color CamSelected   = Color.FromArgb(102, 178, 255);  // Matrix 와 통일
        public static readonly Color CamUnselected = SystemColors.Control;
        public static readonly Color CamDisabled   = Color.FromArgb(220, 220, 220);
        public static readonly Color StatusOk      = Color.FromArgb(144, 238, 144);  // light green
        public static readonly Color StatusWarn    = Color.FromArgb(255, 179, 71);
        public static readonly Color StatusError   = Color.FromArgb(255, 99, 99);
        public static readonly Color PresetSet     = Color.FromArgb(255, 215, 0);    // gold
    }
}
```

---

## 8. 테스트 하네스 — `CameraController/`

### 8.1 파일

```
CameraController/
  Program.cs
  UI/MainForm.cs (.Designer.cs, .resx)
  UserSettings.cs
  Properties/AssemblyInfo.cs
  CameraController.csproj
  App.config
  app.manifest
```

### 8.2 `MainForm` 구성 (v1.1 — 로그 toolbar + ServiceLog 구독)

- ClientSize **900 × 580** (v1.1: 로그 toolbar 공간 확보)
- `CameraControl _cameraControl;` (Y=28, 880×400)
- 메뉴: 슬롯 수 변경 (3/4/5/6), 로그 지우기
- **로그 영역** (Y=432~):
  - 라벨 "Log"
  - **로그 toolbar 4버튼** (v1.1, X=615~888): 복사 / 지우기 / 저장 / 일시정지 (각 60×22, 일시정지만 78×22)
  - ToolTip: 각 버튼에 한국어 설명
  - 일시정지 시 gold(255,215,0) 배경 + 텍스트 "재  개" 토글
  - 저장: `SaveFileDialog` — 기본 파일명 `camera-YYYYMMDD-HHmmss.log`, filter `*.log`
  - `_txtLog` (Y=455, 880×110): `Multiline=true`, `ReadOnly=true`, `WordWrap=false` (hex 가독성), `ScrollBars=Vertical`, Consolas 9pt, `MaxLogLines = 2000`
- `Load`:
  - `UserSettings.Load()` → `_cameraControl.AttachFactory(new CameraServiceFactory())` → `_cameraControl.ApplySlotConfigs(savedConfigs)`
  - **`_cameraControl.ServiceLog += OnServiceLog`** (v1.1) — 슬롯별 LogEntry 를 `[Cam N] TX/RX/INFO/ERR <text>` 형식으로 `_txtLog` 에 출력
- `FormClosing`: `_cameraControl.GetSlotConfigs()` → `UserSettings.Save(...)` → `_cameraControl.ServiceLog -= OnServiceLog` → `_cameraControl.DetachAll()`

### 8.3 `UserSettings.cs`

`PN8080Controller/UserSettings.cs` 패턴 그대로. `CameraSlotConfig[]` 직렬화 (XML or INI). 키 prefix: `Camera.Slot{i}.{Field}`.

### 8.4 `CameraController.csproj` (요약)

`PN8080Controller.csproj` 와 동일 구조. ProjectReference 만 `Xeno.Framework.Camera.csproj` 로.

```xml
<ItemGroup>
  <ProjectReference Include="..\Xeno.Framework.Camera\Xeno.Framework.Camera.csproj">
    <Project>{A2C3D4E5-F6B7-48C9-9A1D-2E3F4B5C6A7B}</Project>
    <Name>Xeno.Framework.Camera</Name>
  </ProjectReference>
</ItemGroup>
```

---

## 9. 솔루션 통합 / 기존 코드 처리

### 9.1 `Xeno.Framework.Camera.csproj` 변경

| 항목 | 현재 | 변경 후 |
|------|------|---------|
| `OutputType` | `WinExe` | `Library` |
| 포함 파일 | Form1.cs, Form1.Designer.cs, Program.cs | **제거** (목업은 Plan/Design 본문에 흡수, 구현 후 git 에서 삭제) |
| 신규 추가 | Core/, Protocols/Visca/, Transports/, Services/, UI/ | 위 §3~§7 |
| References | (현재 그대로) | + `System.IO.Ports` |

### 9.2 `PN-8080 Controller.sln` 변경

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Xeno.Framework.Camera", "Xeno.Framework.Camera\Xeno.Framework.Camera.csproj", "{A2C3D4E5-F6B7-48C9-9A1D-2E3F4B5C6A7B}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "CameraController", "CameraController\CameraController.csproj", "{B3D4E5F6-A7B8-49CA-AB2E-3F4A5B6C7D8E}"
EndProject
```
+ `GlobalSection(ProjectConfigurationPlatforms)` 4줄/프로젝트.

### 9.3 기존 `Xeno.Framework.Matrix` 영향

**없음** — 신규 라이브러리는 독립. `LogEntry`/`LogDirection` 시그니처 동일하게 두어 v2 통합 시 부담 최소화.

---

## 10. VS2022 Designer 호환 — 체크리스트

| 항목 | 규약 |
|------|------|
| `InitializeComponent()` | 컨트롤 생성·고정 위치/크기/Text 만. **루프 금지, 동적 컨트롤 생성 금지** |
| 동적 행 생성 | `RebuildRows()` 등 별도 메서드, 런타임에서만 호출 |
| 이벤트 와이어링 | `WireOwnEvents()` (Designer 외부) |
| `ISupportInitialize` | `NumericUpDown` Begin/End Init 래핑 (Matrix 와 동일) |
| `partial class` 분리 | 핸들러는 `.cs`, Designer 코드는 `.Designer.cs` 만 |
| Designer 가 시드할 슬롯 수 | `SlotCount = 3` 기본. 디자이너에서 `SlotCount` 변경 시 OnHandleCreated 후 `RebuildRows()` 호출 |
| 모든 컨트롤 `_camelCase` 또는 표준 명명 | Matrix 컨트롤과 일관성 |

---

## 11. 테스트 전략

### 11.1 빌드 검증

- Debug/Release 양쪽 모두 0 경고 (Matrix 동일 기준)
- 솔루션 한 번 빌드로 3개 프로젝트(Matrix, Camera, PN8080Controller, CameraController) 모두 컴파일

### 11.2 코덱 단위 검증 (정적)

- `ViscaCodec.PanTiltDrive(1, Up, 0x10, 0x10)` → `81 01 06 01 10 10 03 01 FF`
- `ViscaCodec.ZoomDrive(1, Tele, 4)` → `81 01 04 07 24 FF`
- `ViscaCodec.PresetRecall(1, 5)` → `81 01 04 3F 02 05 FF`
- `UdpViscaTransport` 패킷 헤더: `01 00 00 09 00 00 00 01 81 01 04 07 24 FF`

### 11.3 실기 검증 (선택, KDI 환경)

- EVI-H100 + USB-RS232 변환기: 단일 명령(Pan Right, Zoom Tele) 응답 ACK/Completion 확인
- SRG-300H + LAN: 동일

### 11.4 실패 시나리오

- 잘못된 IP/COM → 연결 단계에서 RaiseError, UI 빨강 + CAM Disable
- 미연결 상태 PTZ 클릭 → 무시 (이벤트 무시)
- 디자이너 reload 시 Designer.cs 깨짐 없음

---

## 12. 구현 순서 (Do 단계 권장)

1. **Step 1 — Core 골격** (1h)
   - Core/ 의 enum/Info/Capabilities/LogEntry/Factory 생성, csproj 에 등록
   - 빌드 통과 확인
2. **Step 2 — VISCA 코덱** (1h)
   - Protocols/Visca/ 작성. `ViscaCodec` 의 모든 빌더 + Parser. **OSD 4종은 PDF 검증 후 확정** (placeholder 로 시작)
3. **Step 3 — Transport 2종** (2h)
   - SerialViscaTransport, UdpViscaTransport. 단위 콘솔 테스트(임시)
4. **Step 4 — Service 2종** (1h)
   - EviH100, SrgIp 작성. csproj 에 OutputType Library 전환, Form1/Program.cs 제거
5. **Step 5 — `CameraControl` Designer + 정적 영역** (2h)
   - Designer 에 OSD/PTZ/프리셋 12/속도 NumericUpDown 직선 배치. _rowsHost Panel 만 디자이너에 두고 비워둠
6. **Step 6 — 동적 행 생성 + 상태머신** (3h)
   - RebuildRows, SlotState, 연결/CAM 선택/Hold-to-Move/프리셋 SET 토글/OSD/Home Recall
7. **Step 7 — `CameraController/` 테스트 하네스** (1h)
   - 솔루션 등록, MainForm, UserSettings
8. **Step 8 — 빌드 0 경고 확인 + 디자이너 라운드트립 검증** (0.5h)

총 예상 ~11.5h (Plan 의 16+4 파일 추정과 일치).

---

## 13. 위험 재평가 (Plan §10 대비)

| # | Plan 위험 | Design 단계 보완 |
|---|----------|------------------|
| R1 | 시리얼 실기 미보유 | §11.2 정적 코덱 검증으로 1차 안전망. 실기 검증은 KDI 환경에서 |
| R2 | OSD 명령 불명확 | §4.2 placeholder + Do 단계 첫 작업으로 PDF 발췌 → ViscaCodec 교정. 미지원 모델은 OSD 버튼 비활성화 처리 |
| R3 | UDP 시퀀스 RESET | §5.2 `SendControlReset()` 가 Open 시 자동 송신 |
| R4 | Designer 깨짐 | §10 체크리스트로 명시 — InitializeComponent 직선형, RebuildRows 분리 |
| R5 | SerialPort 닫힘 hang | §5.1 Discard*Buffer + 별도 lock |
| R6 | Form1 Home Preset 의미 | Q3 결정 반영, §7.2/§7.6 에 명시 |

---

## 14. Open Items

### v1.0 위임 항목 (변동 없음)

| # | 항목 | 상태 (2026-05-06) |
|---|------|------------------|
| O1 | EVI-H100 / SRG-300H **OSD 4종 정확한 바이트** | 미해결 — `ViscaCodec.OsdSelect/OsdBack` 은 `8x 01 06 06 05/04 FF` placeholder 유지. PDF 발췌 후 교정 필요 |
| O2 | EVI-H100 **PresetCount** (6 vs 16) | 미해결 — `CameraCapabilities.cs` 6 채택 유지 |
| O3 | EVI-H100 **TiltSpeedMax** (0x14 vs 0x18) | 미해결 — 0x14 채택 유지 |
| O4 | RS-485 사용처 (현재 H100 은 232/422 만) | 유효 — UI 콤보에 표시. v1.1 에서 TCP 까지 5항목으로 확장되며 다중 transport 패턴 정착 |

### v1.1 신규 항목

| # | 항목 | 비고 |
|---|------|------|
| O5 | **VISCA-IP 모델별 default 포트는 vendor convention** — 카메라 web UI 에서 실제 포트 확인 후 UI 의 Port TextBox 에 입력 필수. 코드의 `CameraCapabilities.DefaultIpPort` 는 placeholder (Sony=52381, FR/PTZOptics=5678 등) | 실측 (2026-05-04): FR-H50SN 의 실 unit 은 TCP 52381 사용. capability default 와 다름. UI 가 사용자 입력 우선이므로 코드 수정 불필요 |
| O6 | **SRG-300H 실기 검증** — 원격지 카메라 접근 가능 시점 1회 | Plan §10 R1 잔여분 |
| M1 | **`CameraControl` 생성자 시드 안전성** — `RebuildRows()`/`RebuildCamButtons()` 가 ctor 에서 직접 호출됨. `if (!DesignMode)` 가드 권장 (현재 디자이너 라운드트립 무문제 보고됨) | 우선순위 낮음 |

위 항목 모두 **실측 매뉴얼 발췌 또는 일회성 micro-task** 이며, 본 v1.1 설계의 클래스 구조에는 영향 없음.

---

**문서 상태**: Approved (v1.1, 2026-05-06)

### v1.1 Round-2 흡수 변경 요약

본 v1.1 갱신은 v1.0 Design 승인 후 Do/Check 단계에서 도출된 9건의 implementation-ahead 변경을 단방향 동기화한 결과:

1. **§5.0 (신규)** — `IViscaIpTransport` 공통 추상화
2. **§5.2 (재기술)** — UDP 백그라운드 RX 루프 + `BlockingCollection` 큐 + `WaitForReply` + late-Completion auto-skip ("방식 A") + 분류 drain 로깅. 1000ms→300ms timeout
3. **§5.3 (신규)** — TCP transport (raw VISCA, 0xFF terminator slicing, 2초 connect timeout, 기본 5678)
4. **§6.2 (재기술)** — `SrgIpCameraService` → `ViscaIpCameraService` (generic, `(Info, Caps)` ctor, UDP/TCP dispatch)
5. **§3.4 (확장)** — `ExpectReply` 플래그 + `FrH50Sn()` 정적 팩토리
6. **§3.7 (확장)** — FR-H50SN 모델 등록, SRG-300H/FR-H50SN 모두 `ViscaIpCameraService` 로 생성
7. **§7.4·§7.7 (갱신)** — 통신 콤보 5항목 (TCP 추가)
8. **§7.2 (확장)** — `ServiceLog` 이벤트 + `ServiceLogEventArgs` (Round-1 M2 갭 해결)
9. **§7.9 (재기술)** — Hold-flag 가드로 hover-induced spurious Stop 차단
10. **§8.2 (재기술)** — MainForm 로그 toolbar (복사/지우기/저장/일시정지) + ServiceLog 구독 + ClientSize 900×580

전체 변경의 동기·증거·매칭 분석은 `docs/03-analysis/camera-remote-control.analysis.md` (round-2) 참조.
