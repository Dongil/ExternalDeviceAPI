# camera-remote-control — 완성 보고서

> **요약**: PTZ 카메라 통합 제어를 위한 재사용 가능한 `Xeno.Framework.Camera` 클래스 라이브러리와 `CameraController` 테스트 하네스 완성. EVI-H100 (RS-232/RS-422 VISCA), SRG-300H (VISCA over IP UDP 52381), FR-H50SN (raw VISCA over TCP) 3개 모델 지원. 외부 기기 통합 제어 API 의 두 번째 사례 (Matrix 다음).
>
> **작성자**: 개발팀 (KDI)
> **생성일**: 2026-05-06
> **기간**: 2026-05-04 ~ 2026-05-06 (3일, 2 라운드 반복)
> **상태**: Approved (코드 매칭율 97% / Design v1.1 동기화 완료)

---

## Executive Summary

### 1.1 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | camera-remote-control |
| **기간** | 2026-05-04 ~ 2026-05-06 (실작업 ~14h) |
| **PDCA 라운드** | 2회 (round-1: 초기 구현, round-2: 디버깅 주도 진화) |
| **담당자** | 개발팀 (KDI) |
| **참조 패턴** | `Xeno.Framework.Matrix` (matrix-controller, 2026-04 완료) |

### 1.2 결과 요약

| 항목 | 수치 |
|------|------|
| **신규 프로젝트** | 2개 (`Xeno.Framework.Camera` Library, `CameraController` WinExe) |
| **신규 파일** | 라이브러리 23개 (.cs) + 테스트 하네스 5개 = **28개** |
| **수정 파일** | `PN8080Controller.sln`, `Xeno.Framework.Camera.csproj` (WinExe→Library) + Form1 mockup 4개 제거 |
| **지원 모델** | 3개 (EVI-H100 / SRG-300H / FR-H50SN) |
| **VISCA Transport** | 3종 (Serial / UDP-with-header / TCP-raw) |
| **빌드 경고** | 0 (Debug / Release 모두) |
| **설계-구현 매칭율** | **97%** (코드 품질 기준) |
| **실측 검증** | 2/3 모델 (EVI-H100 RS-232 ✅, FR-H50SN TCP ✅, SRG-300H ⏸ 원격지) |
| **Critical/Major Gap** | **0건** |

### 1.3 Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | 사내 PTZ 카메라가 브랜드(Sony, FR …)·프로토콜(VISCA Serial / VISCA-IP UDP / raw VISCA-IP TCP)·firmware 응답성(silent OEM 펌웨어 포함)이 모두 다양했음. 기존엔 메트릭스 외 외부 기기 제어용 공통 추상화 부재로 새 모델 추가가 산발적이었고 통합 운영 솔루션에서 재사용 어려움. |
| **Solution** | 5계층 추상화 (`ICameraService` → `CameraServiceBase` → `ViscaIpCameraService` 또는 `EviH100CameraService` → `IViscaIpTransport`/`SerialViscaTransport` → `ViscaCodec`). 새 VISCA-IP 모델 추가는 **0 Service + 1 Capabilities + 1 factory case** 만으로 완료 (FR-H50SN 추가로 가설 1회 검증). `CameraControl` UserControl 은 csproj 참조 → 드래그 → `AttachFactory` 3단계로 다른 WinForms 솔루션에서 즉시 재사용. |
| **Function UX Effect** | 운영자가 한 화면에서 카메라 N대 동시 등록·연결, CAM 선택으로 명령 라우팅, PTZ/Zoom/Focus Hold-to-Move (MouseDown=Drive / MouseUp=Stop, hover spurious 차단 가드 포함), 프리셋 SET 토글 워크플로우, 연결 직후 Home Preset 자동 호출, 모델별 속도 슬라이더 자동 동기화. **응답 지연 ACK ~5ms** (FR-H50SN TCP 실측). 호스트는 `ServiceLog` 이벤트 구독으로 슬롯별 TX/RX/INFO/ERR 트래픽 즉시 노출. |
| **Core Value** | "외부 기기 통합 제어 API"의 두 번째 사례 정착. 메트릭스 (round-1 : Sony/Blackmagic 2 vendor) → 카메라 (Sony 2 + FR OEM 1 vendor, raw + IP-headered 2 wire-format) 으로 패턴 검증. 향후 Pelco-D, ONVIF, 다른 PTZ 모델, 신규 카테고리(스위처·인코더) 추가 시 동일 5계층 패턴 적용 가능한 사내 표준 확립. 실측 디버깅 (`ReceiveAsync` race, hover spurious Stop, silent firmware) 에서 도출된 **9건의 정당한 진화**가 round-2 에 흡수되어 운영 견고성도 입증. |

