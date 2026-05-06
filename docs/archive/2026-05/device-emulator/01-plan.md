# Plan — device-emulator (외부 디바이스 에뮬레이터)

> **요약**: 사내 운용 솔루션에서 카메라/메트릭스/마이크CCU/인코더 등 외부 디바이스의 **반대편 역할**을 수행하는 에뮬레이터 앱 신규. 컨트롤러(우리 라이브러리·서드파티) 가 보낸 패킷을 수신·표시·검증하고, 적절한 응답을 생성한다. 실제 장비 없이 컨트롤러 개발/회귀/멀티-디바이스 시나리오 테스트 가능. **별도 솔루션 (`DeviceEmulator.sln`) + 기존 `Xeno.Framework.Camera` 라이브러리 참조**로 codec 재사용. 단일 폴더 복사로 standalone 배포.
>
> **작성자**: 개발팀 (KDI)
> **작성일**: 2026-05-06
> **상태**: Approved (Open Questions 6건 — 전건 기본값 OK, 2026-05-06 KDI)
> **참조 패턴**: 보관본의 5계층 추상화 (Camera/Matrix) — Codec 은 그대로 재사용, Transport 는 client→server 로 반전

---

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | device-emulator |
| **대상** | 신규 standalone WinExe 프로젝트 (`DeviceEmulator/` 하위) |
| **솔루션** | **신규 `DeviceEmulator.sln`** (기존 `PN8080Controller.sln` 와 분리, `Xeno.Framework.Camera.csproj` 만 참조) |
| **신규 파일(예상)** | 에뮬레이터 코어 ~6 + 프로토콜 parser/builder ~6 + Transport server ~3 + 모델별 emulator ~5 + UI ~3 = **~23개** |
| **기존 라이브러리 영향** | `Xeno.Framework.Camera` 에 **inverse parser 추가** (`ViscaCommandParser`, `PelcoDCommandParser` 등 — controller 가 보낸 명령 byte 를 enum/intent 로 역변환). 기존 코드 변경 0 |
| **첫 사이클 (v1) 범위** | **카메라 5 모델** (EVI-H100, SRG-300H, FR-H50SN, CR-N300, Pelco-D Generic) — Q2 결정 |
| **후속 (v2) 범위** | **메트릭스 2 모델** (PN-8080, Blackmagic Videohub) — vendor protocol 정독 필요 |
| **후속 (v3~) 범위** | 회의용 마이크 CCU, 인코더, ONVIF, Pelco-P 등 |
| **배포 방식** | `DeviceEmulator.sln` 을 Release 빌드 → `DeviceEmulator/bin/Release/` 폴더 통째 zip → 어떤 Windows 머신에 풀기만 하면 실행 (.NET Framework 4.8 사전 설치 필요) |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | 컨트롤러 개발/회귀 시 실제 장비 의존 — 장비 미보유, 원격지, 다중 카메라 시나리오 검증 불가. round-1 의 EVI-H100 시리얼·FR-H50SN UDP "silent firmware" 오분류는 모두 실장비 한계 때문에 발생. 또한 신규 컨트롤러가 보내는 packet 이 정확한지 byte 수준 검증 도구 부재. |
| **Solution** | **컨트롤러의 반대편 역할** 을 수행하는 단일 standalone 앱. Device Type → Model → Protocol → Transport 선택 후 Listen → 컨트롤러 packet 수신 시 hex + 분류 (예: "VISCA PT Drive Right (pan=0x10, tilt=0x10)") 표시 + 적절한 ACK/Completion 자동 응답. 5계층 추상화 패턴 그대로 적용 (Codec 재사용, Transport 만 client→server 반전). |
| **Function UX Effect** | 운영자/개발자가 노트북 1대로 "내가 카메라가 되어보기" 가능. 컨트롤러 개발자는 실장비 없이 OOO PT Drive 명령이 정확한 byte 시퀀스로 송신되는지 즉시 확인. 멀티-디바이스 시나리오 (3 카메라 + 1 메트릭스 동시) 도 에뮬레이터 instance 다중 실행으로 완전 재현. 에러 케이스 (NAK, 응답 지연, 잘못된 byte) 도 시뮬레이션 가능. |
| **Core Value** | 사내 "외부 기기 통합 제어 API" 의 **개발/검증/운영 인프라 완성** — 라이브러리(controller side) 와 에뮬레이터(device side) 가 한 쌍을 이룸. 향후 신규 모델/프로토콜 추가 시 양쪽 동시 구현으로 회귀 위험 0. 컨트롤러의 silent bug (e.g., FR-H50SN 사례) 가 에뮬레이터 단위 검증으로 사전 차단. |

