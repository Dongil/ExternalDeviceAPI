# Plan — camera-remote-control (PTZ 카메라 통합 제어 프레임워크)

> **요약**: 다양한 브랜드/프로토콜의 PTZ 카메라(Sony EVI-H100 RS-232/422, Sony SRG-300H VISCA over IP)를 단일 추상화로 제어하는 재사용 가능한 `Xeno.Framework.Camera` 클래스 라이브러리와 `CameraControl` UserControl을 구축. 신규 모델/업체 추가가 용이한 확장 구조로 외부 기기 통합 제어 API의 기반을 마련한다.
>
> **작성자**: 개발팀 (KDI)
> **작성일**: 2026-05-04
> **상태**: Approved (Open Questions 5건 해결 — 2026-05-04, KDI)
> **참조 패턴**: [matrix-controller.report.md](../../04-report/matrix-controller.report.md) (`Xeno.Framework.Matrix` 동일 아키텍처)

---

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | camera-remote-control |
| **대상 솔루션** | `PN-8080 Controller.sln` (신규 프로젝트 2개 추가) |
| **신규 프로젝트** | `Xeno.Framework.Camera` (Library), `CameraController` (테스트 하네스 WinExe) |
| **신규 파일(예상)** | 라이브러리 ~16개 / 테스트 하네스 ~4개 |
| **지원 모델 (v1)** | Sony EVI-H100 (RS-232/RS-422 VISCA), Sony SRG-300H (VISCA over IP UDP 52381) |
| **참조 UI** | `Xeno.Framework.Camera/Form1.cs` (목업) |
| **재사용 패턴** | `Xeno.Framework.Matrix` 의 IService 추상화 + UserControl + AttachService(...) 구조 |

### 결과 가치 — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | 사내 취급 PTZ 카메라가 브랜드(Sony, Canon …)·프로토콜(VISCA Serial, VISCA over IP, Pelco-D, TCP …)이 다양하고, 신제품·신규 업체가 계속 추가됨. 모델별 산발적 구현으로 인해 통합 운영 솔루션에서 재사용·관리·확장이 어렵다. 메트릭스 라이브러리 외에는 외부 기기 제어용 공통 추상화가 없다. |
| **Solution** | `ICameraService` 추상화 + `CameraServiceBase` 공통 베이스 + 모델별 구현체(EviH100, SrgIp …) 의 3계층 구조. VISCA 코덱(`ViscaCodec`)과 Transport(`SerialViscaTransport`, `UdpViscaTransport`) 를 분리해 동일 명령 사전을 시리얼/IP 양쪽에서 공유. `CameraControl` UserControl을 csproj 참조 → 드래그 → `AttachServices(...)` 호출의 3단계로 다른 WinForms 프로젝트에서 즉시 재사용. |
| **Function UX Effect** | 운영자가 한 화면에서 카메라 1~3을 동시 등록·연결하고, CAM 버튼으로 명령 대상 카메라를 선택, PTZ/Zoom/Focus 는 누르고 있는 동안 연속 이동(Hold-to-Move), OSD/Preset 은 단발 명령으로 즉시 동작. 프리셋 SET 모드를 토글해 12개 위치를 빠르게 저장. 모델·통신 방식·속도가 모델별 기본값으로 자동 적용되어 운영자는 IP/COM/ID 만 입력. |
| **Core Value** | "외부 기기 통합 제어 API"의 두 번째 사례(첫 사례: Matrix)로서, 향후 메트릭스 다른 브랜드·다른 PTZ 모델·신규 카테고리(스위처, 인코더 등) 추가 시 동일한 패턴(`I*Service` + Codec/Transport 분리)을 적용할 수 있는 사내 표준 정립. 신규 모델 추가 시 비용은 "1개 Service 클래스 + 모델별 기본값" 수준으로 최소화. |

---

## 1. 배경 & 동기

### 1.1 현황

