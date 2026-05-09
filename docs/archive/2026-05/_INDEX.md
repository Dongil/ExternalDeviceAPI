# Archive Index — 2026-05

## camera-remote-control

> PTZ 카메라 통합 제어 프레임워크 (`Xeno.Framework.Camera` Library + `CameraController` 테스트 하네스)

| 항목 | 내용 |
|------|------|
| **기능명** | camera-remote-control |
| **시작** | 2026-05-04 |
| **완료 / 보관** | 2026-05-06 |
| **PDCA 라운드** | 2 (round-1 초기 + round-2 디버깅 흡수) |
| **매칭율** | 97% (코드 품질 기준) |
| **신규 파일** | 라이브러리 23개 + 테스트 하네스 5개 = 28 |
| **지원 모델** | Sony EVI-H100 (RS-232/422 VISCA), Sony SRG-300H (VISCA-IP UDP), FR-H50SN (raw VISCA-IP TCP) |
| **실측 검증** | 2/3 (EVI-H100 ✅, FR-H50SN TCP ✅, SRG-300H ⏸ 원격지) |
| **Critical/Major Gap** | 0 |
| **빌드** | Debug + Release 양쪽 0 경고 0 오류 |

### 보관 문서

| 단계 | 파일 | 분량 |
|------|------|-----|
| Plan | [`camera-remote-control/01-plan.md`](camera-remote-control/01-plan.md) | ~340 라인 |
| Design v1.1 | [`camera-remote-control/02-design.md`](camera-remote-control/02-design.md) | ~1,900 라인 |
| Analysis (round-2) | [`camera-remote-control/03-analysis.md`](camera-remote-control/03-analysis.md) | ~270 라인 |
| Report | [`camera-remote-control/04-report.md`](camera-remote-control/04-report.md) | ~320 라인 |

### 핵심 산출 코드 (보관 후에도 활성)

- `Xeno.Framework.Camera/` (Library, GUID `{A2C3D4E5-F6B7-48C9-9A1D-2E3F4B5C6A7B}`)
- `CameraController/` (WinExe, GUID `{B3D4E5F6-A7B8-49CA-AB2E-3F4A5B6C7D8E}`)
- `PN8080Controller.sln` 에 등록

### 후속 작업 (open items)

- O1 — EVI-H100/SRG-300H OSD Sel/Back 정확한 바이트 (PDF 매뉴얼 발췌)
- O2 — EVI-H100 PresetCount 검증 (6 vs 16)
- O3 — EVI-H100 TiltSpeedMax 검증 (0x14 vs 0x18)
- O4 — UI RS-485 표시 정책 (현 표시 유지)
- O5 — VISCA-IP vendor 포트 컨벤션 운영 가이드 보강
- O6 — SRG-300H 실기 검증 (원격지 카메라 접근 가능 시)
- M1 — `CameraControl` 생성자 시드 안전성 (`if (!DesignMode)` 가드)

---

## pelco-d-and-udp-fix

> VISCA-over-IP UDP 수신 critical 버그 수정 + Pelco-D 신규 protocol family 추가 + Canon CR-N300 모델 등록 — `Xeno.Framework.Camera` Library 확장

| 항목 | 내용 |
|------|------|
| **기능명** | pelco-d-and-udp-fix |
| **시작 / 완료 / 보관** | 2026-05-06 (단일 세션) |
| **PDCA 라운드** | 1 (Design v1.0 → Do → 실측 → Design v1.1 단방향 흡수) |
| **매칭율** | **99% (코드)** / **99% (설계, v1.1 흡수 후)** |
| **신규 파일** | 5개 (`PelcoConstants`, `PelcoDCodec`, `PelcoDCameraService`, **`ViscaUdpHub` ⭐ 멀티-카메라 demux 싱글턴**) |
| **수정 파일** | 6개 (`UdpViscaTransport` 전면 재작성, `SerialViscaTransport` WaitForReply, `CameraCapabilities`, Factory, `CameraControl`, csproj) |
| **신규 모델** | Canon CR-N300, Pelco-D Generic |
| **누적 모델** | **5종** (EVI-H100, SRG-300H, FR-H50SN, **CR-N300**, **Pelco-D Generic**) |
| **누적 protocol** | 2종 (Sony VISCA, Pelco-D) |
| **누적 transport** | 5종 (RS-232/422/485, UDP-VISCA, TCP-VISCA, UDP-Raw, Serial) |
| **실측 검증** | **3/3 모델** (Canon UDP ✅, FR TCP+UDP ✅, Pelco-D RS-232 ✅) **+ 3 카메라 × 3 protocol × 3 transport 동시 운용** ✅ |
| **Critical/Major Gap** | 0 |
| **빌드** | Debug + Release 양쪽 0 경고 0 오류 |

