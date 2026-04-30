# Plan — matrix-panel-v2 (별칭 기반 패널형 매트릭스 컨트롤)

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | matrix-panel-v2 |
| **기반 모듈** | Xeno.Framework.Matrix (기존 `MatrixControl` 유지, 신규 컨트롤 추가) |
| **대상 장비** | PN-8080 (8×8), Blackmagic Videohub (가변 — 8×8, 16×16, 20×20 등) |
| **참조** | [asset/NewUI.png](../../../asset/NewUI.png), [IMatrixService.cs](../../../Xeno.Framework.Matrix/Core/IMatrixService.cs) |
| **예상 공수** | ~9시간 (Phase 1~6) |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | 기존 `MatrixControl`의 NxN 체크 그리드는 채널이 많을수록 "어떤 출력이 어떤 소스와 연결됐는지" 한눈에 확인하기 어려움. 장비별 별칭(카메라1/TV1) 없이 번호만으로 조작하는 것은 현장 오퍼레이터에게 부담. 실수로 셀을 잘못 누르면 즉시 라우팅이 변경되어 방송사고 위험. |
| **Solution** | `AliasMatrixControl` — 4행 패널형 UserControl. ① 입력 별칭(편집 가능) ② No. 번호(고정) ③ 연결 상태(읽기 전용 자동 갱신) ④ 출력 별칭(편집 가능). 입력 선택 → 연결된 출력 주황 하이라이트 → 추가 출력 선택 스테이징 → Take 버튼 또는 Auto Take 로 전송. 기존 컨트롤은 그대로 두고 병행 제공. |
| **Function UX Effect** | 한글 별칭 최대 10자 표시로 장비 식별성 향상; 연결 행의 색상·텍스트로 현재 상태 즉시 가시; Take 스테이징으로 실수 방지; 신규 누른 출력만 전송되어 네트워크 부하·깜박임 최소화; Auto Take 토글로 빠른 운용 모드 전환. |
| **Core Value** | 방송·CCTV 등 운용 환경에서 요구되는 **"별칭 가시성 + Take 확인"** 표준 패턴을 프레임워크에 포함. 타 솔루션에서 `MatrixControl`(간단) vs `AliasMatrixControl`(현장 운용) 중 용도에 맞게 선택 가능. 프레임워크 하위 호환 유지. |

---

## 1. Context

### 1.1 배경
- [matrix-controller.report.md](../../04-report/matrix-controller.report.md) 에 따라 v1 구현 완료. 기존 `MatrixControl` 은 엔지니어링/디버깅용 체크 그리드 형태로 설계됨.
- 현장 AV 엔지니어 피드백: "번호 그리드는 복잡한 장비(16×16+) 에서 어떤 소스가 어디로 가는지 파악이 어렵다", "잘못 누르면 바로 전환되어 방송 중 위험하다", "장비에 카메라1/TV1 같은 별칭을 붙여 사용하는 것이 표준이다".
- 기존 `MatrixControl` 은 엔지니어 도구로 유지하고, 신규 컨트롤은 운용자 도구로 병행 제공.

### 1.2 참조 이미지 (asset/NewUI.png)

```
┌────┬──────┬──────┬──────┬────┬────┬────┬────┬────┐
│입력│카메라1│카메라2│ PC1  │ 4  │ 5  │ 6  │ 7  │ 8  │   ← 입력 별칭 (편집 가능, 셀 선택 가능)
├────┼──────┼──────┼──────┼────┼────┼────┼────┼────┤
│No. │  1   │  2   │  3   │ 4  │ 5  │ 6  │ 7  │ 8  │   ← 단자 번호 (선택/편집 불가)
├────┼──────┼──────┼──────┼────┼────┼────┼────┼────┤
│연결│카메라1│ PC1  │카메라2│ 4  │ 5  │ 6  │ 7  │ 8  │   ← 현재 연결 상태 (읽기 전용, 연한 색)
├────┼──────┼──────┼──────┼────┼────┼────┼────┼────┤
│출력│ TV1  │ TV2  │  3   │ 4  │ 5  │ 6  │ 7  │ 8  │   ← 출력 별칭 (편집 가능, 셀 선택 가능)
└────┴──────┴──────┴──────┴────┴────┴────┴────┴────┘
                                                       [Take]  ☐ Auto Take
```