- 사내 취급 영상장비 중 PTZ 카메라는 다양한 브랜드(Sony, Canon, Panasonic …)와 다양한 제어 프로토콜(VISCA, Pelco-D, ONVIF, 자체 규격)을 가지고 있다.
- 신제품·신규 업체 카메라가 지속적으로 추가되며, 운영 현장에서는 단일 컨트롤 UI로 통합 조작하길 원한다.
- 현재 PN-8080/Videohub 메트릭스는 `Xeno.Framework.Matrix` 라이브러리로 추상화되어 있으나, 카메라 제어용 공통 라이브러리가 없다.
- 메트릭스 라이브러리도 향후 다른 브랜드 추가 시 리팩토링 예정. 카메라 라이브러리는 그 미래 패턴을 미리 적용하는 두 번째 사례로 설계한다.

### 1.2 목표 가설

- 카메라 제어 도메인의 공통점(연결/해제, 명령 대상 선택, PTZ 이동, 줌/포커스, 프리셋, OSD 메뉴, 응답 로깅)을 추상화하면, 모델별 구현은 "프로토콜 코덱 + Transport 조합 + 모델 기본값"으로 축약된다.
- VISCA 명령 사전은 시리얼/IP 모두 동일한 바이트 시퀀스를 사용하며, 차이는 Transport 계층(주소 바이트 vs 시퀀스 번호 헤더)에서만 발생한다 → 코덱·Transport 분리 시 EVI-H100 과 SRG-300H 가 동일 명령 코드를 공유한다.

### 1.3 비목표 (Non-Goals, v1)

- Pelco-D, TCP-VISCA, ONVIF, RTSP 영상 수신은 v1 범위 외 (구조만 확장 가능하게 두고 구현은 보류).
- 메트릭스 기존 코드 리팩토링 (별도 후속 작업).
- 카메라 영상 미리보기(프리뷰) 기능 (제어 전용).
- 다중 사용자/원격 제어 서버 모드.

---

## 2. 목표 (Goals)

| 우선순위 | 목표 | 측정 기준 |
|---------|------|----------|
| Must | 한 UserControl로 카메라 1~3대 동시 등록·선택·제어 가능 | Form1 목업의 모든 컨트롤이 동작 |
| Must | EVI-H100 RS-232/RS-422 VISCA 제어 동작 | PTZ/Zoom/Focus/Preset/OSD 명령 송신 및 ACK 수신 |
| Must | SRG-300H VISCA over IP (UDP 52381) 제어 동작 | 동일 |
| Must | PTZ/Zoom/Focus 의 Hold-to-Move (누르는 동안 이동, 떼면 정지) | MouseDown/Up 이벤트로 Drive/Stop VISCA 명령 송신 |
| Must | 프리셋 SET 모드 토글 워크플로우 | SET 클릭 → Preset N 클릭 → 저장 → 자동 모드 종료 |
| Must | 다른 WinForms 프로젝트에서 csproj 참조 + UserControl 드래그 + AttachServices 호출의 3단계로 재사용 | `CameraController/` 테스트 하네스로 검증 |
| Should | 신규 카메라 모델 추가가 1개 클래스 작성 수준으로 가능 | 새 `XxxCameraService : CameraServiceBase` 작성만으로 UI 등록 |
| Should | 모델별 기본 통신 파라미터(보드레이트, 기본 IP 포트 등) 자동 적용 | 모델 선택 시 ComboBox/TextBox 기본값 채움 |
| Could | 향후 메트릭스와 통합 가능한 `IDeviceService` 공통 베이스 추출 | 인터페이스 시그니처 호환성 검토 |

---

## 3. 사용자 시나리오

### 3.1 Persona: AV 시스템 운영자

방송실/회의실 PC 앞에서 PTZ 카메라 1~3대를 단일 윈폼 유틸리티로 제어. 모델·통신 방식·IP/COM/ID 를 등록하고 라이브 운영 중 카메라를 전환·이동시킨다.

### 3.2 핵심 시나리오