### 보관 문서

| 단계 | 파일 | 분량 |
|------|------|-----|
| Plan | [`pelco-d-and-udp-fix/01-plan.md`](pelco-d-and-udp-fix/01-plan.md) | ~300 라인 |
| Design v1.1 | [`pelco-d-and-udp-fix/02-design.md`](pelco-d-and-udp-fix/02-design.md) | ~960 라인 |
| Analysis | [`pelco-d-and-udp-fix/03-analysis.md`](pelco-d-and-udp-fix/03-analysis.md) | ~280 라인 |
| Report | [`pelco-d-and-udp-fix/04-report.md`](pelco-d-and-udp-fix/04-report.md) | ~640 라인 |

### 핵심 진화 요약

1. **UDP critical fix**: `UdpClient.Connect()` source-port filter 제거 → unconnected pattern + local 52381 explicit bind. Canon CR-N300 응답 수신 정상화 + round-1 의 FR-H50SN "silent firmware" 분류 사후 갱신
2. **`ViscaUdpHub` 싱글턴**: 모든 VISCA-IP firmware 가 dest port 52381 로만 reply 보내는 vendor 컨벤션 → 1개 socket + source IP demux 로 N대 카메라 동시 운용
3. **Pelco-D protocol family**: `PelcoDCodec` 7바이트 packet 빌더 (sync FF + addr + cmd1 + cmd2 + speeds + checksum) → Sony VISCA 외 두 번째 protocol 정착
4. **Canon CR-N300 zero-Service-cost**: 기존 `ViscaIpCameraService` + 1개 Capabilities — round-2 가설 검증

### 후속 작업 (open items)

- OD1 — Pelco-D over UDP vendor 포트 (4001 채택, 사용자 web UI 입력 우선)
- OD2 — Pelco-D PresetCount 64 추정 (실측 1/2만 사용)
- OD3 — Pelco-D over TCP framing 변형 (사용자 환경 RS-232 검증으로 충분)
- OD4 — UI Address placeholder text (UX 개선)
- OD5 — 같은 vendor 모델 N대 동시 (예: Canon × 2) hub demux 검증
- OD6 — Hub local 52381 점유 실패 시 ephemeral fallback 위험 (실 환경 발생 시 명시 경고)

---

## device-emulator

> Standalone WinForms 디바이스-사이드 에뮬레이터 (`DeviceEmulator/` 별도 솔루션) — 기존 `Xeno.Framework.Camera` 라이브러리에 inverse parser 5 파일 추가하여 controller↔emulator **한 쌍의 자산** 정착