---

## 1. 배경 & 동기

### 1.1 직전 PDCA 에서 도출된 필요성

- **camera-remote-control round-2**: FR-H50SN UDP 응답이 안 와서 "silent firmware" 로 분류됨. round-3 (`pelco-d-and-udp-fix`) 에서 사실 우리 UDP 수신 버그였음을 사후 확정. **에뮬레이터 있었으면 round-2 시점에 controller bug 와 device behavior 를 분리 진단 가능**했음.
- **pelco-d-and-udp-fix Do 단계**: Canon CR-N300 실측 전엔 가설만 있었고 Wireshark 같은 외부 도구 없이는 packet flow 검증 불가. 에뮬레이터가 있으면 controller 가 보내는 byte 와 자신이 받은 byte 를 비교하는 게 즉시 가능.
- **멀티-카메라 시나리오** (3 cam × 3 protocol) 같은 복잡한 통합 테스트는 실장비로 구성하기 어려움. 에뮬레이터로 5대, 10대 시나리오 부담 없이 재현.

### 1.2 5계층 추상화의 자연스러운 확장

기존 controller-side 5계층 (Core/Protocols/Transports/Services/UI) 의 거의 전부를 에뮬레이터 측에서 재사용 가능:
- **Codec**: 똑같음 (`ViscaCodec.PanTiltDrive(addr, dir, p, t)` 가 만든 byte 를 `ViscaCommandParser.Parse(bytes)` 로 역변환)
- **Transport**: client → **server** 로 반전 (UdpClient.SendAsync → UdpListener.ReceiveAsync; TcpClient.Connect → TcpListener.Accept; SerialPort.Write → SerialPort.DataReceived)
- **Service**: controller 가 "명령 송신 + 응답 수신" 이라면 emulator 는 "명령 수신 + 응답 생성"

신규 protocol family (Pelco-P, ONVIF) 추가 시 동일 한 쌍 (codec parser + reply builder) 추가로 양쪽 동시 지원.

### 1.3 비목표 (Non-Goals, v1)

- **실제 장비 동작의 100% 재현** — emulator 는 packet 검증과 응답 생성에 집중. 카메라 PTZ 의 실제 모터 동작, 영상 출력, 센서 데이터 모두 비대상.
- **운영 환경 배포** — 개발/QA 도구. 운영 시스템에 배포 안 됨.
- **GUI 디바이스 시뮬레이션** — 카메라 OSD 메뉴 화면 재현 등 시각적 시뮬은 비대상 (단순 packet log 만).
- **고성능** — 동시 100대 emulation 같은 부하 테스트 비대상. 한 instance 가 1~3 카메라 emulate 정도면 충분.
- **CCU/인코더 protocol** — v1 은 알려진 7 모델 (camera 4 + Pelco-D 1 + matrix 2). 후속 cycle 에서 vendor 정보 확보 후 추가.

---

## 2. 목표 (Goals)