**S1. 초기 설정 & 연결**
1. 운영자가 카메라 1~3 행에 모델, 통신 방식, ID(또는 COM #), Port(또는 Baud), IP 입력
2. **연결** 버튼 클릭 → 활성화된(모델·필수 필드 입력된) 모든 카메라 동시 연결
3. 연결 성공한 카메라 표시(라벨 색상/상태 LED)

**S2. 카메라 선택 & PTZ 제어**
1. **CAM1** 클릭 → 선택 표시(버튼 강조)
2. **▲** 버튼을 누른 채 유지 → CAM1 으로 Pan-Tilt Drive UP (속도=속도 슬라이더 값) 송신
3. 버튼을 떼면 → CAM1 으로 Pan-Tilt Stop 송신
4. **CAM2** 클릭 → 이후 명령 대상이 CAM2 로 전환

**S3. 줌/포커스 (Hold-to-Move)**
1. **Zoom +** 누름 → Zoom Tele 시작
2. 떼면 → Zoom Stop
3. (Focus 도 동일)

**S4. OSD 메뉴 조작**
1. **OSD ON** 클릭 → 메뉴 표시 명령 1회 전송
2. **OSD SEL/BACK** 으로 메뉴 항목 이동·확정
3. **OSD OFF** 로 메뉴 닫기

**S5. 프리셋 저장 & 호출**
1. 일반 상태에서 **Preset 5** 클릭 → 5번 위치로 이동
2. **PRESET SET** 클릭 → 저장 모드 진입(버튼 색 변경)
3. **Preset 5** 클릭 → 현재 위치를 5번에 저장 → 자동 모드 해제

**S6. 연결 종료**
1. **연결 종료** 클릭 → 모든 카메라 연결 해제, 리소스 정리

---

## 4. 기능 요구사항 (Functional Requirements)

### 4.1 라이브러리 (`Xeno.Framework.Camera`)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-01 | `ICameraService` 인터페이스 제공 (Connect/Disconnect, PanTiltDrive, PanTiltStop, ZoomDrive, ZoomStop, FocusDrive, FocusStop, GoPreset, SetPreset, OsdOn/Off/Select/Back, Events) | Matrix `IMatrixService` 와 동일한 패턴 |
| FR-02 | `CameraServiceBase` 추상 클래스: 공통 속성·이벤트·상태 캐시 | `MatrixServiceBase` 동등 |
| FR-03 | `EviH100CameraService` 구현 (RS-232/RS-422 VISCA, 9600 8N1 기본) | `SerialViscaTransport` + `ViscaCodec` 조합 |
| FR-04 | `SrgIpCameraService` 구현 (VISCA over IP UDP 52381) | `UdpViscaTransport` + `ViscaCodec` 조합 |
| FR-05 | `ViscaCodec`: PT Drive/Stop, Zoom Tele/Wide/Stop, Focus Far/Near/Stop, Memory Set/Recall, Menu On/Off/Sel/Back 바이트 빌더 | EVI-H100/SRG-300H 동일 사전 |
| FR-06 | `SerialViscaTransport`: `System.IO.Ports.SerialPort` 기반 송수신, ACK/Completion/Error 파싱 | RS-232/RS-422/RS-485 공통 |
| FR-07 | `UdpViscaTransport`: Sony VISCA over IP 시퀀스 헤더(8바이트) 부착, UDP 52381 송수신, 시퀀스 번호 관리 | UdpClient 사용 |
| FR-08 | 연결 상태/로그/디바이스 정보 변경 이벤트 발행 | Matrix 동일 |
| FR-09 | `CameraServiceFactory.Create(model, transport, options)` 모델→서비스 매핑 | UI 가 모델 선택 시 호출 |
| FR-10 | **`CameraCapabilities`** (per-model): Pan 속도 범위, Tilt 속도 범위, Zoom 속도 범위, 지원 보드레이트 목록, 기본 통신 방식, 프리셋 개수, OSD 지원 여부 | Q1 결정. UI 가 모델 선택 시 슬라이더·콤보 범위 자동 적용 |

### 4.2 UserControl (`CameraControl`)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-20 | 카메라 N 행 (기본 3, 호스트 옵션으로 N 변경 가능): **모델 콤보, 통신 방식 콤보, COM, Baudrate 콤보, ID(VISCA Address), IP, Port, Home Preset 콤보** 입력. 통신 방식에 따라 무관 필드는 Disable(시각적 회색) | Q4·Q5 결정. UI 가 Form1 목업 대비 Baud/COM/Port 분리 |
| FR-21 | **연결** / **연결 종료** 버튼 (모든 활성 카메라 동시 처리, 결과 행별 표시, 실패 시 사유 메시지 노출) | Q2 결정 |
| FR-22 | **CAM N** 선택 버튼 (단일 선택, 시각 강조). **연결 실패한 카메라의 CAM 버튼은 비활성화(Enabled=false)** 되어 선택 불가 | Q2 결정. 잘못된 라우팅 방지 |
| FR-23 | 8방향 PTZ 패드 (▲▼◀▶ + 대각선 4개) Hold-to-Move | MouseDown=Drive, MouseUp/Leave=Stop |
| FR-24 | Zoom +/-, Focus +/- Hold-to-Move | 동일 |
| FR-25 | 속도 컨트롤: Pan/Tilt/Zoom 속도 NumericUpDown — **선택된 카메라의 모델 메타데이터에 따라 범위·기본값이 동적으로 적용**. CAM 전환 시 슬라이더 범위·현재값 갱신 | Q1 결정. 모델별 능력 차이를 흡수 |
| FR-26 | OSD ON/OFF/SEL/BACK 4 버튼 (단발 명령) | 각각 1회 송신 |
| FR-27 | 프리셋 1~12 버튼 + PRESET SET 토글 | SET 모드 시 다음 클릭은 저장, 일반 모드는 호출 |
| FR-27a | **Home Preset 자동 호출**: 카메라 연결 성공 시, 해당 카메라 행의 Home Preset 콤보값(1~12 또는 None)이 지정되어 있으면 즉시 `Memory Recall N` 송신 | Q3 결정 |
| FR-28 | 호스트로부터 `AttachServices(IList<ICameraService> services)` 또는 `ICameraServiceFactory` 주입 | Matrix `AttachService()` 와 유사, 다중 서비스 |
| FR-29 | 디자인 타임 호환 (VS2022 Designer): Designer 가 `InitializeComponent` 를 깨지 않도록 직선형 할당, 루프 없음 | 기존 Matrix 컨트롤 규약 준수 |

### 4.3 테스트 하네스 (`CameraController/`)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-40 | `MainForm` 에 `CameraControl` 1개 배치 | 라이브러리 사용 예시 |
| FR-41 | 사용자 마지막 입력값(모델/IP/COM/ID 등) 영속화 | `UserSettings.cs` 패턴 (PN8080Controller 참조) |
| FR-42 | 빌드 후 `bin\Debug\CameraController.exe` 실행으로 실제 카메라 검증 | RS-232/RS-422 USB 변환기 또는 IP 카메라 |

---

## 5. 비기능 요구사항 (Non-Functional)

| 분류 | 요구사항 |
|------|---------|
| 플랫폼 | .NET Framework 4.8 (기존 솔루션 일관성 유지), Windows Forms |
| 빌드 | Debug/Release 모두 0 경고, 솔루션 한 번 빌드로 모두 컴파일 |
| 디자인 타임 | VS2022 Designer 에서 `CameraControl` 정상 미리보기·편집 가능 |
| 응답성 | UI 스레드 블록 금지, 모든 송수신은 async/await, 스레드 동기화는 `SemaphoreSlim` 사용 (Matrix 패턴) |
| Hold-to-Move 지연 | MouseDown 부터 첫 Drive 명령 송신까지 ≤ 50ms 목표 |
| 로깅 | 모든 TX/RX/Error 가 시간/방향/내용 포함된 `LogEntry` 로 노출 (호스트가 표시 방식 결정) |
| 안전성 | 연결 실패/끊김 시 자동 재연결(옵션), 컨트롤 Dispose 시 모든 소켓·시리얼 포트 닫힘 보장 |
| 확장성 | 신규 모델 추가는 `CameraServiceBase` 상속 1개 + Factory 등록 1줄 |

---

## 6. 아키텍처 개요

```
┌────────────────────────────────────────────────────────────────────┐
│ CameraController (WinExe, 테스트 하네스)                          │
│   MainForm                                                         │
│     └── CameraControl (UserControl)  ──── AttachServices(...) ──┐  │
└────────────────────────────────────────────────────────────────┼──┘
                                                                 │
┌────────────────────────────────────────────────────────────────▼──┐
│ Xeno.Framework.Camera (Class Library, .NET 4.8)                   │
│                                                                    │
│   ┌──────────────────┐      ┌────────────────────────┐             │
│   │ ICameraService   │◄─────│ CameraServiceBase      │             │
│   │ (interface)      │      │ (abstract; 공통 상태)  │             │
│   └──────────────────┘      └─────────┬──────────────┘             │
│                                        │                            │
│             ┌──────────────────────────┼──────────────────────────┐ │
│             ▼                          ▼                          ▼ │
│   ┌────────────────────┐  ┌────────────────────┐  ┌───────────────┐│
│   │ EviH100Camera      │  │ SrgIpCamera        │  │ (Future:      ││
│   │ Service            │  │ Service            │  │  PelcoD, …)   ││
│   └────────┬───────────┘  └────────┬───────────┘  └───────────────┘│
│            │                        │                               │
│            ▼                        ▼                               │
│   ┌────────────────────┐  ┌────────────────────┐                    │
│   │ SerialVisca        │  │ UdpVisca           │                    │
│   │ Transport          │  │ Transport          │                    │
│   │ (SerialPort)       │  │ (UdpClient, 시퀀스)│                    │
│   └────────┬───────────┘  └────────┬───────────┘                    │
│            └────────────┬───────────┘                                │
│                         ▼                                            │
│              ┌──────────────────┐                                    │
│              │ ViscaCodec       │   (공통 명령 사전)                  │
│              │  + Response      │                                    │
│              │    Parser        │                                    │
│              └──────────────────┘                                    │
│                                                                       │
│   ┌──────────────────────────────────────────────────────────────┐   │
│   │ UI/CameraControl.cs (UserControl)                            │   │
│   │   - 카메라 1~3 등록 행 / CAM 선택 / PTZ Hold-to-Move /       │   │
│   │     Zoom·Focus / OSD / Preset(12) + SET 토글                 │   │
│   │   - AttachServices(IList<ICameraService>) 또는                │   │
│   │     AttachFactory(ICameraServiceFactory)                      │   │
│   └──────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────┘
```

### 6.1 폴더 구조 (제안)

```
Xeno.Framework.Camera/
  Core/
    ICameraService.cs
    CameraServiceBase.cs
    CameraInfo.cs            (Brand, Model)
    CameraCapabilities.cs    (PanSpeedRange, TiltSpeedRange, ZoomSpeedRange, SupportedBaudRates, DefaultTransport, PresetCount, OsdSupported)
    CameraTransportKind.cs   (RS232, RS422, RS485, UdpVisca, TcpVisca, …)
    Direction.cs             (Up, Down, Left, Right, UpLeft, …)
    LogDirection.cs          (Tx, Rx, Info, Error)   ← Matrix 와 동일 시그니처(향후 공유 검토)
    LogEntry.cs
    PresetMode.cs            (Recall, Set)
    ICameraServiceFactory.cs
    CameraServiceFactory.cs  (default)
  Protocols/
    Visca/
      ViscaCodec.cs          (PT Drive/Stop, Zoom, Focus, Memory, Menu …)
      ViscaResponseParser.cs (ACK / Completion / Error)
      ViscaConstants.cs      (속도 범위, 명령 코드 상수)
  Transports/
    SerialViscaTransport.cs
    UdpViscaTransport.cs     (Sony VISCA over IP 8-byte 시퀀스 헤더)
  Services/
    EviH100CameraService.cs
    SrgIpCameraService.cs
  UI/
    CameraControl.cs (.Designer.cs, .resx)
    CameraUiColors.cs

CameraController/                    (신규 WinExe)
  Program.cs
  UI/MainForm.cs (.Designer.cs, .resx)
  UserSettings.cs
  Properties/AssemblyInfo.cs
  CameraController.csproj
  App.config
  app.manifest
```

기존 `Xeno.Framework.Camera/Form1.cs` 는 **목업 참고용**으로만 사용. v1 구현 후에는 라이브러리 출력 형태(Library) 로 전환하면서 Form1·Program.cs 는 제거 또는 `CameraController/` 로 이관.

---

## 7. 지원 모델/프로토콜 (v1)

| 모델 | 브랜드 | Transport | 프로토콜 | 기본 파라미터 | 주소/식별자 |
|------|--------|----------|----------|--------------|------------|
| **EVI-H100** | Sony | RS-232 (D-Sub 8P 미니딘) / RS-422 (RJ-45) | VISCA | 9600 bps, 8N1 | VISCA Address 1~7 |
| **SRG-300H** | Sony | UDP/IP | VISCA over IP | UDP 포트 52381 | IP + 시퀀스 번호(주소는 헤더에서 자동 처리) |

### 향후 추가 후보 (구조만 확장 가능, v1 범위 외)

- Pelco-D / Pelco-P (RS-485)
- VISCA over TCP
- ONVIF Profile S/T
- Canon CR-N300 (Form1 콤보에 이미 항목 존재 → v2)

---

## 8. UI 사양 (Form1 목업 기준)

### 8.1 영역 분할

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ [상단] 카메라 등록 영역 (행이 N 만큼 동적 생성, 기본 3)                       │
│  헤더: 모델 │ 통신 │ COM │ Baud │ ID │ IP │ Port │ Home Preset             │
│  Cam1 : [▼]  [▼]  [   ] [▼]  [  ] [          ] [    ] [▼]                   │
│  Cam2 : [▼]  [▼]  [   ] [▼]  [  ] [          ] [    ] [▼]                   │
│  Cam3 : [▼]  [▼]  [   ] [▼]  [  ] [          ] [    ] [▼]                   │
│   ※ 통신 방식에 따라 무관 필드는 회색(Disable)                                │
├──────────────────────────────────────────────────────────────────────────────┤
│ [중단] OSD 행 :  [OSD ON] [OSD OFF] [OSD SEL] [OSD BACK]                     │
│ [중단] 컨트롤 행 :                                                            │
│   [CAM1] [CAM2] [CAM3] …(N)   [연결] [연결 종료]                              │
│   ※ 연결 실패 카메라의 CAM 버튼은 Disable + 행에 사유 메시지(라벨/툴팁)        │
├──────────────────────────────────────────────────────────────────────────────┤
│ [좌] PTZ 패드 (8방향)         [우] Zoom +/-, Focus +/-                        │
│   ↖ ▲ ↗                            속도(Pan / Tilt / Zoom)                    │
│   ◀   ▶                            ※ 슬라이더 범위는 선택된 CAM 의 모델       │
│   ↙ ▼ ↘                              `CameraCapabilities` 에서 자동 적용       │
├──────────────────────────────────────────────────────────────────────────────┤
│ [최우측] 프리셋 영역                                                           │
│   [PRESET SET]                                                                │
│   [Preset 1] [Preset 2]                                                       │
│   [Preset 3] [Preset 4]                                                       │
│   ... [Preset 11] [Preset 12]                                                 │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 8.2 동작 규칙

- **연결**: 모델 + 통신 방식 + (시리얼: COM·Baud·ID / IP: IP·Port) 필수 필드가 채워진 모든 행에 동시 연결 시도. 결과는 행별 라벨/색상으로 표시. **실패한 행**은 사유 메시지를 노출하고 해당 CAM 버튼을 Disable 한다(Q2).
- **연결 후 Home Preset**: 각 행의 Home Preset 콤보가 1~12 중 값을 가지면, 연결 직후 해당 카메라에 `Memory Recall N` 1회 송신(Q3).
- **CAM 선택**: 선택된 CAM 버튼만 강조(예: `Color.FromArgb(102, 178, 255)` — Matrix 와 통일감). 연결 실패 카메라는 Disable 되어 클릭 자체가 불가.
- **PTZ Hold-to-Move**: MouseDown → `PanTiltDrive(direction, panSpeed, tiltSpeed)`, MouseUp/MouseLeave → `PanTiltStop()`. 떼지 않은 채 외부로 마우스가 빠지면 Stop 송신.
- **Zoom/Focus Hold-to-Move**: 동일 패턴.
- **OSD**: 클릭 1회 = VISCA Menu On/Off/Sel/Back 1회 송신.
- **프리셋 일반 모드**: Preset N 클릭 = `Memory Recall N` 송신.
- **프리셋 SET 모드**: PRESET SET 클릭 → 모든 Preset 버튼 색상 변경(예: `#FFD700` Gold) → 다음 Preset N 클릭 시 `Memory Set N` 송신 후 SET 모드 자동 해제.
- **속도**: Pan/Tilt/Zoom 속도 NumericUpDown 의 Min/Max/기본값은 **선택된 CAM 의 모델 `CameraCapabilities`** 에서 가져온다(Q1). CAM 전환 시 즉시 갱신, 현재값이 새 범위를 벗어나면 클램프. 예) EVI-H100 Pan 1-18, Tilt 1-17, Zoom 0-7 / SRG-300H Pan 1-24, Tilt 1-20, Zoom 0-7.