| 항목 | 내용 |
|------|------|
| **기능명** | device-emulator |
| **시작 / 완료 / 보관** | 2026-05-06 (단일 세션) |
| **PDCA 라운드** | 1 (Design v1.0 → Do → Check 95% → Design v1.1 흡수 → Report) |
| **매칭율** | **95% (코드)** / **100% (설계, v1.1 흡수 후)** |
| **신규 솔루션** | `DeviceEmulator/DeviceEmulator.sln` (PN8080Controller.sln 와 분리) |
| **신규 파일** | 25개 (라이브러리 5 + DeviceEmulator 앱 20) |
| **수정 파일** | 1 (`Xeno.Framework.Camera.csproj`) |
| **코드 규모** | ~1,250 LoC |
| **v1 모델 (5종)** | Sony EVI-H100 Serial, SRG-300H UDP/TCP, FR-H50SN UDP/TCP, Canon CR-N300 UDP, Pelco-D Generic Serial/UDP/TCP |
| **실측 검증** | **4/5** (Canon CR-N300 UDP 22cmds ✅, FR-H50SN TCP ~50cmds ✅, FR-H50SN UDP 36cmds ✅ 보너스, Pelco-D Serial 31cmds ✅) — 총 ~140 명령 byte-correct |
| **미실측** | EVI-H100 Serial (OD7), Pelco-D UDP/TCP (OD8), Inject NAK/Latency 500ms (OD6) |
| **Critical/Major Gap** | 0 |
| **빌드** | DeviceEmulator.sln + PN8080Controller.sln Release 양쪽 0 경고 0 오류 |
| **회귀** | 0 (라이브러리: 신규 파일만, 기존 코드 무변경) |

### 보관 문서

| 단계 | 파일 | 분량 |
|------|------|-----|
| Plan | [`device-emulator/01-plan.md`](device-emulator/01-plan.md) | ~250 라인 |
| Design v1.1 | [`device-emulator/02-design.md`](device-emulator/02-design.md) | ~1,800 라인 |
| Analysis | [`device-emulator/03-analysis.md`](device-emulator/03-analysis.md) | ~150 라인 |
| Report | [`device-emulator/04-report.md`](device-emulator/04-report.md) | ~250 라인 |

### 핵심 진화 요약

1. **VISCA codec 양방향 (amphibian)**: `ViscaCodec` (controller→bytes) + `ViscaCommandParser` (bytes→ViscaCommand) + `ViscaReplyBuilder` (ACK/Completion/Error 빌드) — 같은 라이브러리 내. 신규 모델 추가 시 controller/emulator 양쪽 동시 구현 패턴 정착
2. **Server-side transport 반전**: client `UdpViscaTransport` → server `UdpServerTransport` (Sony reply convention 옵션화), `TcpViscaTransport` → `TcpServerTransport`, `SerialViscaTransport` → `SerialServerTransport` (VISCA terminator + Pelco fixed-length 양 framing)
3. **Wrap-detection 일반화** (Design v1.1 M1 흡수): `data[0]==0x01 && (data[1]==0x00||0x10)` 가드로 RESET (0x0200) 등 제어 패킷이 raw path 로 빠지게 → 1개 `ViscaIpEmulator` 가 Sony+Canon+FR variants 를 per-model branching 없이 커버
4. **Cross-solution ProjectReference**: `..\..\Xeno.Framework.Camera\Xeno.Framework.Camera.csproj` 상대 경로로 standalone 솔루션이 라이브러리 참조 — VS2022 정상 동작, 기존 솔루션 회귀 0
5. **에러 주입 + latency 시뮬**: `EmulatorOptions.InjectNak/Timeout/Malformed` + `LatencyMs 0~1000` + `ConsumeOnNextCommand` 1회용 토글 — controller 회복력 검증 인프라 확보

### 후속 작업 (open items)

- OD1 — Inquiry 응답 (현재 미응답 — 4 시나리오 실측 영향 없음)
- OD2 — Sony reply socket 번호 1 fixed (실 사용 시 옵션화)
- OD3 — UI Address 필터 (v2 deferred)
- OD4 — 멀티-인스턴스 port 충돌 — 현재 MessageBox 부분 처리, 가용 port 자동 제안 검토
- OD5 — RESET (VISCA-IP 0x0200) → 정식 RESET ACK (0x0201) 응답 (현 raw 90 41 FF 응답으로 controller drain 흡수)
- OD6 — I4 Inject NAK / I5 Latency 500ms 실측 (사용자 환경 준비 시)
- OD7 — EVI-H100 Serial VISCA 실측 (com0com 또는 USB-RS422 어댑터)
- OD8 — Pelco-D UDP/TCP 실측 (`SourcePortMirror` 모드 검증)