| 우선순위 | 목표 | 측정 기준 |
|---------|------|----------|
| Must | Standalone 앱 1개로 7 모델 emulate (EVI-H100, SRG-300H, FR-H50SN, CR-N300, Pelco-D, PN-8080, Videohub) | 모델 콤보 7 항목, 각 선택 후 Listen → 컨트롤러 연결 가능 |
| Must | 컨트롤러 packet 수신 시 hex + 분류 표시 (e.g. "PT Drive Right (pan=0x10, tilt=0x10)") | 모든 명령 1회 lookup 으로 식별 |
| Must | 적절한 응답 자동 생성 (VISCA: ACK + Completion / Pelco-D: 응답 없음 / Matrix: vendor protocol 기준) | 컨트롤러가 정상 인식 (응답 ACK 받았다고 판단) |
| Must | `DeviceEmulator.sln` 별도 솔루션 — Release 빌드 → `bin/Release/` 폴더 통째 복사로 standalone 배포 | 다른 PC 에서 .NET Framework 4.8 만 있으면 zip 풀어 즉시 실행 |
| Must | `Xeno.Framework.Camera` library 의 codec 재사용 (`ViscaCodec`, `PelcoDCodec`) — duplication 0 | csproj ProjectReference, 빌드 시 dll 자동 bundle |
| Should | 패킷 로그 파일 저장 (CameraController 의 toolbar 패턴 재사용) | 저장 버튼 → `.log` 파일 |
| Should | 멀티-인스턴스 동시 실행 (1 인스턴스 = 1 디바이스) — 멀티-디바이스 시나리오 | 동일 .exe 여러 번 실행 가능, 포트만 다르게 |
| Should | 에러 시뮬레이션 토글 (NAK, slow response, malformed reply) — 컨트롤러 회복력 테스트 | UI 체크박스 |
| Could | 디바이스 응답 지연 (latency) 슬라이더 — 0~1000ms | 실 환경 latency 시뮬 |
| Could | "Save / Load Scenario" — 사전 정의 응답 시퀀스 재생 | 회귀 테스트 자동화 |

---

## 3. 사용자 시나리오

### 3.1 Persona

- **개발자**: 신규 컨트롤러 코드 짤 때 실장비 없이 byte 검증
- **QA**: 회귀 테스트 시 멀티-디바이스 시나리오 재현
- **현장 운영자**: (드물게) 출시 전 라이브러리 버전 호환성 확인

### 3.2 핵심 시나리오

**S1. 단일 카메라 에뮬레이션 (개발자 일상)**
1. `DeviceEmulator.exe` 실행
2. Device Type 콤보 → "Camera"
3. Model 콤보 → "Sony EVI-H100" (또는 "Sony SRG-300H" 등)
4. Transport 콤보 → "VISCA over IP UDP" (모델 capability 기본값 자동)
5. Local IP/Port 자동 (52381) → **Listen** 클릭
6. 컨트롤러 (`CameraController.exe`) 가 IP=`127.0.0.1` (또는 LAN IP) 로 연결
7. PT Drive Right 보내면 emulator UI 에:
   ```
   [TCP] connected from 127.0.0.1:54321
   RX from :54321 raw=01 00 00 09 00 00 00 01 81 01 06 01 10 10 02 03 FF
   PARSED: VISCA PT Drive Right (pan=0x10, tilt=0x10) seq=1
   AUTO-REPLY: ACK + Completion (90 41 FF / 90 51 FF)
   ```

**S2. 멀티-카메라 시나리오 (QA)**
1. `DeviceEmulator.exe` 3번 실행 (각각 다른 model 또는 다른 port)
2. 컨트롤러 `CameraController.exe` 의 Cam1/Cam2/Cam3 가 각 emulator 에 연결
3. 양쪽 패킷 흐름 비교 — 컨트롤러 송신 == emulator 수신 byte 일치 확인

**S3. 메트릭스 에뮬레이션**
1. Device Type → "Matrix"
2. Model → "PN-8080" (또는 "Blackmagic Videohub")
3. Transport 자동 (TCP 8000 / 9990)
4. **Listen**
5. 컨트롤러 메트릭스 스위칭 명령 보내면 emulator 가 ASCII (PN-8080) 또는 block (Videohub) 응답 자동 생성

**S4. 에러 시뮬레이션 (회복력 테스트)**
1. Listen 후 "Inject NAK on next command" 체크
2. 컨트롤러 명령 송신 → emulator 가 ACK 대신 VISCA Error (`90 60 02 FF` Syntax error) 응답
3. 컨트롤러 측에서 RaiseError 정상 동작하는지 확인

**S5. Standalone 배포**
1. `DeviceEmulator/bin/Release/` 폴더 zip
2. 다른 머신에서 zip 풀고 `DeviceEmulator.exe` 실행 — .NET Framework 4.8 만 사전 설치되어 있으면 즉시 동작

---

