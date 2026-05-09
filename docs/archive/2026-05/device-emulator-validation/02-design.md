# Design — device-emulator-validation

> **문서 버전**: v1.0 (2026-05-08)
> **참조 Plan**: [device-emulator-validation.plan.md](../../01-plan/features/device-emulator-validation.plan.md)
> **선행 사이클**: device-emulator (archived 2026-05-06)

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | device-emulator-validation |
| **신규 파일** | 1 (`Xeno.Framework.Camera/Protocols/Visca/ViscaIpWrapValidator.cs`) |
| **수정 파일** | 3 (`ViscaIpEmulator.cs` integration, `Xeno.Framework.Camera.csproj` register, design doc 갱신) |
| **솔루션 영향** | DeviceEmulator.sln 빌드, PN8080Controller.sln 회귀 0 |
| **추가 LoC** | ~100 (validator ~80 + 통합 ~10 + csproj 1 line + comments) |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | wrap 헤더 무결성 검증 부재 — seq 역행/중복/length 불일치를 raw byte 수동 추적해야 발견 |
| **Solution** | 라이브러리 헬퍼 `ViscaIpWrapValidator` (단일 책임: wrap 검증). per-source IP 상태 추적, 4 검증 룰, immutable result 객체 반환 |
| **Function UX Effect** | emulator 로그에 즉시 `⚠️ SEQ REGRESSION (last 28, got 20)` 표시. UX 변경 없음 (관찰만) |
| **Core Value** | 같은 validator 가 향후 v2 매트릭스 emulator (TCP-VISCA 메트릭스 vendor) 에도 직접 재사용 가능 — wrap 형식 동일 시 |

---

## 1. Plan Decisions 매핑 (Q1~Q5 → 설계 위치)

| Q | 결정 | 본 문서 위치 |
|---|------|--------------|
| Q1 | seq 추적 = source IP only | §3 Validator API (Dictionary key = `IPAddress`) |
| Q2 | 로그 레벨 혼합 (⚠️ regression/dup/length, ℹ️ unknown) | §3 ValidationSeverity enum |
| Q3 | Stop 시 reset, TTL 없음 | §3 Reset() 메서드, §4 ViscaIpEmulator.Stop 호출 |
| Q4 | inner VISCA 시작/종료 검증 → F3 (length) 와 합쳐 single check | §3 ValidateWrap 의 inner check |
| Q5 | `Protocols/Visca/ViscaIpWrapValidator.cs` | §2 폴더 위치 |

---

## 2. 폴더/파일 구조

```
Xeno.Framework.Camera/                                          (수정 csproj 1 line)
  Protocols/
    Visca/
      ViscaIpWrapValidator.cs            ← NEW (~80 LoC)
      ViscaCommand.cs                    (변동 없음)
      ViscaCommandParser.cs              (변동 없음)
      ViscaReplyBuilder.cs               (변동 없음)
      ViscaCodec.cs                      (변동 없음)
      ViscaConstants.cs                  (변동 없음)
      ViscaResponseParser.cs             (변동 없음)
DeviceEmulator/DeviceEmulator/
  Emulators/
    ViscaIpEmulator.cs                   ← MOD (~10 LoC 추가, OnViscaIp + Stop)
```

---

## 3. `ViscaIpWrapValidator` API 명세

