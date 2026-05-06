# Plan — pelco-d-and-udp-fix (Pelco-D 프로토콜 추가 + VISCA-over-IP UDP 수신 버그 수정 + Canon CR-N300 모델 추가)

> **요약**: `camera-remote-control` v1.1 완료 후 도출된 3개 후속 작업을 단일 PDCA 사이클로 묶어 진행. ① **신규 프로토콜 Pelco-D** (RS-232/RS-422/RS-485 + TCP + UDP transport) — 사용자 RS-232 실기 연결 확인됨. ② **VISCA-over-IP UDP 수신 버그** — `UdpClient.Connect()` 의 source-port filter 가 Canon CR-N300 처럼 reply 를 다른 port 에서 보내는 펌웨어를 차단. Old program 비교로 root cause 확정. ③ **Canon CR-N300 모델 등록** — UDP fix 후 `ViscaIpCameraService` 에 `(Info, Caps)` 한 쌍 추가만으로 완료.
>
> **작성자**: 개발팀 (KDI)
> **작성일**: 2026-05-06
> **상태**: Approved (Open Questions 6건 해결 — 2026-05-06, KDI)
> **참조 패턴**: [camera-remote-control 보관 archive](../../archive/2026-05/camera-remote-control/) (5계층 추상화 그대로 확장)

---

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | pelco-d-and-udp-fix |
| **대상 솔루션** | `PN8080Controller.sln` (`Xeno.Framework.Camera` Library 확장) |
| **신규 프로젝트** | 없음 — 기존 라이브러리 확장 |
| **신규 파일(예상)** | Pelco 코덱 1, PelcoSerialTransport 0~1 (재사용 검토), PelcoCameraService 1, Capabilities 추가 정적 팩토리 2~3, 총 ~3-5개 .cs |
| **수정 파일** | `UdpViscaTransport.cs` (Connect 제거 → unconnected pattern), `CameraCapabilities.cs` (CR-N300, PelcoD 정적 팩토리), `ICameraServiceFactory.cs` (모델 2개 추가), 옵션: `CameraTransportKind` (UdpVisca/TcpVisca → Udp/Tcp 리네임) |
| **지원 모델 추가** | Canon CR-N300 (VISCA over IP), Pelco-D Generic (RS-232/422/485 + TCP/UDP) |
| **스코프 1: UDP fix** | Critical bug — Canon/SRG 같은 비-FR 펌웨어 모두 영향 |
| **스코프 2: Pelco-D** | 신규 프로토콜 추가 — 5계층 패턴 확장의 두 번째 사례 |
| **스코프 3: CR-N300** | UDP fix 검증 + 패턴 재사용 입증 |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | (1) `UdpViscaTransport` 가 `UdpClient.Connect(host, port)` 사용 → socket 이 source-port filter 에 묶여 카메라 firmware 가 다른 port 에서 보내는 reply 를 OS 단에서 drop. Canon CR-N300, SRG-300H 등 vendor 별로 reply port 가 다른 펌웨어가 모두 silent 로 보임. FR-H50SN 만 우연히 같은 port 로 reply 해서 동작 → 잘못된 false positive. (2) Pelco-D 프로토콜 미지원 → 레거시 CCTV/PTZ 운용 현장 흡수 불가 — Plan §1.3 의 "외부 기기 통합 제어 API 사내 표준" 의 미지원 영역. |
| **Solution** | (1) `UdpClient.Connect()` 제거 → **unconnected socket pattern**: Send 는 `_client.Send(packet, len, endpoint)` explicit 지정, Receive 는 `IPEndPoint any = null` 로 any-source 받고 IP 만 화이트리스트 매칭(port 무시). FR-H50SN 도 그대로 동작 보장. (2) `Protocols/Pelco/PelcoDCodec` 신설 (sync 0xFF + address + cmd1 + cmd2 + pan-speed + tilt-speed + checksum), `Services/PelcoDCameraService` 또는 generic 구현. Visca 와 codec 만 다르고 transport 는 동일 인프라 재사용. (3) Canon CR-N300 = `ViscaIpCameraService` + `CanonCrN300()` Capabilities 정적 팩토리 (port 52381, ExpectReply=true). |
| **Function UX Effect** | UDP fix 후 — Canon CR-N300 / SRG-300H / 향후 임의 VISCA-IP 펌웨어가 vendor 별 reply port 무관 하게 모두 정상 응답 수신. Pelco-D — 운영자가 모델 콤보에서 "Pelco-D" 선택 + 통신 방식 선택(RS-232/422/485/UDP/TCP) + 주소(1-255) + Baud → 동일 UI 흐름. PT/Zoom/Focus/Preset 동작은 VISCA 와 동일 UX. |
| **Core Value** | (1) **숨겨진 critical bug 해소** — 우리가 제어 가능한 VISCA-IP 카메라 universe 가 1대(FR)에서 N대(모든 표준 따른 펌웨어)로 확장. (2) **두 번째 프로토콜 추가로 5계층 추상화 패턴 검증** — Codec 만 swap 하고 transport 인프라 100% 재사용 가능함을 입증. 향후 ONVIF, Pelco-P, 자체 규격 등 추가 시 동일 비용으로 확장. (3) **CR-N300 1회 추가로 "0 Service + 1 Capabilities + 1 factory case" 가설 한 번 더 검증**. |