## 4. 기능 요구사항 (Functional Requirements)

### 4.1 코어 (DeviceEmulator 앱)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-01 | Device Type / Model / Transport 3단계 캐스케이드 콤보 | "Camera → Sony EVI-H100 → RS-232/422 VISCA" 같은 흐름 |
| FR-02 | Listen 버튼 클릭 시 transport server 시작, Stop 버튼으로 종료 | UI 상태 표시 (Listening/Stopped) |
| FR-03 | 수신 packet 표시 — hex + 분류된 의미 (VISCA PT/Zoom/Focus/OSD/Memory, Pelco-D 동상, Matrix routing) | 시간 stamp + 방향 (RX/TX) 라벨 |
| FR-04 | 자동 응답 — model + protocol + 명령 종류 보고 적절한 reply byte 생성 | VISCA: ACK + Completion / Pelco-D: 무응답 / Matrix: vendor 별 다름 |
| FR-05 | 로그 패널 (CameraController toolbar 패턴 재사용 — 복사/지우기/저장/일시정지) | hex log 가독성 |
| FR-06 | (옵션) 에러 주입 — UI 체크박스 (NAK / Timeout / Malformed) | 컨트롤러 회복력 시험 |
| FR-07 | (옵션) 응답 latency 슬라이더 (0~1000ms) | 실 환경 시뮬 |
| FR-08 | (옵션) 멀티-인스턴스 동시 실행 — 같은 exe 여러 번 (port 만 충돌 안 나게) | OS 가 알아서, 별도 코드 없음 |

### 4.2 프로토콜 parser/builder (양쪽 라이브러리 공유)

기존 `Xeno.Framework.Camera/Protocols/` 의 `ViscaCodec`/`PelcoDCodec` 는 controller 측 (송신 byte 빌드). **에뮬레이터는 그 inverse 가 필요**:

| ID | 요구사항 | 위치 |
|----|---------|------|
| FR-20 | `ViscaCommandParser` — 수신 byte (`81 01 06 01 VV WW pp tt FF`) 를 `(addr, command-type, params)` 객체로 역변환 | `Xeno.Framework.Camera/Protocols/Visca/ViscaCommandParser.cs` (NEW, 라이브러리 추가) |
| FR-21 | `ViscaReplyBuilder` — `(seq, kind=Ack/Completion/Error)` → reply byte | 동상 |
| FR-22 | `PelcoDCommandParser` — 수신 7byte 를 `(addr, command, speeds)` 로 역변환 | `Xeno.Framework.Camera/Protocols/Pelco/PelcoDCommandParser.cs` (NEW) |
| FR-23 | `Pn8080Parser/ReplyBuilder` — PN-8080 ASCII protocol | DeviceEmulator 내부 (Matrix codec 은 `Xeno.Framework.Matrix` 에 있지만 inverse 는 emulator 만 사용) |
| FR-24 | `VideohubParser/ReplyBuilder` — block-based protocol | 동상 |

→ **VISCA/Pelco-D parser 는 `Xeno.Framework.Camera` 에 추가** (controller 도 응답 verify 용도로 미래에 사용 가능). **Matrix protocol parser 는 emulator 전용** (현재 controller 도 ASCII parsing 하지만 별개 구조).

### 4.3 Transport server (DeviceEmulator 전용)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-40 | `SerialServerTransport` — `SerialPort.DataReceived` 이벤트로 packet 수신 | RS-232/422/485 모두 동일 |
| FR-41 | `TcpServerTransport` — `TcpListener` accept + 각 client 별 handler | PN-8080, Videohub, FR-H50SN TCP 등 |
| FR-42 | `UdpServerTransport` — `UdpClient.Bind(port)` + ReceiveAsync 루프 | VISCA-over-IP cameras |
| FR-43 | (옵션) UDP 의 경우 reply 송신 시 dest port 결정 — `52381` (Sony 컨벤션) 또는 source port mirror | controller side 의 `ViscaUdpHub` 와 정합 |

### 4.4 모델별 emulator (v1 7 종)

