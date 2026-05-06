# Design — device-emulator

> **문서 버전**: v1.1 (2026-05-06)
> **참조 Plan**: [device-emulator.plan.md](../../01-plan/features/device-emulator.plan.md)
> **베이스 라이브러리**: `Xeno.Framework.Camera` v1.1 (pelco-d-and-udp-fix 보관본 참조)
> **Changelog v1.1**: Check phase Match Rate 95% 결과로 6건 minor hardening 흡수 (M1 wrap-detection guard, M2 Ack(0) clamp 명시, M3 Model combo "Brand Model" 형태, M4 BeginInvoke 마샬링, M5 UdpServerTransport null-guard, M6 baud 8값 + MaxLogLines). 신규 OD5~OD8 추가.

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | device-emulator |
| **대상** | 신규 standalone WinExe 프로젝트 (`DeviceEmulator/` 하위) |
| **신규 솔루션** | `DeviceEmulator/DeviceEmulator.sln` (기존 `PN8080Controller.sln` 와 분리) |
| **신규 파일** | 라이브러리 inverse parser **3개** (`ViscaCommand.cs`, `ViscaCommandParser.cs`, `ViscaReplyBuilder.cs` 추가, `PelcoDCommand.cs` + `PelcoDCommandParser.cs`) + DeviceEmulator 앱 **20개** (Core 4 + Transports 3 + Emulators 6 + UI 4 + Misc 3) = **~25개** |
| **수정 파일** | `Xeno.Framework.Camera.csproj` (3 신규 파일 등록) |
| **v1 모델** | 5종 (EVI-H100, SRG-300H, FR-H50SN, CR-N300, Pelco-D Generic) — Q2 결정 |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | Plan §1.2 의 "5계층 추상화 device-side 반전" 가설을 구체 시그니처로 환원하지 않으면, Do 단계에서 server-side transport 시그니처 (`UdpServerTransport.OnPacketReceived` 콜백 vs queue?) · ViscaCommandParser 의 enum 표현 (cmd kind 분류 granularity) · 모델별 reply 자동 생성 로직 (delay, error injection 통합) 이 매번 재결정되어 일관성 부족. |
| **Solution** | 25개 파일 단위 namespace/클래스/시그니처/이벤트 흐름/UI 상태머신 사전 확정. **VISCA codec 양방향 대칭** (`ViscaCodec` 빌드 ↔ `ViscaCommandParser` 역변환) 보장. Server-side transport 3종 + 모델별 emulator 5종 명세. **`UdpServerTransport` 의 "Sony reply convention"** (dest port 52381 of source IP) 옵션화로 정/legacy 컨트롤러 모두 검증 가능. |
| **Function UX Effect** | Do 단계는 본 문서 §5~§9 의 코드 블록을 그대로 옮겨 쓰면 컴파일 통과. 25개 파일 평균 50줄 = ~1,250줄 코드. 2 시간 안에 v1 7 변형 emulator 가동 가능. UI 는 Device Type/Model/Transport 캐스케이드 콤보 + Listen/Stop + 에러 주입 옵션 + 로그 toolbar (CameraController 패턴 재사용). |
| **Core Value** | "외부 기기 통합 제어 API" 의 **개발/검증 인프라 정립** — controller (라이브러리 보관본) + emulator (본 사이클) 가 한 쌍. ViscaCommandParser 가 라이브러리에 추가되면서 controller 도 미래에 응답 verify 용도로 재사용 가능 (양방향 자산). 신규 모델 추가 시 양쪽 동시 구현 패턴 정착. |

---

## 1. Plan Decisions 매핑 (Q1~Q6 → 설계 위치)

| Q | 결정 | 본 문서 위치 |
|---|------|--------------|
| Q1 | 별도 `DeviceEmulator.sln` | §3 Architecture, §4 Solution & csproj |
| Q2 | v1 = 카메라 5 모델만 | §8 모델 emulator (5개), §13 Implementation order |
| Q3 | 에러 주입 / latency 옵션 v1 포함 | §10 Error injection / latency, §9 UI |
| Q4 | 1 instance = 1 device | §7 IDeviceEmulator (singleton 가정), §9 UI (single device pane) |
| Q5 | Matrix parser 위치 → v2 사이클 | 본 문서 명시 안 함 (v2 별도) |
| Q6 | `DeviceEmulator/README.md` 추가 | §11 Build & deployment |

---

## 2. 아키텍처 — 5계층 device-side 반전

```
┌────────────────────────────────────────────────────────────────────────────┐
│ DeviceEmulator.exe (WinExe, .NET Framework 4.8)                           │
│   MainForm                                                                 │
│     ├── DeviceTypeCombo / ModelCombo / TransportCombo (캐스케이드)        │
│     ├── Local IP/Port + Listen/Stop                                        │
│     ├── Latency 슬라이더 + 에러 주입 옵션 (NAK / Timeout / Malformed)     │
│     └── 로그 패널 + Toolbar (복사/지우기/저장/일시정지)                    │
└─────────────────────────────────────┬──────────────────────────────────────┘
                                      │ uses
                                      ▼
┌────────────────────────────────────────────────────────────────────────────┐
│ DeviceEmulator (자체 코드, namespace `DeviceEmulator`)                    │
│                                                                            │
│  Core/                                                                     │
│    IDeviceEmulator (interface)                                             │
│    DeviceEmulatorBase (abstract — 공통 logging, 에러 주입, latency)       │
│    EmulatorRegistry (모델 콤보 데이터 소스, factory)                      │
│    EmulatorOptions (NAK/timeout/malformed/latency 옵션 묶음)              │
│                                                                            │
│  Transports/  (server-side, DeviceEmulator 전용)                          │
│    SerialServerTransport (SerialPort.DataReceived 이벤트)                  │
│    UdpServerTransport (UdpClient + Sony reply convention 옵션)             │
│    TcpServerTransport (TcpListener + per-connection handler)               │
│                                                                            │
│  Emulators/  (모델별)                                                     │
│    EviH100Emulator (Serial 전용)                                           │
│    ViscaIpEmulator (SRG/FR/CR-N300 generic, UDP+TCP)                       │
│    PelcoDGenericEmulator (Serial/UDP/TCP raw)                              │
│                                                                            │
│  UI/  (간단한 WinForms)                                                   │
│    MainForm.cs (.Designer.cs, .resx)                                       │
└────────────────────────────────────┬───────────────────────────────────────┘
                                     │ ProjectReference
                                     ▼
┌────────────────────────────────────────────────────────────────────────────┐
│ Xeno.Framework.Camera.dll  (기존 라이브러리, 본 사이클에 inverse 추가)    │
│                                                                            │
│  Protocols/Visca/                                                          │
│    ViscaCodec, ViscaResponseParser, ViscaConstants  (기존)                 │
│    ViscaCommand, ViscaCommandKind                   ← NEW                  │
│    ViscaCommandParser                               ← NEW (controller→bytes 의 inverse) │
│    ViscaReplyBuilder                                ← NEW (ACK/Completion/Error 빌드)   │
│                                                                            │
│  Protocols/Pelco/                                                          │
│    PelcoConstants, PelcoDCodec  (기존)                                     │
│    PelcoDCommand, PelcoDCommandKind                 ← NEW                  │
│    PelcoDCommandParser                              ← NEW                  │
│                                                                            │
│  Core/, Transports/, Services/, UI/  (변동 없음)                          │
└────────────────────────────────────────────────────────────────────────────┘
```

### 폴더 구조

```
PN-8080 Controller/                          (repo root)
  PN8080Controller.sln                       (기존 — 4 프로젝트)
  Xeno.Framework.Matrix/                     (기존)
  Xeno.Framework.Camera/                     (기존, 본 사이클에 inverse parser 추가)
  PN8080Controller/                          (기존)
  CameraController/                          (기존)
  DeviceEmulator/                            ← NEW 폴더
    DeviceEmulator.sln                       ← NEW 솔루션 (기존과 분리)
    DeviceEmulator/                          ← NEW WinExe 프로젝트
      DeviceEmulator.csproj
      Program.cs
      App.config
      app.manifest
      Properties/AssemblyInfo.cs
      Core/
        IDeviceEmulator.cs
        DeviceEmulatorBase.cs
        EmulatorRegistry.cs
        EmulatorOptions.cs
      Transports/
        SerialServerTransport.cs
        UdpServerTransport.cs
        TcpServerTransport.cs
      Emulators/
        EviH100Emulator.cs
        ViscaIpEmulator.cs
        PelcoDGenericEmulator.cs
      UI/
        MainForm.cs (.Designer.cs, .resx)
    README.md                                ← NEW (사용법 1장)
```

---

## 3. Solution & csproj 구조 (Q1 결정)

### 3.1 신규 솔루션 — `DeviceEmulator/DeviceEmulator.sln`

```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17

# 본 솔루션 자체 프로젝트 (1개)
Project("{FAE04EC0-...}") = "DeviceEmulator", "DeviceEmulator\DeviceEmulator.csproj", "{C4E5F6A7-...}"
EndProject

# 외부 참조 라이브러리 (relative path)
Project("{FAE04EC0-...}") = "Xeno.Framework.Camera", "..\Xeno.Framework.Camera\Xeno.Framework.Camera.csproj", "{A2C3D4E5-F6B7-48C9-9A1D-2E3F4B5C6A7B}"
EndProject

Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
    Release|Any CPU = Release|Any CPU
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
    {C4E5F6A7-...}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    ...
    {A2C3D4E5-F6B7-48C9-9A1D-2E3F4B5C6A7B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    ...
  EndGlobalSection
EndGlobal
```

