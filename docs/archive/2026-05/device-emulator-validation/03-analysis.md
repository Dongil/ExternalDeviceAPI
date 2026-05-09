# Analysis — device-emulator-validation

> **문서 버전**: v1.0 (2026-05-09)
> **참조 Design**: [device-emulator-validation.design.md](../02-design/features/device-emulator-validation.design.md) v1.0
> **참조 Plan**: [device-emulator-validation.plan.md](../01-plan/features/device-emulator-validation.plan.md)

## Executive Summary

미니 사이클 — Design v1.0 명세 100% 일치. 3 파일 (1 신규 + 2 수정) 모두 시그니처/동작/통합 위치 정확. 양 솔루션 Release 0/0. Cosmetic 3건 (emoji→ASCII, em-dash→hyphen, unused using 제거) 은 모두 의도된 개선 — gap 아님.

## Match Rate

| 카테고리 | 점수 | 상태 |
|----------|:---:|:---:|
| API Match (Design §3) | **100%** | ✅ |
| Issue Codes & Severity (7/7) | **100%** | ✅ |
| PayloadType Constants (5/5) | **100%** | ✅ |
| Integration Points (Design §4) | **100%** | ✅ |
| csproj Registration | **100%** | ✅ |
| Convention Compliance | 98% | ✅ |
| **종합** | **99%** | ✅ |

## File Verification

| Design 경로 | 구현 | 상태 |
|---|---|:---:|
| `Xeno.Framework.Camera/Protocols/Visca/ViscaIpWrapValidator.cs` | NEW ~110 LoC | ✅ |
| `Xeno.Framework.Camera/Xeno.Framework.Camera.csproj` 등록 | +1 line (alphabetic 위치) | ✅ |
| `DeviceEmulator/Emulators/ViscaIpEmulator.cs` 통합 (3 spots) | field + OnViscaIp top + Stop bottom | ✅ |

## Verification 상세

### 1. API Match (Design §3) — 100%

| 항목 | Design | 구현 | 일치 |
|------|--------|------|:---:|
| Class sealed | sealed | sealed (line 33) | ✅ |
| Single lock | `private readonly object _lock` | line 35 | ✅ |
| State map | `Dictionary<IPAddress, uint>` | line 36 | ✅ |
| `ValidateWrap(byte[], IPAddress)` 시그니처 | matches | line 45 | ✅ |
| Return `IReadOnlyList<ValidationIssue>` | matches | line 45 | ✅ |
| `Reset()` lock 하위 clear | matches | line 116-119 | ✅ |

### 2. Issue Codes & Severity — 100% (7/7)

| Code | Design | 구현 | 일치 |
|------|:---:|:---:|:---:|
| MALFORMED | Warning | Warning (line 50) | ✅ |
| UNKNOWN_TYPE | Info | Info (line 64) | ✅ |
| LENGTH_MISMATCH | Warning | Warning (line 70) | ✅ |
| INVALID_VISCA_HEADER | Warning | Warning (line 81) | ✅ |
| MISSING_VISCA_TERMINATOR | Warning | Warning (line 87) | ✅ |
| SEQ_REGRESSION | Warning | Warning (line 100) | ✅ |
| SEQ_DUPLICATE | Warning | Warning (line 105) | ✅ |

### 3. PayloadType Constants — 100%

5 상수 모두 정확한 값: 0x0100, 0x0110, 0x0111, 0x0200, 0x0201 (lines 39-43).

### 4. Integration in `ViscaIpEmulator.cs` — 100%

| Spot | Design Spec | 구현 | 일치 |
|------|-------------|------|:---:|
| (a) Field 선언 | `_validator = new ViscaIpWrapValidator()` | line 18 | ✅ |
| (b) `OnViscaIp` 시작부 (unwrap 전) | yes | lines 75-81 (line 83 unwrap 전) | ✅ |
| (b) `!isTcp && src != null` 가드 | both required | line 77 has both | ✅ |
| (c) `_validator.Reset()` Stop() 끝 | yes | line 70 | ✅ |

### 5. csproj Registration — 100%

`Protocols\Visca\ViscaIpWrapValidator.cs` 알파벳 순서로 `ViscaConstants.cs` 와 `ViscaReplyBuilder.cs` 사이 위치 (line 63). Design §5 가이드 일치.

## Cosmetic Deviations (acknowledged improvements)

| # | 항목 | Design | 구현 | 판정 |
|---|------|--------|------|------|
| C1 | Format() 아이콘 | emoji (⚠️/ℹ️) | `[WARN]` / `[INFO]` ASCII | **개선** (.NET 4.8 console/log 인코딩 안전) |
| C2 | 메시지 em-dash | em-dash | hyphen `-` | **개선** (ASCII 일관성) |
| C3 | `using System;` | Design 코드에 포함 | 제거됨 (`Collections.Generic`, `Net` 만 사용) | **개선** (cleaner) |

→ 3 건 모두 의도된 개선이며 design v1.1 으로 흡수 추천.

## Gaps

### Critical
*없음.*

### Major
*없음.*

### Minor (functional)
*없음.* (cosmetic 3건 모두 design 보다 cleaner)

## Open Items 인계 (Design §9)

| # | 항목 | 상태 |
|---|------|------|
| OD1 | per-source state TTL | v2 검토 |
| OD2 | Validator 결과 export (JSON/CSV) | v2 |
| OD3 | inner 다중 VISCA frame 패킷 | OD 유지 |
| OD4 | TCP raw 모드 frame-level 검증 | 별도 사이클 |

## Recommendations

### 즉시 조치
*없음.* 99% 매칭, 0 functional gap — Report 단계 진입 권장.

### Design v1.1 흡수 후보 (cosmetic)
- C1 §3 Format() 코드 블록 — emoji 대신 `[WARN]`/`[INFO]` ASCII (특히 .NET Framework / Windows console target)
- C2 메시지 hyphen 일관 사용 (Korean 문장에서도 ASCII hyphen 권장)
- C3 `using System;` 제거 — minimal using 정책

### Lessons (Report 단계 반영)
- Design 문서의 emoji 코드 블록은 .NET Framework / Windows console target 시 ASCII-equivalent 으로 대체 default 화 권장 — 본 사이클이 첫 사례

## 결론

Match Rate **99%** — 즉시 `/pdca report device-emulator-validation` 진입 가능. iterate 불필요.

**문서 상태**: Final