---

## 1. 배경 & 동기

### 1.1 직전 PDCA (camera-remote-control) 마무리 후 발견 사항

`camera-remote-control` round-2 종료 시점에 사용자가 **Canon CR-N300 (192.168.1.131)** 을 SRG-300H 모델로 연결 시도. 결과:
- 카메라 정상 동작 (명령 수신·실행)
- **응답 0개** (모든 명령 timeout)
- 동일 카메라에 **이전 사내 프로그램** 으로 연결 시 응답 정상 (ACK + Completion 양쪽)

이는 round-2 의 FR-H50SN (silent firmware) 와 다른 패턴 — 이쪽은 "응답을 보내지만 우리가 못 받는" 케이스.

### 1.2 Old program 로그 분석으로 root cause 확정

```
송신: 01 00 00 09 00 00 00 00 81 01 06 01 18 01 01 03 FF
수신: 192.168.1.131:01110003000000009041FF + padding
수신: 192.168.1.131:01110003000000009051FF + padding
```

- 응답 양식: `[Type 0x0111] [Len 0x0003] [Seq 0x00000000] [Reply 90 41 FF / 90 51 FF]` + zero padding 23 bytes
- ACK + Completion 둘 다 도착
- **로그가 source IP 만 표시, source port 미표시** → unconnected socket 으로 모든 port 수용

**우리 코드 (구 v1.1)**:
```csharp
_client = new UdpClient();
_client.Connect(IPAddress.Parse(Host), Port);   // ← THE BUG
```

`UdpClient.Connect()` 는 socket 의 RemoteEndPoint 를 fix → 그 endpoint **외에서 오는 datagram 은 OS 가 drop**. Canon CR-N300 펌웨어가 reply 를 random ephemeral port 또는 다른 fixed port 에서 보낼 가능성 → 우리 socket 이 못 받음.

FR-H50SN 만 우연히 reply 도 52381 로 와서 동작 — false positive 였음.

### 1.3 Pelco-D 프로토콜 추가 동기

운영 현장의 레거시 CCTV/PTZ 카메라 다수가 Pelco-D 사용. 현재 우리 라이브러리는 VISCA 단일 프로토콜만 지원 → 레거시 흡수 불가. 사용자가 RS-232 Pelco-D 카메라 1대 실기 연결 확인 — 추가 시점 적정.

또한 `camera-remote-control` 의 Plan §1.3 비목표 ("Pelco-D 는 v1 범위 외, 구조만 확장 가능하게") 의 v2 이행 시점.