```csharp
using System;
using System.Collections.Generic;
using System.Net;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    public enum ValidationSeverity { Info, Warning }

    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public string Code { get; }       // "SEQ_REGRESSION", "SEQ_DUPLICATE", "LENGTH_MISMATCH", "UNKNOWN_TYPE"
        public string Message { get; }    // human-readable

        public ValidationIssue(ValidationSeverity sev, string code, string message)
        {
            Severity = sev; Code = code; Message = message;
        }

        public string Format()
        {
            string icon = Severity == ValidationSeverity.Warning ? "⚠️" : "ℹ️";
            return icon + " " + Code + ": " + Message;
        }
    }

    /// <summary>
    /// Stateful per-source-IP VISCA-over-IP wrap header validator.
    /// Detects sequence regression/duplicate, length mismatch, unknown payload type.
    /// Thread-safe (single lock — call frequency is bounded by network packet rate).
    /// </summary>
    public sealed class ViscaIpWrapValidator
    {
        private readonly object _lock = new object();
        private readonly Dictionary<IPAddress, uint> _lastSeqBySource =
            new Dictionary<IPAddress, uint>();

        // Known VISCA-over-IP PayloadType codes
        private const ushort Type_ViscaCommand = 0x0100;
        private const ushort Type_ViscaInquiry = 0x0110;
        private const ushort Type_ViscaReply   = 0x0111;
        private const ushort Type_ControlCommand = 0x0200;  // RESET, etc.
        private const ushort Type_ControlReply   = 0x0201;

        /// <summary>
        /// Validate a wrapped VISCA-over-IP packet. Caller passes the FULL raw packet
        /// (header + payload) and the source IP. Returns 0..N issues.
        /// </summary>
        public IReadOnlyList<ValidationIssue> ValidateWrap(byte[] data, IPAddress source)
        {
            var issues = new List<ValidationIssue>();
            if (data == null || data.Length < 8)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "MALFORMED",
                    "Packet too short (<8 bytes) for VISCA-IP wrap"));
                return issues;
            }

            ushort type = (ushort)((data[0] << 8) | data[1]);
            ushort declaredLen = (ushort)((data[2] << 8) | data[3]);
            uint   seq = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
            int    actualInnerLen = data.Length - 8;

            // F4 — Unknown payload type (INFO — vendor extensions allowed)
            if (type != Type_ViscaCommand && type != Type_ViscaInquiry
                && type != Type_ViscaReply  && type != Type_ControlCommand
                && type != Type_ControlReply)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Info, "UNKNOWN_TYPE",
                    "PayloadType=0x" + type.ToString("X4") + " (not in standard set)"));
            }

            // F3 — Length field mismatch
            if (declaredLen != actualInnerLen)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "LENGTH_MISMATCH",
                    "declared=" + declaredLen + " bytes, actual inner=" + actualInnerLen + " bytes"));
            }

            // F3 — Inner VISCA frame check (only when payload looks like a VISCA command/inquiry/reply)
            if (actualInnerLen > 0
                && (type == Type_ViscaCommand || type == Type_ViscaInquiry || type == Type_ViscaReply))
            {
                byte first = data[8];
                byte last  = data[data.Length - 1];
                if ((first & 0xF0) != 0x80 && (first & 0xF0) != 0x90)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "INVALID_VISCA_HEADER",
                        "inner first byte 0x" + first.ToString("X2")
                        + " — expected 0x8x (cmd) or 0x9x (reply)"));
                }
                if (last != 0xFF)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "MISSING_VISCA_TERMINATOR",
                        "inner last byte 0x" + last.ToString("X2") + " — expected 0xFF"));
                }
            }

            // F1/F2 — Sequence regression / duplicate (per source IP)
            if (source != null)
            {
                lock (_lock)
                {
                    if (_lastSeqBySource.TryGetValue(source, out uint lastSeq))
                    {
                        if (seq < lastSeq)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "SEQ_REGRESSION",
                                "last=" + lastSeq + ", got=" + seq + " (controller reset sequence?)"));
                        }
                        else if (seq == lastSeq)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "SEQ_DUPLICATE",
                                "seq=" + seq + " repeated (replay or controller bug)"));
                        }
                    }
                    // Always update last seen seq (regardless of regression — track latest received)
                    _lastSeqBySource[source] = seq;
                }
            }

            return issues;
        }

        /// <summary>Reset all per-source state. Call from emulator's Stop().</summary>
        public void Reset()
        {
            lock (_lock) { _lastSeqBySource.Clear(); }
        }
    }
}
```

### 핵심 설계 결정