### 8.3 통신 방식별 필드 활성화 규칙

| 통신 방식 선택 | 활성 필드 | 비활성 필드 | 기본값(모델별 `CameraCapabilities` 우선) |
|---------------|----------|------------|----------------------------------------|
| RS232 / RS422 / RS485 | COM, Baud, ID(VISCA Address) | IP, Port | EVI-H100: Baud=9600, ID=1 |
| UDP (VISCA over IP) | IP, Port | COM, Baud, ID | SRG-300H: Port=52381 |
| TCP (예약) | IP, Port | COM, Baud | — |

- 모든 필드(COM/Baud/ID/IP/Port/Home Preset)는 항상 **표시는 하되**, 통신 방식이 의미가 없으면 회색(Disable)·툴팁 안내(Q4).
- 모델 콤보 변경 시 해당 모델 `CameraCapabilities.DefaultTransport` 로 통신 방식·기본 Baud·기본 Port 를 자동 채움(사용자가 덮어쓸 수 있음).
- Home Preset 콤보 항목: `(없음)`, `1` … `PresetCount` (모델별 가변).

---

## 9. 산출물 (Deliverables)

| 산출물 | 위치 | 단계 |
|--------|------|------|
| 본 Plan 문서 | `docs/01-plan/features/camera-remote-control.plan.md` | Plan ✅ |
| Design 문서 | `docs/02-design/features/camera-remote-control.design.md` | Design (다음) |
| 라이브러리 코드 | `Xeno.Framework.Camera/` (16개 파일 내외) | Do |
| 테스트 하네스 | `CameraController/` (4개 파일 내외) | Do |
| 솔루션 등록 | `PN-8080 Controller.sln` 에 두 프로젝트 추가 | Do |
| 사용 가이드 | `Xeno.Framework.Camera/README.md` (선택) | Do |
| Gap Analysis | `docs/03-analysis/camera-remote-control.analysis.md` | Check |
| 완료 보고서 | `docs/04-report/camera-remote-control.report.md` | Report |