### 1.3 사용자 요구사항 (원문 요약)
1. `asset/NewUI.png` 참고
2. **No. 행**: 메트릭스 단자 번호, 셀 선택 안됨. 위쪽은 입력, 아래쪽은 출력으로 구분
3. **입력/출력 행**: 장비 별칭 편집 가능, 개별 셀 선택 가능, 기본값 = 채널 번호
4. **연결 행**: 각 출력이 어느 입력에 연결되었는지 표시. 입력 별칭 있으면 별칭, 없으면 채널 번호. 최대 한글 10자. 셀 선택 불가
5. **스위칭**: 입력 셀 클릭 → 연두색 하이라이트 + 현재 연결된 출력 셀 주황 하이라이트 → 추가 출력 셀 클릭 시 switch 명령 전송 (Auto Take 시)
6. **오동작 방지**: Take 버튼 + Auto Take 체크박스. 비-Auto Take 일 때는 스테이징 후 Take 클릭 시 **새로 눌러진 채널만** 전송. Auto Take 면 즉시 전송
7. **UI 구현**: 기본 그리드, 외부 라이브러리, 또는 기본 컨트롤 조합 UserControl 모두 허용

---

## 2. Scope

### 2.1 In Scope
- 신규 `AliasMatrixControl` UserControl (파일: `Xeno.Framework.Matrix/UI/AliasMatrixControl.cs`, `.Designer.cs`, `.resx`)
- 4행 레이아웃: 입력 별칭 / No. / 연결 상태 / 출력 별칭
- 입력·출력 별칭 편집 (더블클릭 또는 F2 로 진입, Enter/포커스 이탈로 커밋)
- 연결 행 자동 갱신 (`IMatrixService.StateUpdated` 이벤트 연동)
- 입력 선택 → 연결 출력 자동 주황 하이라이트
- Pending 라우팅 스테이징 + Take/Auto Take 메커니즘
- 기존 `IMatrixService` 재사용 + 배치 메서드 1개 추가 (`RouteBatchAsync`)
- PN8080Controller 테스트 하네스에 신·구 컨트롤 전환 수단 추가

### 2.2 Out of Scope
- 기존 `MatrixControl` 수정 (기능/외관 변경 없음)
- 라벨 별도 서버 영속화 (이번 단계는 `UserSettings` 파일 기반 로컬 영속만)
- Drag-and-drop 라우팅
- 스크립트/시퀀스 기반 자동 스위칭 (예약, 매크로)
- 비정방 매트릭스(입력수 ≠ 출력수) UI 정확성은 **Phase 1 범위 제외** — Videohub 16×32 같은 경우 `max(inputs, outputs)` 열수로 표시하되 범위 밖 셀은 Disabled 스타일로 구분 (FR-10 참조, 단순 구현)

---

## 3. Functional Requirements

