# Design — matrix-panel-v2 (별칭 기반 패널형 매트릭스 컨트롤)

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | matrix-panel-v2 |
| **참조 Plan** | [matrix-panel-v2.plan.md](../../01-plan/features/matrix-panel-v2.plan.md) |
| **대상 솔루션** | `PN-8080 Controller.sln` (Xeno.Framework.Matrix + PN8080Controller) |
| **신규 파일** | 5개 (Core 1, UI 3, 상수 1) |
| **수정 파일** | 6개 (Interface 1, Service 2, csproj 1, 테스트 하네스 2) |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | Plan의 Open Question 5건을 의사결정으로 해소하지 않으면 구현 단계에서 UX/영속성/색상 시안이 흔들려 재작업 발생. 또한 기존 `IMatrixService` 는 단발 Route 만 지원 → Take 배치 전송 시 Videohub 에서도 순차 TX가 되어 원자성 상실. |
| **Solution** | 5개 Open Question 모두 확정 + 각 파일·클래스의 구체 시그니처·필드·이벤트 흐름·색상 수치·예외 처리 정의. `IMatrixService.RouteBatchAsync` 로 Videohub 단일 블록 전송/PN-8080 순차 전송을 추상화. 편집 UX는 Label↔TextBox 스왑. 별칭 영속은 `Host:Port` 키로 장비별 분리. |
| **Function UX Effect** | 구현자가 문서만 보고 5개 파일 생성 + 6개 파일 수정으로 바로 작성 가능. 모든 색상·폰트·여백 수치가 사전 확정되어 디자이너에서 수정 재작업 최소화. 엣지 케이스(비정방 매트릭스, 편집 중 서비스 상태 변경, Take 중 재클릭) 별도 명시. |
| **Core Value** | Design이 "구현 전 최종 기술 계약" 역할. Do 단계에서는 Design 문서 체크리스트만 따라가면 되므로 개발 속도↑·버그↓. 또한 `RouteBatchAsync` 는 이번 UI를 넘어 향후 시나리오(프리셋/매크로) 에서도 재사용 가능한 기초 API로 확장. |

---

## 1. Open Questions 해결 (Plan §11)

| # | 질문 | 결정 | 근거 |
|---|------|------|------|
| 1 | Take / Auto Take 레이블 한/영 | **영문 유지** "Take" / "Auto Take" | 방송/AV 업계 표준 용어. 사용자 원문도 "Take 버턴" 명시. |
| 2 | 별칭 편집 UX | **더블클릭 → 편집, Enter/LostFocus=커밋, Esc=취소** | Windows 표준(탐색기·Excel과 동일). F2 는 접근성 키로 추가(키보드 사용자). |
| 3 | Ellipsis 기준 | **픽셀 폭 기반**(셀 너비 초과 시 `TextFormatFlags.EndEllipsis`) + 문자 수 10자 상한 | 폰트/크기에 따라 체감 상이. WinForms `TextRenderer.DrawText` 가 내장 처리. |
| 4 | 별칭 영속 범위 | **Host:Port 키 기반**(장비별 분리) | 여러 장비 관리 시 필수. 현재 `UserSettings` INI 에 키 prefix 로 저장. |
| 5 | Pending 색상 구분 | **Gold `#FFD700`** (Orange `#FFB347` 및 Green `#90EE90` 과 충분히 구분) | 3색 모두 한 화면에 보일 수 있으므로 명도·채도 차이가 큰 Gold 채택. 구현 후 시각 검증. |

---

## 2. 아키텍처 오버뷰