### 1.4 비목표 (Non-Goals, 본 사이클)

- Pelco-P (Pelco-D 와 호환 부분 있지만 별도 프로토콜) — v3 후속
- ONVIF — v3 후속
- 응답 ordering 의 socket-level 매칭 (현재 sequence 검증 + 방식 A 로 충분)
- 시리얼 포트 자동 검출 / 핫플러그 — v3
- Pelco-D Block Inquiry 같은 status 쿼리 (대부분 일방향 protocol)

---

## 2. 목표 (Goals)

| 우선순위 | 목표 | 측정 기준 |
|---------|------|----------|
| **Must** | `UdpViscaTransport` 의 source-port filter 버그 수정 — unconnected socket pattern | Canon CR-N300 실기에서 ACK/Completion 정상 수신, FR-H50SN 도 회귀 없음 |
| **Must** | Canon CR-N300 모델 등록 (VISCA over IP) | 모델 콤보에 표시, 연결 + PT/Zoom/Preset 동작 |
| **Must** | Pelco-D 프로토콜 신설 — RS-232/422/485 + TCP + UDP 5종 transport | RS-232 실기 카메라 PT/Zoom/Preset 동작 |
| **Should** | Pelco-D 모델 1개 (Generic Pelco-D) 등록 — vendor agnostic | 모델 콤보에 표시 |
| **Should** | 프로토콜 추상화 강화 — `Protocol` 개념 도입 (Visca / PelcoD) 또는 codec class 분리만으로 충분히 깔끔 | 모델 추가 비용 = "Capabilities 1개 + factory case 1개" 유지 |
| **Could** | `CameraTransportKind` 의 `UdpVisca/TcpVisca` → 일반화 (`Udp/Tcp`) — wire 와 codec 분리 | 기존 saved settings 자동 마이그레이션 |
| **Could** | 다른 VISCA-IP 모델 추가 시 동일 패턴 재사용 가설 검증 — CR-N300 으로 1회 |  |

---

## 3. 사용자 시나리오

### 3.1 Persona

`camera-remote-control` 와 동일 — AV 시스템 운영자.

### 3.2 핵심 시나리오

**S1. Canon CR-N300 연결** (UDP fix 검증)
1. 모델 콤보 → "CR-N300 (Canon)" 선택
2. 통신 자동 UDP, 포트 자동 52381
3. IP `192.168.1.131` 입력 → 연결
4. CAM 선택 → PT 버튼 → 응답 정상 수신 (ACK ~10ms)

**S2. Pelco-D RS-232 카메라 연결**
1. 모델 콤보 → "Pelco-D Generic" 선택
2. 통신 자동 RS-232, **Baud 9600** (Q4 결정)
3. COM 포트, 주소 (1-255) 입력 → 연결
4. CAM 선택 → PT 버튼 → 카메라 동작 (Pelco-D 는 응답 없는 경우가 많음 — `ExpectReply=false` 기본)

**S3. Pelco-D over UDP/TCP** (사용자 요구)
1. 모델 그대로, 통신 → UDP 또는 TCP 변경
2. IP/Port 입력 → 연결
3. PT 버튼 → 동작 (Pelco-D over IP 는 wire 만 다름, codec 동일)

**S4. 회귀 테스트 — FR-H50SN, EVI-H100**
- 기존 동작 유지 (UDP fix 후에도 FR 그대로 작동, EVI RS-232 그대로 작동)

---

## 4. 기능 요구사항 (Functional Requirements)