| ID | 요구사항 |
|----|---------|
| **FR-01** | 4행 그리드 렌더링: 입력 별칭 / No. / 연결 / 출력 별칭. 최좌측 열에 행 레이블 "입 력 / No. / 연 결 / 출 력" 고정 표시 |
| **FR-02** | 입력·출력 별칭 셀은 더블클릭(또는 F2) 시 편집 모드 진입. Enter 로 커밋, Esc 로 취소. 포커스 이탈 시 커밋 |
| **FR-03** | 별칭 기본값은 채널 번호 문자열("1", "2", ..., "N"). 별칭이 "N" 과 동일하거나 빈 문자열이면 "기본값"으로 간주 |
| **FR-04** | No. 행은 채널 번호(1..N)만 표시. 마우스 이벤트 차단 (클릭·편집 불가). 시각적으로 ControlLight 배경 |
| **FR-05** | 연결 행은 `IMatrixService.GetInputFor(output)` 결과 기반. 해당 입력의 별칭 값 표시 (기본값이면 채널 번호). 길이 초과 시 Ellipsis (`...`) — 측정 기준: **한글 10자(대략 영문 20자) 또는 셀 폭 초과 시점** |
| **FR-06** | 연결 행 배경은 연한 하늘색(`#B0E0E6` 또는 `LightBlue` 계열). 마우스 이벤트 차단 |
| **FR-07** | 입력 행 셀 한 번 클릭 → 선택 상태 (단일 선택). 배경 연두색(`#90EE90` LightGreen 계열). 다른 입력 셀 클릭 시 이전 해제 + 새 선택 |
| **FR-08** | 입력이 선택되면 `IMatrixService.GetInputFor()` 기반으로 현재 해당 입력과 연결된 **모든 출력 셀**을 주황(`#FFB347` 계열) 하이라이트 |
| **FR-09** | 입력 선택 상태에서 출력 셀 클릭 → `_pendingRoutes[output] = selectedInput`. 해당 출력 셀 노랑/Gold(`#FFD700` 계열) 하이라이트(pending) |
| **FR-10** | **Auto Take 체크 상태**: FR-09 클릭 즉시 `IMatrixService.RouteAsync(in, out)` 전송 후 pending 즉시 제거 |
| **FR-11** | **Auto Take 해제 상태**: FR-09 클릭은 pending 으로 누적. Take 버튼 클릭 시 `RouteBatchAsync(pending)` 전송 — **현재 이미 연결된 상태와 동일한 entry 는 전송 목록에서 제외** (사용자 스펙 "추가로 눌러진 채널만 전송") |
| **FR-12** | Take 또는 Auto Take 전송 완료 후 선택/pending 모두 해제. `StateUpdated` 이벤트 수신 시 연결 행 재페인트 |
| **FR-13** | 우측 Take 버튼 + Auto Take 체크박스 — 영역 고정. Take 버튼은 `_pendingRoutes` 비어있을 때 비활성화. Auto Take 토글 시 pending 이 있으면 즉시 flush |
| **FR-14** | 스레드 안전: `StateUpdated` / `Logged` / `ConnectionStateChanged` 는 BeginInvoke 로 UI 스레드 마샬링 (기존 `MatrixControl` 동일 패턴) |
| **FR-15** | 비정방 매트릭스: 열 수 = `max(inputs, outputs)`. 범위 밖의 입력/출력 셀은 배경 회색 + 마우스 이벤트 차단 |
| **FR-16** | `AliasSettings` (별칭 데이터) 를 `public` 프로퍼티로 노출 + `AliasChanged` 이벤트. 테스트 하네스(`PN8080Controller`) 가 `UserSettings`에 저장/복원 |

---

## 4. Non-Functional Requirements

| ID | 요구사항 |
|----|---------|
| **NFR-01** | VS 2022 WinForms Designer 에서 `AliasMatrixControl` 이 폼에 드래그 시 정상 프리뷰. `InitializeComponent` 는 straight-line, 반복문 없음 (기존 `MatrixControl` 과 동일 규칙) |
| **NFR-02** | 한국어 UI. Take/Auto Take 레이블은 한글 표기 ("전송" / "자동 전송") 또는 영문 유지 여부는 구현 단계에서 최종 확정 |
| **NFR-03** | 깜박임 방지: `WM_SETREDRAW` 기반 `SuspendDrawing/ResumeDrawing` 을 BuildGrid / 일괄 페인트에 적용 (기존 `MatrixControl` 동일 헬퍼 재사용) |
| **NFR-04** | In-flight 가드: Take 전송 중 입력·출력 셀·버튼 비활성화 (기존 `_busy` 패턴 재사용) |
| **NFR-05** | 로그 일관성: 신규 컨트롤도 `IMatrixService.Logged` 이벤트 수신·표시. 별도 로그 패널 여부는 구현 단계에서 결정 (기본은 외부 로그 패널과 공유) |
| **NFR-06** | 하위 호환: `IMatrixService` 추가 메서드(`RouteBatchAsync`)는 인터페이스에 추가하되 `MatrixServiceBase` 에 기본 구현 제공 → 기존 사용자 영향 없음 |