---

## 2. PDCA Cycle 요약

### 2.1 Plan 단계 (2026-05-04)

**문서**: `docs/01-plan/features/camera-remote-control.plan.md`

- 사용자 1차 요구: "메트릭스 라이브러리처럼, 카메라도 통합 추상화 + 새 모델/업체 추가 용이하게"
- Form1.cs 목업 (3 cameras, PTZ pad, OSD 4종, Preset 12 + SET, 속도 슬라이더) 분석
- EVI-H100 / SRG-300H 두 모델 우선 선정
- Matrix 의 `IService + ServiceBase + UserControl + AttachService` 패턴 채택
- **5건 Open Questions (Q1~Q5) 제시 → 사용자가 모두 답변 → Decisions Log 로 확정**:
  - Q1 속도: 모델별 능력 (`CameraCapabilities` 메타데이터)
  - Q2 연결: 모두 동시 + 실패 시 CAM 버튼 Disable
  - Q3 Home Preset: 연결 직후 Auto-Recall
  - Q4 필드: 모든 통신 필드 표시 + 통신 방식별 Disable
  - Q5 카메라 수: N대 확장 (기본 3)

### 2.2 Design 단계 (2026-05-04 v1.0 → 2026-05-06 v1.1)

**문서**: `docs/02-design/features/camera-remote-control.design.md` (v1.1, 1900+ 라인)

**v1.0 (2026-05-04)**:
- 19개 파일 명세 (클래스 시그니처·바이트 사전·UI 상태머신·디자이너 호환 규약)
- VISCA 코덱 정적 빌더 (PT/Zoom/Focus/Memory/OSD 모두 명시)
- UDP 8B 헤더 transport 명세
- `SrgIpCameraService` 단일 모델 서비스
- 8단계 구현 순서 (총 ~11.5h 예상)
- §14 Open Items 4건 (O1 OSD 바이트, O2 PresetCount, O3 TiltSpeedMax, O4 RS-485 표시)

**v1.1 (2026-05-06)** — round-2 implementation-ahead 9건 단방향 흡수:
- §5.0 (신규) `IViscaIpTransport` 공통 추상화
- §5.2 (재기술) UDP 백그라운드 RX 루프 + `BlockingCollection` 큐 + `WaitForReply` + late-Completion auto-skip + 분류 drain 로깅, timeout 1000→300ms
- §5.3 (신규) TCP transport (raw VISCA, 0xFF terminator slicing, 2초 connect timeout, 기본 5678)
- §6.2 (재기술) `SrgIpCameraService` → `ViscaIpCameraService` (generic, `(Info, Caps)` ctor, UDP/TCP dispatch)
- §3.4 (확장) `ExpectReply` + `FrH50Sn()` 정적 팩토리
- §3.7 (확장) FR-H50SN 등록
- §7.4·§7.7 통신 콤보 5항목, §7.2 `ServiceLog` 이벤트, §7.9 Hold-flag 가드, §8.2 MainForm 로그 toolbar

### 2.3 Do 단계 (2026-05-04, ~3h)

**구현 산출물**:
- `Xeno.Framework.Camera/` 16개 파일 (Core 8, Visca 3, Transports 2, Services 2, UI 4)
- `CameraController/` 8개 파일 (Program/UserSettings/MainForm + Designer + Properties)
- 솔루션 등록 (`PN8080Controller.sln` 4 프로젝트)
- 기존 mockup 제거 (Form1.cs/Designer/.resx, Program.cs, 단독 sln, App.config)
- **Build: Debug + Release 양쪽 0 경고 0 오류**

### 2.4 Check 단계 (2026-05-04 round-1, 2026-05-06 round-2)

**문서**: `docs/03-analysis/camera-remote-control.analysis.md`

**Round-1 (2026-05-04)**: 매칭율 **97%**, Critical/Major 0건. Minor 2건(M1 Designer ctor, M2 MainForm 로그 패널), Cosmetic 2건, Bonus 7건, Known Limitations 4건(O1~O4).