```
┌─────────────────────────────────────────────────────────────┐
│ PN8080Controller (WinExe, 테스트 하네스)                    │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ MainForm                                                │ │
│ │  ├── _grpConnection (기존 그대로)                       │ │
│ │  └── _tabControl (NEW)                                  │ │
│ │      ├── Tab1 "기본 그리드"  → MatrixControl (기존)    │ │
│ │      └── Tab2 "별칭 패널"    → AliasMatrixControl (NEW)│ │
│ │                                 ↑                       │ │
│ │     두 컨트롤 모두 AttachService(_service) 로 공유      │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ Xeno.Framework.Matrix (Class Library)                       │
│ ┌───────────────┐     ┌──────────────────────┐              │
│ │ IMatrixService│◄────│ MatrixServiceBase    │              │
│ │  + RouteBatch │     │  + RouteBatch default│              │
│ └───────────────┘     │    (순차)            │              │
│        ▲              └──────────────────────┘              │
│        │                 ▲              ▲                   │
│        │                 │              │                   │
│     ┌──┴───────┐   ┌─────┴────────┐  ┌──┴────────────┐      │
│     │UI        │   │Pn8080        │  │Videohub       │      │
│     │controls  │   │MatrixService │  │MatrixService  │      │
│     └──────────┘   │(inherit      │  │+ RouteBatch   │      │
│                    │ default)     │  │  override     │      │
│                    └──────────────┘  │ (single block)│      │
│                                      └───────────────┘      │
│                                                             │
│  UI:                                                        │
│  ┌────────────────┐   ┌────────────────────────┐            │
│  │ MatrixControl  │   │ AliasMatrixControl (NEW)│           │
│  │  (기존, 유지)   │   │  - 4행 그리드            │           │
│  └────────────────┘   │  - Take/Auto Take       │            │
│                       │  - AliasSettings 속성    │            │
│                       └────────────────────────┘            │
│                                                             │
│  Core:                                                      │
│  + AliasSettings (NEW)                                      │
│  + AliasChangedEventArgs (NEW)                              │
│  + MatrixUiColors (NEW, 색상 상수 공유)                     │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. 인터페이스 변경 — `IMatrixService.RouteBatchAsync`

### 3.1 인터페이스 선언

[Xeno.Framework.Matrix/Core/IMatrixService.cs](../../../Xeno.Framework.Matrix/Core/IMatrixService.cs):

```csharp
/// <summary>
/// Apply multiple routing changes as a single logical transaction.
/// Videohub batches them into one VIDEO OUTPUT ROUTING block (atomic at protocol level).
/// PN-8080 falls back to sequential RouteAsync calls (no atomic protocol support).
/// </summary>
/// <param name="outputToInput">
/// Map of output port (1-based, key) to input port (1-based, value). Empty map returns immediately.
/// </param>
Task RouteBatchAsync(IDictionary<int, int> outputToInput);
```

### 3.2 MatrixServiceBase 기본 구현

[Services/MatrixServiceBase.cs](../../../Xeno.Framework.Matrix/Services/MatrixServiceBase.cs) 에 추가:

```csharp
public virtual async Task RouteBatchAsync(IDictionary<int, int> outputToInput)
{
    if (outputToInput == null || outputToInput.Count == 0) return;
    foreach (var kvp in outputToInput)
    {
        await RouteAsync(kvp.Value, kvp.Key).ConfigureAwait(false);
    }
}
```

**책임**: 기본은 단순 순차. 각 `RouteAsync` 에 대해 개별 TX/RX 발생.

### 3.3 VideohubMatrixService override

```csharp
public override async Task RouteBatchAsync(IDictionary<int, int> outputToInput)
{
    if (outputToInput == null || outputToInput.Count == 0) return;
    var lines = new List<string>(outputToInput.Count);
    foreach (var kvp in outputToInput)
    {
        lines.Add(string.Format("{0} {1}", kvp.Key - 1, kvp.Value - 1));
    }
    bool ok = await SendBlockAsync("VIDEO OUTPUT ROUTING", lines).ConfigureAwait(false);
    if (!ok) Log(LogDirection.Error, "Videohub batch: NAK / timeout");
    if (Mode == ConnectionMode.PerCommand)
    {
        foreach (var kvp in outputToInput) ApplyRoute(kvp.Value, kvp.Key);
        RaiseStateUpdated();
    }
}
```

**책임**: 하나의 `VIDEO OUTPUT ROUTING:` 블록에 여러 라인 포함 → 단일 ACK. 프로토콜 원자성 보장.

### 3.4 Pn8080MatrixService

**override 불필요** — base class 기본 구현(순차) 그대로 사용. ASCII 프로토콜이 배치 문법을 지원하지 않음.

### 3.5 영향

- 인터페이스에 메서드 1개 추가 → **breaking change**. 외부 구현체가 있으면 컴파일 에러.
- 완화: 이번 프로젝트는 내부만 구현 중이므로 영향 없음. 향후 인터페이스 공개 시 버전 릴리즈 노트에 명기.

---

## 4. `AliasSettings` — 신규 데이터 모델

### 4.1 파일 경로

`Xeno.Framework.Matrix/Core/AliasSettings.cs` (신규)

### 4.2 클래스 정의

```csharp
namespace Xeno.Framework.Matrix.Core
{
    public sealed class AliasSettings
    {
        public string[] InputAliases { get; set; }
        public string[] OutputAliases { get; set; }

        public const int MaxAliasLength = 10;  // Korean chars (≈ 20 ASCII)

        public static AliasSettings CreateDefault(int inputs, int outputs);
        public string GetInputAlias(int input1Based);
        public string GetOutputAlias(int output1Based);
        public bool SetInputAlias(int input1Based, string value);    // returns changed
        public bool SetOutputAlias(int output1Based, string value);  // returns changed
        public AliasSettings Clone();
        public bool IsDefaultInputAlias(int input1Based);            // value == N.ToString() or null/empty
        public bool IsDefaultOutputAlias(int output1Based);
        public void Resize(int inputs, int outputs);                 // keep existing values within range
    }