---

## 5. 기술 접근

### 5.1 UI 구현 방식
**선택**: TableLayoutPanel + 커스텀 경량 Cell Label 조합 (**DataGridView 대신**)

이유:
- 기존 `MatrixControl` 이 이미 TableLayoutPanel 기반 — 일관성
- DataGridView 는 편집/렌더링 커스터마이징이 복잡하고 4행짜리에 과함
- TableLayoutPanel + `Label` (읽기 전용 셀) / `TextBox` (편집 시 오버레이) / `Button`(클릭 이벤트)으로 충분

셀 구성:
- **No. 행, 연결 행**: `Label` (Enabled=false on mouse events, 커스텀 배경)
- **입력·출력 행**: `Label` 기본 상태 + 더블클릭 시 `TextBox` 오버레이 (간단) 또는 `Button` FlatStyle 흉내
- 권장 단순 구현: 읽기 전용 `Label` + 편집 시 TextBox 바꿔치기

### 5.2 IMatrixService 확장

[IMatrixService.cs](../../../Xeno.Framework.Matrix/Core/IMatrixService.cs) 에 다음 추가:

```csharp
/// <summary>
/// Apply multiple routing changes as a single logical transaction.
/// Default implementation: sequential RouteAsync calls.
/// Videohub overrides to emit a single VIDEO OUTPUT ROUTING block.
/// </summary>
Task RouteBatchAsync(IDictionary<int, int> outputToInput);
```

[MatrixServiceBase.cs](../../../Xeno.Framework.Matrix/Services/MatrixServiceBase.cs) 기본 구현:
```csharp
public virtual async Task RouteBatchAsync(IDictionary<int, int> routes)
{
    if (routes == null) return;
    foreach (var kvp in routes) await RouteAsync(kvp.Value, kvp.Key);
}
```

[VideohubMatrixService.cs](../../../Xeno.Framework.Matrix/Services/VideohubMatrixService.cs) 오버라이드:
```csharp
public override async Task RouteBatchAsync(IDictionary<int, int> routes)
{
    if (routes == null || routes.Count == 0) return;
    var lines = new List<string>(routes.Count);
    foreach (var kvp in routes) lines.Add((kvp.Key - 1) + " " + (kvp.Value - 1));
    bool ok = await SendBlockAsync("VIDEO OUTPUT ROUTING", lines).ConfigureAwait(false);
    if (!ok) Log(LogDirection.Error, "Videohub batch: NAK / timeout");
    if (Mode == ConnectionMode.PerCommand)
    {
        foreach (var kvp in routes) ApplyRoute(kvp.Value, kvp.Key);
        RaiseStateUpdated();
    }
}
```

[Pn8080MatrixService.cs](../../../Xeno.Framework.Matrix/Services/Pn8080MatrixService.cs) 는 기본 구현(순차) 그대로 사용.

### 5.3 상태 모델 (AliasMatrixControl 내부)

```csharp
public sealed class AliasSettings
{
    public string[] InputAliases  { get; set; }  // length = MaxInputs;  default: "1".."N"
    public string[] OutputAliases { get; set; }  // length = MaxOutputs; default: "1".."M"
}

private AliasSettings _aliases;
private int? _selectedInput;                  // 1-based, null = 해제
private Dictionary<int, int> _pendingRoutes;  // key=output(1-based), value=input(1-based)
private bool _autoTake;
```