**Round-2 (2026-05-06, Design v1.0 ↔ 코드 round-2)**:
- 가중 평균 매칭율 (설계 문구 기준) **87%** ⚠️ — Design 이 round-2 변경을 반영 못한 lag
- 참조 매칭율 (코드 품질 기준) **97%** ✅
- Genuine Gap 0건, Implementation-Ahead 9건, Match (M2 해결) 1건, Minor 1건(M1 잔존), Known 4건, Bonus 12건
- 동기화 결정: **Design 단방향 업데이트** → Design v1.1 작성 → 매칭율 회복

### 2.5 Act 단계 — 실측 디버깅 주도 진화 (2026-05-04 ~ 05-06)

#### Act-1: UDP 응답 안 오는 문제

**증상**: SRG-300H VISCA over IP 가 Reset 응답만 받고 그 이후 모든 명령 timeout.
**진단**: 로그 추가 → `Task.WhenAny(c.ReceiveAsync(), Task.Delay)` 패턴이 timeout 시 background `ReceiveAsync` Task 를 cancel 못함 → orphan task 가 다음 응답을 가로채는 race.
**해결**:
- `UdpViscaTransport` 백그라운드 RX 루프 + `BlockingCollection` 큐 패턴으로 전면 재작성
- `ReceiveTimeoutMs` 1000ms → 300ms (Sony 응답 보통 <50ms)
- 매 send 직전 stale drain (이전 명령 늦은 Completion 정리)
- `WaitForReply` 플래그 추가 (silent firmware 대응)

#### Act-2: MouseLeave 가 Drive 없이 Stop 송신

**증상**: 버튼 누르고 한참 후에야 카메라 움직이고, 누르고 있는데 멈췄다 가는 현상.
**진단**: 로그 분석 → 사용자가 호버만 한 버튼들에서 Stop 명령들이 9개 연속 송신되고 그 뒤에 진짜 PT Drive 가 큐에 막혀서 1초씩 밀림.
**해결**: `_ptHoldActive` / `_zoomHoldActive` / `_focusHoldActive` per-axis 플래그 — Drive 가 실제로 송신될 때만 set, MouseUp/Leave 는 flag true 일 때만 Stop 발송.

#### Act-3: VISCA over TCP 추가 (FR-H50SN 대응)

**계기**: SRG-300H 가 다른 현장이라 미테스트. 대체 카메라 FR-H50SN (Chinese OEM firmware) 도입했으나 UDP 응답 없음.
**사용자 제안**: "카메라 web UI 에 VISCA TCP 항목 있는데 TCP 면 응답 오지 않을까?"
**해결**:
- `IViscaIpTransport` 공통 인터페이스 추출
- `TcpViscaTransport` 신설 (raw VISCA, 0xFF terminator slicing, 2초 connect timeout)
- `SrgIpCameraService` → `ViscaIpCameraService` 일반화 (`(CameraInfo, CameraCapabilities)` ctor, Transport 보고 UDP/TCP dispatch)
- FR-H50SN 모델 등록
- UI 통신 콤보 5항목 (TCP 추가)
**결과**: 실측 ACK ~5ms, 모든 명령 정상 동작.

#### Act-4: 로그 가독성 + 도구 추가

- Drain 로그 분류: `DRAIN stale=...` → `DRAIN late <ACK|Completion|Error[..]> : ...`
- `ViscaResponseParser.DescribeReply()` 헬퍼 추가
- MainForm 로그 toolbar (복사 / 지우기 / 저장 / 일시정지) — 일시정지 시 gold 배경, SaveFileDialog 기본 파일명 `camera-YYYYMMDD-HHmmss.log`
- `CameraControl.ServiceLog` 이벤트 + `ServiceLogEventArgs` (round-1 M2 갭 정식 해결)

#### Act-5: 응답 순서 어긋남 보강 ("방식 A")

**증상**: 드물게 PT Drive 의 응답으로 Completion 이 ACK 보다 먼저 도착 (이전 명령 Completion 이 새 명령 ACK 와 race).
**해결**: UDP/TCP 양쪽 transport 의 `SendCommandAsync` — 첫 dequeue 가 Completion 이면 `RX late Completion (skipped)` 로그 후 한 번 더 dequeue (최대 1회 재시도). ACK 매칭 정확성 ↑.

---

## 3. 최종 아키텍처