    public sealed class AliasChangedEventArgs : EventArgs
    {
        public bool IsInput { get; }
        public int Index { get; }   // 1-based
        public string OldValue { get; }
        public string NewValue { get; }
        public AliasChangedEventArgs(bool isInput, int index, string oldValue, string newValue);
    }
}
```

### 4.3 정규화 규칙

- `SetInputAlias(3, "카메라1")` → `InputAliases[2] = "카메라1"`
- `SetInputAlias(3, null)` or `SetInputAlias(3, "")` → `InputAliases[2] = null` (= 기본값 표시)
- `SetInputAlias(3, "  ")` → Trim → null
- `SetInputAlias(3, "A".repeat(15))` → 앞 10자만 유지 (한글 기준. UTF-16 code point count 로 측정. `value.Length > 10 ? value.Substring(0, 10) : value`)
- `IsDefaultInputAlias(3)` → `InputAliases[2] == null` 또는 `InputAliases[2] == "3"` 일 때 true

### 4.4 GetInputAlias 동작

```csharp
public string GetInputAlias(int input1Based)
{
    if (InputAliases == null || input1Based < 1 || input1Based > InputAliases.Length)
        return input1Based.ToString();
    var v = InputAliases[input1Based - 1];
    return string.IsNullOrEmpty(v) ? input1Based.ToString() : v;
}
```

---

## 5. `MatrixUiColors` — 공통 색상 상수

### 5.1 파일 경로

`Xeno.Framework.Matrix/UI/MatrixUiColors.cs` (신규)

### 5.2 상수 정의

```csharp
namespace Xeno.Framework.Matrix.UI
{
    internal static class MatrixUiColors
    {
        // Cell selection states
        public static readonly Color InputSelected     = Color.FromArgb(144, 238, 144); // #90EE90 LightGreen
        public static readonly Color OutputConnected   = Color.FromArgb(255, 179, 71);  // #FFB347 Pastel Orange
        public static readonly Color OutputPending     = Color.FromArgb(255, 215, 0);   // #FFD700 Gold
        public static readonly Color CellDefault       = Color.White;
        public static readonly Color CellDisabled      = SystemColors.ControlDark;

        // Row backgrounds
        public static readonly Color NoRowBackground   = SystemColors.ControlLight;     // No. row
        public static readonly Color ConnectRowBackground = Color.FromArgb(176, 224, 230); // #B0E0E6 PowderBlue
        public static readonly Color RowLabelBackground   = SystemColors.ControlLight;    // leftmost label

        // Borders
        public static readonly Color CellBorder        = Color.Silver;
    }
}
```

---

## 6. `AliasMatrixControl` — 신규 UserControl

### 6.1 파일 경로

- `Xeno.Framework.Matrix/UI/AliasMatrixControl.cs`
- `Xeno.Framework.Matrix/UI/AliasMatrixControl.Designer.cs`
- `Xeno.Framework.Matrix/UI/AliasMatrixControl.resx`

### 6.2 공개 API

```csharp
namespace Xeno.Framework.Matrix.UI
{
    public partial class AliasMatrixControl : UserControl
    {
        public AliasMatrixControl();

        // --- Public API ---
        public void AttachService(IMatrixService service);
        public void DetachService();
        public IMatrixService Service { get; }         // Browsable(false)

        public AliasSettings Aliases { get; set; }     // 외부 주입 가능. 이 setter는 그리드 재페인트 트리거.
        public bool AutoTake { get; set; }

