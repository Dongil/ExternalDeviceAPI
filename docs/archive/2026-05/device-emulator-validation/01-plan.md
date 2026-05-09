# Plan — device-emulator-validation

> **문서 버전**: v1.0 (2026-05-06)
> **선행 사이클**: device-emulator (archived 2026-05-06, Match Rate 95%)
> **유형**: 미니 사이클 (단일 세션 추정 ~2h)

## Executive Summary

| 항목 | 내용 |
|------|------|
| **기능명** | device-emulator-validation |
| **트리거** | 사용자 별도 테스트 — "previous program" 의 CR-N300 Preset 명령이 실 카메라에서 안 먹힘. emulator 로그 분석 결과 **VISCA-IP 시퀀스 번호 역행** (28 → 20) 발견. emulator 가 wrap 안 inner 만 표시하고 wrap 헤더 자체 무결성 검증 미흡 → 같은 부류 버그 진단 어려움 |
| **목표** | VISCA-IP wrap-level 검증 4종 (seq regression, seq duplicate, length mismatch, unknown payload type) 추가하여 third-party 컨트롤러 버그 즉시 가시화 |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | 실 측에서 발견된 third-party 컨트롤러의 sequence rollback 버그 — emulator 가 wrap 풀고 inner VISCA 만 표시하므로 wrap 헤더 자체의 이상 (seq 역행/중복, length 불일치, type 오류) 은 사용자가 timestamp 와 raw byte 를 수동 추적해야 발견 가능. 본질적 진단 인프라 부재 |
| **Solution** | 라이브러리에 `ViscaIpWrapValidator` 추가 (stateful per-source-IP seq 추적 + 4 종 검증 룰), `ViscaIpEmulator.OnViscaIp` 가 unwrap 후 호출하여 결과를 WARN 로그로 표시. 라이브러리 헬퍼화로 향후 매트릭스/CCU emulator (v2 사이클) 도 재사용 |
| **Function UX Effect** | 로그 라인에 `⚠️ SEQ REGRESSION (last 28, got 20)` 같은 즉시-진단 메시지 표시. timestamp 와 byte 추적 없이 한눈에 controller 버그 식별 가능. emulator 동작 자체는 변경 없음 (관찰만 추가) |
| **Core Value** | controller↔emulator 양방향 자산에 **진단/품질 검증** 차원 추가. controller 측 버그를 emulator 가 능동적으로 surface 하는 패턴 — Sony/Canon 외 third-party 통합 시나리오에 결정적 |

---

## 1. 배경 (실측 증거)

2026-05-06 16:09 사용자 별도 테스트:
- "previous program" → device-emulator(CR-N300 UDP) 연결
- PT 명령 정상 동작
- Preset Set/Recall 명령은 byte 자체 정상 (`81 01 04 3F 01 05 FF` / `81 01 04 3F 02 05 FF`)
- 그러나 실 CR-N300 에서는 안 먹힘
- **emulator 로그에서 발견**: PT 명령 seq=20→28 까지 정상 증가, Preset Recall 가 seq=20 으로 되돌아감 (역행) → 실 카메라가 replay/duplicate 으로 drop 추정

```
16:09:21.303  RX seq=28  PT Stop
16:09:27.023  RX seq=20  Preset Recall 5  ← 역행 (28 → 20)
16:09:34.046  RX seq=21  Preset Recall 5  ← 역행 (28 → 21)
```

→ 본 사이클로 같은 부류 버그를 **emulator 측에서 즉시 표시** 가능하게 함.

---

## 2. 범위 (FR-MUST)