→ **2 프로젝트만**: DeviceEmulator (WinExe) + Xeno.Framework.Camera (Library, ProjectReference 로 끌어옴). 기존 Matrix 라이브러리는 v2 사이클에서 추가.

### 3.2 `DeviceEmulator/DeviceEmulator/DeviceEmulator.csproj`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{C4E5F6A7-B8C9-4ADB-9E2F-3A4B5C6D7E8F}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <RootNamespace>DeviceEmulator</RootNamespace>
    <AssemblyName>DeviceEmulator</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <Deterministic>true</Deterministic>
    <LangVersion>7.3</LangVersion>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Configuration" />
    <Reference Include="System.Core" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.IO.Ports" />
    <Reference Include="System.Windows.Forms" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Xeno.Framework.Camera\Xeno.Framework.Camera.csproj">
      <Project>{A2C3D4E5-F6B7-48C9-9A1D-2E3F4B5C6A7B}</Project>
      <Name>Xeno.Framework.Camera</Name>
    </ProjectReference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="Core\IDeviceEmulator.cs" />
    <Compile Include="Core\DeviceEmulatorBase.cs" />
    <Compile Include="Core\EmulatorRegistry.cs" />
    <Compile Include="Core\EmulatorOptions.cs" />
    <Compile Include="Transports\SerialServerTransport.cs" />
    <Compile Include="Transports\UdpServerTransport.cs" />
    <Compile Include="Transports\TcpServerTransport.cs" />
    <Compile Include="Emulators\EviH100Emulator.cs" />
    <Compile Include="Emulators\ViscaIpEmulator.cs" />
    <Compile Include="Emulators\PelcoDGenericEmulator.cs" />
    <Compile Include="UI\MainForm.cs">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include="UI\MainForm.Designer.cs">
      <DependentUpon>MainForm.cs</DependentUpon>
    </Compile>
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="UI\MainForm.resx">
      <DependentUpon>MainForm.cs</DependentUpon>
    </EmbeddedResource>
  </ItemGroup>
  <ItemGroup>
    <None Include="App.config" />
    <None Include="app.manifest" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

→ ProjectReference 의 상대 경로는 `..\..\Xeno.Framework.Camera\` (DeviceEmulator/DeviceEmulator/ → DeviceEmulator/ → repo root → Xeno.Framework.Camera/).

빌드 결과 (`bin/Release/`): `DeviceEmulator.exe` + `Xeno.Framework.Camera.dll` 한 폴더 → 폴더 zip 만으로 standalone 배포.

---

## 4. Inverse Parsers — `Xeno.Framework.Camera` 라이브러리 확장

### 4.1 `Protocols/Visca/ViscaCommand.cs` (NEW)

수신된 VISCA byte 를 의미 단위로 표현:

```csharp
using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// Categorized VISCA command kind (from controller's TX byte).
    /// </summary>
    public enum ViscaCommandKind
    {
        Unknown,
        PanTiltDrive,
        PanTiltStop,
        ZoomDrive,
        ZoomStop,
        FocusDrive,
        FocusStop,
        PresetSet,
        PresetRecall,
        PresetReset,
        OsdOn,
        OsdOff,
        OsdSelect,
        OsdBack,
        InquiryPanTiltStatus,    // 81 09 06 23 FF (estimate)
        InquiryZoomPosition      // 81 09 04 47 FF
    }

    /// <summary>Parsed VISCA command + parameters.</summary>
    public sealed class ViscaCommand
    {
        public ViscaCommandKind Kind { get; set; }
        public int Address { get; set; }                 // 1..7 (header 0x80 | addr)
        public PanTiltDirection? PanTiltDir { get; set; }
        public ZoomDirection? ZoomDir { get; set; }
        public FocusDirection? FocusDir { get; set; }
        public int PanSpeed { get; set; }
        public int TiltSpeed { get; set; }
        public int ZoomSpeed { get; set; }
        public int FocusSpeed { get; set; }
        public int PresetNumber { get; set; }
        public byte[] RawBytes { get; set; }

        /// <summary>Human-readable description for logs (e.g. "PT Drive Right (pan=0x10, tilt=0x10)").</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case ViscaCommandKind.PanTiltDrive:
                    return "PT Drive " + PanTiltDir + " (pan=0x" + PanSpeed.ToString("X2")
                         + ", tilt=0x" + TiltSpeed.ToString("X2") + ")";
                case ViscaCommandKind.PanTiltStop:    return "PT Stop";
                case ViscaCommandKind.ZoomDrive:      return "Zoom " + ZoomDir + " (speed=" + ZoomSpeed + ")";
                case ViscaCommandKind.ZoomStop:       return "Zoom Stop";
                case ViscaCommandKind.FocusDrive:     return "Focus " + FocusDir + " (speed=" + FocusSpeed + ")";
                case ViscaCommandKind.FocusStop:      return "Focus Stop";
                case ViscaCommandKind.PresetSet:      return "Preset Set " + PresetNumber;
                case ViscaCommandKind.PresetRecall:   return "Preset Recall " + PresetNumber;
                case ViscaCommandKind.OsdOn:          return "OSD On";
                case ViscaCommandKind.OsdOff:         return "OSD Off";
                case ViscaCommandKind.OsdSelect:      return "OSD Select";
                case ViscaCommandKind.OsdBack:        return "OSD Back";
                default:                              return "Unknown VISCA";
            }
        }
    }
}
```

### 4.2 `Protocols/Visca/ViscaCommandParser.cs` (NEW)

`ViscaCodec` 의 inverse — 수신 byte 를 `ViscaCommand` 객체로 역변환:

```csharp
using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Visca
{
    public static class ViscaCommandParser
    {
        /// <summary>
        /// Parse a raw VISCA frame (8x ... FF). Returns ViscaCommand with Kind=Unknown if not recognized.
        /// </summary>
        public static ViscaCommand Parse(byte[] data)
        {
            var cmd = new ViscaCommand { RawBytes = data };
            if (data == null || data.Length < 3) { cmd.Kind = ViscaCommandKind.Unknown; return cmd; }
            if ((data[0] & 0xF0) != 0x80) { cmd.Kind = ViscaCommandKind.Unknown; return cmd; }
            if (data[data.Length - 1] != 0xFF) { cmd.Kind = ViscaCommandKind.Unknown; return cmd; }

            cmd.Address = data[0] & 0x0F;

            // 8x 01 06 01 VV WW pp tt FF — PT Drive
            if (data.Length == 9 && data[1] == 0x01 && data[2] == 0x06 && data[3] == 0x01)
            {
                cmd.PanSpeed = data[4];
                cmd.TiltSpeed = data[5];
                byte pp = data[6], tt = data[7];
                if (pp == 0x03 && tt == 0x03) { cmd.Kind = ViscaCommandKind.PanTiltStop; return cmd; }
                cmd.Kind = ViscaCommandKind.PanTiltDrive;
                cmd.PanTiltDir = ResolvePtDir(pp, tt);
                return cmd;
            }
            // 8x 01 04 07 0p FF — Zoom
            if (data.Length == 6 && data[1] == 0x01 && data[2] == 0x04 && data[3] == 0x07)
            {
                byte p = data[4];
                if (p == 0x00) { cmd.Kind = ViscaCommandKind.ZoomStop; return cmd; }
                cmd.Kind = ViscaCommandKind.ZoomDrive;
                if ((p & 0xF0) == 0x20)      { cmd.ZoomDir = ZoomDirection.Tele; cmd.ZoomSpeed = p & 0x07; }
                else if ((p & 0xF0) == 0x30) { cmd.ZoomDir = ZoomDirection.Wide; cmd.ZoomSpeed = p & 0x07; }
                else if (p == 0x02)          { cmd.ZoomDir = ZoomDirection.Tele; cmd.ZoomSpeed = -1; }
                else if (p == 0x03)          { cmd.ZoomDir = ZoomDirection.Wide; cmd.ZoomSpeed = -1; }
                return cmd;
            }
            // 8x 01 04 08 0p FF — Focus (same shape as Zoom)
            if (data.Length == 6 && data[1] == 0x01 && data[2] == 0x04 && data[3] == 0x08)
            {
                byte p = data[4];
                if (p == 0x00) { cmd.Kind = ViscaCommandKind.FocusStop; return cmd; }
                cmd.Kind = ViscaCommandKind.FocusDrive;
                if ((p & 0xF0) == 0x20)      { cmd.FocusDir = FocusDirection.Far;  cmd.FocusSpeed = p & 0x07; }
                else if ((p & 0xF0) == 0x30) { cmd.FocusDir = FocusDirection.Near; cmd.FocusSpeed = p & 0x07; }
                else if (p == 0x02)          { cmd.FocusDir = FocusDirection.Far;  cmd.FocusSpeed = -1; }
                else if (p == 0x03)          { cmd.FocusDir = FocusDirection.Near; cmd.FocusSpeed = -1; }
                return cmd;
            }
            // 8x 01 04 3F 0[12] 0p FF — Memory Set/Recall
            if (data.Length == 7 && data[1] == 0x01 && data[2] == 0x04 && data[3] == 0x3F)
            {
                byte action = data[4];
                cmd.PresetNumber = data[5] & 0x7F;
                if (action == 0x00) cmd.Kind = ViscaCommandKind.PresetReset;
                else if (action == 0x01) cmd.Kind = ViscaCommandKind.PresetSet;
                else if (action == 0x02) cmd.Kind = ViscaCommandKind.PresetRecall;
                return cmd;
            }
            // 8x 01 06 06 0X FF — OSD (placeholder per ViscaCodec)
            if (data.Length == 6 && data[1] == 0x01 && data[2] == 0x06 && data[3] == 0x06)
            {
                switch (data[4])
                {
                    case 0x02: cmd.Kind = ViscaCommandKind.OsdOn; break;
                    case 0x03: cmd.Kind = ViscaCommandKind.OsdOff; break;
                    case 0x04: cmd.Kind = ViscaCommandKind.OsdBack; break;
                    case 0x05: cmd.Kind = ViscaCommandKind.OsdSelect; break;
                    default:   cmd.Kind = ViscaCommandKind.Unknown; break;
                }
                return cmd;
            }
            // Inquiry: 8x 09 06 23 FF (PT status) / 8x 09 04 47 FF (Zoom pos)
            if (data.Length == 5 && data[1] == 0x09)
            {
                if (data[2] == 0x06 && data[3] == 0x23) cmd.Kind = ViscaCommandKind.InquiryPanTiltStatus;
                else if (data[2] == 0x04 && data[3] == 0x47) cmd.Kind = ViscaCommandKind.InquiryZoomPosition;
                return cmd;
            }

            cmd.Kind = ViscaCommandKind.Unknown;
            return cmd;
        }

        private static PanTiltDirection ResolvePtDir(byte pp, byte tt)
        {
            // pp: 01=Left 02=Right 03=Stop ; tt: 01=Up 02=Down 03=Stop
            bool L = pp == 0x01, R = pp == 0x02;
            bool U = tt == 0x01, D = tt == 0x02;
            if (U && L) return PanTiltDirection.UpLeft;
            if (U && R) return PanTiltDirection.UpRight;
            if (D && L) return PanTiltDirection.DownLeft;
            if (D && R) return PanTiltDirection.DownRight;
            if (U) return PanTiltDirection.Up;
            if (D) return PanTiltDirection.Down;
            if (L) return PanTiltDirection.Left;
            if (R) return PanTiltDirection.Right;
            return PanTiltDirection.Stop;
        }
    }
}
```

### 4.3 `Protocols/Visca/ViscaReplyBuilder.cs` (NEW)

ACK / Completion / Error 응답 byte 빌드:

```csharp
namespace Xeno.Framework.Camera.Protocols.Visca
{
    /// <summary>
    /// Builds VISCA reply byte sequences from an emulated camera (source address).
    /// Reply header byte = 0x80 | (sourceAddr | 0x08) — by convention 0x90 for source 0x81.
    /// Most controllers use address 1, so reply header is 0x90 (i.e. 0x80 | 0x10 — not literal).
    /// Standard formula: reply header z = (source_addr * 16). For controller addr=1, source addr 1 → z = 0x90.
    /// </summary>
    public static class ViscaReplyBuilder
    {
        // For address=1, header is 0x90 (Sony spec convention).
        // General: replyHeader = ((sourceAddress + 8) << 4) | 0  → 1+8=9 << 4 = 0x90 ✓
        private static byte ReplyHeader(int sourceAddress)
        {
            int a = sourceAddress < 1 ? 1 : (sourceAddress > 7 ? 7 : sourceAddress);
            return (byte)((a + 8) << 4);   // 1 → 0x90, 2 → 0xA0 ...
        }