### 5.4 인터랙션 흐름

```
[사용자] 입력 셀 N 클릭
  → _selectedInput = N
  → 입력 셀 N 배경 LightGreen
  → 현재 GetInputFor(o) == N 인 모든 출력 o 에 대해 출력 셀 o 배경 LightSalmon
  → _pendingRoutes 는 그대로 (선택만 변경)

[사용자] 출력 셀 M 클릭 (단, _selectedInput != null 일 때만 활성)
  → 이미 GetInputFor(M) == _selectedInput 이면 무시 (이미 연결)
  → _pendingRoutes[M] = _selectedInput
  → 출력 셀 M 배경 Gold (pending)
  → Auto Take 체크 시: RouteAsync(_selectedInput.Value, M) 바로 전송 → 성공 시 pending 에서 제거 + 일반 상태로 페인트

[사용자] Take 버튼 클릭 (Auto Take 해제 상태)
  → RouteBatchAsync(_pendingRoutes)
  → 완료 후 _pendingRoutes.Clear(); _selectedInput = null;
  → StateUpdated 수신 시 연결 행 재페인트

[사용자] Auto Take 체크 박스 토글
  → 체크로 전환 시 _pendingRoutes 있으면 즉시 flush (Take 동일 동작)
  → 해제로 전환 시 즉시 동작 변경만, 기존 상태 유지
```

### 5.5 하이라이트 색상 (초안, 구현 단계 조정)

| 상태 | 색상(힌트) | 대상 |
|------|-----------|------|
| 입력 선택 | `#90EE90` (LightGreen) | 입력 행 해당 셀 |
| 현재 연결된 출력 | `#FFB347` (Pastel Orange) | 출력 행 |
| Pending (신규 매핑) | `#FFD700` (Gold) | 출력 행 |
| 연결 행 기본 배경 | `#B0E0E6` (PowderBlue) | 연결 행 전체 |
| No. 행 배경 | `SystemColors.ControlLight` | No. 행 |
| 범위 밖 셀 | `SystemColors.ControlDark` + Enabled=false | 비정방 경우 |

### 5.6 레이아웃 구조

```
AliasMatrixControl (UserControl, Dock=Fill 권장)
├── _root (TableLayoutPanel, 1 row × 2 cols: Fill, 120px)
│   ├── _grid (TableLayoutPanel, 4 rows × (N+1) cols) — Dock=Fill
│   │   ├── row 0: [Label "입 력"] + 입력 별칭 셀 × N
│   │   ├── row 1: [Label "No."]   + 번호 셀 × N
│   │   ├── row 2: [Label "연 결"] + 연결 상태 셀 × N
│   │   └── row 3: [Label "출 력"] + 출력 별칭 셀 × N
│   └── _sidePanel (Panel, 120px wide) — Dock=Right
│       ├── _btnTake (Button, Dock=Top, 80px)
│       └── _chkAutoTake (CheckBox, Dock=Top)
```

### 5.7 테스트 하네스 통합 (PN8080Controller)

[MainForm](../../../PN8080Controller/UI/MainForm.cs) 구성 변경:
- 연결 그룹 아래 현재 `MatrixControl` 대신 **TabControl** 도입
  - Tab 1: "기본 그리드" → 기존 `MatrixControl`
  - Tab 2: "별칭 패널" → 신규 `AliasMatrixControl`
- 두 컨트롤 모두 `IMatrixService` 주입 → 동일 서비스 공유
- `UserSettings` 에 `InputAliases[]`, `OutputAliases[]`, `AutoTake`, `DefaultTabIndex` 추가

---

## 6. Acceptance Criteria