```
┌──────────────────────────────────────────────────────────────────────────┐
│ CameraController.exe (테스트 하네스, WinExe, .NET 4.8)                   │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │ MainForm (900×580)                                                  │  │
│  │   ├── 메뉴: 카메라 수 (3/4/5/6) + 로그 지우기                       │  │
│  │   ├── _cameraControl (CameraControl, 880×400)                       │  │
│  │   ├── 로그 toolbar: 복사 / 지우기 / 저장 / 일시정지                  │  │
│  │   └── _txtLog (Multiline, ReadOnly, Consolas 9pt, 2000 라인 cap)    │  │
│  │   ServiceLog 이벤트 구독 → [Cam N] TX/RX/INFO/ERR <text> 출력       │  │
│  │   UserSettings (INI, %LocalAppData%\CameraController\settings.ini)  │  │
│  └────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────┬────────────────────────────────────────┘
                                  │ ProjectReference
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ Xeno.Framework.Camera.dll (Class Library, .NET 4.8, C# 7.3)              │
│                                                                          │
│  Core/  (8 files)                                                        │
│    ICameraService, CameraServiceBase                                     │
│    CameraInfo, CameraCapabilities (+ ExpectReply v1.1, FrH50Sn factory)  │
│    CameraTransportKind enum (Rs232/Rs422/Rs485/UdpVisca/TcpVisca)        │
│    Direction, LogEntry, ICameraServiceFactory + CameraServiceFactory     │
│                                                                          │
│  Protocols/Visca/  (3 files)                                             │
│    ViscaCodec  ── 공통 명령 사전 (PT/Zoom/Focus/Memory/OSD)              │
│    ViscaResponseParser  ── ACK/Completion/Error 분류 + DescribeReply     │
│    ViscaConstants + ViscaHeader                                          │
│                                                                          │
│  Transports/  (4 files)                                                  │
│    IViscaIpTransport  ── UDP/TCP 공통 추상화 (v1.1 신설)                 │
│    SerialViscaTransport (RS-232/422/485, SerialPort)                     │
│    UdpViscaTransport (Sony 8B 헤더 + 백그라운드 RX 루프 + 큐 + 방식 A)   │
│    TcpViscaTransport (raw VISCA, 0xFF slicing, 백그라운드 RX 루프 + 큐) │
│                                                                          │
│  Services/  (2 files)                                                    │
│    EviH100CameraService (RS-232/422 전용)                                │
│    ViscaIpCameraService (generic, UDP/TCP dispatch — SRG/FR 모두 사용)   │
│                                                                          │
│  UI/  (6 files)                                                          │
│    CameraControl.cs / .Designer.cs / .resx (UserControl, N대 동적 생성)  │
│    CameraSlotConfig (DTO)                                                │
│    CameraUiColors (Selected/Status/PresetSet 색상)                       │
│    ServiceLogEventArgs (v1.1 — slot index + LogEntry)                    │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 4. 핵심 설계 결정과 근거

| 결정 | 근거 |
|------|------|
| **Matrix 패턴 그대로 차용** | 같은 솔루션 내 외부 기기 제어 라이브러리 두 번째 사례. 일관성으로 신규 개발자 온보딩 시간 단축 |
| **`CameraCapabilities` 메타데이터 (Q1)** | 모델별 속도 범위·프리셋 개수·지원 baud 등이 모두 다름. 코드 분기 대신 데이터 분리. 신규 모델 = 1개 정적 팩토리 추가 |
| **N대 동적 슬롯 (Q5)** | Form1 목업 3대 고정이지만 실 운영장 4~6대 시나리오 대응. `RebuildRows()` 패턴으로 슬롯 수 변경 시 행 + CAM 버튼 동시 재구성 |
| **연결 직후 Home Preset 자동 호출 (Q3)** | Form1 의 Home Preset 콤보 의미 명확화. 운영 시작 시 카메라가 항상 알려진 위치에서 시작되어 사용성 ↑ |
| **VISCA-IP transport 추상화 (v1.1)** | UDP-headered (Sony) 와 raw-TCP (PTZOptics/FR) 변형을 동일 Service 가 다루도록. FR-H50SN 추가가 0 Service 비용 |
| **백그라운드 RX 루프 + 큐 (v1.1)** | `Task.WhenAny + ReceiveAsync` race 회피. UDP/TCP 동일 패턴으로 대칭성 유지 |
| **Hold-flag 가드 (v1.1)** | MouseLeave hover 만으로 Stop 송신되는 버그를 호버 통과와 진짜 release 를 구분 못하는 한계로 보고 axis 별 state machine 도입 |
| **ServiceLog 이벤트 (v1.1)** | round-1 M2 갭 (MainForm 로그 패널 미동작) 정식 해결. UI 스레드 자동 marshal 로 안전성 ↑ |

---

## 5. 실측 검증 결과

| 모델 | Transport | 결과 | 핵심 지표 |
|------|----------|:---:|----------|
| EVI-H100 | RS-232 | ✅ 작동 | PTZ/Preset 모두 정상. Plan §10 R1 (시리얼 미보유 위험) 해소 |
| FR-H50SN | UDP (port 52381) | ⚠️ 부분 | 명령 실행은 되나 응답 없음 (silent OEM firmware) |
| FR-H50SN | TCP (port 52381, vendor unit 별 상이) | ✅ 작동 | ACK ~5ms, late Completion 정상 drain, 모든 axes 응답성 양호 |
| SRG-300H | VISCA-IP | ⏸ 미검증 | 원격지 카메라 — 향후 micro-task |

**디버깅 효율성**: `[Cam N] [UDP|TCP] TX seq=N : <hex>` / `RX raw=<hex>` / `DRAIN late <kind> : <hex>` 분류 로그가 사용자 보고 → AI 진단 → 코드 fix 사이클을 ~30분 안에 완료시킴 (5회 라운드).

---

## 6. 산출물 인벤토리

### 6.1 Plan/Design/Analysis/Report 4종 문서

| 단계 | 위치 | 분량 |
|------|------|-----|
| Plan | `docs/01-plan/features/camera-remote-control.plan.md` | ~340 라인 |
| Design v1.1 | `docs/02-design/features/camera-remote-control.design.md` | **~1900 라인** |
| Analysis (round-2) | `docs/03-analysis/camera-remote-control.analysis.md` | ~270 라인 |
| Report (본 문서) | `docs/04-report/camera-remote-control.report.md` | (본 파일) |

### 6.2 코드

**라이브러리** (`Xeno.Framework.Camera/`, 23 .cs):
```
Core/                Protocols/Visca/      Transports/              Services/
  CameraInfo           ViscaConstants        IViscaIpTransport (v1.1)  EviH100CameraService
  CameraTransportKind  ViscaCodec            SerialViscaTransport      ViscaIpCameraService (v1.1)
  CameraCapabilities   ViscaResponseParser   UdpViscaTransport
  Direction                                  TcpViscaTransport (v1.1)
  LogEntry           UI/
  ICameraService       CameraControl.cs
  CameraServiceBase    CameraControl.Designer.cs
  ICameraService       CameraControl.resx
    Factory            CameraSlotConfig
                       CameraUiColors
                       ServiceLogEventArgs (v1.1)