        /// <summary>ACK: z0 4y FF (y = socket 1..2, default 1).</summary>
        /// <remarks>v1.1 note: sourceAddress=0 (e.g., when ViscaCommandParser returns Address=0 for
        /// Unknown / non-VISCA bytes such as VISCA-IP RESET 02 00 00 01 ...) is clamped by
        /// ReplyHeader to addr=1 → 0x90 header. Side effect makes Unknown-command replies still
        /// well-formed VISCA frames the controller can DRAIN cleanly.</remarks>
        public static byte[] Ack(int sourceAddress, int socket = 1)
        {
            return new byte[] { ReplyHeader(sourceAddress), (byte)(0x40 | (socket & 0x0F)), 0xFF };
        }

        /// <summary>Completion: z0 5y FF.</summary>
        public static byte[] Completion(int sourceAddress, int socket = 1)
        {
            return new byte[] { ReplyHeader(sourceAddress), (byte)(0x50 | (socket & 0x0F)), 0xFF };
        }

        /// <summary>Error: z0 6y EE FF (EE = error code).</summary>
        public static byte[] Error(int sourceAddress, byte errorCode, int socket = 1)
        {
            return new byte[] { ReplyHeader(sourceAddress), (byte)(0x60 | (socket & 0x0F)), errorCode, 0xFF };
        }
    }
}
```

### 4.4 `Protocols/Pelco/PelcoDCommand.cs` + `PelcoDCommandParser.cs` (NEW)

Pelco-D 7-byte packet 역변환:

```csharp
namespace Xeno.Framework.Camera.Protocols.Pelco
{
    public enum PelcoDCommandKind
    {
        Unknown, Stop, PanTiltDrive,
        ZoomDrive, FocusDrive,
        IrisOpen, IrisClose,
        PresetSet, PresetClear, PresetRecall
    }

    public sealed class PelcoDCommand
    {
        public PelcoDCommandKind Kind { get; set; }
        public int Address { get; set; }
        public Core.PanTiltDirection? PanTiltDir { get; set; }
        public Core.ZoomDirection? ZoomDir { get; set; }
        public Core.FocusDirection? FocusDir { get; set; }
        public int PanSpeed { get; set; }
        public int TiltSpeed { get; set; }
        public int PresetNumber { get; set; }
        public byte[] RawBytes { get; set; }

        public string Describe()
        {
            switch (Kind)
            {
                case PelcoDCommandKind.Stop: return "Pelco Stop";
                case PelcoDCommandKind.PanTiltDrive:
                    return "Pelco PT " + PanTiltDir + " (pan=0x" + PanSpeed.ToString("X2")
                         + ", tilt=0x" + TiltSpeed.ToString("X2") + ")";
                case PelcoDCommandKind.ZoomDrive:    return "Pelco Zoom " + ZoomDir;
                case PelcoDCommandKind.FocusDrive:   return "Pelco Focus " + FocusDir;
                case PelcoDCommandKind.IrisOpen:     return "Pelco Iris Open";
                case PelcoDCommandKind.IrisClose:    return "Pelco Iris Close";
                case PelcoDCommandKind.PresetSet:    return "Pelco Preset Set " + PresetNumber;
                case PelcoDCommandKind.PresetRecall: return "Pelco Preset Recall " + PresetNumber;
                case PelcoDCommandKind.PresetClear:  return "Pelco Preset Clear " + PresetNumber;
                default:                             return "Unknown Pelco-D";
            }
        }
    }

    public static class PelcoDCommandParser
    {
        public static PelcoDCommand Parse(byte[] data)
        {
            var cmd = new PelcoDCommand { RawBytes = data };
            if (data == null || data.Length != 7 || data[0] != 0xFF)
            {
                cmd.Kind = PelcoDCommandKind.Unknown;
                return cmd;
            }
            // Verify checksum
            byte expected = (byte)((data[1] + data[2] + data[3] + data[4] + data[5]) % 256);
            if (expected != data[6])
            {
                cmd.Kind = PelcoDCommandKind.Unknown;
                return cmd;
            }
            cmd.Address = data[1];
            byte cmd1 = data[2], cmd2 = data[3];
            cmd.PanSpeed = data[4];
            cmd.TiltSpeed = data[5];
            int dataField = data[5];   // For preset extended cmd

            // Stop = 0x00 0x00 0x00 0x00
            if (cmd1 == 0 && cmd2 == 0 && data[4] == 0 && data[5] == 0)
            {
                cmd.Kind = PelcoDCommandKind.Stop;
                return cmd;
            }
            // Extended: cmd1=0x00, cmd2 in {0x03,0x05,0x07}, data2 = preset#
            if (cmd1 == 0x00 && (cmd2 == 0x03 || cmd2 == 0x05 || cmd2 == 0x07))
            {
                cmd.PresetNumber = data[5];
                if (cmd2 == 0x03) cmd.Kind = PelcoDCommandKind.PresetSet;
                else if (cmd2 == 0x05) cmd.Kind = PelcoDCommandKind.PresetClear;
                else cmd.Kind = PelcoDCommandKind.PresetRecall;
                return cmd;
            }
            // Iris (cmd1 bits)
            if ((cmd1 & PelcoConstants.Cmd1_IrisOpen) != 0)  { cmd.Kind = PelcoDCommandKind.IrisOpen; return cmd; }
            if ((cmd1 & PelcoConstants.Cmd1_IrisClose) != 0) { cmd.Kind = PelcoDCommandKind.IrisClose; return cmd; }
            // Focus (cmd1 + cmd2 bits)
            if ((cmd1 & PelcoConstants.Cmd1_FocusNear) != 0) { cmd.Kind = PelcoDCommandKind.FocusDrive; cmd.FocusDir = Core.FocusDirection.Near; return cmd; }
            if ((cmd2 & PelcoConstants.Cmd2_FocusFar) != 0)  { cmd.Kind = PelcoDCommandKind.FocusDrive; cmd.FocusDir = Core.FocusDirection.Far;  return cmd; }
            // Zoom (cmd2 bits)
            if ((cmd2 & PelcoConstants.Cmd2_ZoomTele) != 0)  { cmd.Kind = PelcoDCommandKind.ZoomDrive; cmd.ZoomDir = Core.ZoomDirection.Tele; return cmd; }
            if ((cmd2 & PelcoConstants.Cmd2_ZoomWide) != 0)  { cmd.Kind = PelcoDCommandKind.ZoomDrive; cmd.ZoomDir = Core.ZoomDirection.Wide; return cmd; }
            // Pan/Tilt (cmd2 bits)
            bool U = (cmd2 & PelcoConstants.Cmd2_TiltUp) != 0;
            bool D = (cmd2 & PelcoConstants.Cmd2_TiltDown) != 0;
            bool L = (cmd2 & PelcoConstants.Cmd2_PanLeft) != 0;
            bool R = (cmd2 & PelcoConstants.Cmd2_PanRight) != 0;
            if (U || D || L || R)
            {
                cmd.Kind = PelcoDCommandKind.PanTiltDrive;
                if (U && L) cmd.PanTiltDir = Core.PanTiltDirection.UpLeft;
                else if (U && R) cmd.PanTiltDir = Core.PanTiltDirection.UpRight;
                else if (D && L) cmd.PanTiltDir = Core.PanTiltDirection.DownLeft;
                else if (D && R) cmd.PanTiltDir = Core.PanTiltDirection.DownRight;
                else if (U) cmd.PanTiltDir = Core.PanTiltDirection.Up;
                else if (D) cmd.PanTiltDir = Core.PanTiltDirection.Down;
                else if (L) cmd.PanTiltDir = Core.PanTiltDirection.Left;
                else if (R) cmd.PanTiltDir = Core.PanTiltDirection.Right;
                return cmd;
            }
            cmd.Kind = PelcoDCommandKind.Unknown;
            return cmd;
        }
    }
}
```

→ `Xeno.Framework.Camera.csproj` 에 5 신규 파일 (`ViscaCommand.cs`, `ViscaCommandKind.cs` 통합, `ViscaCommandParser.cs`, `ViscaReplyBuilder.cs`, `PelcoDCommand.cs` + `PelcoDCommandParser.cs`) 등록 — 실 코드 5개지만 enum+class 통합으로 파일 3개.

---

## 5. Server-side Transports (DeviceEmulator 전용)

### 5.1 `Transports/SerialServerTransport.cs`

`SerialPort` 의 client/server 차이는 거의 없음 — `DataReceived` 이벤트로 수신, `Write` 로 송신.

```csharp
using System;
using System.IO.Ports;
using System.Text;