        public event EventHandler<AliasChangedEventArgs> AliasChanged;
        public event EventHandler TakeCompleted;       // 전송 완료 시 1회 발화
        public event EventHandler<int> PendingCountChanged;  // pending 수 변경 시
    }
}
```

### 6.3 내부 상태

```csharp
private IMatrixService _service;
private AliasSettings _aliases;
private readonly Dictionary<int, int> _pendingRoutes = new Dictionary<int, int>();
private int? _selectedInput;
private Label[,] _cells;              // [row, col], 1-based col; rows: 0=input, 1=no, 2=connect, 3=output
private Label _inputHeader, _noHeader, _connectHeader, _outputHeader;  // leftmost column labels
private TextBox _editOverlay;         // 단일 공유 TextBox (Label↔TextBox 스왑용)
private Label _editingLabel;          // 현재 편집 중인 Label 참조 (null = 편집 모드 아님)
private bool _editingIsInput;         // true = 입력 별칭, false = 출력 별칭
private int _editingIndex;            // 1-based
private bool _busy;
private int _columns;                 // = Max(InputCount, OutputCount)
private int _inputCount, _outputCount;
```

### 6.4 레이아웃 (Designer.cs 정적 구성)

```
AliasMatrixControl (UserControl)
├── _grid (TableLayoutPanel, Dock=Fill)    <- 추가 후 Fill
│   - ColumnCount = 1 (row label)
│   - RowCount = 4
│   - 실제 N+1 열은 런타임에 동적 추가 (BuildGrid)
│   - Visible=true 로 유지. 디자이너에서 4x1 프리뷰 표시
├── _sidePanel (Panel, Dock=Right, Width=140)   <- 추가 후 Right (grid 보다 먼저 Add)
│   ├── _btnTake (Button, Dock=Top, Height=60)
│   │   - Text = "Take"
│   │   - Font = Segoe UI 12pt Bold
│   │   - Enabled = false (초기, pending 없을 때)
│   ├── _chkAutoTake (CheckBox, Dock=Top, Height=30, Padding=8)
│   │   - Text = "Auto Take"
│   ├── _lblPending (Label, Dock=Top, Height=24, TextAlign=Center)
│   │   - Text = "Pending: 0"
│   └── _lblInstructions (Label, Dock=Fill, TextAlign=Top, Font=Segoe UI 9pt)
│       - Text = "입력 셀 선택 후\n출력 셀 클릭"
```

**Designer 호환 주의**:
- `InitializeComponent` 에서 `_grid.RowCount = 4`, `_grid.ColumnCount = 1` 만 설정. 동적 부분은 생성자 후 `BuildGrid()` 에서.
- `SuspendLayout/ResumeLayout` 쌍 적용.
- `_grid` 먼저 Add (Dock=Fill), `_sidePanel` 나중 Add (Dock=Right). Z-order 에 따라 _sidePanel 이 먼저 우측 140px 차지, 남은 영역은 _grid.
- 잠깐 — 실제 WinForms 는 **마지막 Add 된 컨트롤이 먼저 도킹**. 따라서 Fill 을 먼저 Add, Right 를 나중 Add 하면 Right 가 먼저 140px 차지하고 Fill 이 나머지를 채움. 올바름.

### 6.5 `BuildGrid()` — 런타임 동적 구성

```csharp
private void BuildGrid()
{
    int inputs = _service?.Device?.InputCount ?? _aliases?.InputAliases?.Length ?? 8;
    int outputs = _service?.Device?.OutputCount ?? _aliases?.OutputAliases?.Length ?? 8;
    int columns = Math.Max(inputs, outputs);
    _columns = columns;
    _inputCount = inputs;
    _outputCount = outputs;

    SuspendDrawing(_grid);
    _grid.SuspendLayout();
    try
    {
        _grid.Controls.Clear();
        _grid.ColumnStyles.Clear();
        _grid.RowStyles.Clear();

        int totalCols = columns + 1;  // +1 for row label
        _grid.ColumnCount = totalCols;
        _grid.RowCount = 4;

        // Column 0 (row label): fixed 60px
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60f));
        // Columns 1..N: equal percent
        for (int c = 1; c < totalCols; c++)
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));

        // Row heights: equal
        for (int r = 0; r < 4; r++)
            _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

        // Row labels (column 0)
        _grid.Controls.Add(MakeRowLabel("입 력"), 0, 0);
        _grid.Controls.Add(MakeRowLabel("No."),   0, 1);
        _grid.Controls.Add(MakeRowLabel("연 결"), 0, 2);
        _grid.Controls.Add(MakeRowLabel("출 력"), 0, 3);

        // Data cells
        _cells = new Label[4, columns + 1]; // [row, col], col 1..N
        for (int c = 1; c <= columns; c++)
        {
            _cells[0, c] = BuildInputAliasCell(c);
            _cells[1, c] = BuildNoCell(c);
            _cells[2, c] = BuildConnectCell(c);
            _cells[3, c] = BuildOutputAliasCell(c);

            _grid.Controls.Add(_cells[0, c], c, 0);
            _grid.Controls.Add(_cells[1, c], c, 1);
            _grid.Controls.Add(_cells[2, c], c, 2);
            _grid.Controls.Add(_cells[3, c], c, 3);
        }

        PaintAll();
    }
    finally
    {
        _grid.ResumeLayout(false);
        _grid.PerformLayout();
        ResumeDrawing(_grid);
    }
}
```

### 6.6 셀 생성 메서드 (헬퍼)

```csharp
private Label MakeRowLabel(string text) { /* Bold, Dock=Fill, BG=RowLabelBackground, 1px border */ }
private Label BuildInputAliasCell(int col1Based)  { /* Click/DoubleClick wired */ }
private Label BuildOutputAliasCell(int col1Based) { /* Click/DoubleClick wired */ }
private Label BuildNoCell(int col1Based)          { /* Enabled=false for mouse, BG=NoRowBackground */ }
private Label BuildConnectCell(int col1Based)     { /* Enabled=false for mouse, BG=ConnectRowBackground */ }
```

**Input/Output 셀의 Disabled 처리**: 
- 비정방 매트릭스에서 범위 밖 셀은 `cell.Enabled = false; cell.BackColor = CellDisabled;`
- No/Connect 셀은 `Enabled=false` 로 시작하여 포인터 이벤트 무시.

### 6.7 페인트 로직

```csharp
private void PaintAll()
{
    SuspendDrawing(_grid);
    try
    {
        for (int c = 1; c <= _columns; c++)
        {
            PaintInputCell(c);
            PaintNoCell(c);
            PaintConnectCell(c);
            PaintOutputCell(c);
        }
    }
    finally { ResumeDrawing(_grid); }
}

private void PaintInputCell(int c)
{
    var cell = _cells[0, c];
    if (c > _inputCount) { cell.Enabled = false; cell.BackColor = MatrixUiColors.CellDisabled; cell.Text = ""; return; }
    cell.Text = _aliases?.GetInputAlias(c) ?? c.ToString();
    cell.BackColor = (_selectedInput == c) ? MatrixUiColors.InputSelected : MatrixUiColors.CellDefault;
}

private void PaintOutputCell(int c)
{
    var cell = _cells[3, c];
    if (c > _outputCount) { cell.Enabled = false; cell.BackColor = MatrixUiColors.CellDisabled; cell.Text = ""; return; }
    cell.Text = _aliases?.GetOutputAlias(c) ?? c.ToString();

    if (_pendingRoutes.ContainsKey(c))
        cell.BackColor = MatrixUiColors.OutputPending;
    else if (_selectedInput.HasValue && _service != null && _service.GetInputFor(c) == _selectedInput.Value)
        cell.BackColor = MatrixUiColors.OutputConnected;
    else
        cell.BackColor = MatrixUiColors.CellDefault;
}

private void PaintConnectCell(int c)
{
    var cell = _cells[2, c];
    if (c > _outputCount) { cell.Enabled = false; cell.BackColor = MatrixUiColors.CellDisabled; cell.Text = ""; return; }
    int input = _service?.GetInputFor(c) ?? 0;
    string display = input >= 1 ? (_aliases?.GetInputAlias(input) ?? input.ToString()) : "";
    cell.Text = display;
    cell.BackColor = MatrixUiColors.ConnectRowBackground;
    // Ellipsis handled by TextRenderer at Paint time (AutoEllipsis = true)
}