| ID | 모델 | Protocol / Transport | 응답 패턴 |
|----|------|---------------------|----------|
| FR-60 | Sony EVI-H100 | VISCA / RS-232 9600 | ACK + Completion |
| FR-61 | Sony SRG-300H | VISCA / UDP 52381 | ACK + Completion (dest port 52381 reply, FR-H50SN/CR-N300 와 동일 vendor 컨벤션) |
| FR-62 | Sony SRG-300H | VISCA / TCP 5678 | 동상 (TCP) |
| FR-63 | FR-H50SN | VISCA / TCP 5678 raw | ACK + Completion |
| FR-64 | Canon CR-N300 | VISCA / UDP 52381 | ACK + Completion |
| FR-65 | Pelco-D Generic | Pelco-D / RS-232 9600 (또는 UDP 4001 / TCP 4001) | 무응답 (fire-and-forget) |
| FR-66 | PN-8080 | ASCII / TCP 8000 | vendor 별 ASCII 응답 (e.g. `s in 1 av out 1!` → 동일 echo + 상태) |
| FR-67 | Blackmagic Videohub | block / TCP 9990 | initial dump (`PROTOCOL PREAMBLE:` + `VIDEOHUB DEVICE:` + `INPUT LABELS:` + `VIDEO OUTPUT ROUTING:`) + ACK 후 라우팅 변경 응답 |

### 4.5 별도 솔루션 + 배포 (FR-MUST)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-80 | `DeviceEmulator.sln` 신설 (기존 `PN8080Controller.sln` 와 분리) | VS2022 에서 별도 open |
| FR-81 | `DeviceEmulator.csproj` 가 `..\Xeno.Framework.Camera\Xeno.Framework.Camera.csproj` 만 ProjectReference | 빌드 시 Camera dll 자동 bundle |
| FR-82 | Release 빌드 → `DeviceEmulator/bin/Release/DeviceEmulator.exe` + `Xeno.Framework.Camera.dll` 1 폴더 | zip 후 다른 머신 풀기만 하면 동작 (.NET Framework 4.8 사전 설치 필요) |
| FR-83 | (옵션) `README.md` 또는 시작 가이드 1장 — 사용법 (Listen → 컨트롤러 연결 → 로그 확인) | onboarding |

---

## 5. 비기능 요구사항

| 분류 | 요구사항 |
|------|---------|
| 플랫폼 | .NET Framework 4.8, C# 7.3, WinForms (기존 솔루션 일관성) |
| 빌드 | Debug + Release 0 경고 0 오류 |
| 의존성 | `Xeno.Framework.Camera.dll` 1개만 bundle. .NET Framework 4.8 OS 사전 설치 |
| 응답성 | 컨트롤러 명령 수신 후 ACK 송신까지 ≤10ms (실장비 평균 5~12ms 와 유사) |
| 동시성 | 1 instance 가 1 device emulation. 멀티-디바이스는 멀티-인스턴스 |
| 디자인 타임 | VS2022 Designer 호환 (CameraControl 패턴 — 정적 layout, 동적 row 만 코드비하인드) |
| 로그 | CameraController 와 동일 hex 표현 + 분류 라벨 |

---

## 6. 아키텍처 — 5계층 device-side 반전

```
┌──────────────────────────────────────────────────────────────────┐
│ DeviceEmulator.exe (standalone WinExe)                           │
│   MainForm                                                       │
│     ├── DeviceTypeCombo / ModelCombo / TransportCombo            │
│     ├── Local IP/Port 입력 + Listen/Stop 버튼                    │
│     ├── 에러 주입 / latency 옵션                                 │
│     └── 패킷 로그 (CameraController toolbar 패턴 재사용)         │
└─────────────────────────────┬────────────────────────────────────┘
                              │ uses
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ Core/                                                            │
│   IDeviceEmulator (interface — Start/Stop/OnPacketReceived)      │
│   DeviceEmulatorBase (abstract)                                  │
│   EmulatorInfo, EmulatorRegistry (model 콤보 데이터 소스)        │
│                                                                  │
│ Emulators/  (모델별)                                             │
│   EviH100Emulator, SrgIpEmulator, FrH50SnEmulator,               │
│   CanonCrN300Emulator, PelcoDGenericEmulator,                    │
│   Pn8080Emulator, BlackmagicVideohubEmulator                     │
│                                                                  │
│ Protocols/  (codec parser/builder — 양쪽 라이브러리 공유)         │
│   Visca/  (Xeno.Framework.Camera 라이브러리 참조 + 추가)         │
│     ViscaCommandParser  ← NEW (Camera 라이브러리에 추가)          │
│     ViscaReplyBuilder   ← NEW (Camera 라이브러리에 추가)          │
│   Pelco/                                                         │
│     PelcoDCommandParser ← NEW (Camera 라이브러리에 추가)          │
│   Matrix/  (DeviceEmulator 전용)                                 │
│     Pn8080Protocol  ← NEW                                        │
│     VideohubProtocol ← NEW                                       │
│                                                                  │
│ Transports/  (server-side, DeviceEmulator 전용)                   │
│   SerialServerTransport                                          │
│   UdpServerTransport                                             │
│   TcpServerTransport                                             │
└──────────────────────────────────────────────────────────────────┘
```