| # | 기준 |
|---|------|
| 1 | NewUI.png 과 동일한 4행 레이아웃 렌더링 (입력/No./연결/출력) |
| 2 | 입력·출력 별칭 편집 가능, Enter 커밋, Esc 취소, 재실행 시 영속 복원 |
| 3 | No. 행 클릭/편집 불가 (마우스 이벤트 차단 확인) |
| 4 | 연결 행이 장비 상태 변경 시 1초 이내 자동 업데이트 |
| 5 | 연결 행에 입력 별칭 표시, 별칭 없으면 채널 번호, 10자 초과 시 Ellipsis |
| 6 | 입력 셀 클릭 → LightGreen 표시 + 연결된 출력 셀 Pastel Orange 표시 |
| 7 | 입력 선택 후 출력 셀 클릭 → (Auto Take 체크) 즉시 TX / (미체크) Gold 표시만 |
| 8 | Take 버튼 → pending 모두 전송. **이미 연결된 상태와 동일한 매핑은 TX 에서 제외** (사용자 요구 6번) |
| 9 | Take 완료 후 선택·pending 모두 해제 |
| 10 | `IMatrixService.RouteBatchAsync` 가 Videohub 에서 단일 블록 전송으로 원자적 실행 (로그 1회 TX) |
| 11 | PN-8080 에서 `RouteBatchAsync` 가 순차 `RouteAsync` 로 동작 (로그에 여러 TX) |
| 12 | 기존 `MatrixControl` 동작·외관 영향 없음 (회귀 테스트) |
| 13 | VS 2022 Designer 에서 `AliasMatrixControl` 정상 프리뷰 |
| 14 | 깜박임 없음 (Pending 하이라이트, Take, 상태 갱신 시 부드러운 전환) |

---

## 7. Risks / Mitigations

| 위험 | 완화 |
|------|------|
| TableLayoutPanel 성능 (40×40 매트릭스 + 4행 × 41열 = 164 셀) | SuspendDrawing + 셀 재사용(리사이클) + 단일 TLP |
| 편집 모드 UX (더블클릭으로 TextBox 오버레이) 구현 복잡도 | Label → TextBox 스왑 방식으로 단순화. Enter/Esc/Focus 이탈 3가지 경로만 커밋 |
| 비정방 매트릭스 UI 정확성 (Videohub 16×32 등) | Phase 1 단순 구현(`max` 열) + 범위 밖 Disabled. 피드백 후 Phase 2 개선 |
| Take 전송 중 사용자 재클릭 | `_busy` 플래그로 버튼/셀 비활성화. 기존 `MatrixControl` 패턴 재사용 |
| "이미 연결된 매핑 제외" 로직 누락 시 불필요 TX | Take 실행 직전 `GetInputFor(out) == pendingIn` 비교로 제외. 단위 검증 |
| 별칭 영속 실패 (파일 깨짐 등) | `UserSettings.Load()` 예외 catch → 기본값으로 리셋 (기존 패턴 동일) |
| 기존 컨트롤과 색상/스타일 불일치 | 공통 색상 상수 클래스 `MatrixUiColors` 도입 (선택 시 양쪽 리팩토링) |

---

## 8. Implementation Phases

| Phase | 작업 | 예상 시간 | 산출물 |
|-------|------|----------|--------|
| 1 | `IMatrixService.RouteBatchAsync` 추가 + 기본/Videohub 구현 | 1h | 인터페이스·베이스·Videohub·PN-8080 수정 |
| 2 | `AliasMatrixControl.Designer.cs` — 정적 구성 (TableLayoutPanel, Take 버튼, AutoTake 체크) | 2h | Designer + resx |
| 3 | 4행 동적 셀 생성 로직 (BuildGrid) + 기본 페인트 | 2h | `AliasMatrixControl.cs` (Build + Paint) |
| 4 | 인터랙션 흐름 (입력 선택 → 출력 하이라이트 → pending → Take) | 2h | 이벤트 핸들러 + `_busy` 가드 |
| 5 | 별칭 편집 (Label↔TextBox 스왑) + `AliasSettings` 노출 + 영속 | 1h | UserSettings 확장 |
| 6 | 테스트 하네스 TabControl 통합 + 양쪽 컨트롤 공통 서비스 주입 + 빌드/회귀 검증 | 1h | MainForm 수정, Debug/Release 빌드 |

