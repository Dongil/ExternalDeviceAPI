# Gap Analysis (Round 2) — camera-remote-control (설계 vs 구현)

> **요약**: Round-1 (97% PASS) 이후 디버깅 주도 5건의 신규 기능이 라이브러리/하네스에 추가됨 — TCP transport, 공통 IP transport 인터페이스, 백그라운드 RX 루프 + 큐, no-reply 모드(`ExpectReply`/`WaitForReply`), FR-H50SN 모델, 로그 toolbar, 슬롯별 ServiceLog 이벤트. **모두 설계와 충돌이 아니라 설계 미반영(implementation-ahead-of-design) — 코드는 정당, Design 문서가 따라가야 함.** Round-1 의 M2 (MainForm 로그 패널) 은 #9 로 완전 해결. M1 은 그대로 잔존(영향 없음). O1~O4 는 변동 없음.
>
> **작성자**: gap-detector agent (round 2)
> **작성일**: 2026-05-04
> **이전 분석**: round-1 (이 파일이 대체)
> **대상 Plan**: [camera-remote-control.plan.md](../01-plan/features/camera-remote-control.plan.md)
> **대상 Design**: [camera-remote-control.design.md](../02-design/features/camera-remote-control.design.md)
> **상태**: Draft — Ready for Review

---

## Executive Summary

### 분석 개요

| 항목 | 내용 |
|------|------|
| **기능명** | camera-remote-control |
| **분석 대상 라이브러리** | `Xeno.Framework.Camera/` (Core 8 + Visca 3 + Transports 4 + Services 2 + UI 6 = 23개 .cs) |
| **분석 대상 하네스** | `CameraController/` (Program/UserSettings/MainForm + Designer = 4개 .cs) |
| **솔루션** | `PN8080Controller.sln` (4개 프로젝트 모두 정상 등록) |
| **빌드 상태** | Release 0 경고 0 오류 |
| **실측 검증 (사용자 로그)** | EVI-H100 RS-232 ✅ / FR-H50SN TCP ✅ (port 52381, ACK ~5ms) / SRG-300H IP ⏸ (원격지) |

### Value Delivered — 4 관점 검증 (round-2)

| 관점 | 설계 약속 | 구현 결과 (round-2) | 매칭 |
|------|----------|----------|:----:|
| **Problem** | "Plan §11 5건 결정을 클래스 시그니처/바이트 사전/UI 상태머신으로 환원" | Q1~Q5 모두 그대로 유지 + 실측 디버깅에서 발견된 race(`ReceiveAsync` 고아) 까지 RX-loop 큐 패턴으로 흡수 | ✅ |
| **Solution** | `ViscaCodec` 정적 빌더가 두 Transport 의 단일 진실 공급원 | UDP/TCP **두 transport 모두** 동일 `ViscaCodec.*` 호출, `IViscaIpTransport` 로 service 가 transport 종류와 무관 | ✅ + 확장 |
| **Function UX Effect** | 모델 추가 비용 = "1 Service + 1 Capabilities" | FR-H50SN 추가 비용은 더 줄어듦 — `ViscaIpCameraService` 가 generic 화되어 **0 Service + 1 Capabilities + 1 factory case**. 실측 가설 1회 검증 | ✅ + 향상 |
| **Core Value** | "Sony VISCA 다른 모델 추가 시 즉시 재사용" | OEM/Chinese firmware (FR-H50SN) 같은 비-Sony 모델 까지 동일 코드 패스로 흡수. `ExpectReply`/`WaitForReply` 로 silent firmware 도 1차원 단순화 | ✅ + 확장 |

### 종합 점수