namespace DeviceEmulator.Transports
{
    /// <summary>
    /// Server-side serial transport. Listens on a COM port, raises OnPacketReceived for each
    /// completed VISCA frame (until 0xFF) or fixed-length Pelco frame (7 bytes).
    /// </summary>
    internal sealed class SerialServerTransport : IDisposable
    {
        public enum FrameMode { ViscaTerminator, PelcoFixed }

        private SerialPort _port;
        private readonly object _lock = new object();
        private readonly System.Collections.Generic.List<byte> _accumulator = new System.Collections.Generic.List<byte>(16);

        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = Parity.None;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public FrameMode Framing { get; set; } = FrameMode.ViscaTerminator;

        public Action<byte[]> OnPacketReceived { get; set; }
        public Action<string> OnLog { get; set; }

        public bool IsOpen { get { lock (_lock) { return _port != null && _port.IsOpen; } } }

        public void Start()
        {
            lock (_lock)
            {
                Stop();
                _port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false
                };
                _port.DataReceived += OnDataReceived;
                _port.Open();
                Log("Serial listen on " + PortName + "@" + BaudRate + " (" + Framing + ")");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                try { if (_port != null) { _port.DataReceived -= OnDataReceived; _port.Close(); _port.Dispose(); } } catch { }
                _port = null;
                _accumulator.Clear();
            }
        }