1. **Stateless validator + stateful per-source map**: validator 인스턴스는 long-lived (emulator 생존 동안 1개), 내부에 `Dictionary<IPAddress, uint>` 만 보유.
2. **Lock 단일화**: 네트워크 패킷 빈도는 ms 단위 — 단일 lock 으로 충분 (per-source lock 불필요).
3. **Result immutable**: `ValidationIssue` readonly 속성으로 caller 가 안전하게 로깅.
4. **Sequence update 정책**: regression 발생해도 last seq 는 갱신 (real CR-N300 처럼 reject 하지 않고 관찰만). 이로써 후속 seq 가 새 baseline 기준 정상이면 추가 warn 없음.
5. **VISCA-IP 시작 byte 0x9x 도 허용**: validator 가 reply (0x90 41 FF) 도 검증 가능 (양방향). 단 emulator 는 보통 RX (0x8x) 만 검증하지만, 헬퍼 자체는 양방향 지원.

---

## 4. `ViscaIpEmulator` 통합

### 4.1 변경 부분만 (diff style)

**필드 추가 (class top)**:
```csharp
private readonly ViscaIpWrapValidator _validator = new ViscaIpWrapValidator();
```

**`OnViscaIp` 시작부** (UDP 경로에서 `wrapped` 가 true 인 경우 또는 wrap 실패 케이스도):
```csharp
private async void OnViscaIp(byte[] data, IPEndPoint src, bool isTcp)
{
    // v1.1+1: wrap-level validation (UDP only — TCP raw has no wrap to validate)
    if (!isTcp && src != null)
    {
        var issues = _validator.ValidateWrap(data, src.Address);
        foreach (var issue in issues) Log(issue.Format());
    }

    byte[] inner;
    uint seq = 0;
    bool wrapped = false;
    if (!isTcp && data != null && data.Length >= 8
        && data[0] == 0x01 && (data[1] == 0x00 || data[1] == 0x10))
    {
        // ... (기존 unwrap 로직, 변동 없음)
    }
    // ... (rest 변동 없음)
}
```

**`Stop()` 메서드 끝**:
```csharp
public override void Stop()
{
    _listening = false;
    try { if (_udp != null) _udp.Dispose(); } catch { }
    try { if (_tcp != null) _tcp.Dispose(); } catch { }
    _udp = null; _tcp = null;
    _validator.Reset();   // v1.1+1: clear per-source seq state
}
```

### 4.2 통합 위치 정당성

- **OnViscaIp 진입 직후** (unwrap 전): wrap 헤더 자체가 검증 대상이므로 unwrap 무관하게 raw data 로 검증
- **isTcp=false 가드**: TCP raw 모드는 wrap 없음 → validator 호출 의미 없음
- **src.Address 사용**: source endpoint 의 port 는 무시 (Q1 결정 — IP only)
- **issues foreach Log**: 0 건이면 추가 출력 없음 → 정상 패킷 로그 형태 무변경

---

## 5. csproj 등록

```xml
<Compile Include="Protocols\Visca\ViscaIpWrapValidator.cs" />
```