---

## device-emulator-validation

> device-emulator 후속 미니 사이클 — third-party 컨트롤러의 VISCA-IP **wrap-level 버그** (seq regression/duplicate, length mismatch 등) 을 emulator 가 능동적으로 surface 하도록 라이브러리 헬퍼 `ViscaIpWrapValidator` 추가

| 항목 | 내용 |
|------|------|
| **기능명** | device-emulator-validation |
| **트리거** | 사용자 별도 테스트 — third-party 프로그램의 CR-N300 Preset 명령이 실 카메라에서 동작 안 함. emulator 로그 분석으로 seq=28→20 역행 발견 (raw byte 수동 추적 필요) |
| **시작 / 완료 / 보관** | 2026-05-06 (Plan) → 2026-05-08 (Design) → 2026-05-09 (Do/Check/Report/Archive) |
| **PDCA 라운드** | 1 (Match Rate 99% 단발 통과) |
| **매칭율** | **99%** (0 critical/major/minor functional gap, 3 cosmetic 개선) |
| **신규 파일** | 1 (`ViscaIpWrapValidator.cs` ~110 LoC) |
| **수정 파일** | 2 (`ViscaIpEmulator.cs` +12 LoC, `Xeno.Framework.Camera.csproj` +1 line) |
| **검증 룰 (7종)** | MALFORMED, UNKNOWN_TYPE, LENGTH_MISMATCH, INVALID_VISCA_HEADER, MISSING_VISCA_TERMINATOR, SEQ_REGRESSION, SEQ_DUPLICATE |
| **상태 추적** | per-source-IP `Dictionary<IPAddress, uint>`, single lock, Stop 시 reset |
| **Critical/Major Gap** | 0 |
| **빌드** | DeviceEmulator.sln + PN8080Controller.sln Release 양쪽 0 경고 0 오류 |
| **회귀** | 0 (기존 4 통합 시나리오 동작 무변경 — observer 패턴) |

### 보관 문서

| 단계 | 파일 | 분량 |
|------|------|-----|
| Plan | [`device-emulator-validation/01-plan.md`](device-emulator-validation/01-plan.md) | ~140 라인 |
| Design v1.0 | [`device-emulator-validation/02-design.md`](device-emulator-validation/02-design.md) | ~330 라인 |
| Analysis | [`device-emulator-validation/03-analysis.md`](device-emulator-validation/03-analysis.md) | ~140 라인 |
| Report | [`device-emulator-validation/04-report.md`](device-emulator-validation/04-report.md) | ~200 라인 |

### 핵심 진화 요약

1. **Library helper 패턴**: validator 가 `Xeno.Framework.Camera/Protocols/Visca/` 에 위치하여 v2 매트릭스/CCU emulator 재사용 가능. emulator-specific 위치 거부 결정
2. **Observer 패턴**: validator 는 부수효과 없이 로그만 표시 — emulator reply 동작 무변경. 회귀 risk 0
3. **Per-source IP 격리**: 같은 controller 가 다른 ephemeral source port 사용해도 동일 seq 흐름 추적 (port 무시)
4. **Cosmetic 개선 3건** (design 보다 cleaner): emoji → `[WARN]`/`[INFO]` ASCII (Windows console 안전), em-dash → hyphen, unused `using System;` 제거
5. **Real-world bug 즉시 진단**: 사용자 시나리오 (seq 28→20) 가 한 줄 로그 `[WARN] SEQ_REGRESSION: last=28, got=20 (controller reset sequence?)` 로 즉시 노출

### 후속 작업 (open items, v2 검토)

- OD1 — per-source state TTL (장기 실행 시 inactive IP 정리)
- OD2 — Validator 결과 export (JSON/CSV — 제3자 분석 도구 연동)
- OD3 — inner 다중 VISCA frame 패킷 (vendor 확장)
- OD4 — TCP raw 모드 frame-level 검증 (별도 사이클)

---

*마지막 갱신: 2026-05-09 (device-emulator-validation 추가)*
