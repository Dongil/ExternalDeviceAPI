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

*마지막 갱신: 2026-05-06 (pelco-d-and-udp-fix 추가)*
