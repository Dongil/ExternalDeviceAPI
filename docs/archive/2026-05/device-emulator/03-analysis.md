# Analysis — device-emulator

> **문서 버전**: v1.0 (2026-05-06)
> **참조 Design**: [device-emulator.design.md](../02-design/features/device-emulator.design.md) v1.0
> **참조 Plan**: [device-emulator.plan.md](../01-plan/features/device-emulator.plan.md)

## Executive Summary

구현은 Design v1.0 을 충실히 따른다. 25개 파일 모두 명세된 namespace/시그니처/동작으로 존재하며 양 솔루션 (DeviceEmulator.sln + PN8080Controller.sln) 모두 Release 0/0 빌드 통과. 통합 테스트 5 시나리오 중 3 통과 (I1 CR-N300 UDP, I2 Pelco-D Serial, I3 FR-H50SN TCP — 추가로 FR-H50SN UDP 도 통과). 실측에서 6 건의 minor 구현 강화 (hardening) 발견 — 모두 비파괴, Design v1.1 흡수 후보.

## Match Rate

| 카테고리 | 점수 | 상태 |
|----------|:---:|:---:|
| Design Match (파일/시그니처/동작) | **97%** | ✅ |
| Architecture Compliance (5계층 device-side) | **100%** | ✅ |
| Convention Compliance (폴더/명명) | **100%** | ✅ |
| Field Validation (I1–I5) | **60%** (3/5, 의도된 OD) | ⚠️ |
| **종합** | **~95%** | ✅ |

## File Presence Verification

| Design §2 경로 | 구현 | 상태 |
|---|---|:---:|
| `Xeno.Framework.Camera/Protocols/Visca/ViscaCommand.cs` | 존재 | ✅ |
| `…/Visca/ViscaCommandParser.cs` | 존재 | ✅ |
| `…/Visca/ViscaReplyBuilder.cs` | 존재 | ✅ |
| `…/Pelco/PelcoDCommand.cs` | 존재 | ✅ |
| `…/Pelco/PelcoDCommandParser.cs` | 존재 | ✅ |
| `DeviceEmulator/DeviceEmulator.sln` | 존재 | ✅ |
| `DeviceEmulator/DeviceEmulator/{Program,App.config,app.manifest,Properties/AssemblyInfo}` | 4/4 | ✅ |
| `Core/{IDeviceEmulator,DeviceEmulatorBase,EmulatorRegistry,EmulatorOptions}.cs` | 4/4 | ✅ |
| `Transports/{Serial,Udp,Tcp}ServerTransport.cs` | 3/3 | ✅ |
| `Emulators/{EviH100,ViscaIp,PelcoDGeneric}Emulator.cs` | 3/3 | ✅ |
| `UI/MainForm.{cs,Designer.cs,resx}` | 3/3 | ✅ |
| `DeviceEmulator/README.md` | 존재 | ✅ |

총 25개 파일 모두 명세 일치.

## Gaps

### Critical
*없음.*

### Major
*없음.*

### Minor — 구현 hardening (Design v1.1 흡수 후보)

| # | 종류 | 항목 | Design 위치 | 구현 위치 | 비고 |
|---|---|---|---|---|---|
| M1 | 💡 추가 | `ViscaIpEmulator.OnViscaIp` 가 wrap 감지 가드 `data[0]==0x01 && (data[1]==0x00 || data[1]==0x10)` 추가, `wrapped` 플래그를 `SendInner` 에 전달 | §7.2 (단순 `data.Length>=8` 만) | `ViscaIpEmulator.cs:76` | RESET (type 0x0200) 등 제어 패킷이 raw path 로 빠짐. 결과: emulator 가 RESET 에 raw `90 41 FF` 응답 → controller drain 흡수. 무해하지만 spec-correct 아님. **권장**: v1.1 §7.2 에 가드 명시 OR 별도 OD 로 RESET ack (type 0x0201) 처리 추가. |
| M2 | 💡 추가 | `ViscaCommandParser.Parse` 가 비-VISCA byte (예: RESET) 를 Address=0 으로 반환, `ViscaReplyBuilder.Ack(0)` 가 [1,7] clamp 으로 0x90 로 보정 | §4.3 clamp 자체는 명시, emergent 상호작용 미명시 | `ViscaCommandParser.cs:15-17`, `ViscaReplyBuilder.cs:13` | 무해한 우연 — clamp 가 모든 invalid src addr → 0x90 보장. v1.1 §4.3 한 줄 추가 권장. |
| M3 | ⚠️ deviation (cosmetic) | `MainForm` Model 콤보가 `"Brand Model"` 저장 (e.g., "Sony SRG-300H") + entry 해소 시 `(x.Brand+" "+x.Model)==m` 비교 | §8.3 sample 은 Model 만 | `MainForm.cs:78, 111` | Sony vs Canon 이름 충돌 방지. 표시는 Design 일치, 저장만 다름. **권장**: §8.3 sample 업데이트. |
| M4 | 💡 추가 | `MainForm` 이 emulator async 콜백 → UI 스레드 마샬링 위해 `BeginInvoke` 사용 | §8.4 thread-safety 미언급 | `MainForm.cs:123` | 표준 WinForms 관행. 누락 시 cross-thread 예외. **권장**: v1.1 §8.4 에 1 줄 추가. |
| M5 | 💡 추가 | `UdpServerTransport.SendReply` 가 `sourceEndpoint==null` 시 early-return | §5.2 미언급 | `UdpServerTransport.cs:60` | 방어적 가드. 사소함. |
| M6 | 💡 추가 | `MainForm` 이 baud combo 8 표준값 + COM 포트 자동 enum + `MaxLogLines=2000` ring buffer | §8 baud list/log capacity 미명시 | `MainForm.cs:14, 56-67` | UX 구체화. **권장**: v1.1 §8.2 에 명시. |