```

**테스트 하네스** (`CameraController/`, 8 files): `Program.cs`, `UserSettings.cs`, `UI/MainForm.cs / .Designer.cs / .resx`, `Properties/AssemblyInfo.cs`, `App.config`, `app.manifest`, `CameraController.csproj`

**솔루션**: `PN8080Controller.sln` (4 프로젝트, 16개 BuildConfig 라인)

---

## 7. 학습된 교훈 (Lessons Learned)

### 7.1 잘 된 점

| # | 교훈 |
|---|------|
| L1 | **Decisions Log (Q1~Q5) 가 Design 품질을 크게 좌우** — Plan 단계에서 5건 명시적 의사결정으로 Design 단계 재작업 거의 0. 이 패턴은 표준화 가치 있음 |
| L2 | **Matrix 의 IService + UserControl + AttachService 패턴이 두 번째 사례에서도 작동** — 카메라까지 흡수. "외부 기기 통합 제어 API" 사내 표준으로 자리잡음 |
| L3 | **`CameraCapabilities` 메타데이터 패턴의 위력** — round-2 에서 FR-H50SN 추가가 정말로 "1 Capabilities 팩토리"만으로 끝남. round-1 가설 실증 |
| L4 | **로그 분류 라벨이 AI ↔ 사람 협업 디버깅 사이클을 가속** — `DRAIN late Completion` 처럼 의미 있는 이름이면 사용자가 로그 보내고 AI 가 즉시 진단 가능 |

### 7.2 개선 가능했던 점

| # | 교훈 |
|---|------|
| L5 | **v1.0 UDP transport 의 `Task.WhenAny + ReceiveAsync` 패턴은 대표적 anti-pattern** — Design 작성 시 알아채지 못함. 향후 IO transport 설계 시 "background reader + queue" 가 default 로 |
| L6 | **MouseLeave 동작 가정의 위험성** — 마우스 호버만으로 이벤트가 발화한다는 사실을 디버깅 후에야 인지. 향후 Hold-to-Move 패턴은 항상 `_holdActive` flag 동반 |
| L7 | **Vendor convention 차이는 default 값이 아닌 사용자 입력으로 흡수** — FR-H50SN 의 5678 vs 52381 처럼 같은 모델 클래스 내에서도 unit 별 포트가 달라짐. Capability default 는 placeholder 의미 강조 |

### 7.3 메타 교훈 (PDCA 자체)

| # | 교훈 |
|---|------|
| L8 | **Implementation-ahead-of-design 은 정상적 진화** — 실측 디버깅에서 도출된 9건이 모두 정당. 코드를 Design 으로 되돌리면 알려진 버그 재도입. **Design 단방향 업데이트 옵션** 이 PDCA 의 정상 결말 중 하나 |
| L9 | **2-라운드 PDCA 사이클의 정착 패턴**: round-1 (초기 Plan→Design→Do→Check) + round-2 (실측 디버깅 흡수 + 문서 동기화). round-2 가 없으면 코드/문서 lag 가 누적 |

---

## 8. 미해결 / 후속 작업 (Open Items)

| # | 항목 | 우선순위 | 비고 |
|---|------|:-------:|------|
| O1 | EVI-H100/SRG-300H **OSD Sel/Back 정확한 바이트** | 중 | `ViscaCodec.OsdSelect/OsdBack` 은 `8x 01 06 06 05/04 FF` placeholder. PDF 발췌 후 교정 또는 `OsdSupported=false` 가드 |
| O2 | EVI-H100 **PresetCount=6** 검증 (vs 16) | 낮음 | 현재 보수 값. 매뉴얼 발췌 후 한 줄 교정 |
| O3 | EVI-H100 **TiltSpeedMax=0x14** 검증 (vs 0x18) | 낮음 | 동상 |
| O4 | UI **RS-485 항목** 표시 정책 | 낮음 | 표시 유지 (향후 Pelco-D 모델 추가 시 활용) |
| O5 | **VISCA-IP vendor 포트 컨벤션** Design 명시 | 낮음 | v1.1 Design §14 에 이미 명시. 운영 가이드 보강 정도 |
| O6 | **SRG-300H 실기 검증** | 중 | 원격지 카메라 접근 가능 시점 1회 — Plan §10 R1 잔여분 |
| M1 | `CameraControl` 생성자 `RebuildRows`/`RebuildCamButtons` 호출 시드 안전성 | 매우 낮음 | `if (!DesignMode)` 가드 — 디자이너 라운드트립 무문제 보고됨 |

---

## 9. 통계

| 지표 | 값 |
|------|-----|
| Plan → Report 까지 경과 | 3일 (2026-05-04 ~ 2026-05-06) |
| 실작업 시간 (추정) | ~14h (Plan 1, Design v1.0 2.5, Do 4, debugging 5, Design v1.1 1, Report 0.5) |
| PDCA 사이클 라운드 | 2 |
| 신규 코드 라인 (.cs 추정) | ~3,200 |
| 신규 문서 라인 (md) | ~2,500 (Plan + Design v1.1 + Analysis + Report) |
| 코드 빌드 사이클 (Release) | 12+ 회 (모두 0 경고) |
| 실측 검증 모델 | 2/3 (66%) |
| Critical/Major Gap | 0 |
| 매칭율 (코드 기준) | 97% |

---

## 10. 다음 단계

| 단계 | 명령 | 목적 |
|------|------|------|
| Archive | `/pdca archive camera-remote-control` | 4종 문서를 `docs/archive/2026-05/camera-remote-control/` 로 이동, 인덱스 갱신 |
| (선택) Archive with summary | `/pdca archive camera-remote-control --summary` | `.pdca-status.json` 에 요약 보존 |
| 후속 micro-task | (수동) | O1 OSD PDF 발췌 / O6 SRG-300H 실기 검증 |
| 다음 feature | `/pdca plan {feature}` | 메트릭스/카메라 외 신규 외부 기기 — 동일 패턴 재사용 |

---

**완료 선언 (2026-05-06)**: camera-remote-control 기능이 Plan→Design v1.1→Do→Check (round-2)→Report 사이클을 완료했습니다. 코드 매칭율 97%, Critical/Major Gap 0건, 실측 2/3 모델 검증. `iterate` 단계 미진입, Design v1.1 단방향 동기화로 round-2 implementation-ahead 9건 모두 흡수.