### 4.1 UDP Transport 수정 (Critical Fix)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-01 | `UdpViscaTransport.Open()` 에서 `_client.Connect()` 제거 | unconnected socket |
| FR-02 | Send 는 `_client.Send(packet, len, IPEndPoint)` 로 명시 endpoint 지정 | RemoteEndPoint 매번 전달 |
| FR-03 | Background RX loop 의 `client.ReceiveAsync()` 가 IPEndPoint any 로 수신, **source IP 가 expected host 와 매칭되는 datagram 만** 큐에 enqueue (port 는 무시) | 다른 카메라/방송과 혼선 방지 |
| FR-04 | Multicast / spoofed datagram 무시 (Source IP 화이트리스트) | 보안 |
| FR-05 | 회귀: FR-H50SN UDP (port 52381 reply) 정상 동작 유지 | 실측 검증 |
| FR-06 | 신규: Canon CR-N300 (VISCA over IP) ACK/Completion 정상 수신 | 사용자 검증 |

### 4.2 Pelco-D 프로토콜 (신규)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-20 | `Protocols/Pelco/PelcoDCodec` 신설 — Pan/Tilt drive/stop, Zoom tele/wide/stop, Focus far/near/stop, Memory(Set/Recall N), Aux On/Off (선택) | 7바이트 packet: `FF [addr] [cmd1] [cmd2] [pan-speed] [tilt-speed] [checksum]` |
| FR-21 | 체크섬: address ~ tilt-speed 의 모듈로 256 합 | Pelco-D 표준 |
| FR-22 | 주소 범위 1-255 (VISCA 1-7 보다 넓음) | UI Address 필드 max 변경 |
| FR-23 | Pelco-D 응답 처리 — 대부분 일방향 protocol 이지만 일부 vendor 가 ACK 송신 가능. `ExpectReply` 모델 기본 false | 응답 없으면 fire-and-forget |
| FR-24 | `Services/PelcoDCameraService` 또는 generic — Serial / TCP / UDP transport 모두 사용 | 기존 transport 인프라 100% 재사용 |
| FR-25 | Pelco-D over TCP/UDP wire format — raw Pelco-D bytes (보통 7바이트) 그대로 전송 | OQ2 확정 필요 |

### 4.3 Canon CR-N300 모델 (신규, UDP fix 의존)

| ID | 요구사항 | 비고 |
|----|---------|------|
| FR-40 | `CameraCapabilities.CanonCrN300()` 정적 팩토리 — VISCA over IP, port 52381, ExpectReply=true, PresetCount=16 (TBD) | Canon 매뉴얼 발췌 |
| FR-41 | `CameraServiceFactory` 에 "CR-N300" case 추가 — 기존 `ViscaIpCameraService` 재사용 (0 Service 추가) | 가설 재검증 |
| FR-42 | UI 모델 콤보에 "CR-N300 (Canon)" 표시 | Brand/Model 분리 표기 |

### 4.4 (보류) Transport enum 일반화 — Q5 결정으로 본 사이클 제외

`UdpVisca/TcpVisca` enum 리네임은 v3 후속 사이클로 분리. 본 사이클은 UDP fix 와 Pelco-D 추가에 집중. 현재 enum 명칭 그대로 사용하면서, Pelco-D 도 동일 transport 종류 (`UdpVisca`/`TcpVisca`) 를 사용 (codec 만 다름).

**근거**: enum 이름이 "Visca" 를 포함해도 실제 wire 동작은 generic UDP/TCP 이며, 코덱이 transport 와 분리되어 있어 동작상 문제 없음. 리네임은 cosmetic 개선이라 우선순위 낮음.

---

## 5. 비기능 요구사항 (Non-Functional)

| 분류 | 요구사항 |
|------|---------|
| 플랫폼 | .NET Framework 4.8, C# 7.3 (camera-remote-control 와 동일) |
| 빌드 | Debug/Release 양쪽 0 경고 |
| 회귀 | 기존 3 모델 (EVI-H100, SRG-300H, FR-H50SN) 동작 유지 |
| 보안 | UDP 수신 시 source IP 화이트리스트로 spoofed datagram 무시 |
| 안정성 | 다른 카메라 트래픽이 같은 OS port 로 와도 무시 (IP 매칭) |
| Hold-to-Move 지연 | Pelco-D 도 ≤50ms 첫 명령 송신 |

---

## 6. 아키텍처 — 5계층 확장