### 6.1 폴더 구조

```
DeviceEmulator/                              ← 신규 폴더 (repo root 하위)
  DeviceEmulator.sln                         ← 신규 솔루션
  DeviceEmulator/                            ← WinExe 프로젝트
    DeviceEmulator.csproj
    Program.cs
    App.config
    app.manifest
    Properties/AssemblyInfo.cs
    Core/
      IDeviceEmulator.cs
      DeviceEmulatorBase.cs
      EmulatorInfo.cs
      EmulatorRegistry.cs
    Protocols/Matrix/
      Pn8080Protocol.cs
      VideohubProtocol.cs
    Transports/
      SerialServerTransport.cs
      UdpServerTransport.cs
      TcpServerTransport.cs
    Emulators/
      EviH100Emulator.cs
      ViscaIpEmulator.cs        (SRG/FR/CR-N300 generic)
      PelcoDGenericEmulator.cs
      Pn8080Emulator.cs
      BlackmagicVideohubEmulator.cs
    UI/
      MainForm.cs (.Designer.cs, .resx)
  README.md                                  ← 사용법 1장
```

### 6.2 라이브러리 변경 (`Xeno.Framework.Camera` 만)

| 신규 파일 | 위치 |
|----------|------|
| `ViscaCommandParser.cs` | `Xeno.Framework.Camera/Protocols/Visca/` |
| `ViscaReplyBuilder.cs` | 동상 |
| `PelcoDCommandParser.cs` | `Xeno.Framework.Camera/Protocols/Pelco/` |

→ 기존 코드 변경 0. 신규 internal class 3개 추가만.

### 6.3 모델 추가 비용 (에뮬레이터 측)

신규 모델 추가 시:
- VISCA 계열: 1 Capabilities 행 + 1 emulator class (기존 `ViscaIpEmulator` generic 재사용 가능 시 0 추가)
- 신규 protocol: 1 codec parser + 1 reply builder + 1 transport binding + 1 emulator class

→ controller 측 5계층 비용 모델과 거의 동일.

---

## 7. 지원 디바이스 매트릭스

### v1 (본 사이클) — 카메라 5 모델 / 8 변형

| Device Type | Model | Protocol | Transport | Listen Port (default) | 응답 |
|-------------|-------|----------|-----------|----------------------|------|
| Camera | Sony EVI-H100 | VISCA | RS-232 9600 | COM port (사용자 선택) | ACK + Completion |
| Camera | Sony SRG-300H | VISCA | UDP | 52381 | ACK + Completion (dest 52381 reply) |
| Camera | Sony SRG-300H | VISCA | TCP | 5678 | 동상 |
| Camera | FR-H50SN | VISCA (raw) | TCP | 5678 | ACK + Completion |
| Camera | FR-H50SN | VISCA (raw) | UDP | 52381 | 동상 (vendor 컨벤션) |
| Camera | Canon CR-N300 | VISCA | UDP | 52381 | 동상 |
| Camera | Pelco-D Generic | Pelco-D | RS-232 9600 | COM port | 무응답 |
| Camera | Pelco-D Generic | Pelco-D | UDP / TCP 4001 | (선택) | 무응답 |

### v2 (후속 사이클) — 메트릭스 2 모델