---

## 10. 위험 및 의존성

| # | 위험/의존성 | 영향 | 완화책 |
|---|------------|------|--------|
| R1 | RS-232/RS-422 실기 테스트용 USB 변환기 또는 케이블 미보유 | High (시리얼 검증 불가) | 가급적 실기 검증 권장. 부재 시 송신 바이트 검증·외부 시뮬레이터(Visca-IP-Server, com0com) 활용 |
| R2 | EVI-H100 의 OSD 메뉴 명령 코드가 모델 사양서에서 명확하지 않을 가능성 | Med | Design 단계에서 PDF (`asset/16349-EVI-H100V-S Tech Manual.pdf`) 정독 후 확정. 미지원 시 OSD 버튼은 비활성화 처리. |
| R3 | SRG-300H VISCA over IP 시퀀스 번호 동기화(서버에서 RESET 요청) 로직 누락 | Med | Sony 공식 사양에 따른 RESET 패킷(`02 00 00 01 00 00 00 01 01`) 처리. 응답 ACK/Completion 의 시퀀스 일치 검증. |
| R4 | VS2022 Designer 가 PTZ MouseDown/Up 이벤트 핸들러 코드 자동 변경 시 깨짐 | Low | Matrix 컨트롤과 동일하게 InitializeComponent 직선형 유지, 이벤트 와이어링은 코드비하인드 별도 메서드(`WireOwnEvents`)에서 처리. |
| R5 | .NET Framework 4.8 의 `SerialPort` 닫힘 행 hang 이슈 | Low | DiscardInBuffer/OutBuffer 후 별도 스레드에서 Close, 타임아웃 적용 |
| R6 | Form1 목업의 일부 필드(Home Preset 콤보 2개) 의미 불명 | Low | Open Question (§11) 으로 사용자 확인. 미지정 시 v1 에서 제외. |
| D1 | 의존성: `Xeno.Framework.Matrix` 의 `LogEntry`/`LogDirection` 와 시그니처 통일성 검토 (공유 vs 복제) | Low | v1 은 복제(독립). v2 에서 공통 베이스 추출 검토. |