        public void SendReply(byte[] data)
        {
            SerialPort p;
            lock (_lock) { p = _port; }
            if (p == null || !p.IsOpen) return;
            try { p.Write(data, 0, data.Length); } catch (Exception ex) { Log("Serial send failed: " + ex.Message); }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort p;
            lock (_lock) { p = _port; }
            if (p == null) return;
            try
            {
                int n = p.BytesToRead;
                if (n <= 0) return;
                var buf = new byte[n];
                p.Read(buf, 0, n);
                lock (_lock)
                {
                    for (int i = 0; i < buf.Length; i++)
                    {
                        _accumulator.Add(buf[i]);
                        bool packetReady = false;
                        if (Framing == FrameMode.ViscaTerminator && buf[i] == 0xFF) packetReady = true;
                        else if (Framing == FrameMode.PelcoFixed && _accumulator.Count == 7) packetReady = true;
                        if (packetReady)
                        {
                            var packet = _accumulator.ToArray();
                            _accumulator.Clear();
                            try { OnPacketReceived?.Invoke(packet); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { Log("Serial read failed: " + ex.Message); }
        }

        private void Log(string msg) { var h = OnLog; if (h != null) try { h(msg); } catch { } }
        public void Dispose() { Stop(); }
    }
}
```

### 5.2 `Transports/UdpServerTransport.cs` ⭐ Sony reply convention

```csharp
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceEmulator.Transports
{
    /// <summary>
    /// Server-side UDP transport. Binds to a local port, raises OnPacketReceived for each datagram.
    ///
    /// Reply mode:
    ///   - SonyConvention (default): reply to dest port 52381 of source IP
    ///     (matches what Sony/Canon/FR firmwares actually do, so legacy controllers that
    ///     don't bind locally to 52381 will fail to receive — useful regression diagnostic)
    ///   - SourcePortMirror: reply to the actual source port of the received datagram
    ///     (permissive — for testing legacy controllers)
    /// </summary>
    internal sealed class UdpServerTransport : IDisposable
    {
        public enum ReplyMode { SonyConvention = 0, SourcePortMirror = 1 }

        private UdpClient _client;
        private CancellationTokenSource _cts;
        private Task _rxTask;
        private readonly object _lock = new object();

        public int LocalPort { get; set; } = 52381;
        public ReplyMode Reply { get; set; } = ReplyMode.SonyConvention;
        public int SonyReplyPort { get; set; } = 52381;   // dest port for SonyConvention mode

        public Action<byte[], IPEndPoint> OnPacketReceived { get; set; }
        public Action<string> OnLog { get; set; }

        public bool IsOpen { get { lock (_lock) { return _client != null; } } }

        public void Start()
        {
            lock (_lock)
            {
                Stop();
                _client = new UdpClient(new IPEndPoint(IPAddress.Any, LocalPort));
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                var c = _client;
                _rxTask = Task.Run(() => RxLoop(c, token));
                Log("UDP listen on 0.0.0.0:" + LocalPort + " (reply " + Reply + ")");
            }
        }

        public void Stop()
        {
            CancellationTokenSource cts;
            UdpClient c;
            lock (_lock) { cts = _cts; c = _client; _cts = null; _client = null; }
            try { cts?.Cancel(); } catch { }
            try { c?.Close(); } catch { }
            try { cts?.Dispose(); } catch { }
        }

        /// <summary>Send a reply to the specified source endpoint, applying the configured reply mode.</summary>
        /// <remarks>v1.1: defensive early-return on sourceEndpoint==null (callers occasionally
        /// pass null for raw-mode emulators that lack a source IP context).</remarks>
        public void SendReply(byte[] data, IPEndPoint sourceEndpoint)
        {
            UdpClient c;
            lock (_lock) { c = _client; }
            if (c == null || sourceEndpoint == null) return;
            try
            {
                IPEndPoint target;
                if (Reply == ReplyMode.SonyConvention)
                    target = new IPEndPoint(sourceEndpoint.Address, SonyReplyPort);
                else
                    target = sourceEndpoint;
                c.Send(data, data.Length, target);
            }
            catch (Exception ex) { Log("UDP send failed: " + ex.Message); }
        }

        private async Task RxLoop(UdpClient client, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await client.ReceiveAsync().ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;
                    try { OnPacketReceived?.Invoke(result.Buffer, result.RemoteEndPoint); } catch { }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (Exception ex) { Log("UDP RX loop: " + ex.Message); }
            }
        }

        private void Log(string msg) { var h = OnLog; if (h != null) try { h(msg); } catch { } }
        public void Dispose() { Stop(); }
    }
}
```

### 5.3 `Transports/TcpServerTransport.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceEmulator.Transports
{
    /// <summary>
    /// Server-side TCP transport. Accepts client connections, raises OnPacketReceived per
    /// VISCA frame (read until 0xFF) or fixed-length Pelco-D frame (7 bytes).
    /// Currently supports a single client at a time (v1 — 1 instance = 1 device).
    /// </summary>
    internal sealed class TcpServerTransport : IDisposable
    {
        public enum FrameMode { ViscaTerminator, PelcoFixed }

        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private Task _acceptTask;
        private Task _rxTask;
        private readonly object _lock = new object();

        public int LocalPort { get; set; } = 5678;
        public FrameMode Framing { get; set; } = FrameMode.ViscaTerminator;

        public Action<byte[]> OnPacketReceived { get; set; }
        public Action<string> OnLog { get; set; }

        public bool IsOpen { get { lock (_lock) { return _listener != null; } } }
        public bool HasClient { get { lock (_lock) { return _client != null && _client.Connected; } } }

        public void Start()
        {
            lock (_lock)
            {
                Stop();
                _listener = new TcpListener(IPAddress.Any, LocalPort);
                _listener.Start();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _acceptTask = Task.Run(() => AcceptLoop(token));
                Log("TCP listen on 0.0.0.0:" + LocalPort + " (" + Framing + ")");
            }
        }

        public void Stop()
        {
            CancellationTokenSource cts;
            TcpListener l; TcpClient c; NetworkStream s;
            lock (_lock)
            {
                cts = _cts; l = _listener; c = _client; s = _stream;
                _cts = null; _listener = null; _client = null; _stream = null;
            }
            try { cts?.Cancel(); } catch { }
            try { s?.Close(); } catch { }
            try { c?.Close(); } catch { }
            try { l?.Stop(); } catch { }
            try { cts?.Dispose(); } catch { }
        }

        public void SendReply(byte[] data)
        {
            NetworkStream s;
            lock (_lock) { s = _stream; }
            if (s == null) return;
            try { s.Write(data, 0, data.Length); s.Flush(); }
            catch (Exception ex) { Log("TCP send failed: " + ex.Message); }
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    var ep = client.Client.RemoteEndPoint as IPEndPoint;
                    Log("TCP client connected from " + ep);
                    lock (_lock)
                    {
                        try { _client?.Close(); } catch { }   // close previous if any
                        _client = client;
                        _stream = client.GetStream();
                        _rxTask = Task.Run(() => RxLoop(_stream, ct));
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
            }
        }

        private async Task RxLoop(NetworkStream stream, CancellationToken ct)
        {
            var accumulator = new List<byte>(16);
            var buf = new byte[256];
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int n = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                    if (n <= 0) { Log("TCP client disconnected"); break; }
                    for (int i = 0; i < n; i++)
                    {
                        accumulator.Add(buf[i]);
                        bool packetReady = false;
                        if (Framing == FrameMode.ViscaTerminator && buf[i] == 0xFF) packetReady = true;
                        else if (Framing == FrameMode.PelcoFixed && accumulator.Count == 7) packetReady = true;
                        if (packetReady)
                        {
                            var packet = accumulator.ToArray();
                            accumulator.Clear();
                            try { OnPacketReceived?.Invoke(packet); } catch { }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (System.IO.IOException) { break; }
                catch (Exception ex) { Log("TCP RX loop: " + ex.Message); break; }
            }
        }

        private void Log(string msg) { var h = OnLog; if (h != null) try { h(msg); } catch { } }
        public void Dispose() { Stop(); }
    }
}
```

---

## 6. Core 추상화 — `IDeviceEmulator`, base, registry, options

### 6.1 `Core/EmulatorOptions.cs`

```csharp
namespace DeviceEmulator.Core
{
    /// <summary>UI 에서 토글 가능한 동작 변형 옵션.</summary>
    public sealed class EmulatorOptions
    {
        public bool InjectNak { get; set; } = false;       // 다음 명령에 ACK 대신 VISCA Error 응답
        public bool InjectTimeout { get; set; } = false;   // 다음 명령에 응답 안 함
        public bool InjectMalformed { get; set; } = false; // 다음 명령에 잘못된 byte 응답
        public int LatencyMs { get; set; } = 5;            // 응답 지연 (0~1000)
        public bool ConsumeOnNextCommand { get; set; } = true;  // inject 옵션이 1회용인지
    }
}
```

### 6.2 `Core/IDeviceEmulator.cs`

```csharp
using System;

namespace DeviceEmulator.Core
{
    public interface IDeviceEmulator : IDisposable
    {
        string DeviceType { get; }           // "Camera"
        string Brand { get; }                // "Sony"
        string Model { get; }                // "EVI-H100"
        string Description { get; }
        EmulatorOptions Options { get; }

        bool IsListening { get; }
        event EventHandler<string> Logged;

        /// <summary>
        /// Configure connection parameters before Start.
        /// Implementation reads ComPort/BaudRate/IpAddress/Port as appropriate.
        /// </summary>
        void Configure(EmulatorConfig cfg);

        void Start();
        void Stop();
    }

    public sealed class EmulatorConfig
    {
        public string TransportKind { get; set; }   // "Serial" | "Udp" | "Tcp"
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public string LocalIpFilter { get; set; }   // future
        public int LocalPort { get; set; }
        public bool SonyReplyConvention { get; set; } = true;   // UDP only
    }
}
```

### 6.3 `Core/DeviceEmulatorBase.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceEmulator.Core
{
    public abstract class DeviceEmulatorBase : IDeviceEmulator
    {
        public string DeviceType { get; protected set; }
        public string Brand { get; protected set; }
        public string Model { get; protected set; }
        public string Description { get; protected set; }
        public EmulatorOptions Options { get; } = new EmulatorOptions();

        protected EmulatorConfig _cfg;
        protected volatile bool _listening;
        public bool IsListening => _listening;

        public event EventHandler<string> Logged;

        public abstract void Configure(EmulatorConfig cfg);
        public abstract void Start();
        public abstract void Stop();

        /// <summary>Apply latency + (optional) inject-timeout, then invoke sender. Subclasses call this around real reply send.</summary>
        protected async Task SendReplyAsync(Action sendAction)
        {
            int latency = Options.LatencyMs;
            if (latency > 0) await Task.Delay(latency).ConfigureAwait(false);

            if (Options.InjectTimeout)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectTimeout = false;
                Log("INJECT timeout — skip reply");
                return;
            }

            try { sendAction(); } catch (Exception ex) { Log("Reply send failed: " + ex.Message); }
        }

        protected void Log(string msg)
        {
            var h = Logged;
            if (h != null) try { h(this, msg); } catch { }
        }

        public abstract void Dispose();
    }
}
```

### 6.4 `Core/EmulatorRegistry.cs`

UI 콤보 데이터 소스 + factory:

```csharp
using System.Collections.Generic;

namespace DeviceEmulator.Core
{
    public static class EmulatorRegistry
    {
        public sealed class Entry
        {
            public string DeviceType;
            public string Brand;
            public string Model;
            public string[] SupportedTransports;   // "Serial", "Udp", "Tcp"
            public string Description;
            public System.Func<IDeviceEmulator> Create;
        }

        public static IReadOnlyList<Entry> All => _all;

        private static readonly Entry[] _all = new[]
        {
            new Entry {
                DeviceType = "Camera", Brand = "Sony", Model = "EVI-H100",
                SupportedTransports = new[] { "Serial" },
                Description = "Sony EVI-H100 PTZ (RS-232/422 VISCA)",
                Create = () => new Emulators.EviH100Emulator()
            },
            new Entry {
                DeviceType = "Camera", Brand = "Sony", Model = "SRG-300H",
                SupportedTransports = new[] { "Udp", "Tcp" },
                Description = "Sony SRG-300H PTZ (VISCA over IP)",
                Create = () => new Emulators.ViscaIpEmulator("Sony", "SRG-300H")
            },
            new Entry {
                DeviceType = "Camera", Brand = "FR", Model = "FR-H50SN",
                SupportedTransports = new[] { "Tcp", "Udp" },
                Description = "FR-H50SN (raw VISCA over TCP/UDP)",
                Create = () => new Emulators.ViscaIpEmulator("FR", "FR-H50SN")
            },
            new Entry {
                DeviceType = "Camera", Brand = "Canon", Model = "CR-N300",
                SupportedTransports = new[] { "Udp" },
                Description = "Canon CR-N300 (VISCA over IP)",
                Create = () => new Emulators.ViscaIpEmulator("Canon", "CR-N300")
            },
            new Entry {
                DeviceType = "Camera", Brand = "Pelco", Model = "Pelco-D Generic",
                SupportedTransports = new[] { "Serial", "Udp", "Tcp" },
                Description = "Generic Pelco-D (RS-232/422/485 + UDP/TCP raw)",
                Create = () => new Emulators.PelcoDGenericEmulator()
            }
        };
    }
}
```

→ v2 사이클에서 PN-8080 / Videohub Entry 만 추가하면 끝 (factory 등록 1건씩).

---

## 7. Model Emulators

### 7.1 `Emulators/EviH100Emulator.cs` (Serial 전용)

```csharp
using System;
using DeviceEmulator.Core;
using DeviceEmulator.Transports;
using Xeno.Framework.Camera.Protocols.Visca;

namespace DeviceEmulator.Emulators
{
    public sealed class EviH100Emulator : DeviceEmulatorBase
    {
        private SerialServerTransport _serial;

        public EviH100Emulator()
        {
            DeviceType = "Camera"; Brand = "Sony"; Model = "EVI-H100";
            Description = "Sony EVI-H100 PTZ (RS-232/422 VISCA)";
        }

        public override void Configure(EmulatorConfig cfg) { _cfg = cfg; }

        public override void Start()
        {
            if (_cfg == null || string.IsNullOrEmpty(_cfg.ComPort))
                throw new InvalidOperationException("ComPort not configured");
            _serial = new SerialServerTransport
            {
                PortName = _cfg.ComPort,
                BaudRate = _cfg.BaudRate <= 0 ? 9600 : _cfg.BaudRate,
                Framing = SerialServerTransport.FrameMode.ViscaTerminator
            };
            _serial.OnLog = msg => Log("[Serial] " + msg);
            _serial.OnPacketReceived = OnVisca;
            _serial.Start();
            _listening = true;
        }

        public override void Stop()
        {
            _listening = false;
            try { _serial?.Dispose(); } catch { }
            _serial = null;
        }

        private async void OnVisca(byte[] data)
        {
            var cmd = ViscaCommandParser.Parse(data);
            Log("RX " + ToHex(data) + "  | " + cmd.Describe());

            // Check error injection
            if (Options.InjectMalformed)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectMalformed = false;
                Log("INJECT malformed — sending garbage");
                await SendReplyAsync(() => _serial?.SendReply(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFF }));
                return;
            }
            if (Options.InjectNak)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectNak = false;
                var err = ViscaReplyBuilder.Error(cmd.Address, 0x02);   // Syntax error
                Log("INJECT NAK — TX " + ToHex(err));
                await SendReplyAsync(() => _serial?.SendReply(err));
                return;
            }

            // Normal reply: ACK then Completion
            var ack = ViscaReplyBuilder.Ack(cmd.Address);
            await SendReplyAsync(() => { _serial?.SendReply(ack); Log("TX ACK " + ToHex(ack)); });
            var done = ViscaReplyBuilder.Completion(cmd.Address);
            await SendReplyAsync(() => { _serial?.SendReply(done); Log("TX Completion " + ToHex(done)); });
        }

        public override void Dispose() { Stop(); }

        private static string ToHex(byte[] d)
        {
            if (d == null) return "(null)";
            var sb = new System.Text.StringBuilder(d.Length * 3);
            for (int i = 0; i < d.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(d[i].ToString("X2")); }
            return sb.ToString();
        }
    }
}
```

### 7.2 `Emulators/ViscaIpEmulator.cs` (SRG/FR/CR-N300 generic, UDP+TCP)

UDP + TCP 양쪽 Transport 지원:

```csharp
using System;
using System.Net;
using DeviceEmulator.Core;
using DeviceEmulator.Transports;
using Xeno.Framework.Camera.Protocols.Visca;