private void PaintNoCell(int c) { /* static; "1"~"N" */ }
```

**Ellipsis**: `Label.AutoEllipsis = true; Label.AutoSize = false` 로 셀 너비 초과 시 "..." 자동 처리. 추가로 최대 10자 `Substring` 절단.

### 6.8 이벤트 시퀀스

#### 6.8.1 입력 셀 클릭

```
1. Label.Click → OnInputCellClick(col)
2. if _busy || cell.Enabled==false: return
3. if _editingLabel != null: CancelEdit()
4. _selectedInput = col (= 이전과 같으면 null 로 토글해 해제)
5. PaintAll()
6. UpdateTakeButtonState()
```

#### 6.8.2 입력 셀 더블클릭

```
1. Label.DoubleClick → OnInputCellDoubleClick(col)
2. if _busy: return
3. BeginEdit(isInput=true, col)
   → Hide Label, place _editOverlay over the cell, populate text, focus + select-all
```

#### 6.8.3 편집 커밋

```
_editOverlay events:
  KeyDown(Enter) → CommitEdit()
  KeyDown(Escape) → CancelEdit()
  Leave → CommitEdit()  (LostFocus)

CommitEdit():
  string newValue = _editOverlay.Text.Trim();
  if newValue.Length > MaxAliasLength: newValue = newValue.Substring(0, MaxAliasLength);
  bool changed = _editingIsInput
     ? _aliases.SetInputAlias(_editingIndex, newValue)
     : _aliases.SetOutputAlias(_editingIndex, newValue);
  if (changed) AliasChanged?.Invoke(this, new AliasChangedEventArgs(...));
  EndEdit();
  PaintAll();
```

#### 6.8.4 출력 셀 클릭

```
OnOutputCellClick(col):
  if _busy || cell.Enabled==false: return
  if _editingLabel != null: CancelEdit()
  if _selectedInput == null: return  (beep? or log hint)
  int srcIn = _selectedInput.Value;
  if _service.GetInputFor(col) == srcIn: return  (이미 연결 — no-op)
  if _pendingRoutes.TryGetValue(col, out int existing) && existing == srcIn:
    return  (이미 pending 중)
  _pendingRoutes[col] = srcIn
  PaintOutputCell(col)
  UpdateTakeButtonState()
  PendingCountChanged?.Invoke(this, _pendingRoutes.Count)
  if _autoTake: _ = TakeAsync()
```

#### 6.8.5 Take 버튼 클릭 / AutoTake 자동

```
TakeAsync():
  if _pendingRoutes.Count == 0: return
  if _service == null || !_service.IsConnected : 로그 후 return

  // 이미 현재 라우팅과 동일한 entry 제외
  var effective = new Dictionary<int,int>();
  foreach (kvp in _pendingRoutes):
    if _service.GetInputFor(kvp.Key) != kvp.Value:
      effective[kvp.Key] = kvp.Value

  if effective.Count == 0:
    AppendLog("모든 pending 항목이 이미 연결된 상태와 동일합니다. 전송 생략.")
    _pendingRoutes.Clear(); _selectedInput = null; PaintAll(); return

  _busy = true; SetUiBusy(true)
  try:
    var sw = Stopwatch.StartNew()
    await _service.RouteBatchAsync(effective)
    sw.Stop()
    AppendLog($"Take: {effective.Count}건 전송 완료 {sw.ElapsedMilliseconds}ms")
    TakeCompleted?.Invoke(this, EventArgs.Empty)
  catch (Exception ex):
    AppendLog(error: ex.Message)
  finally:
    _pendingRoutes.Clear()
    _selectedInput = null
    _busy = false; SetUiBusy(false)
    PaintAll()
```

#### 6.8.6 Auto Take 토글

```
_chkAutoTake.CheckedChanged → OnAutoTakeChanged
  _autoTake = _chkAutoTake.Checked
  if _autoTake && _pendingRoutes.Count > 0: _ = TakeAsync()
  UpdateTakeButtonState()
```

#### 6.8.7 서비스 이벤트 수신

```
_service.StateUpdated → InvokeIfRequired(PaintAll)
_service.DeviceInfoChanged → InvokeIfRequired:
  int newIn = svc.Device.InputCount, newOut = svc.Device.OutputCount
  if newIn != _inputCount || newOut != _outputCount:
    _aliases.Resize(newIn, newOut)
    BuildGrid()
  PaintAll()