**총 예상**: ~9시간 (단일 세션 가능 범위)

---

## 9. 영향 파일 목록 (예정)

### 신규
- [Xeno.Framework.Matrix/UI/AliasMatrixControl.cs](../../../Xeno.Framework.Matrix/UI/AliasMatrixControl.cs)
- [Xeno.Framework.Matrix/UI/AliasMatrixControl.Designer.cs](../../../Xeno.Framework.Matrix/UI/AliasMatrixControl.Designer.cs)
- [Xeno.Framework.Matrix/UI/AliasMatrixControl.resx](../../../Xeno.Framework.Matrix/UI/AliasMatrixControl.resx)
- [Xeno.Framework.Matrix/Core/AliasSettings.cs](../../../Xeno.Framework.Matrix/Core/AliasSettings.cs)

### 수정
- [Xeno.Framework.Matrix/Core/IMatrixService.cs](../../../Xeno.Framework.Matrix/Core/IMatrixService.cs) — `RouteBatchAsync` 추가
- [Xeno.Framework.Matrix/Services/MatrixServiceBase.cs](../../../Xeno.Framework.Matrix/Services/MatrixServiceBase.cs) — 기본 구현
- [Xeno.Framework.Matrix/Services/VideohubMatrixService.cs](../../../Xeno.Framework.Matrix/Services/VideohubMatrixService.cs) — 단일 블록 override
- [Xeno.Framework.Matrix/Xeno.Framework.Matrix.csproj](../../../Xeno.Framework.Matrix/Xeno.Framework.Matrix.csproj) — 새 파일 추가
- [PN8080Controller/UI/MainForm.cs](../../../PN8080Controller/UI/MainForm.cs) + `MainForm.Designer.cs` — TabControl 도입
- [PN8080Controller/UserSettings.cs](../../../PN8080Controller/UserSettings.cs) — 별칭·Auto Take·기본 탭 영속

### 영향 없음 (회귀 확인 대상)
- [Xeno.Framework.Matrix/UI/MatrixControl.cs](../../../Xeno.Framework.Matrix/UI/MatrixControl.cs) — 변경 없음
- 기존 PDCA 보고서

---

## 10. 다음 단계

- [ ] `/pdca design matrix-panel-v2` — Design 문서 작성 (상세 클래스/메서드 시그니처, 이벤트 시퀀스 다이어그램, 색상·폰트·여백 구체 수치)
- [ ] Design 승인 후 `/pdca do matrix-panel-v2` — 구현 시작
- [ ] 구현 완료 후 `/pdca analyze matrix-panel-v2` — Gap 분석
- [ ] 목표 충족 시 `/pdca report matrix-panel-v2` — 완료 보고서

---

## 11. Open Questions (Design 단계에서 최종 결정)

1. Take / Auto Take 레이블은 한글("전송"/"자동 전송") vs 영어("Take"/"Auto Take") — 사용자 피드백 반영
2. 별칭 편집 UX — 더블클릭 진입 vs F2 키 vs 느슨한 싱글 클릭 후 Enter (현장 표준 반영 필요)
3. 연결 행 10자 초과 표시 — Ellipsis 기준이 **문자 수(10)** vs **픽셀 폭(셀 너비)** — 폰트에 따라 달라짐, 측정 기반이 권장
4. 별칭 영속 범위 — (a) 장비 IP 별 별도 저장 / (b) 앱 단일 저장 — 여러 장비 관리 시 (a) 선호
5. Pending 표시 색상 — Gold(#FFD700) 가 출력행 Orange(#FFB347) 와 구분 충분한지 — 구현 후 시각 확인