namespace DeviceEmulator.Emulators
{
    public sealed class ViscaIpEmulator : DeviceEmulatorBase
    {
        private UdpServerTransport _udp;
        private TcpServerTransport _tcp;

        public ViscaIpEmulator(string brand, string model)
        {
            DeviceType = "Camera"; Brand = brand; Model = model;
            Description = brand + " " + model + " (VISCA over IP)";
        }

        public override void Configure(EmulatorConfig cfg) { _cfg = cfg; }

        public override void Start()
        {
            if (_cfg == null) throw new InvalidOperationException("not configured");
            if (string.Equals(_cfg.TransportKind, "Udp", StringComparison.OrdinalIgnoreCase))
                StartUdp();
            else if (string.Equals(_cfg.TransportKind, "Tcp", StringComparison.OrdinalIgnoreCase))
                StartTcp();
            else throw new NotSupportedException("Transport: " + _cfg.TransportKind);
            _listening = true;
        }

        private void StartUdp()
        {
            _udp = new UdpServerTransport
            {
                LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 52381,
                Reply = _cfg.SonyReplyConvention ? UdpServerTransport.ReplyMode.SonyConvention : UdpServerTransport.ReplyMode.SourcePortMirror,
                SonyReplyPort = 52381
            };
            _udp.OnLog = msg => Log("[UDP] " + msg);
            _udp.OnPacketReceived = (data, src) => OnViscaIp(data, src, isTcp: false);
            _udp.Start();
        }

        private void StartTcp()
        {
            _tcp = new TcpServerTransport
            {
                LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 5678,
                Framing = TcpServerTransport.FrameMode.ViscaTerminator
            };
            _tcp.OnLog = msg => Log("[TCP] " + msg);
            _tcp.OnPacketReceived = data => OnViscaIp(data, null, isTcp: true);
            _tcp.Start();
        }

        public override void Stop()
        {
            _listening = false;
            try { _udp?.Dispose(); } catch { }
            try { _tcp?.Dispose(); } catch { }
            _udp = null; _tcp = null;
        }

        // For UDP: data is already the inner VISCA payload IF we strip the 8-byte header.
        // For TCP raw: data is just VISCA bytes.
        // For UDP with full Sony VISCA-over-IP: data includes 8-byte header → strip first.
        // v1.1: wrap-detection guard added — only strip when header looks like a VISCA-IP
        // command/inquiry (PayloadType 0x0100 or 0x0110). Otherwise (RESET 0x0200, RESET-ACK
        // 0x0201, etc.) leave data untouched and let parser report Unknown — emulator then
        // replies with raw VISCA so controller's drain logic can absorb cleanly. The 'wrapped'
        // flag is plumbed into SendInner so reply wrap symmetrically matches request wrap.
        // (Field-validated: Canon CR-N300 + FR-H50SN UDP both work without per-model branching.)
        private async void OnViscaIp(byte[] data, IPEndPoint src, bool isTcp)
        {
            byte[] inner;
            uint seq = 0;
            bool wrapped = false;
            if (!isTcp && data != null && data.Length >= 8
                && data[0] == 0x01 && (data[1] == 0x00 || data[1] == 0x10))
            {
                // Sony VISCA-over-IP command/inquiry: [Type 0x0100/0x0110][Len 2B][Seq 4B][payload]
                seq = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
                inner = new byte[data.Length - 8];
                Buffer.BlockCopy(data, 8, inner, 0, inner.Length);
                wrapped = true;
            }
            else inner = data;

            var cmd = ViscaCommandParser.Parse(inner);
            Log("RX " + (isTcp ? "" : "seq=" + seq + " ") + ToHex(data) + "  | " + cmd.Describe());

            // Inject error options (same as EviH100)
            if (Options.InjectMalformed)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectMalformed = false;
                await SendReplyAsync(() => SendInner(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFF }, src, isTcp, seq, wrapped));
                Log("INJECT malformed");
                return;
            }
            if (Options.InjectNak)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectNak = false;
                var err = ViscaReplyBuilder.Error(cmd.Address, 0x02);
                await SendReplyAsync(() => SendInner(err, src, isTcp, seq, wrapped));
                Log("INJECT NAK");
                return;
            }

            // Normal reply: ACK + Completion
            var ack = ViscaReplyBuilder.Ack(cmd.Address);
            await SendReplyAsync(() => { SendInner(ack, src, isTcp, seq, wrapped); Log("TX ACK " + ToHex(ack)); });
            var done = ViscaReplyBuilder.Completion(cmd.Address);
            await SendReplyAsync(() => { SendInner(done, src, isTcp, seq, wrapped); Log("TX Completion " + ToHex(done)); });
        }

        // v1.1: 'wrapped' parameter ensures reply wrap symmetrically matches request wrap.
        // When request was wrapped (type 0x0100/0x0110), reply is wrapped (type 0x0111) with
        // mirrored sequence. When request was raw (e.g., RESET 0x0200), reply is raw (no wrap).
        private void SendInner(byte[] viscaPayload, IPEndPoint src, bool isTcp, uint seq, bool wrapped)
        {
            if (isTcp) { _tcp?.SendReply(viscaPayload); return; }
            byte[] outBuf;
            if (wrapped)
            {
                int n = viscaPayload.Length;
                outBuf = new byte[8 + n];
                outBuf[0] = 0x01; outBuf[1] = 0x11;             // VISCA reply
                outBuf[2] = (byte)(n >> 8); outBuf[3] = (byte)(n & 0xFF);
                outBuf[4] = (byte)(seq >> 24); outBuf[5] = (byte)(seq >> 16);
                outBuf[6] = (byte)(seq >> 8);  outBuf[7] = (byte)(seq & 0xFF);
                Buffer.BlockCopy(viscaPayload, 0, outBuf, 8, n);
            }
            else outBuf = viscaPayload;
            _udp?.SendReply(outBuf, src);
        }

        public override void Dispose() { Stop(); }

        private static string ToHex(byte[] d)
        {
            if (d == null) return "(null)";
            var sb = new System.Text.StringBuilder(d.Length * 3);
            for (int i = 0; i < d.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(d[i].ToString("X2")); }
            return sb.ToString();
        }
    }
}
```

### 7.3 `Emulators/PelcoDGenericEmulator.cs` (Serial/UDP/TCP)

Pelco-D 는 fire-and-forget 이 표준 — 응답 없음 (옵션 ConsumeOnNextCommand 도 적용 안 됨, 단지 packet 분석/표시만):

```csharp
using System;
using DeviceEmulator.Core;
using DeviceEmulator.Transports;
using Xeno.Framework.Camera.Protocols.Pelco;

namespace DeviceEmulator.Emulators
{
    public sealed class PelcoDGenericEmulator : DeviceEmulatorBase
    {
        private SerialServerTransport _serial;
        private UdpServerTransport _udp;
        private TcpServerTransport _tcp;

        public PelcoDGenericEmulator()
        {
            DeviceType = "Camera"; Brand = "Pelco"; Model = "Pelco-D Generic";
            Description = "Generic Pelco-D";
        }

        public override void Configure(EmulatorConfig cfg) { _cfg = cfg; }

        public override void Start()
        {
            if (_cfg == null) throw new InvalidOperationException("not configured");
            switch (_cfg.TransportKind?.ToLowerInvariant())
            {
                case "serial":
                    _serial = new SerialServerTransport
                    {
                        PortName = _cfg.ComPort,
                        BaudRate = _cfg.BaudRate <= 0 ? 9600 : _cfg.BaudRate,
                        Framing = SerialServerTransport.FrameMode.PelcoFixed
                    };
                    _serial.OnLog = msg => Log("[Serial] " + msg);
                    _serial.OnPacketReceived = OnPelco;
                    _serial.Start();
                    break;
                case "udp":
                    _udp = new UdpServerTransport
                    {
                        LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 4001,
                        Reply = UdpServerTransport.ReplyMode.SourcePortMirror   // Pelco-D 는 fire-and-forget — 사실 무관
                    };
                    _udp.OnLog = msg => Log("[UDP] " + msg);
                    _udp.OnPacketReceived = (data, src) => OnPelco(data);
                    _udp.Start();
                    break;
                case "tcp":
                    _tcp = new TcpServerTransport
                    {
                        LocalPort = _cfg.LocalPort > 0 ? _cfg.LocalPort : 4001,
                        Framing = TcpServerTransport.FrameMode.PelcoFixed
                    };
                    _tcp.OnLog = msg => Log("[TCP] " + msg);
                    _tcp.OnPacketReceived = OnPelco;
                    _tcp.Start();
                    break;
                default:
                    throw new NotSupportedException("Transport: " + _cfg.TransportKind);
            }
            _listening = true;
        }