| # | 요구사항 | 상세 |
|---|---------|------|
| F1 | Sequence regression 검출 | 같은 source IP 의 last seen seq 보다 작은 seq 도착 시 WARN |
| F2 | Sequence duplicate 검출 | 같은 source IP 의 last seen seq 와 동일한 seq 도착 시 WARN |
| F3 | Length 필드 검증 | wrap 헤더의 Length(2B) 가 실 inner payload 길이와 다르면 WARN |
| F4 | Unknown payload type 검출 | type 이 0x0100/0x0110/0x0200/0x0201/0x0111 외이면 INFO (not error — vendor 확장 가능) |
| F5 | 라이브러리 헬퍼화 | `Xeno.Framework.Camera/Protocols/Visca/ViscaIpWrapValidator.cs` 신규 |
| F6 | Emulator 통합 | `ViscaIpEmulator.OnViscaIp` 가 unwrap 후 validator 호출, 결과를 `Log("⚠️ ...")` 으로 표시 |
| F7 | per-source 상태 격리 | 다른 IP 는 독립 seq 추적 (IPAddress 기반 dict) |
| F8 | 회귀 0 | 검증은 관찰만, emulator reply 동작 무변경 |

## 3. 범위 외 (out-of-scope)

- TCP raw 모드 검증 (wrap 없음 — 적용 불가)
- Pelco-D 검증 (wrap 없음, 단일 7-byte frame)
- UI 검증 통계 대시보드 (현재는 로그 라인만)
- Validator 결과 export (JSON/CSV) — v2
- TTL 기반 inactive source 정리 (단일 세션 가정, Stop 시 reset 으로 충분)

---

## 4. Open Questions (Q1~Q5 — 모두 default OK 시 진행)

| Q | 질문 | Default |
|---|------|---------|
| Q1 | seq 추적 단위: source IP only vs (IP, port) 페어? | **IP only** (같은 controller 가 다른 source port 사용해도 동일 seq 흐름) |
| Q2 | 검증 결과 로그 레벨: WARN 통일 vs (regression=ERROR, dup=WARN, length=WARN, unknown=INFO)? | **혼합** (regression=⚠️, duplicate=⚠️, length=⚠️, unknown=ℹ️) |
| Q3 | per-source state 메모리 — TTL? 명시 reset? | **Stop 시 reset, TTL 없음** (단일 세션) |
| Q4 | validator 가 inner VISCA 시작 (0x8x) / 종료 (0xFF) 도 검증? | **포함** (wrap 검증의 자연스러운 확장 — F3 으로 흡수) |
| Q5 | 라이브러리 위치 — `Protocols/Visca/` vs 별도 `Diagnostics/`? | **Protocols/Visca/** (Visca-IP 전용 — 다른 protocol 검증은 별도 클래스) |

---

## 5. 위험

| # | 위험 | 완화 |
|---|------|------|
| R1 | Validator 결과가 false-positive 생성 (e.g., legitimate seq reset on reconnect) | reconnect 시 source IP 가 같으면 같은 seq 흐름 가정. 실 컨트롤러가 고의로 seq=0 부터 재시작 시 첫 패킷에서 false-positive 가능 — UX 수용 가능 (1회성) |
| R2 | per-source state 메모리 누수 (장시간 실행 시 다양한 IP 누적) | 단일 세션 가정, Stop 시 reset. 실측 상 emulator 는 짧은 테스트 세션 |
| R3 | inner VISCA 0xFF 종료 검증이 multi-frame 패킷 (rare) 오인 | VISCA-IP 표준은 1 wrap = 1 VISCA frame. 표준 외 케이스는 OD |

---

## 6. 추정

| 단계 | 시간 |
|------|-----|
| Plan | 0.2h (본 문서) |
| Design | 0.5h |
| Do | 1.0h (라이브러리 헬퍼 + emulator 통합 + 빌드 검증) |
| Check | 0.2h (gap-detector) |
| Report + Archive | 0.3h |
| **합계** | **~2.2h** |

---

## 7. 성공 기준

- ✅ `ViscaIpWrapValidator` 라이브러리 헬퍼 추가 (~80 LoC)
- ✅ `ViscaIpEmulator` 통합 (~10 LoC 추가)
- ✅ 양 솔루션 Release 0/0
- ✅ 회귀 0 (controller 측 코드 무변경)
- ✅ 사용자 실측 시나리오 재현: PT seq 정상 → Preset seq 역행 시 `⚠️ SEQ REGRESSION (last 28, got 20)` 로그 표시
- ✅ Match Rate ≥ 90%

---

**문서 상태**: Approved (defaults 채택 시 즉시 Design 진행)