### Field Validation (Design §11.3)

| # | 시나리오 | 상태 | 비고 |
|---|---|:---:|---|
| I1 | CR-N300 UDP wrapped (22 명령) | ✅ | Sony VISCA-over-IP, sequence mirror 정상 |
| I2 | Pelco-D Serial RS-232 (31 명령, com0com pair) | ✅ | byte-correct fire-and-forget |
| I3 | FR-H50SN TCP raw (~50 명령) | ✅ | + FR-H50SN UDP wrapped 36 명령 추가 통과 (보너스) |
| I4 | Inject NAK | ⏳ 미실측 | §14 OD 항목 |
| I5 | Latency 500ms | ⏳ 미실측 | §14 OD 항목 |

추가로 Pelco-D 8방향 모든 비트 조합 정확 식별: PT UpRight (0x0A), UpLeft (0x0C), DownRight (0x12), DownLeft (0x14), Zoom Tele (0x20), Wide (0x40), Preset Set 1/2.

### Confirmed-correct 항목 (gap 없음)

- `EmulatorRegistry.All` → `IReadOnlyList<Entry>` 5 entry 전부 §6.4 와 verbatim 일치 (DeviceType/Brand/Model/SupportedTransports/Description/Create factory).
- `EmulatorOptions` 기본값: `LatencyMs=5`, `ConsumeOnNextCommand=true`, inject 플래그 모두 `false` — §6.1 일치.
- `DeviceEmulatorBase.SendReplyAsync` 흐름: latency `Task.Delay` → `InjectTimeout` consume-and-skip → invoke sender — §6.3 일치.
- `ViscaReplyBuilder.ReplyHeader` 공식 `((a+8)<<4)` + [1,7] clamp — §4.3 일치.
- `PelcoDCommandParser` checksum 검증, Stop 감지, Extended preset (0x03/0x05/0x07), Iris/Focus/Zoom/PT 비트 마스크 — §4.4 일치.
- `UdpServerTransport.ReplyMode` enum {SonyConvention=0, SourcePortMirror=1}, default SonyConvention, SonyReplyPort=52381 — §5.2 일치.
- `TcpServerTransport` 단일 클라이언트 모델 (v1 caveat 명시) — §5.3 일치.
- `ViscaIpEmulator` UDP 기본 52381, TCP 기본 5678 — §7.2 일치.
- `PelcoDGenericEmulator` UDP/TCP 기본 4001, no-reply — §7.3 일치.

## Open Items 인계 (Design §14)

| # | 항목 | 해소 상태 |
|---|---|---|
| OD1 | Inquiry 응답 (`InquiryPanTiltStatus`/`InquiryZoomPosition`) | **As designed** — 무응답, Unknown 로깅. v1 수용. |
| OD2 | Sony reply socket 번호 (1 vs 2) | **As designed** — socket 1 default 인자로 hardcoded. |
| OD3 | UI Address 필터 | **v2 deferred** (계획 그대로). |
| OD4 | 멀티-인스턴스 port 충돌 UX | **부분 처리** — Listen 실패 시 MessageBox (`MainForm.cs:150`) 노출. v2 친화적 경고 검토. |

신규 OD 후보:
| # | 항목 | 우선도 |
|---|---|:---:|
| OD5 | RESET (VISCA-IP type 0x0200) → RESET ACK (type 0x0201) 정식 응답 | 낮음 (현 상태로 controller 정상 동작) |
| OD6 | Inject NAK / Latency 500ms 실측 (I4, I5) | 중간 (사용자 환경 준비 시) |
| OD7 | EVI-H100 Serial VISCA 실측 (com0com 또는 USB-RS422 어댑터 필요) | 중간 |
| OD8 | Pelco-D UDP/TCP 실측 (SourcePortMirror 모드 검증) | 낮음 |

## Recommendations

### 즉시 조치
*없음.* 검증된 3 시나리오 (+ 보너스 1) 에 대해 production-ready.

### 100% 선언 전
1. I4 (Inject NAK) — 체크박스 토글, PT 명령 송신, controller 측 `RaiseError` 발화 확인.
2. I5 (Latency 500ms) — 슬라이더 설정, 명령 송신, controller 측 응답 지연 인식 확인.

### Design v1.1 흡수 후보
1. **§7.2**: wrap 감지 가드 + `wrapped` 플래그 plumbing 명시. RESET 처리 정책 결정 (현 상태 vs OD5 처리).
2. **§4.3**: `Ack(0)` clamp → 0x90 동작 1 줄 추가 (Unknown 명령 경로 커버).
3. **§8.2/§8.3**: Model 콤보 sample 을 `"Sony SRG-300H"` (Brand+Model) 로 갱신, baud-rate 8 값 + `MaxLogLines=2000` 명시.
4. **§8.4**: `BeginInvoke` 마샬링 1 줄 추가.

### Promote-to-features
- `wrap`-aware `OnViscaIp` 설계는 Sony-only 를 넘어선 의미 있는 일반화 — Sony/Canon/FR 변형을 per-model branching 없이 한 emulator 가 커버하는 contract 로 격상.

## 결론

Match Rate 95% — Report 진입 가능. 미실측 시나리오 (I4 Inject NAK, I5 Latency) 는 OD 로 흡수, Design v1.1 흡수 6건은 report 단계에서 design 갱신 후 archive.

**문서 상태**: Final