```
Core/                                       (변경: ICameraServiceFactory + 2 모델 추가)
  ICameraService, CameraServiceBase
  CameraInfo, CameraCapabilities (+ CanonCrN300, PelcoDGeneric 정적 팩토리)
  CameraTransportKind  (옵션: UdpVisca/TcpVisca → Udp/Tcp 리네임)
  ICameraServiceFactory (CR-N300 case + Pelco-D case)

Protocols/                                  (신규 Pelco/ 서브폴더)
  Visca/  ViscaCodec, ViscaResponseParser, ViscaConstants
  Pelco/  PelcoDCodec, PelcoConstants     ← NEW

Transports/                                 (변경: UdpViscaTransport unconnected pattern)
  IViscaIpTransport                       ← 그대로
  SerialViscaTransport                    ← Pelco-D 도 SerialPort 같은 인프라 사용 가능 → 그대로 또는 generic SerialTransport 추출 (선택)
  UdpViscaTransport                       ← Connect() 제거, source IP 매칭 추가
  TcpViscaTransport                       ← 그대로

Services/
  EviH100CameraService                    ← 그대로
  ViscaIpCameraService                    ← CR-N300 도 같은 클래스 재사용
  PelcoDCameraService                     ← NEW (또는 generic + factory case)

UI/                                         (변경: Address max 1→255 또는 모델별 dynamic)
  CameraControl                           ← 변경 없음 (Address 가 모델 capability 에서 max 가져오면 1줄 추가)
  CameraSlotConfig, CameraUiColors, ServiceLogEventArgs  ← 그대로
```

### 모델 추가 비용 (재검증)

| 모델 추가 시점 | 비용 |
|---------------|------|
| camera-remote-control round-1 (EVI/SRG) | 1 Service + 1 Capabilities |
| camera-remote-control round-2 (FR) | 0 Service + 1 Capabilities + 1 factory case (`ViscaIpCameraService` generic 화 후) |
| **본 사이클 (Canon CR-N300)** | **0 Service + 1 Capabilities + 1 factory case** (가설 재검증) |
| **본 사이클 (Pelco-D Generic)** | **1 Codec + 1 Service + 1 Capabilities + 1 factory case** (신규 프로토콜 — Codec 비용 있음) |

→ **신규 프로토콜 추가는 Codec 1개 추가, 신규 모델은 Capabilities 1개 추가** — 5계층 패턴의 비용 모델이 일관성 있게 작동.

---

## 7. 위험 및 의존성

| # | 위험/의존성 | 영향 | 완화책 |
|---|------------|------|--------|
| R1 | Old program 의 source-port-not-shown 로그가 단순 표기 차이일 가능성 (실제로는 같은 port 사용) | High (root cause 오진단) | unconnected socket 패턴은 양쪽 케이스 모두 안전 — 가설이 틀려도 회귀 없음. Wireshark 로 Canon CR-N300 reply packet 의 source port 1회 확인 권장 |
| R2 | Pelco-D 카메라가 vendor 별로 미묘하게 다른 변형 사용 (Pelco-D vs Pelco-P vs Pelco-D extended) | Med | Pelco-D Generic 모델로 표준 7바이트만 구현, vendor 특화는 후속 |
| R3 | Pelco-D 응답 처리 — 일부 카메라는 일방향, 일부는 ACK 송신 | Low | `ExpectReply=false` 기본, 사용자가 UI 로 toggle 가능하도록 (또는 새 모델 등록 시 결정) |
| R4 | Pelco-D Address 1-255 범위 — UI 의 Address TextBox max validation 변경 필요 | Low | 모델 capability 에 `MaxAddress` 추가, UI 가 동적 적용 |
| R5 | UdpVisca/TcpVisca 리네임 → 기존 user settings INI 의 enum 이름 깨짐 | Med (existing user 영향) | `Enum.Parse` fallback 으로 "UdpVisca" → `Udp`, "TcpVisca" → `Tcp` 자동 마이그레이션. OQ5 결정에 따라 리네임 자체 보류 가능 |
| R6 | Canon CR-N300 매뉴얼 미보유 → PresetCount/속도 범위 추정 | Low | Sony VISCA 표준 기본값 채택, OQ6 으로 사용자 확인 |
| D1 | UDP fix 가 회귀 발생 시 (FR-H50SN 등 기존 모델 영향) | High | 회귀 테스트 필수. 사용자가 FR/EVI 양쪽 한 번 더 검증 |