        public override void Stop()
        {
            _listening = false;
            try { _serial?.Dispose(); } catch { }
            try { _udp?.Dispose(); } catch { }
            try { _tcp?.Dispose(); } catch { }
            _serial = null; _udp = null; _tcp = null;
        }

        private void OnPelco(byte[] data)
        {
            var cmd = PelcoDCommandParser.Parse(data);
            Log("RX " + ToHex(data) + "  | " + cmd.Describe());
            // Pelco-D fire-and-forget — 무응답이 표준. (vendor 별 ACK 송신 시 향후 옵션 추가)
        }

        public override void Dispose() { Stop(); }

        private static string ToHex(byte[] d)
        {
            if (d == null) return "(null)";
            var sb = new System.Text.StringBuilder(d.Length * 3);
            for (int i = 0; i < d.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(d[i].ToString("X2")); }
            return sb.ToString();
        }
    }
}
```

---

## 8. UI 사양 — `MainForm`

### 8.1 디자이너 호환 패턴 (CameraControl 와 동일 규약)

- `InitializeComponent()` 안에 루프 없음, 모든 컨트롤 직선형 할당
- 이벤트 와이어링은 `WireOwnEvents()` (Designer 외부)
- 캐스케이드 콤보 데이터 채우기는 `Load` 이벤트에서 (Designer 영향 없음)

### 8.2 UI 구성 (단일 Form, ~640x575)

**v1.1 명시**:
- **Baud 콤보 값** (`_cmbBaud`): 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 (default 9600)
- **COM 콤보 자동 enum**: `Form_Load` 에서 `SerialPort.GetPortNames()` 결과로 채움 (사용자가 직접 입력도 가능 — `DropDownStyle=DropDown`)
- **로그 ring buffer**: `MaxLogLines = 2000` — 초과 시 오래된 라인 dequeue
- **Latency NumericUpDown**: Min=0, Max=1000, default=5
- **Inject 1회용 체크박스**: default checked


```
┌────────────────────────────────────────────────────────────────────┐
│ DeviceEmulator                                              [_][□][X]│
├────────────────────────────────────────────────────────────────────┤
│ Device Type: [Camera ▼]                                            │
│ Model:       [Sony SRG-300H ▼]                                     │
│ Transport:   [UDP ▼]                                               │
│ COM Port:    [COM1 ▼]   Baud: [9600 ▼]   ← 시리얼 시 활성          │
│ Local Port:  [52381]   ☑ Sony reply convention (52381)             │
│                                                                    │
│ [   Listen   ]  [    Stop    ]   ◉ Stopped                         │
│                                                                    │
│ Latency: [  5]ms ━━━━○━━━━━━━━━━━━━━━━━ (0~1000)                   │
│ ☐ Inject NAK   ☐ Inject Timeout   ☐ Inject Malformed   ☑ 1회용     │
├────────────────────────────────────────────────────────────────────┤
│ [복사] [지우기] [저장] [일시정지]              ◉ Listening / ●STOP│
│ ┌──────────────────────────────────────────────────────────────┐   │
│ │ 14:23:45.123 Listening on 0.0.0.0:52381 (reply SonyConvention)│   │
│ │ 14:23:50.234 RX seq=1 01 00 00 09 00 00 00 01 81 01 06 01 .. │   │
│ │              | PT Drive Right (pan=0x10, tilt=0x10)           │   │
│ │ 14:23:50.241 TX ACK 90 41 FF                                  │   │
│ │ 14:23:50.245 TX Completion 90 51 FF                           │   │
│ └──────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────┘
```

### 8.3 캐스케이드 콤보 동작

```csharp
private void Form_Load(...)
{
    // Device Type 콤보 채우기 (distinct from Registry)
    foreach (var t in EmulatorRegistry.All.Select(e => e.DeviceType).Distinct())
        _cmbDeviceType.Items.Add(t);
    _cmbDeviceType.SelectedIndex = 0;
}

private void _cmbDeviceType_SelectedIndexChanged(...)
{
    // v1.1: Store "Brand Model" composite (e.g., "Sony SRG-300H") in combo to disambiguate
    // duplicate Model strings across brands. SelectedEntry() resolves via composite match.
    _cmbModel.Items.Clear();
    foreach (var e in EmulatorRegistry.All.Where(x => x.DeviceType == _cmbDeviceType.SelectedItem.ToString()))
        _cmbModel.Items.Add(e.Brand + " " + e.Model);
    if (_cmbModel.Items.Count > 0) _cmbModel.SelectedIndex = 0;
}

private EmulatorRegistry.Entry SelectedEntry()
{
    string dt = (_cmbDeviceType.SelectedItem ?? "").ToString();
    string m  = (_cmbModel.SelectedItem ?? "").ToString();
    return EmulatorRegistry.All.FirstOrDefault(x =>
        x.DeviceType == dt && (x.Brand + " " + x.Model) == m);
}

private void _cmbModel_SelectedIndexChanged(...)
{
    var entry = SelectedEntry();
    _cmbTransport.Items.Clear();
    foreach (var t in entry.SupportedTransports) _cmbTransport.Items.Add(t);
    _cmbTransport.SelectedIndex = 0;
}

private void _cmbTransport_SelectedIndexChanged(...)
{
    string t = _cmbTransport.SelectedItem.ToString();
    bool serial = t == "Serial";
    bool ip = !serial;
    _cmbCom.Enabled = _cmbBaud.Enabled = serial;
    _txtPort.Enabled = ip;
    _chkSonyReply.Enabled = (t == "Udp");
    // 기본 포트 자동 채우기
    if (t == "Udp")  _txtPort.Text = "52381";
    if (t == "Tcp")  _txtPort.Text = "5678";
    if (t == "Serial" && _cmbBaud.SelectedItem == null) _cmbBaud.SelectedItem = 9600;
}
```

### 8.4 Listen / Stop 동작

```csharp
private IDeviceEmulator _emulator;

private void OnListenClick(...)
{
    if (_emulator != null) return;   // already started
    var entry = SelectedEntry();
    _emulator = entry.Create();
    // v1.1: emulator.Logged fires from async tasks (UDP RxLoop, TCP RxLoop, SerialPort.DataReceived).
    // Marshal to UI thread via BeginInvoke before touching _txtLog (else cross-thread exception).
    _emulator.Logged += (s, msg) => BeginInvoke((Action)(() => AppendLog("[" + entry.Model + "] " + msg)));
    _emulator.Options.LatencyMs = (int)_numLatency.Value;
    _emulator.Options.InjectNak = _chkInjectNak.Checked;
    _emulator.Options.InjectTimeout = _chkInjectTimeout.Checked;
    _emulator.Options.InjectMalformed = _chkInjectMalformed.Checked;
    _emulator.Options.ConsumeOnNextCommand = _chkOnce.Checked;

    var cfg = new EmulatorConfig
    {
        TransportKind = _cmbTransport.SelectedItem.ToString(),
        ComPort = _cmbCom.Text,
        BaudRate = (int)(_cmbBaud.SelectedItem ?? 9600),
        LocalPort = int.TryParse(_txtPort.Text, out int p) ? p : 0,
        SonyReplyConvention = _chkSonyReply.Checked
    };
    _emulator.Configure(cfg);
    _emulator.Start();
    _lblStatus.Text = "Listening";
    _lblStatus.BackColor = Color.LightGreen;
}

private void OnStopClick(...)
{
    if (_emulator == null) return;
    try { _emulator.Stop(); _emulator.Dispose(); } catch { }
    _emulator = null;
    _lblStatus.Text = "Stopped";
    _lblStatus.BackColor = SystemColors.Control;
}
```

### 8.5 로그 toolbar (CameraController 패턴 재사용)

복사/지우기/저장/일시정지 4 버튼 동작은 `CameraController/UI/MainForm.cs:CopyLogToClipboard, ClearLog, SaveLogToFile, ToggleLogPaused` 와 동일.

---

## 9. 에러 주입 / latency 시뮬 (Q3 결정)

### 9.1 `EmulatorOptions` 흐름

```
[UI 체크박스 토글] → emulator.Options.InjectNak = true
                                    │
[다음 packet 수신 시] → emulator.OnVisca() 안에서:
   if (Options.InjectMalformed) → 잘못된 byte 응답
   else if (Options.InjectNak)  → VISCA Error (0x60 0x02 — Syntax error) 응답
   else                          → 정상 ACK + Completion

   if (Options.ConsumeOnNextCommand) → 옵션 자동 해제 (1회용)
```

### 9.2 Latency

`DeviceEmulatorBase.SendReplyAsync(Action)` 가 `Task.Delay(LatencyMs)` 후 sender 호출. 실 환경 (Sony 표준 5~12ms) 보다 느린 시나리오 (network 지연) 시뮬에 사용.

---

## 10. 빌드 & 배포 (FR-MUST)

### 10.1 빌드

VS2022 에서:
1. `DeviceEmulator/DeviceEmulator.sln` 열기
2. Configuration `Release` 선택
3. 솔루션 빌드 (Ctrl+Shift+B)
4. 결과물: `DeviceEmulator/DeviceEmulator/bin/Release/`
   - `DeviceEmulator.exe`
   - `Xeno.Framework.Camera.dll` (자동 bundle)
   - `*.pdb` (선택)

### 10.2 배포 절차 (Q6 결정 — `README.md`)

`DeviceEmulator/README.md` 1장 — 사용법:

```markdown
# DeviceEmulator