| Device Type | Model | Protocol | Transport | Listen Port | 비고 |
|-------------|-------|----------|-----------|-------------|------|
| Matrix | PN-8080 | ASCII | TCP | 8000 | vendor protocol 정독 필요 — `Xeno.Framework.Matrix/Services/Pn8080MatrixService` reverse engineering |
| Matrix | Blackmagic Videohub | block | TCP | 9990 | initial dump + 라우팅 변경 응답 — `Xeno.Framework.Matrix/Services/VideohubMatrixService` reverse engineering |

→ **v1 작업량 축소** (메트릭스 vendor protocol 분석을 v2 별도 사이클로 분리).

### v3+ (후속 사이클들)

- 회의용 마이크 CCU (vendor 정보 확보 후)
- 인코더 (vendor 정보 확보 후)
- ONVIF (Profile S/T)
- Pelco-P (Pelco-D 변형)

---

## 8. UI 사양 (간단)

```
┌──────────────────────────────────────────────────────────────────┐
│ DeviceEmulator                                            [_][□][X]│
├──────────────────────────────────────────────────────────────────┤
│ Device Type: [Camera ▼]                                          │
│ Model:       [Sony SRG-300H ▼]                                   │
│ Transport:   [VISCA over IP UDP ▼]                               │
│ Local IP:    [0.0.0.0]    Port: [52381]                          │
│                                                                  │
│ [   Listen   ]  [    Stop    ]   ◉ Stopped                       │
│                                                                  │
│ ☐ Inject NAK    ☐ Inject timeout    Latency: [0]ms ━━━           │
├──────────────────────────────────────────────────────────────────┤
│ [복사] [지우기] [저장] [일시정지]                                 │
│ ┌──────────────────────────────────────────────────────────────┐ │
│ │ 11:23:45.123 Listening on 0.0.0.0:52381                       │ │
│ │ 11:23:50.234 RX from 192.168.1.50:54321                       │ │
│ │              raw=01 00 00 09 00 00 00 01 81 01 06 01 10 10... │ │
│ │              PARSED: VISCA PT Drive Right (pan=0x10, tilt=0x10)│ │
│ │ 11:23:50.236 TX ACK seq=1 : 01 11 00 03 00 00 00 01 90 41 FF  │ │
│ │ 11:23:50.238 TX Completion seq=1 : 01 11 ... 90 51 FF         │ │
│ └──────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

- Device Type 변경 → Model 콤보 자동 갱신
- Model 변경 → Transport 콤보 (모델 capability) 자동 갱신
- Transport 변경 → Port 자동 (vendor convention) 갱신, 사용자 override 가능
- Listen 시 시작 로그, Stop 시 종료 로그
- 패킷 수신 시 timestamp + raw hex + PARSED 라벨

---

## 9. 산출물 (Deliverables)

| 산출물 | 위치 | 단계 |
|--------|------|------|
| 본 Plan 문서 | `docs/01-plan/features/device-emulator.plan.md` | Plan ✅ |
| Design 문서 | `docs/02-design/features/device-emulator.design.md` | Design (다음) |
| `DeviceEmulator/DeviceEmulator.sln` | repo root 하위 | Do |
| `DeviceEmulator/DeviceEmulator/` 프로젝트 (~17 파일) | 동상 | Do |
| `Xeno.Framework.Camera/Protocols/Visca/ViscaCommandParser.cs` 외 3 신규 | 기존 라이브러리 확장 | Do |
| `DeviceEmulator/README.md` 사용법 가이드 | 동상 | Do |
| Gap Analysis | `docs/03-analysis/device-emulator.analysis.md` | Check |
| 완료 보고서 | `docs/04-report/device-emulator.report.md` | Report |

---

## 10. 위험 및 의존성

| # | 위험/의존성 | 영향 | 완화책 |
|---|------------|------|--------|
| R1 | 별도 솔루션 → controller 라이브러리와의 동기화 부담 (ViscaCodec 변경 시 emulator parser 도 갱신) | Med | parser 를 같은 라이브러리 (`Xeno.Framework.Camera`) 에 두어 빌드 단위로 묶음. controller 빌드 시 emulator 도 자동 영향 |
| R2 | PN-8080 / Videohub vendor protocol 의 정확한 reply 사양 — 매뉴얼 정독 필요 | Med | `Xeno.Framework.Matrix/Services/` 의 `Pn8080MatrixService` / `VideohubMatrixService` controller 코드를 reverse engineering — 어떤 명령에 어떤 응답 기대하는지 추출 |
| R3 | Serial loopback — emulator 와 controller 가 같은 머신에서 시리얼 통신하려면 가상 COM port pair (com0com 같은) 또는 USB null-modem 케이블 필요 | Low | 사용자가 도구 준비. README 에 명시 |
| R4 | UDP / TCP — emulator 가 listen 하는 port 가 다른 프로세스 (실 카메라 제어 SDK 등) 와 충돌 | Low | UI 에서 port 변경 가능 |
| R5 | VS2022 Designer 가 `DeviceEmulator.sln` 내 `Xeno.Framework.Camera.csproj` cross-solution reference 를 정상 처리할지 | Low | ProjectReference + 상대 경로로 표준 방식. 실패 시 dll 직접 참조로 fallback |
| R6 | .NET Framework 4.8 사전 설치 의존 — 다른 머신에서 누락 시 실행 안 됨 | Low | README 에 prerequisite 명시. Windows 10 1903+ 는 기본 설치 |
| R7 | 멀티-인스턴스 시 같은 port 충돌 — 1대 외 추가 인스턴스 fail | Low | UI 에서 port 변경. 또는 1 인스턴스 = N 디바이스 멀티-listener 패턴 (v2) |

---

## 11. Decisions Log (확정 — 2026-05-06, KDI)

| # | 질문 | 결정 | 영향 받은 섹션 |
|---|------|------|---------------|
| Q1 | 솔루션 분리 vs 통합 | **별도 `DeviceEmulator.sln`** — 사용자 요구 #3 의 "별도 배포" 부합. `..\Xeno.Framework.Camera\Xeno.Framework.Camera.csproj` ProjectReference | §6.1 폴더 구조, §4.5 FR-80~82 |
| Q2 | v1 모델 우선순위 | **v1 = 카메라 5 모델만** (EVI-H100, SRG-300H, FR-H50SN, CR-N300, Pelco-D Generic). PN-8080/Videohub 는 vendor protocol 정독 필요 → **v2 별도 사이클**. v1 작업량 ~50% 감소 | §1, §7 매트릭스 (5 항목으로 축소), §9 산출물 |
| Q3 | 에러 주입 / latency 옵션 | **v1 포함** — UI 체크박스 (NAK/timeout/malformed) + latency 슬라이더. 회복력 테스트 가치 큼, 구현 비용 작음 | §4.1 FR-06/07 유지, §8 UI 사양 |
| Q4 | 멀티-디바이스 한 인스턴스 | **1 instance = 1 device** (v1). 멀티-디바이스는 멀티-인스턴스 (port 만 다르게) | §4.1 FR-08 |
| Q5 | Matrix protocol parser 위치 | **v2 사이클에서 `Xeno.Framework.Matrix/Protocols/` 신설** + `Pn8080Parser`, `VideohubParser` 추가. Camera 와 동일 패턴 | v2 사이클로 위임 |
| Q6 | README / 사용법 문서 | **`DeviceEmulator/README.md` 1장 추가** — onboarding + 배포 zip 에 함께 | §4.5 FR-83, §9 산출물 |

---

## 12. 다음 단계

1. ✅ Plan 작성 완료 → 사용자 검토 (Open Questions Q1~Q6 회답)
2. ⏭ 회답 반영 후 `/pdca design device-emulator`
3. ⏭ Design 승인 후 `/pdca do device-emulator` 으로 구현 착수

---

**검토 요청 (KDI)**:
- §11 Open Questions 6건 답변 부탁드립니다. 기본 가정으로 진행해도 무방한 항목은 "기본값 OK" 만 회신해주셔도 됩니다.
- 특히 **Q2 (모델 우선순위)** 와 **Q3 (에러 주입 v1 포함 여부)** 가 사이클 작업량을 크게 좌우합니다.