---

## 11. Decisions Log (확정 — 2026-05-04, KDI)

| # | 질문 | 결정 | 영향 받은 섹션 |
|---|------|------|---------------|
| Q1 | 속도 슬라이더 — 전역 vs 카메라별? | **카메라(모델)별** — 모델 메타데이터(`CameraCapabilities`)에 Pan/Tilt/Zoom 속도 범위·기본값을 정의하고, CAM 선택 시 슬라이더 범위·현재값을 동적 적용. 신규 모델 추가 시 제조사 매뉴얼을 보고 `CameraCapabilities` 만 채우면 됨. | §4.1 FR-10, §8.2, §8.3 |
| Q2 | 연결 버튼 — 모두 동시 vs 선택 1대? | **모두 동시 연결**. 실패한 카메라는 행에 사유 메시지 노출 + 해당 **CAM 버튼 Disable** 로 선택 자체를 막아 잘못된 라우팅 방지. | §4.2 FR-21, FR-22, §8.2 |
| Q3 | Form1 의 Home Preset 콤보 용도 | **카메라 연결 성공 직후 선택된 Home Preset 번호로 `Memory Recall` 1회 자동 송신**. 콤보 항목: `(없음)`/`1`…`PresetCount` (모델별). | §4.2 FR-27a, §8.2 |
| Q4 | 시리얼 시 COM/Baud 입력 위치 | **모든 필드(COM·Baud·ID·IP·Port·Home Preset)를 항상 표시**, 통신 방식에 따라 무관 필드는 Disable(회색)·툴팁 안내. UI 헤더가 Form1 목업 대비 확장됨(COM·Baud·Home Preset 컬럼 추가). | §4.2 FR-20, §8.1, §8.3 |
| Q5 | 카메라 수 — 3대 고정 vs N대 확장? | **N대 확장**. `CameraControl` 의 호스트 옵션(`SlotCount` 또는 `AttachServices(IList)`)으로 행 수를 결정, 기본 3. 행은 동적 생성. 디자이너 친화성을 위해 InitializeComponent 는 N=3 으로 시드 후 런타임 재구성. | §4.2 FR-20, §6 아키텍처 다이어그램 |

---

## 12. 다음 단계

1. ✅ Plan 작성 + Open Questions 5건 해결 (2026-05-04)
2. ⏭ `/pdca design camera-remote-control` 진행
   - EVI-H100 (`asset/16349-EVI-H100V-S Tech Manual.pdf`) / SRG-300H (`asset/SRG-300H Technical Manual.pdf`) PDF 매뉴얼 정독 → VISCA 명령 바이트 표·속도 범위·OSD/Memory 명령 코드 확정
   - `ICameraService`, `CameraServiceBase`, `CameraCapabilities`, `ViscaCodec`, `SerialViscaTransport`, `UdpViscaTransport`, `CameraControl` 클래스별 시그니처·필드·이벤트 흐름 명세
   - VS2022 Designer 호환 패턴 명시 (Matrix 와 동일 규약)
   - UI 컬럼/그리드 동적 생성 알고리즘(N대 확장) 의사코드
3. ⏭ Design 승인 후 `/pdca do camera-remote-control` 으로 구현 착수