PTZ 카메라 / 메트릭스 등 외부 장비의 **반대편 역할** (수신·응답) 을
수행하는 standalone 에뮬레이터.

## 사전 요구
- Windows 10/11 (.NET Framework 4.8 사전 설치 필요)

## 빌드
1. VS2022 에서 `DeviceEmulator.sln` 열기
2. Release 빌드
3. `DeviceEmulator/bin/Release/` 폴더 결과물

## 배포 (다른 머신으로)
1. `DeviceEmulator/bin/Release/` 폴더 통째 zip
2. 다른 머신에 zip 풀기
3. `DeviceEmulator.exe` 더블클릭

## 사용법
1. **Device Type** 콤보 → "Camera" 선택
2. **Model** 콤보 → 시뮬레이트할 모델 선택 (e.g. SRG-300H)
3. **Transport** 콤보 → 통신 방식 (Serial / UDP / TCP)
4. (Serial) **COM Port + Baud** 입력
   (IP) **Local Port** 입력 (vendor 컨벤션 자동)
5. **Listen** 클릭
6. 컨트롤러가 이 머신 IP:Port 로 연결
7. 로그 패널에 수신 packet 과 자동 응답 확인

## 옵션
- **Latency**: 응답 지연 (0~1000ms)
- **Inject NAK / Timeout / Malformed**: 컨트롤러 회복력 테스트용 에러 주입
- **1회용**: 체크 시 inject 옵션이 다음 명령에 1회만 적용
```

### 10.3 멀티-인스턴스

같은 `DeviceEmulator.exe` 여러 번 실행 — Windows 가 별도 process 로 인식. 단 같은 port (e.g. 52381) 두 instance 가 동시 점유 불가 → Listen 두 번째 시 SocketException.

→ 멀티-디바이스 시나리오: instance 별로 다른 port (또는 Sony reply convention 비활성화) 사용. v2 에서 1 instance = N devices 패턴 검토 가능.

---

## 11. 테스트 전략

### 11.1 빌드 검증

- Debug + Release 0 경고 0 오류 (DeviceEmulator.sln + 기존 PN8080Controller.sln 양쪽)

### 11.2 정적 parser 검증 (단위)

| 입력 byte | 기대 ViscaCommand |
|----------|-------------------|
| `81 01 06 01 10 10 02 03 FF` | PT Drive Right (pan=0x10, tilt=0x10), addr=1 |
| `81 01 06 01 01 01 03 03 FF` | PT Stop, addr=1 |
| `81 01 04 07 24 FF` | Zoom Tele speed=4 |
| `81 01 04 3F 02 05 FF` | Preset Recall 5 |

| 입력 byte | 기대 PelcoDCommand |
|----------|--------------------|
| `FF 01 00 02 20 00 23` | PT Right (pan=0x20) |
| `FF 01 00 03 00 05 09` | Preset Set 5 |

### 11.3 통합 테스트 (실측)

| # | 시나리오 |
|---|---------|
| I1 | DeviceEmulator (CR-N300, UDP 52381) Listen + CameraController 연결 → PT Drive 전송 → emulator 가 RX + ACK 송신 → controller 가 ACK 정상 수신 |
| I2 | DeviceEmulator (Pelco-D, Serial COM1) + CameraController (Pelco-D, Serial COM2, com0com 페어) → PT Drive 패킷이 양쪽에서 byte-correct 일치 |
| I3 | DeviceEmulator (FR-H50SN, TCP 5678) + CameraController (FR-H50SN, TCP) → PT/Zoom/Preset 시퀀스 |
| I4 | Inject NAK 토글 후 controller 명령 → controller 측 RaiseError 정상 |
| I5 | Latency 500ms 설정 후 controller 명령 → controller 측 응답 지연 인식 |

### 11.4 회귀

DeviceEmulator 빌드는 `Xeno.Framework.Camera` 에 신규 inverse parser 추가만 — 기존 controller 코드 변경 0 → controller 회귀 위험 0.

---

## 12. 구현 순서 (Do 단계 권장)

| Step | 내용 | 시간 |
|------|------|-----|
| 1 | `Xeno.Framework.Camera` 확장 — `ViscaCommand`, `ViscaCommandParser`, `ViscaReplyBuilder`, `PelcoDCommand`, `PelcoDCommandParser` 5 신규 파일 (혹은 enum+class 통합 시 3 파일) + csproj 등록 + 빌드 | 1h |
| 2 | DeviceEmulator 프로젝트 골격 — 폴더 + csproj + Program.cs + AssemblyInfo + App.config + manifest | 0.3h |
| 3 | Core/ — `IDeviceEmulator`, `DeviceEmulatorBase`, `EmulatorOptions`, `EmulatorRegistry` (5 entry) | 0.5h |
| 4 | Transports/ — `SerialServerTransport`, `UdpServerTransport`, `TcpServerTransport` 3 파일 | 1.5h |
| 5 | Emulators/ — `EviH100Emulator`, `ViscaIpEmulator` (generic), `PelcoDGenericEmulator` 3 파일 | 1h |
| 6 | UI — `MainForm.cs/.Designer.cs/.resx` 캐스케이드 콤보 + Listen/Stop + 에러 주입 + 로그 toolbar | 1.5h |
| 7 | `DeviceEmulator.sln` 작성 + Release 빌드 검증 (DeviceEmulator + 외부 ProjectReference) | 0.3h |
| 8 | `README.md` 작성 + 실기 통합 테스트 (CameraController ↔ DeviceEmulator) | 1h |

총 **~7.1h** — camera-remote-control round-1 (11.5h) 보다 작음 (codec 재사용 효과).

---

## 13. 위험 재평가 (Plan §10 대비)

| # | Plan 위험 | Design 단계 보완 |
|---|----------|------------------|
| R1 | 라이브러리 동기화 부담 | parser 가 `Xeno.Framework.Camera` 에 같이 들어가 빌드 단위 묶임. controller 빌드 시 emulator 도 자동 영향 |
| R2 | PN-8080/Videohub vendor protocol | v2 사이클 분리 (Q2 결정) |
| R3 | Serial loopback (com0com) | README 에 명시 (§10.2) |
| R4 | Port 충돌 (실 카메라 SDK 등) | UI Local Port 필드로 사용자 변경 |
| R5 | VS2022 Designer cross-solution reference | 표준 ProjectReference 상대 경로. 실패 시 dll 직접 참조로 fallback |
| R6 | .NET Framework 4.8 사전 설치 의존 | README prerequisite 명시 |
| R7 | 멀티-인스턴스 port 충돌 | UI port 변경. v2 에 1 instance = N devices 검토 |

---

## 14. Open Items

| # | 항목 | 결정 시점 |
|---|------|----------|
| OD1 | Inquiry 명령 응답 (`InquiryPanTiltStatus`, `InquiryZoomPosition`) — v1 은 hardcoded fake reply (e.g. position=0x0000) vs 미응답 | **결정**: v1 은 미응답 (Unknown Inquiry 로 로그만 표시). 실측 4 시나리오 모두 영향 없음 확인 |
| OD2 | Sony reply convention 의 socket 번호 (default 1) — VISCA spec 의 socket 1 vs 2 구분 | **결정**: 1 fixed (default 인자). 실 사용 콘트롤러 구분 요구 시 옵션화 |
| OD3 | UI Address 필드 — 현재 emulator 는 모든 address 응답. 특정 address 필터링 옵션? | **v2 deferred** |
| OD4 | 멀티-인스턴스 시 같은 host port 충돌 — UI 친화적 경고 | **부분 처리** (v1.1): Listen 실패 시 `MainForm.cs:150` MessageBox 노출. 향후 가용 port 자동 제안 검토 |
| OD5 | RESET (VISCA-IP type 0x0200) → 정식 RESET ACK (type 0x0201) 응답 (현재는 raw `90 41 FF` 응답으로 controller drain 흡수) | **결정 보류**: 현 상태 무해, 필요 시 후속 사이클에서 추가 |
| OD6 | I4 Inject NAK / I5 Latency 500ms 실측 | **사용자 환경 준비 시 후속** |
| OD7 | EVI-H100 Serial VISCA 실측 (com0com 또는 USB-RS422 어댑터 필요) | **환경 준비 시 후속**. Pelco-D Serial 동일 transport 검증 완료 (framing 차이만) |
| OD8 | Pelco-D UDP/TCP 실측 (`SourcePortMirror` 모드) | **환경 준비 시 후속**. Serial 검증 완료, UDP/TCP 는 codec 동일 사용 |

---

**문서 상태**: Final (v1.1, Check 95% 흡수 완료)

**v1.1 흡수 항목 (6건)**:
- M1 §7.2: ViscaIpEmulator wrap-detection guard + `wrapped` flag plumbing
- M2 §4.3: Ack(0) clamp 동작 명시 (Unknown 명령 경로 커버)
- M3 §8.3: Model 콤보 "Brand Model" 형태 (Sony vs Canon 충돌 방지)
- M4 §8.4: BeginInvoke UI 마샬링 명시
- M5 §5.2: UdpServerTransport SendReply null-guard
- M6 §8.2: baud 8값 + COM 자동 enum + MaxLogLines=2000

**Field Validation (4/5)**:
- I1 ✅ Canon CR-N300 UDP wrapped (22 cmds)
- I2 ✅ Pelco-D Generic Serial RS-232 (31 cmds)
- I3 ✅ FR-H50SN TCP raw (~50 cmds) + 보너스 FR-H50SN UDP wrapped (36 cmds)
- I4/I5 ⏳ OD6 으로 인계