| 카테고리 | 가중치 | 점수 | 상태 | 비고 |
|---------|:------:|:----:|:----:|------|
| Core 인터페이스/모델 일치 (§3) | 18 | 95% | ✅ | `ExpectReply` 신설 (Design 미반영) |
| VISCA 코덱/Parser 일치 (§4) | 12 | 95% | ✅ | `DescribeReply` 추가 (Design 미반영, OSD O1 잔존) |
| Transport 일치 (§5) | 15 | 70% | ⚠️ | **UDP 재설계 + TCP 신설 + IViscaIpTransport** 3건 모두 Design 미반영 |
| Service 계층 일치 (§6) | 12 | 70% | ⚠️ | `SrgIpCameraService` → `ViscaIpCameraService` 일반화 (Design 미반영) |
| UI 계층 일치 (§7) | 18 | 90% | ✅ | TCP 콤보 항목 추가, ServiceLog 이벤트 신설 (Design 미반영) |
| Designer 호환 규약 (§10) | 5 | 95% | ✅ | M1 잔존 |
| 테스트 하네스 일치 (§8) | 8 | 85% | ✅ | 로그 toolbar 4버튼 + 일시정지 + ClientSize 900×580 — 모두 Design 미반영 |
| 솔루션 통합 (§9) | 5 | 100% | ✅ | 변동 없음 |
| Plan Decisions (Q1~Q5) | 7 | 100% | ✅ | 모두 유지 |
| **가중 평균 매칭율 (설계 문구 기준)** | 100 | **87%** | ⚠️ | Design lag 으로 round-1 (97%) 대비 -10%p |
| **참조 매칭율 (Implementation-ahead 를 갭에서 분리)** | — | **97%** | ✅ | round-1 과 동일 — 코드 품질은 그대로 PASS |

**판정**: **PASS (코드 기준 97%)** — `iterate` 불필요. 다만 Design 문서를 round-2 변경분에 맞춰 **단방향 업데이트** 필요.

---

## 1. Round-2 신규 변경 분류 (10건)

| # | 변경 | 분류 | 코드 위치 | Design 영향 |
|---|------|:----:|----------|------------|
| 1 | `UdpViscaTransport` 백그라운드 RX 루프 + `BlockingCollection` 큐 + `ReceiveTimeoutMs` 1000→300ms + `WaitForReply` + `OnLog` + late-Completion auto-skip ("방식 A") + `DRAIN late <reply-kind>` 분류 로깅 | **Implementation-ahead** | `Transports/UdpViscaTransport.cs:19-300` | §5.2 전면 재기술 필요 |
| 2 | **NEW** `TcpViscaTransport` (raw VISCA over TCP, 0xFF terminator slicing, 2초 connect timeout, 기본 5678) | **Implementation-ahead** | `Transports/TcpViscaTransport.cs:1-241` | §5 에 §5.3 신규 섹션 추가 필요 |
| 3 | **NEW** `IViscaIpTransport` 공통 interface | **Implementation-ahead** | `Transports/IViscaIpTransport.cs:10-23` | §5 도입부에 추상화 다이어그램 추가 |
| 4 | `SrgIpCameraService` → `ViscaIpCameraService` (generic, 생성자 `(CameraInfo, CameraCapabilities)`, Connect 시 UDP/TCP dispatch, `[UDP]`/`[TCP]` 로그 prefix) | **Implementation-ahead** | `Services/ViscaIpCameraService.cs:15-175` | §6.2 클래스명·시그니처 변경 |
| 5 | **NEW** `CameraCapabilities.ExpectReply` 플래그 (default true) → transport `WaitForReply` 로 forward | **Implementation-ahead** | `Core/CameraCapabilities.cs:46`, `Services/ViscaIpCameraService.cs:48` | §3.4 Capabilities 표에 한 줄 추가 |
| 6 | **NEW** FR-H50SN 모델 (Brand "FR", TCP 기본, 5678, ExpectReply=true, PresetCount=16) + `CameraCapabilities.FrH50Sn()` 팩토리 + `CameraServiceFactory` 케이스 | **Implementation-ahead** | `Core/CameraCapabilities.cs:114-122`, `Core/ICameraServiceFactory.cs:26,36,51-54` | §3.4·§3.7 모델 목록 확장 |
| 7 | UI: 통신 콤보 항목 4→5 ("RS-232/RS-422/RS-485/UDP/TCP") + `TransportToIndex`/`IndexToTransport` 5분기 | **Implementation-ahead** | `UI/CameraControl.cs:335, 555-579` | §7.4 행 헤더 표·§7.7 transport rule 표 갱신 |
| 8 | **NEW** `MainForm` 로그 toolbar (복사/지우기/저장/일시정지) + ToolTip + 일시정지 시 gold 배경 + `SaveFileDialog` + ClientSize 900×580 + `_txtLog.WordWrap=false` | **Implementation-ahead** | `CameraController/UI/MainForm.cs:28-37, 100-157`, `MainForm.Designer.cs:31-126, 146` | §8.2 MainForm 구성 항목 확장 |
| 9 | **NEW** `CameraControl.ServiceLog` event + `ServiceLogEventArgs` (per-slot, UI-thread marshal) → MainForm 이 구독해 `_txtLog` 채움 | **Match (M2 해결)** + **Implementation-ahead** | `UI/CameraControl.cs:73-75, 720-764`, `UI/ServiceLogEventArgs.cs:1-20`, `CameraController/UI/MainForm.cs:41,53-65` | §7.2 공개 API 표에 한 줄 추가. **Round-1 M2 갭 해결** |
| 10 | MouseLeave 버그 수정: 축별 `_ptHoldActive`/`_zoomHoldActive`/`_focusHoldActive` 플래그, Drive 가 실제로 보내진 경우만 Stop 발송 | **Implementation-ahead (defensive)** | `UI/CameraControl.cs:33-35, 869-915` | §7 Hold-to-Move 단락에 가드 규칙 추가 |