_service.Logged → (기존 MatrixControl 과 동일 외부 로그 브릿지 — 6.9 참조)
_service.ConnectionStateChanged → UpdateTakeButtonState + (연결 끊김 시) _pendingRoutes.Clear + PaintAll
```

### 6.9 로그 브릿지

AliasMatrixControl 자체에 로그 패널을 두지 **않고**, `IMatrixService.Logged` 를 외부(MainForm 또는 기존 MatrixControl) 로 공유. 이유: 두 컨트롤이 같은 서비스를 바라볼 때 로그가 한 곳에 모이는 것이 UX상 자연스러움.

- AliasMatrixControl 은 자체 타이밍 로그(`Take: N건 전송 완료 Nms`)를 생성하되, `_service.Logged` 이벤트를 중계하지는 않음.
- 로그 수신·표시는 MainForm 이 담당 (테스트 하네스의 로그 TextBox가 공동 소비).

### 6.10 Busy 상태

```csharp
private void SetUiBusy(bool busy)
{
    _btnTake.Enabled = !busy && _pendingRoutes.Count > 0;
    _chkAutoTake.Enabled = !busy;
    _grid.Enabled = !busy;
    Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
}
```

(기존 `MatrixControl.SetUiBusy` 와 동일 패턴. 공통화는 차후 리팩토링 후보.)

### 6.11 깜박임 방지

`MatrixControl` 의 `WM_SETREDRAW` 기반 `SuspendDrawing/ResumeDrawing` 메서드를 `AliasMatrixControl` 에도 **static helper 로 복사** 또는 `internal static class DrawingHelpers` 로 추출하여 공유.

권장: `Xeno.Framework.Matrix/UI/DrawingHelpers.cs` 신규 파일에 `internal static class DrawingHelpers { SuspendDrawing, ResumeDrawing }` 두고 두 컨트롤이 공유. `MatrixControl` 의 기존 메서드도 이 helper 호출하도록 교체(선택적 리팩토링).

**결정**: 범위 줄이기 위해 이번에는 `AliasMatrixControl` 내부에 copy-paste. 중복 제거 리팩토링은 후속 과제.

---

## 7. 테스트 하네스 (PN8080Controller) 변경

### 7.1 MainForm 변경

[MainForm.Designer.cs](../../../PN8080Controller/UI/MainForm.Designer.cs):
- `_matrixControl` 제거
- 신규 `_tabControl` (TabControl, Dock=Fill) 추가
  - TabPage1 "기본 그리드" → 기존 `_matrixControl` 배치
  - TabPage2 "별칭 패널" → 신규 `_aliasMatrixControl` 배치
- Controls.Add 순서: `_tabControl` 먼저, `_grpConnection` 나중 (Dock=Top 규칙)

[MainForm.cs](../../../PN8080Controller/UI/MainForm.cs) 수정:
```csharp
// Attach 시 두 컨트롤에 모두
_matrixControl.AttachService(service);
_aliasMatrixControl.AttachService(service);
_aliasMatrixControl.Aliases = _settings.GetAliasesFor(service.Host, service.Port, service.MaxInputs, service.MaxOutputs);
_aliasMatrixControl.AutoTake = _settings.AutoTake;

// Detach 시 둘 다
_matrixControl.DetachService();
_aliasMatrixControl.DetachService();

// AliasMatrixControl 이벤트 → UserSettings 동기화
_aliasMatrixControl.AliasChanged += (s, e) => _settings.SaveAliasChange(host, port, e);
_aliasMatrixControl.AutoTakeChanged += (s, e) => _settings.AutoTake = _aliasMatrixControl.AutoTake;
```

### 7.2 UserSettings 확장

[UserSettings.cs](../../../PN8080Controller/UserSettings.cs):

```csharp
public bool AutoTake { get; set; }
public int DefaultTabIndex { get; set; }  // 0 = 기본 그리드, 1 = 별칭 패널

// Host:Port 별 별칭 저장. Key format: "Aliases_{host}_{port}_Inputs|Outputs"
private readonly Dictionary<string, string[]> _aliasCache = new();