---

## 8. Decisions Log (확정 — 2026-05-06, KDI)

| # | 질문 | 결정 | 영향 받은 섹션 |
|---|------|------|---------------|
| Q1 | Pelco-D 모델 등록명 | **"Pelco-D Generic"** 1개로 시작, 필요 시 후속 추가 | §4.3, §6 모델 콤보 |
| Q2 | Pelco-D over TCP/UDP wire format | **raw 7바이트** 그대로 전송 (가장 흔한 third-party 컨벤션) | §4.2 FR-25, Design Pelco transport 명세 |
| Q3 | Pelco-D 응답 처리 | **무응답** (fire-and-forget) — `ExpectReply=false` 기본. 후속 vendor 가 ACK 보내면 모델 추가 시 결정 | §4.2 FR-23, `Capabilities.PelcoDGeneric()` 기본값 |
| Q4 | Pelco-D RS-232 기본 Baud | **9600** (현대 표준 — 사용자 환경 명시) | §3 S2, `Capabilities.PelcoDGeneric().DefaultBaudRate = 9600` |
| Q5 | `UdpVisca/TcpVisca` enum 리네임 | **No (본 사이클 보류)** — UDP fix + Pelco-D 추가에 집중. 리네임은 v3 별도 사이클 | §4.4 (Could) 절 폐기 |
| Q6 | Canon CR-N300 정확 capability | **Sony VISCA 표준 추정값** 사용 (PresetCount=16, Pan 1-0x18, Tilt 1-0x14, Zoom/Focus 0-7). 실측 후 micro-task 로 교정 | §4.3 FR-40, §11 후속 task 추가 |

---

## 9. 산출물 (Deliverables)

| 산출물 | 위치 | 단계 |
|--------|------|------|
| 본 Plan 문서 | `docs/01-plan/features/pelco-d-and-udp-fix.plan.md` | Plan ✅ |
| Design 문서 | `docs/02-design/features/pelco-d-and-udp-fix.design.md` | Design (다음) |
| 코드 변경 | `Xeno.Framework.Camera/` (UDP fix + Pelco/) | Do |
| Gap Analysis | `docs/03-analysis/pelco-d-and-udp-fix.analysis.md` | Check |
| 완료 보고서 | `docs/04-report/pelco-d-and-udp-fix.report.md` | Report |

---

## 10. 다음 단계

1. ✅ Plan 작성 완료 → 사용자 검토 (Open Questions Q1~Q6 회답)
2. ⏭ 회답 반영 후 `/pdca design pelco-d-and-udp-fix`
3. ⏭ Design 승인 후 `/pdca do pelco-d-and-udp-fix` 으로 구현 착수
   - **Step 1: UDP fix** (Critical, 회귀 테스트 우선)
   - **Step 2: Canon CR-N300 등록 + 실기 검증**
   - **Step 3: Pelco-D 코덱 + Service**
   - **Step 4: Pelco-D 모델 등록 + 실기 검증** (사용자 RS-232 카메라)

---

**검토 요청 (KDI)**:
- §8 Open Questions 6건 답변 부탁드립니다. 기본 가정으로 진행해도 무방한 항목은 "기본값 OK" 만 회신해주셔도 됩니다.
- **특히 Q4 (Pelco-D Baud)** 와 **Q2 (Pelco-D over TCP/UDP wire format)** 은 사용자 카메라 매뉴얼/web UI 정보가 있으면 더 정확해집니다.