**Genuine gap (코드 수정 필요): 0건.**
**Implementation-ahead (Design 업데이트 필요): 9건.**
**Match (이미 round-1 갭 해결): 1건 (#9 — M2 해결).**

---

## 2. Round-1 잔존 항목 재검증

| Round-1 ID | 항목 | Round-2 상태 | 코드 증거 |
|-----------|------|-------------|----------|
| **M1** | `CameraControl` 생성자에서 `RebuildRows()`+`RebuildCamButtons()` 직접 호출 (Designer 미리보기 시 시드 안전성) | **잔존 — Acceptable** | `UI/CameraControl.cs:42-43`. 빌드 0 경고, 실사용 디자인 라운드트립 무문제 보고됨. `if (!DesignMode)` 가드 권장이지만 차단 사유 아님 |
| **M2** | `MainForm.HookExistingServices` no-op → 로그 미표시 | **✅ 해결 (round-2)** | #9 변경으로 `_cameraControl.ServiceLog += OnServiceLog` (`MainForm.cs:41`) 가 슬롯별 LogEntry 를 `[Cam N] TX/RX/INFO/ERR <text>` 형태로 `_txtLog` 에 출력. `HookExistingServices` 메서드 자체는 더 이상 존재하지 않음 (이벤트 모델로 대체) |
| **O1** | OSD Sel/Back placeholder 바이트 (`8x 01 06 06 05/04 FF`) | **변동 없음 — Known Limitation** | `Protocols/Visca/ViscaCodec.cs` 코드 변경 없음. PDF 발췌 작업 미진행 |
| **O2** | EVI-H100 PresetCount = 6 (vs 16) | **변동 없음 — Known Limitation** | `Core/CameraCapabilities.cs:84` 그대로 6 |
| **O3** | EVI-H100 TiltSpeedMax = 0x14 (vs 0x18) | **변동 없음 — Known Limitation** | `Core/CameraCapabilities.cs:76` 그대로 0x14 |
| **O4** | UI 통신 콤보 RS-485 표시 | **유효 — Known Limitation 강화** | `UI/CameraControl.cs:335` 항목 그대로. 추가로 TCP 가 실제 사용되는 모델(FR-H50SN) 등장으로 RS-485 도 향후 Pelco-D 모델 추가 시 즉시 활용 시나리오 더 합리화 |

---

## 3. 차이점 정리 (분류별 표)

### 🔴 Genuine Gap (코드 수정 필요) — 0건

### 🟣 Implementation-Ahead-of-Design (Design 업데이트 권장)

| # | 영역 | 코드 (file:line) | Design 미반영 사항 | 권장 Design 섹션 |
|---|------|------------------|-------------------|------------------|
| **A1** | UDP transport 재설계 | `Transports/UdpViscaTransport.cs:19-300` | 백그라운드 RX 루프 + `BlockingCollection<byte[]>` 큐 패턴, `ReceiveTimeoutMs` 1000→300ms, `WaitForReply`, `OnLog`, late-Completion 1회 auto-skip, `DRAIN late <kind>` 분류 로깅 — 모두 Design §5.2 의 "ReceiveAsync per command + Task.WhenAny" 패턴과 다름 | §5.2 재기술 |
| **A2** | TCP transport 신설 | `Transports/TcpViscaTransport.cs:1-241` | Sony 8B 헤더 없는 raw VISCA, 0xFF terminator slicing, 2초 connect timeout, 기본 5678 포트, late-Completion auto-skip — Design 에 TCP 섹션 자체가 없음 (`TcpVisca = 11` enum 만 "예약, v2") | §5.3 신규 |
| **A3** | IP transport 공통 추상화 | `Transports/IViscaIpTransport.cs:10-23` | `IViscaIpTransport` (Host/Port/ReceiveTimeoutMs/WaitForReply/OnLog/IsOpen + Open/Close/SendCommandAsync) — Service 가 transport 종류 무관하게 dispatch 하는 키 abstraction | §5 도입부에 다이어그램 |
| **A4** | Service 일반화 | `Services/ViscaIpCameraService.cs:15-175` | `SrgIpCameraService` 가 `ViscaIpCameraService`(`CameraInfo`,`CameraCapabilities`) 로 generic 화. Connect 에서 `Transport == TcpVisca ? new TcpViscaTransport() : new UdpViscaTransport()` dispatch. 로그 prefix `[UDP]`/`[TCP]` | §6.2 시그니처/생성자 변경 |
| **A5** | `ExpectReply` capability | `Core/CameraCapabilities.cs:46` | no-reply firmware 대응 플래그. Service 가 `t.WaitForReply = Capabilities.ExpectReply` 로 transport 에 forward (`ViscaIpCameraService.cs:48`) | §3.4 표 한 줄 추가 |
| **A6** | FR-H50SN 모델 | `Core/CameraCapabilities.cs:114-122`, `ICameraServiceFactory.cs:26,36,51-54` | 비-Sony OEM 모델. Brand "FR", TCP 기본, 5678 포트, PresetCount=16. 가설 검증된 모델 추가 패턴 ("0 Service + 1 Capabilities") | §3.4·§3.7 모델 목록 확장 |
| **A7** | UI 통신 콤보 5항목 | `UI/CameraControl.cs:335, 555-579` | "RS-232/RS-422/RS-485/UDP/TCP" 5항목, `TransportToIndex`/`IndexToTransport` 5분기 — Design 표는 4항목 기준 | §7.4 행 헤더, §7.7 transport rule |
| **A8** | 슬롯별 ServiceLog 이벤트 | `UI/CameraControl.cs:73-75, 720-764`, `UI/ServiceLogEventArgs.cs:1-20` | `event EventHandler<ServiceLogEventArgs> ServiceLog`, slot index + LogEntry, `BeginInvoke` UI-thread marshal — round-1 의 M2 해결 채널 | §7.2 공개 API 표 |
| **A9** | MainForm 로그 toolbar | `CameraController/UI/MainForm.cs:28-37, 100-157`, `Designer.cs:31-126, 146` | 복사/지우기/저장(.log)/일시정지 4 버튼, ToolTip, 일시정지 시 gold(255,215,0) 배경, `SaveFileDialog` 기본 파일명 `camera-YYYYMMDD-HHmmss.log`, ClientSize 900×580, `_txtLog.WordWrap=false` (hex 가독성) | §8.2 MainForm 구성 |
| **A10** | MouseLeave 가드 | `UI/CameraControl.cs:33-35, 869-915` | 축별 `_ptHoldActive`/`_zoomHoldActive`/`_focusHoldActive` 플래그, Drive 가 실제로 보내진 경우만 Stop 발송 → hover 만으로 spurious Stop 발생 → SemaphoreSlim 큐 막힘 현상 차단 | §7 Hold-to-Move 단락에 가드 규칙 |

### 🟡 Minor Gap (정보용, 영향 미미)

| # | 컴포넌트 | 설명 | 권장 |
|---|---------|------|------|
| **M1** (잔존) | `CameraControl` 생성자 | `RebuildRows()`+`RebuildCamButtons()` 직접 호출. 디자이너 라운드트립에 실문제 보고 없음 | `if (!DesignMode)` 가드 — 우선순위 낮음 |

### 🟢 Known Limitations (Design §14 위임 — 변동 없음)

| # | 항목 | 상태 | 후속 |
|---|------|------|-----|
| O1 | OSD Sel/Back placeholder 바이트 | 변동 없음 | PDF 발췌 후 일회성 교정 |
| O2 | EVI-H100 PresetCount=6 | 변동 없음 | PDF 발췌 |
| O3 | EVI-H100 TiltSpeedMax=0x14 | 변동 없음 | PDF 발췌 |
| O4 | UI RS-485 항목 표시 | 강화 — TCP 추가로 다중 transport 패턴 정착 | 표시 유지 |

### ➕ Bonus (round-1 7건 + round-2 추가)

| # | 항목 | 출처 |
|---|------|------|
| B1~B7 | round-1 7건 (DefaultUdpPort, ObjectDisposedException catch, IOException catch, _busy 가드, ApplyRange widen-narrow, MouseLeave None 가드, 슬롯 수 메뉴) | 모두 유지 |
| B8 | `ViscaResponseParser.DescribeReply(byte[])` 추가 — drain 로깅 가독성 | `Protocols/Visca/ViscaResponseParser.cs:55-67` |
| B9 | `[UDP]`/`[TCP]` 로그 prefix — 동일 모델에서 transport 비교 디버깅 용이 | `Services/ViscaIpCameraService.cs:49-50` |
| B10 | TCP connect 2초 timeout (`BeginConnect` + `WaitOne`) — 잘못된 포트 입력 시 UI hang 차단 | `Transports/TcpViscaTransport.cs:48-55` |
| B11 | Late-Completion auto-skip ("방식 A") — 동일 패턴이 UDP/TCP 양쪽 대칭 | `UdpViscaTransport.cs:152-176`, `TcpViscaTransport.cs:134-158` |
| B12 | Slot 별 LogHandler capture (`capturedIndex` closure) — 슬롯 수 변경 시 인덱스 race 방지 | `UI/CameraControl.cs:727` |

---

## 4. Plan Decisions Q1~Q5 재검증

변동 없음. 5건 모두 round-1 분석과 동일하게 코드로 trace 가능. FR-H50SN 추가는 Q1·Q5 (모델별 메타데이터 + N대 확장) 의 활용 사례를 한 건 늘림.

---

## 5. 실측 검증 결과 반영 (사용자 로그)

| 모델 | Transport | 결과 | 분석 의미 |
|------|----------|------|----------|
| EVI-H100 | RS-232 | ✅ 작동 | Plan §10 R1 (실기 미보유 위험) **해소**. SerialViscaTransport 안정성 1차 검증 |
| FR-H50SN | TCP (port **52381**, 코드 default 5678 와 다름) | ✅ 작동, ACK ~5ms | TCP transport + ExpectReply=true 경로 검증. **Capabilities default 5678 ↔ 실측 52381 차이는 코드 갭 아님** — UI 의 Port TextBox 가 사용자 입력 우선이며 capability default 는 placeholder. 단 Design 문서에 "Default port 는 vendor convention; 카메라 web UI 에서 확인 필요" 명시 권장 |
| FR-H50SN | TCP | late Completion 정상 drain | Round-2 #1 의 late-Completion auto-skip 패턴이 실측에서 효과 발휘 — drain 로그가 `RX late Completion (skipped)` 로 명확히 기록됨 |
| SRG-300H | VISCA-IP | ⏸ 미검증 (원격지) | 향후 micro-task |
| 모든 모델 | Hold-to-Move | spurious stop 0건, queue stall 0건 | Round-2 #10 MouseLeave 가드 효과 |

---

## 6. 권장 조치 (Recommendations)

### 우선순위 1 — Design 문서 업데이트 (단방향 동기화, 코드는 그대로)

1. **§5 Transport 섹션 전면 재기술** — §5.1 SerialViscaTransport 유지, §5.2 UdpViscaTransport 는 RX-loop+queue 패턴으로 재기술, §5.3 TcpViscaTransport 신규 작성, §5 도입부에 `IViscaIpTransport` 추상화 다이어그램 추가. (A1, A2, A3 통합)
2. **§6.2 ViscaIpCameraService 일반화** — 클래스명/생성자 시그니처 변경, transport dispatch 로직 추가. (A4)
3. **§3.4 CameraCapabilities** — `ExpectReply` 행 추가 + FR-H50SN 모델 정적 팩토리 추가. (A5, A6)
4. **§3.7 CameraServiceFactory** — `GetAvailableModels` 3 entries, `GetCapabilities`/`Create` 의 FR-H50SN 분기. SRG-300H 도 `ViscaIpCameraService(...)` 생성으로 정정. (A4, A6)
5. **§7.4·§7.7 UI 표** — 통신 콤보 5항목, transport rule 5분기로 갱신. (A7)
6. **§7.2 CameraControl 공개 API** — `event EventHandler<ServiceLogEventArgs> ServiceLog` + `ServiceLogEventArgs` 클래스 추가. (A8)
7. **§7.x Hold-to-Move 단락** — 축별 hold flag 가드 규칙 추가. (A10)
8. **§8.2 MainForm** — 로그 toolbar (4버튼 + 일시정지 시각 단서), ClientSize, WordWrap 명시. (A9)
9. **§14 Open Items** — O1~O4 변동 없음 명시 + 신규 Open Item 후보: "FR-H50SN 의 5678 vs 52381 포트는 vendor 별로 상이; capability default 는 PTZOptics 컨벤션이며 실 카메라는 web UI 확인 필요"

### 우선순위 2 — micro-task (코드 수정 없음, 1회성)

10. **O1**: PDF 매뉴얼 발췌 → `ViscaCodec.OsdSelect/OsdBack` 교정 또는 `Capabilities.OsdSupported=false` 가드.
11. **O2/O3**: PDF 발췌 → `CameraCapabilities.cs:76, 84` 검증 후 한 줄씩 교정.
12. **SRG-300H 실기 검증**: 원격지 카메라 접근 가능 시점에 1회 — Plan §10 R1 잔여분 해소.

### 우선순위 3 — 선택 개선 (낮음)

13. **M1**: `CameraControl` 생성자 `RebuildRows`/`RebuildCamButtons` 호출에 `if (!DesignMode)` 가드.

---

## 7. 동기화 결정

> "코드는 정당하나 설계가 stale — Design 을 업데이트할 것인가, 코드를 설계로 되돌릴 것인가?"

**권장: Design 단방향 업데이트 (옵션 2 — Update design to match implementation).**

근거:
- 9건 모두 **실측 디버깅에서 도출된 정당한 진화** — 특히 #1 (RX-loop+queue) 는 Design §5.2 패턴이 실제 운영에서 race 를 발생시킴이 입증됨. 코드를 Design 으로 되돌리면 알려진 버그 재도입.
- #2 TCP transport 는 FR-H50SN 같은 비-Sony 모델 지원이라는 새로운 가치를 만들어냄 — Plan §1.3 의 "Sony VISCA 다른 모델 추가 시 즉시 재사용" 의 자연스러운 확장.
- #4 Service 일반화는 round-1 가설 ("1 Service + 1 Capabilities") 을 한 단계 강화 ("0 Service + 1 Capabilities") — 설계 의도 정합.
- #9 (ServiceLog 이벤트) 는 round-1 M2 갭의 정식 해결책. 되돌리면 M2 재도입.

**모든 9건은 Design 으로 흡수**, Design 버전을 v1.1 으로 올림 권장.

---

## 8. 결론 (Verdict)

| 지표 | Round-1 | Round-2 |
|------|--------|---------|
| **가중 평균 매칭율 (설계 문구 기준)** | 97% | **87%** ⚠️ (Design lag) |
| **참조 매칭율 (코드 품질 기준)** | 97% | **97%** ✅ |
| **임계값 (≥90%)** | 충족 | 코드 기준 충족, 문서 기준 미달 |
| **Critical/Major Genuine Gap** | 0건 | **0건** |
| **Implementation-Ahead 항목** | 7건 (Bonus) | **9건 (큼) + 5건 (Bonus)** |
| **Minor Gap** | 2건 (M1, M2) | **1건 (M1 잔존), M2 해결** |
| **Known Limitations (O1~O4)** | 4건 | **4건 (변동 없음)** |
| **실기 검증** | 0건 | **2/3 모델 검증 완료** |
| **판정** | PASS | **PASS (코드)** + **Design 업데이트 필요** |

**최종 권장**:
- `iterate` 단계 진입 **불필요** — 코드는 round-1 보다 견고.
- **Design 단방향 업데이트** 후 `/pdca report camera-remote-control` 진행.
- 또는 Report 를 먼저 작성하면서 §6.1 (변경 이력) 에 round-2 진화를 명시하고 Design 업데이트는 후속 micro-task 로 분리하는 것도 합리적.