public AliasSettings GetAliasesFor(string host, int port, int inputs, int outputs);
public void SaveAliasChange(string host, int port, AliasChangedEventArgs e);
```

### 7.3 INI 파일 포맷 예시

`%LocalAppData%\PN8080Controller\settings.ini`:
```
Device=BlackmagicVideohub
Host=192.168.10.199
Port=9990
Mode=Persistent
AutoReconnect=true
MaxInputs=20
MaxOutputs=20
AutoTake=false
DefaultTabIndex=1
Aliases_192.168.1.100_8000_Inputs=카메라1|카메라2|PC1|||||
Aliases_192.168.1.100_8000_Outputs=TV1|TV2|||||||
Aliases_192.168.10.199_9990_Inputs=||||||||||||||||||||
Aliases_192.168.10.199_9990_Outputs=||||||||||||||||||||
```

- Pipe(`|`) 구분 문자열. 빈 entry = 기본값 (채널 번호).
- `Aliases_{host}_{port}_Inputs` / `_Outputs`

### 7.4 App.config 기본값 추가

```xml
<add key="DefaultAutoTake" value="false" />
<add key="DefaultTabIndex" value="0" />
```

---

## 8. 엣지 케이스 처리

| 시나리오 | 동작 |
|----------|------|
| 편집 중 서비스 상태 변경 (StateUpdated) | `_editingLabel != null` 이면 연결 행만 업데이트. 편집 중 Label 은 유지. |
| 편집 중 DeviceInfoChanged (크기 변경) | CancelEdit() → BuildGrid() → PaintAll() |
| 편집 중 연결 끊김 | CommitEdit() (LostFocus 로 자연 커밋) → 별칭은 유지. pending 은 클리어. |
| 선택된 입력이 out-of-range (비정방) | _selectedInput = null 로 보정 + PaintAll() |
| Take 중 AutoTake 체크 해제 | 이미 in-flight 는 완료까지 진행. 이후 pending 만 수동 Take 대기. |
| Take 중 Disconnect 발생 | RouteBatchAsync 가 예외 → catch 후 AppendLog. pending 유지(사용자가 재연결 후 재시도 가능). |
| 동일 셀을 pending 로 두 번 클릭 | 두 번째 클릭은 _pendingRoutes 에 동일 값이면 no-op, 다르면 덮어씀(새로운 selectedInput 값 적용). |
| 선택된 입력과 동일 입력 행 셀 재클릭 | 선택 해제 (토글). `_selectedInput == col ? null : col` |
| 편집 도중 다른 셀 클릭 | CommitEdit() (LostFocus) 후 새 셀 클릭 처리 |

---

## 9. 색상·폰트·치수 최종 수치

| 요소 | 값 |
|------|-----|
| 셀 기본 폰트 | Segoe UI 10pt Regular (한글은 맑은 고딕으로 시스템 폴백) |
| 행 레이블 폰트 | Segoe UI 10pt **Bold** |
| Take 버튼 폰트 | Segoe UI 12pt Bold |
| 셀 최소 높이 | 36px (행별 25% 공평 분배, 창 높이 144px 이상이면 충족) |
| 행 레이블 열 너비 | 60px 고정 (`SizeType.Absolute`) |
| 데이터 열 너비 | `SizeType.Percent`, 100f / columns |
| 사이드 패널 너비 | 140px 고정 |
| 셀 Border | 1px, `Color.Silver` (`FlatStyle.Flat` 또는 `BorderStyle.FixedSingle`) |
| Cell Text Padding | 좌우 4px |
| 편집 TextBox BorderStyle | `FixedSingle` |
| 편집 TextBox MaxLength | 10 |
| 입력 선택 BG | `#90EE90` |
| 현재 연결 BG | `#FFB347` |
| Pending BG | `#FFD700` |
| 연결 행 BG | `#B0E0E6` |
| No 행 BG | `SystemColors.ControlLight` (#F0F0F0) |
| Disabled 셀 BG | `SystemColors.ControlDark` (#A0A0A0) |

---

## 10. Acceptance Criteria 매핑

Plan §6 AC → Design 구현 포인트

| AC | Design 참조 |
|----|-------------|
| 1. 4행 레이아웃 | §6.4 레이아웃 + §6.5 BuildGrid |
| 2. 별칭 편집 + 영속 | §6.8.3 편집 흐름 + §7.2 UserSettings |
| 3. No. 행 편집/클릭 불가 | §6.6 `Enabled=false` |
| 4. 연결 행 자동 갱신 | §6.8.7 `StateUpdated` → PaintAll |
| 5. 연결 행 별칭 표시 + Ellipsis | §6.7 `PaintConnectCell` + `AutoEllipsis` |
| 6. 입력 선택 → 연결 출력 하이라이트 | §6.7 `PaintOutputCell` 조건 |
| 7. Auto Take 즉시 / 미체크 Gold | §6.8.4~6.8.5 |
| 8. Take 시 이미 연결된 매핑 제외 | §6.8.5 `effective` 필터 |
| 9. Take 완료 후 선택/pending 해제 | §6.8.5 finally 블록 |
| 10. Videohub RouteBatchAsync 단일 블록 | §3.3 override |
| 11. PN-8080 RouteBatchAsync 순차 | §3.2 기본 구현 상속 |
| 12. 기존 MatrixControl 무영향 | §7.1 병행 배치 |
| 13. VS2022 Designer 프리뷰 | §6.4 Designer 호환 규칙 |
| 14. 깜박임 없음 | §6.11 WM_SETREDRAW |

---

## 11. 구현 순서 (Do 단계용 체크리스트)

### Phase 1 — Core/Service (1h)
- [ ] `Core/AliasSettings.cs` 작성 (단위 테스트 대신 Controller 에서 사용 검증)
- [ ] `Core/IMatrixService.cs` 에 `RouteBatchAsync` 추가
- [ ] `Services/MatrixServiceBase.cs` 에 기본 구현 추가
- [ ] `Services/VideohubMatrixService.cs` 에 override 추가
- [ ] csproj 에 AliasSettings.cs 등록
- [ ] **Build 검증** (경고 0)

### Phase 2 — AliasMatrixControl 정적 부분 (2h)
- [ ] `UI/MatrixUiColors.cs` 작성
- [ ] `UI/AliasMatrixControl.cs` (코드 비하인드, 공개 API 시그니처만)
- [ ] `UI/AliasMatrixControl.Designer.cs` (정적 TLP + 사이드 패널)
- [ ] `UI/AliasMatrixControl.resx`
- [ ] csproj 에 3개 파일 등록
- [ ] **Build 검증** (Designer 로드 가능한지 VS에서 확인)

### Phase 3 — BuildGrid + 페인트 (2h)
- [ ] `BuildGrid()` 구현 (동적 셀 생성)
- [ ] `PaintAll`, `PaintInputCell`, `PaintOutputCell`, `PaintConnectCell`, `PaintNoCell`
- [ ] `MakeRowLabel` 헬퍼
- [ ] 화면 시각 확인 (기본 8×8)

### Phase 4 — 인터랙션 (2h)
- [ ] 입력 셀 클릭 → `_selectedInput` + PaintAll
- [ ] 출력 셀 클릭 → `_pendingRoutes` 갱신 + Paint
- [ ] `TakeAsync` (effective 필터 + RouteBatchAsync)
- [ ] `SetUiBusy` + `_busy` 가드
- [ ] AutoTake 토글
- [ ] 서비스 이벤트 핸들러 (StateUpdated/DeviceInfoChanged/ConnectionStateChanged)

### Phase 5 — 편집 UX (1h)
- [ ] 더블클릭 → `BeginEdit`
- [ ] `_editOverlay` TextBox 배치
- [ ] Enter/Esc/LostFocus 처리
- [ ] AliasSettings 갱신 + 이벤트 발화

### Phase 6 — 테스트 하네스 통합 (1h)
- [ ] `MainForm.Designer.cs` 에 TabControl 도입
- [ ] `MainForm.cs` 두 컨트롤 동시 Attach/Detach
- [ ] `UserSettings` 에 `AutoTake`, `DefaultTabIndex`, `Aliases_{host}_{port}_Inputs/Outputs`
- [ ] `App.config` 기본값 추가
- [ ] **Build 검증** (Debug+Release)
- [ ] 수동 테스트 (PN-8080 실기기 + Videohub 실기기)

---

## 12. 수동 테스트 시나리오 (Check 단계용)

1. **기본 렌더링**: 앱 실행 → PN-8080 연결 → "별칭 패널" 탭 → 4행 그리드 확인 (8×8)
2. **별칭 편집**: 입력 1 셀 더블클릭 → "카메라1" 입력 → Enter → 별칭 표시 확인 → 앱 재시작 후 복원 확인
3. **연결 행 자동 갱신**: "기본 그리드" 탭에서 라우팅 변경 → "별칭 패널" 탭 전환 → 연결 행이 반영됐는지 확인
4. **입력 선택 하이라이트**: 입력 1 셀 클릭 → 연두색 + 연결된 출력 셀(들) 주황색 확인
5. **Pending + Take**: AutoTake 해제 → 입력 1 선택 → 출력 3, 5 클릭(pending Gold) → Take 버튼 → 로그 `Take: 2건 전송` 확인 → 연결 행 반영
6. **이미 연결된 매핑 제외**: 위 상태에서 다시 입력 1 선택 → 출력 3(이미 in=1) 클릭 → no-op → 출력 5(in=1) → no-op → Take 버튼 비활성 상태 확인
7. **AutoTake 체크**: 체크 → 입력 2 선택 → 출력 4 클릭 → 즉시 전송 확인
8. **Videohub 단일 블록**: Videohub 연결 → 입력 3 선택 → 출력 5, 6, 7 pending → Take → 로그에 `TX VIDEO OUTPUT ROUTING: 3 lines` 단일 블록 확인
9. **PN-8080 순차**: PN-8080 연결 → 위와 동일 시나리오 → 로그에 개별 `s in X av out Y!` 3회 확인
10. **비정방 매트릭스** (Videohub 설정 Max 변경): 입력 16, 출력 32 등 → No. 행 32 열, 입력 17~32 Disabled 회색 확인
11. **회귀 (기존 탭)**: "기본 그리드" 탭 기능 전체 (Route, All, PTP, Refresh) 동작 확인

---

## 13. Risks (Plan §7 확장)

| 위험 | 완화 (Design 확정 사항) |
|------|------------------------|
| 편집 중 StateUpdated 로 레이아웃 깨짐 | §8 엣지 케이스: 편집 중 연결 행만 업데이트, Label 유지 |
| 출력 셀 클릭이 Label 의 MouseClick 으로 중복 fire | TableLayoutPanel 에 직접 click 이벤트 없음. Label `Click` 이벤트만 사용 |
| 더블클릭 시간대 내 두 번째 클릭이 Click+DoubleClick 모두 fire | Click 에서 단순히 선택만 하고, DoubleClick 에서 편집 진입. 선택 후 편집은 UX상 자연스러움 |
| Label.AutoEllipsis 가 폰트별로 정확도 차이 | MaxAliasLength=10 상한으로 하드 캡 + AutoEllipsis 는 시각 보완 |
| 비정방 Videohub (예: 40×16) 에서 출력 열 17~40 은 pending 불가 | `cell.Enabled = false` 로 클릭 차단. 사용자 혼란 방지 위해 out-of-range 명시 회색 |
| Host:Port 문자열에 path 부적합 문자 (`:` 제한) | INI 파일 내부 키라서 파일명 제약 없음. 단, 파서에 `|`(alias 구분자) 금지 검사 필요 |

---

## 14. Out of Scope (Plan 재확인)

- 기존 `MatrixControl` 기능 변경 없음. 새 컨트롤 병행만.
- 라벨 서버 영속 (이번은 `UserSettings` INI 로컬만).
- 스크립트/매크로/프리셋.
- 드래그 앤 드롭 라우팅.
- 비정방 매트릭스의 최적 UX (상세 피드백은 후속 PDCA).

---

## 15. 다음 단계

이 Design 문서 승인 후:
1. `/pdca do matrix-panel-v2` — Phase 1~6 체크리스트 따라 구현 착수
2. 구현 완료 후 `/pdca analyze matrix-panel-v2` — Gap 분석
3. `>=90%` 시 `/pdca report matrix-panel-v2`

---

**Design 작성 완료**: 2026-04-23
**Plan blocked by**: [matrix-panel-v2.plan.md](../../01-plan/features/matrix-panel-v2.plan.md)