기존 `Visca\` ItemGroup 에 1 line 추가. 알파벳 순서로 `ViscaCommandParser.cs` 와 `ViscaReplyBuilder.cs` 사이에 위치 (실 위치 자유, MSBuild 동작 무관).

---

## 6. 테스트 전략

### 6.1 정적 검증 (단위 — 향후 단위 테스트 추가 시 참조)

| 입력 byte | source IP | 기대 결과 |
|----------|-----------|----------|
| `01 00 00 09 00 00 00 01 81 01 06 01 08 08 01 03 FF` | 192.168.1.10 | 0 issues (정상 첫 패킷) |
| 같은 입력 다시 | 192.168.1.10 | `⚠️ SEQ_DUPLICATE (seq=1 repeated...)` |
| `01 00 00 09 00 00 00 00 81 01 06 01 ... FF` | 192.168.1.10 | `⚠️ SEQ_REGRESSION (last=1, got=0)` |
| `01 00 00 0A 00 00 00 02 81 01 ... FF` (declared 10 vs actual 9) | 192.168.1.10 | `⚠️ LENGTH_MISMATCH (declared=10, actual=9)` |
| `01 02 00 09 ... 81 01 ... FF` (type 0x0102) | 192.168.1.10 | `ℹ️ UNKNOWN_TYPE (PayloadType=0x0102 ...)` |
| `01 00 00 09 00 00 00 03 70 01 ... FF` (inner 0x70 instead of 0x8x) | 192.168.1.10 | `⚠️ INVALID_VISCA_HEADER (inner first byte 0x70...)` |

### 6.2 통합 (실측 재현)

| # | 시나리오 |
|---|---------|
| I1 | DeviceEmulator(CR-N300 UDP) Listen + 사용자 "previous program" 연결 → PT 명령 seq=0~10 정상 → Preset 명령 seq=0 으로 역행 → emulator 로그에 `⚠️ SEQ_REGRESSION (last=10, got=0)` 즉시 표시 |
| I2 | DeviceEmulator + bkit CameraController 연결 (정상 컨트롤러) → seq 단조 증가 → 0 issue 표시 (관찰성 무영향 검증) |
| I3 | Stop 후 다시 Listen → 새 세션의 seq=0 첫 패킷이 false-positive regression 안 띄움 (Reset() 동작) |

### 6.3 회귀

- PN8080Controller.sln Release 0/0
- DeviceEmulator.sln Release 0/0
- 기존 4 통합 시나리오 (CR-N300 UDP, FR-H50SN TCP/UDP, Pelco-D Serial) 동작 무변경

---

## 7. 구현 순서 (Do)

| Step | 내용 | 시간 |
|------|------|-----|
| 1 | `Xeno.Framework.Camera/Protocols/Visca/ViscaIpWrapValidator.cs` 신규 (§3 코드 그대로) | 0.4h |
| 2 | `Xeno.Framework.Camera.csproj` 1 line 등록 | 0.05h |
| 3 | `DeviceEmulator/DeviceEmulator/Emulators/ViscaIpEmulator.cs` 통합 (§4.1 diff) | 0.2h |
| 4 | 양 솔루션 Release 빌드 검증 (0/0) | 0.1h |
| 5 | 정적 검증 (§6.1 6 케이스) — REPL 또는 임시 console 출력으로 sanity check | 0.15h |
| 6 | 통합 검증 (I1 사용자 시나리오 재현 — 다음 사용자 테스트 세션 시) | (사용자) |
| **합계** | | **~0.9h** |

---

## 8. 위험 재평가 (Plan §5 대비)

| # | Plan 위험 | Design 단계 보완 |
|---|----------|------------------|
| R1 | False-positive on legitimate reconnect seq=0 | 첫 패킷 (last seq 미존재) 은 항상 0 issue. 진짜 문제는 reconnect 후 source IP 동일 + seq=0 시작 — 이 경우만 1회 false-positive. 사용자 환경 가정 (per session 1 controller) 상 무시 가능 |
| R2 | per-source state 누수 | Dictionary 단일 — IP 다양성 < 1000 가정에서 ~50KB 미만. Stop reset. v2 에서 LRU 검토 |
| R3 | inner 0xFF 다중 frame 오인 | VISCA-IP 1 wrap = 1 VISCA frame 표준 — 표준 외 케이스는 OD3 |

---

## 9. Open Items

| # | 항목 | 결정 시점 |
|---|------|----------|
| OD1 | per-source state TTL — 장기 실행 시 누적 inactive IP 정리 | v2 검토 (현재 단일 세션 가정) |
| OD2 | Validator 결과 export (JSON/CSV) — 제3자 분석 도구 연동 | v2 |
| OD3 | inner 다중 VISCA frame 패킷 (vendor 확장) — 현재 단일 frame 가정 | OD 유지 |
| OD4 | TCP raw 모드의 frame-level 검증 (wrap 없음 — 다른 검증 필요) | 별도 사이클 (TCP framing 이상 검출은 다른 카테고리) |

---

**문서 상태**: Approved (Do 진행 준비 완료)
