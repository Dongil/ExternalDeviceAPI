# Design — pelco-d-and-udp-fix

> **문서 버전**: v1.1 (2026-05-06 갱신, round-2 implementation-ahead 4건 흡수)
> **이전 버전**: v1.0 (2026-05-06 초기 Design)
> **참조 Plan**: [pelco-d-and-udp-fix.plan.md](../../01-plan/features/pelco-d-and-udp-fix.plan.md)
> **베이스 라이브러리**: `Xeno.Framework.Camera` v1.1 (camera-remote-control 보관본 참조)

## 변경 이력

| 버전 | 일자 | 변경 요약 |
|------|------|----------|
| v1.0 | 2026-05-06 | 초기 Design — 3 스코프 (UDP fix · Pelco-D · CR-N300) 코드 블록 명세 |
| v1.1 | 2026-05-06 | Round-2 Gap Analysis 결과 흡수: ① **`ViscaUdpHub` 싱글턴 + 멀티-카메라 demux** (§3.4 신규, §3.5 신규) — Do 단계 다중 카메라 시나리오에서 발견된 vendor reply-routing 컨벤션 (모든 firmware 가 dest port 52381 로 reply) 대응. ② Pelco-D `DefaultIpPort` 본문 표 정정 (0 → **4001**, §5.5 코드 블록과 정합). ③ Pelco-D Serial `Parity/DataBits/StopBits` 명시 (8N1, §5.5 보강). ④ Preset Recall/Set Clamp 가드 명시 (§5.5 보강). ⑤ §3.3 회귀 R1 expected 갱신 (FR-H50SN UDP 는 silent firmware 가 아니라 UDP 수신 버그 영향이었음을 본 사이클이 사후 확정). 모든 변경은 실측 디버깅에서 정당화된 진화 — 코드 수정 없이 문서만 동기화. |

## Executive Summary

### 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **기능명** | pelco-d-and-udp-fix |
| **대상 솔루션** | `PN8080Controller.sln` |
| **신규 프로젝트** | 없음 |
| **신규 파일 (v1.1 기준)** | **5개** (`Protocols/Pelco/PelcoConstants.cs`, `Protocols/Pelco/PelcoDCodec.cs`, `Services/PelcoDCameraService.cs`, **`Transports/ViscaUdpHub.cs` ⭐ v1.1 흡수**) |
| **수정 파일** | 6개 (`UdpViscaTransport.cs` ⭐ 전면 재작성, `SerialViscaTransport.cs` (WaitForReply 추가), `CameraCapabilities.cs` (CR-N300, PelcoD 정적 팩토리, MaxAddress), `ICameraServiceFactory.cs` (모델 2개), `Xeno.Framework.Camera.csproj` (4 파일 등록), `CameraControl.cs` (Pelco-D 주소 max 1→255 동적) |

### Value Delivered — 4 관점

| 관점 | 내용 |
|------|------|
| **Problem** | Plan §1.2 의 root cause (`UdpClient.Connect()` source-port filter) 와 §1.3 의 protocol family 부재 — 코드로 환원하지 않으면 Do 단계에서 unconnected socket 의 정확한 시그니처·thread safety·source IP 화이트리스트 정책·Pelco-D 명령 비트마스크·체크섬 식이 매번 재결정되어 일관성 무너짐. |
| **Solution** | UDP fix 를 **before/after diff 코드 블록**으로 명시 (Open/Send/RxLoop 3 메서드). Pelco-D 는 7바이트 packet 사전 + 모든 명령의 cmd1/cmd2 비트마스크 표 + 체크섬 식 + Service-Transport 매핑을 Design 본문에서 확정. CR-N300 은 Capabilities 정적 팩토리 시그니처 1개 + factory case 1줄로 명세. |
| **Function UX Effect** | Do 단계는 본 문서 §3·§4·§5 의 코드 블록을 그대로 옮겨 쓰면 컴파일 통과. UDP fix 는 회귀 위험 zero (FR-H50SN 동일 동작 유지 검증 절차 포함). Pelco-D 는 PT/Zoom/Focus/Preset 명령 모두 본 문서의 비트 표 1회 lookup 으로 구현 가능. |
| **Core Value** | "외부 기기 통합 제어 API"의 두 번째 protocol family (Pelco-D) 도입으로 codec-transport 분리 패턴이 Sony VISCA 외에서도 작동함을 입증. UDP fix 는 silent bug 가 user-facing 으로 escalate 된 사례의 정상적 closure — 향후 카메라 추가 시 같은 카테고리 버그 회귀 0 보장. |

---

## 1. Plan Decisions 매핑 (Q1~Q6 → 설계 위치)

| Q | 결정 | 본 문서 위치 |
|---|------|--------------|
| Q1 | "Pelco-D Generic" 1개 모델 | §6 모델 등록 표, §5.4 `CameraCapabilities.PelcoDGeneric()` |
| Q2 | Pelco-D over TCP/UDP raw 7바이트 | §5.3 Transport 재사용 표, §5.5 UDP 재사용 시 RawMode |
| Q3 | Pelco-D 무응답 (`ExpectReply=false`) | §5.2 `PelcoDCameraService.SendAsync` (WaitForReply=false 강제) |
| Q4 | Pelco-D Baud **9600** | §5.4 `Capabilities.PelcoDGeneric().DefaultBaudRate = 9600` |
| Q5 | enum 리네임 보류 | 본 문서에서 "UdpVisca/TcpVisca" 명칭 유지, comment 로 generic 의미 명시 |
| Q6 | CR-N300 Sony 표준 추정값 | §6 `CanonCrN300()` 정적 팩토리 |

---

## 2. 아키텍처 — v1.1 → v1.2 delta

```
Core/                                       (변경)
  CameraCapabilities.cs                    +CanonCrN300(), +PelcoDGeneric()
  ICameraServiceFactory.cs                 +CR-N300 case, +Pelco-D case

Protocols/                                  (신규 Pelco/ 서브폴더)
  Visca/  (변경 없음)
  Pelco/                                   ← NEW
    PelcoConstants.cs                      ← NEW (sync 0xFF, 명령 비트 상수)
    PelcoDCodec.cs                         ← NEW (PT/Zoom/Focus/Preset packet 빌더 + 체크섬)

Transports/                                 (UdpViscaTransport ⭐ critical fix)
  IViscaIpTransport.cs                     변경 없음 (시그니처 동일)
  SerialViscaTransport.cs                  +`WaitForReply` 플래그 (Pelco-D fire-and-forget)
  UdpViscaTransport.cs                     ⭐ Connect() 제거, source IP 매칭, +RawMode 플래그
  TcpViscaTransport.cs                     +RawMode 플래그 (0xFF slicing 비활성)

Services/
  EviH100CameraService.cs                  변경 없음
  ViscaIpCameraService.cs                  변경 없음 (CR-N300 도 그대로 재사용)
  PelcoDCameraService.cs                   ← NEW (generic, (Info,Caps,Transport) 인자)

UI/
  CameraControl.cs                         + Address TextBox max 동적 (모델 capability 기반)
```

→ **신규 4개 파일, 수정 6개. 기존 코드 회귀 보호 우선.**

---

## 3. ⭐ Scope 1: UDP Fix (`UdpViscaTransport.cs`) — Critical

### 3.1 v1.1 (현재, 버그) ↔ v1.2 (수정) Diff

#### `Open()` 메서드

```diff
 public void Open()
 {
     lock (_lock)
     {
         CloseInternal();
         if (string.IsNullOrEmpty(Host)) throw new InvalidOperationException("Host not set");

         _client = new UdpClient();
-        _client.Connect(IPAddress.Parse(Host), Port);
+        // v1.2: Connect() 제거 — 소켓이 source-port filter 에 묶여 카메라가 다른 port 에서
+        //       reply 보낼 때 OS 가 drop 시켜 응답 수신 불가 (Canon CR-N300 / SRG-300H 사례)
+        //       대신 사용 시점마다 Send(packet, len, _remoteEndpoint) 로 명시 endpoint 지정
+        _remoteEndpoint = new IPEndPoint(IPAddress.Parse(Host), Port);
+        _expectedRemoteIp = _remoteEndpoint.Address;
+        // 0 = 시스템 ephemeral port 자동 할당. 어떤 source port 로 reply 가 와도 받을 수 있도록
+        // local end 도 명시 bind (0.0.0.0 any-port).
+        _client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

         _sequence = 1;
         _rxQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
         _rxLoopCts = new CancellationTokenSource();

         Log("UDP open " + Host + ":" + Port);

         var token = _rxLoopCts.Token;
         var client = _client;
         _rxLoopTask = Task.Run(() => RxLoopAsync(client, token));

         SendControlReset();
     }
 }
```

#### 신규 필드

```csharp
private IPEndPoint _remoteEndpoint;     // v1.2 — Send 시 매번 명시
private IPAddress _expectedRemoteIp;    // v1.2 — Receive 시 source IP 화이트리스트
```

#### `SendCommandAsync` / `SendControlReset` — Send 호출만 변경

```diff
-await c.SendAsync(packet, packet.Length).ConfigureAwait(false);
+await c.SendAsync(packet, packet.Length, _remoteEndpoint).ConfigureAwait(false);
```

(Send 시 명시 endpoint 인자 추가. `UdpClient.SendAsync(byte[], int, IPEndPoint)` 오버로드 사용.)

`SendControlReset` 도 동일 — `_client.Send(packet, packet.Length, _remoteEndpoint)` 로 변경.

#### `RxLoopAsync` — Source IP 매칭

```diff
 private async Task RxLoopAsync(UdpClient client, CancellationToken ct)
 {
     Log("RX loop started");
     while (!ct.IsCancellationRequested)
     {
         try
         {
             var result = await client.ReceiveAsync().ConfigureAwait(false);
             if (ct.IsCancellationRequested) break;
+
+            // v1.2: source IP 화이트리스트 — 다른 카메라/방송 트래픽 무시
+            // (port 는 무시 — 카메라 firmware 가 ephemeral port 에서 reply 가능)
+            if (_expectedRemoteIp != null
+                && !result.RemoteEndPoint.Address.Equals(_expectedRemoteIp))
+            {
+                Log("RX dropped (source " + result.RemoteEndPoint
+                    + " != expected " + _expectedRemoteIp + ")");
+                continue;
+            }
+
             var q = _rxQueue;
             if (q == null || q.IsAddingCompleted) break;
-            Log("RX raw=" + ToHex(result.Buffer));
+            Log("RX from " + result.RemoteEndPoint.Port + " raw=" + ToHex(result.Buffer));
             try { q.Add(result.Buffer, ct); }
             catch (InvalidOperationException) { break; }
             catch (OperationCanceledException) { break; }
         }
         ...
     }
 }
```

### 3.2 (신설) `RawMode` 플래그 — Pelco-D over UDP 대응

VISCA-IP 의 8B 헤더 wrap/strip 은 Pelco-D 에서 부적합. `RawMode=true` 시 send/receive 모두 raw datagram.

```csharp
/// <summary>
/// True = send/receive 모두 raw datagram (Pelco-D over UDP 등). 8-byte VISCA-IP 헤더 미사용.
/// False (기본) = Sony VISCA over IP 8-byte 헤더 wrap/strip.
/// </summary>
public bool RawMode { get; set; } = false;
```

`SendInternalAsync` / `RxLoopAsync` 에 `if (RawMode)` 분기:
- Send: `RawMode=true` 면 `BuildPacket` 호출 없이 `viscaPayload` 그대로 Send
- Receive 처리 (response wait 부분): `RawMode=true` 면 8B header strip 안 함, `Classify` 도 RawMode 일 땐 skip (Pelco-D 는 Classify 의미 없음)

`Pelco-D` 모델은 `ExpectReply=false` 이라 응답 처리 거의 안 탐 → 위 분기는 dead code 에 가까우나, 향후 Pelco-D 응답 지원 시 일관성 위해 명시.

### 3.3 회귀 테스트 절차 (필수, v1.1 갱신)

| # | 시나리오 | 기대 결과 (v1.1) |
|---|---------|----------|
| R1 | FR-H50SN UDP (port 52381 reply) — 기존 동작 | **TX → RX `90 41 FF` 정상 ACK 수신** (v1.0 의 "silent firmware" 가정은 사실 UDP 수신 버그 영향이었음을 본 사이클이 사후 확정) |
| R2 | EVI-H100 RS-232 — 기존 동작 | 회귀 0 (UDP 변경은 Serial 영향 없음) |
| R3 | Canon CR-N300 UDP (port 52381 send, dest port 52381 reply) — **신규 동작** | TX → RX `90 41 FF` (ACK) 정상 수신, late Completion 큐 처리 |
| R4 | 잘못된 IP 입력 | Open() 단계에서 IPAddress.Parse 실패 즉시 |
| R5 | Spoofed datagram (다른 IP 에서 같은 port 로 송신) | hub: dropped (no subscriber for X) 로그, 큐 enqueue 안 됨 |
| **R6** (v1.1 추가) | **Cam1 = Canon (192.168.1.131) + Cam2 = FR (192.168.1.124) 동시 연결 + 양쪽 PT Drive** | hub 가 source IP 로 demux — Cam1 reply 는 Cam1 queue, Cam2 reply 는 Cam2 queue. 무간섭 |

### 3.4 (v1.1 신규) `ViscaUdpHub` 싱글턴 — 멀티-카메라 demux

#### 도입 동기

v1.0 §3.1 의 unconnected pattern (각 service 가 own UdpClient 보유 + ephemeral bind) 만으로는 **다중 VISCA-IP 카메라 동시 연결이 불가능**함을 Do 단계 실측에서 확정 (2026-05-06). 원인:

1. Sony/Canon/FR/SRG 등 **모든 VISCA-IP firmware** 가 reply 를 source 의 source-port 가 아니라 **dest port 52381 of source IP** 로 라우팅 (Sony VISCA-over-IP 컨벤션)
2. 따라서 host 의 local 52381 socket 1개만 reply 수신 가능
3. Cam2 가 ephemeral 로 fallback 되면 reply 가 Cam1 socket 으로 가서 IP 화이트리스트 mismatch 로 drop → Cam2 timeout

#### 해결책 — 싱글턴 공유 listener

`Transports/ViscaUdpHub.cs` (~150줄) 신설.

```
ViscaUdpHub (singleton)
├── 단일 UdpClient bound local 52381 (ephemeral fallback 가능)
├── Subscribers: Dictionary<IPAddress, Action<byte[], int>>
├── Background RxLoop: source IP 로 demux 해 해당 handler 호출
├── Send/SendAsync(packet, remoteEndpoint): 공유 socket 사용
└── Reference-counted lifecycle:
    - 첫 Subscribe 시 lazy start
    - 마지막 Unsubscribe 시 stop
```

#### `ViscaUdpHub` 핵심 시그니처

```csharp
internal sealed class ViscaUdpHub
{
    public static ViscaUdpHub Shared { get; }   // singleton

    public int LocalPort { get; }                // 52381 (or fallback ephemeral)
    public bool IsStarted { get; }

    /// <summary>
    /// Subscribe a handler for datagrams arriving from <paramref name="remoteIp"/>.
    /// Handler receives (datagram bytes, source port). Called from background thread.
    /// First Subscribe starts the listener; lazy.
    /// </summary>
    public void Subscribe(IPAddress remoteIp, Action<byte[], int> handler, Action<string> log);

    public void Unsubscribe(IPAddress remoteIp);

    public void Send(byte[] packet, IPEndPoint remote);
    public Task SendAsync(byte[] packet, IPEndPoint remote);
}
```

#### Hub 의 RxLoop — source IP 로 demux

```csharp
private async Task RxLoop(UdpClient client, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        var result = await client.ReceiveAsync().ConfigureAwait(false);
        if (ct.IsCancellationRequested) break;

        Action<byte[], int> handler = null;
        lock (_lock) {
            _subscribers.TryGetValue(result.RemoteEndPoint.Address, out handler);
        }
        if (handler != null)
        {
            handler(result.Buffer, result.RemoteEndPoint.Port);
        }
        else
        {
            Log("hub: dropped (no subscriber for " + result.RemoteEndPoint.Address + ")");
        }
    }
}
```

#### `UdpViscaTransport` 의 hub 통합 (Open/Send/Close 2-branch)

`UdpViscaTransport` 가 **non-RawMode 일 때 hub 사용**, **RawMode (Pelco-D) 일 때 own ephemeral socket** 유지:

```csharp
public void Open()
{
    lock (_lock)
    {
        // ... build _remoteEndpoint, _expectedRemoteIp, _rxQueue ...
        if (RawMode) OpenRawLocked();        // own UdpClient (Pelco-D)
        else        OpenSharedHubLocked();   // hub subscribe (VISCA-IP)
        _opened = true;
    }
}

private void OpenSharedHubLocked()
{
    var hub = ViscaUdpHub.Shared;
    var queue = _rxQueue;
    hub.Subscribe(_expectedRemoteIp, (data, srcPort) =>
    {
        Log("RX from :" + srcPort + " raw=" + ToHex(data));
        try { queue?.Add(data); } catch { }
    }, msg => Log(msg));
    Log("UDP open " + Host + ":" + Port + " (hub local :" + hub.LocalPort + ")");
    SendControlReset();
}
```

```csharp
private async Task<byte[]> SendInternalAsync(...)
{
    // ... drain ...
    byte[] packet = raw ? viscaPayload : BuildPacket(payloadType, seq, viscaPayload);
    if (raw)
        await rawClient.SendAsync(packet, packet.Length, remote).ConfigureAwait(false);
    else
        await ViscaUdpHub.Shared.SendAsync(packet, remote).ConfigureAwait(false);
    // ... wait for reply on _rxQueue ...
}
```

```csharp
public void Close()
{
    // ... cleanup queue, raw socket ...
    if (!wasRaw && expected != null)
        ViscaUdpHub.Shared.Unsubscribe(expected);
}
```

#### 멀티-카메라 검증 결과 (실측)

3 카메라 × 3 프로토콜 동시 운용 (사용자 로그 2026-05-06):

```
Cam 1 (Canon CR-N300, UDP 192.168.1.131) → hub 구독, ACK 6-12ms
Cam 2 (FR-H50SN, UDP 192.168.1.124)      → hub 구독, ACK 5-9ms
Cam 3 (Pelco-D, RS-232 COM6)             → own Serial transport, fire-and-forget

→ 양 UDP 카메라가 같은 hub local 52381 통해 송수신, source IP 로 정확히 demux
→ 무간섭, 트래픽 cross-contamination 0건
```

### 3.5 (v1.1 신규) Transport 내부 분기 표 — RawMode vs Non-RawMode

`UdpViscaTransport` 가 보유한 분기를 명확히 정리:

| 항목 | Non-RawMode (VISCA-IP) | RawMode (Pelco-D over UDP) |
|------|------------------------|----------------------------|
| **사용 socket** | `ViscaUdpHub.Shared` (싱글턴 공유) | `_rawClient` (own ephemeral UdpClient) |
| **Local port** | hub 가 52381 점유 (또는 fallback ephemeral) | 항상 ephemeral (any port) |
| **Receive 메커니즘** | hub.Subscribe handler → `_rxQueue.Add(data)` | own `RawRxLoop` → source IP 매칭 → `_rxQueue.Add(data)` |
| **Send 패킷 형식** | 8-byte VISCA-IP 헤더 wrap (`BuildPacket`) | viscaPayload 그대로 |
| **Reset 송신** | Open 시 `SendControlReset()` 호출 | 송신 안 함 (Pelco-D 미사용) |
| **응답 처리** | 헤더 strip + `Classify` + late-Completion auto-skip | strip 없음, classify 없음, 1-attempt only |
| **`WaitForReply=false` 시** | Send 후 즉시 return | Send 후 즉시 return |
| **사용 모델** | EVI-H100 (X — 시리얼), SRG-300H, FR-H50SN, **CR-N300** | **Pelco-D Generic** (UDP 선택 시) |



---

## 4. SerialViscaTransport — `WaitForReply` 플래그 추가 (Pelco-D 대응)

Pelco-D 는 fire-and-forget 이라 read-until-0xFF 대기가 불필요·해롭. v1.1 의 `SerialViscaTransport.SendCore` 가 무조건 read 하므로, `WaitForReply=false` 플래그 추가:

```diff
 public Task<byte[]> SendAsync(byte[] payload)
 {
     return Task.Run(new Func<byte[]>(() => SendCore(payload)));
 }

 private byte[] SendCore(byte[] payload)
 {
     SerialPort p;
     lock (_lock) { p = _port; }
     if (p == null || !p.IsOpen) throw new InvalidOperationException("serial port not open");

     try { p.DiscardInBuffer(); } catch { }
     p.Write(payload, 0, payload.Length);

+    if (!WaitForReply) return new byte[0];
+
     var buf = new List<byte>(16);
     ...
 }

+public bool WaitForReply { get; set; } = true;
```

---

## 5. Scope 2: Pelco-D 프로토콜 (신규)

### 5.1 `Protocols/Pelco/PelcoConstants.cs`

```csharp
namespace Xeno.Framework.Camera.Protocols.Pelco
{
    internal static class PelcoConstants
    {
        public const byte Sync = 0xFF;

        // Command 1 비트
        public const byte Cmd1_Sense          = 0x80;  // 거의 사용 안 함
        public const byte Cmd1_AutoManualScan = 0x10;
        public const byte Cmd1_CameraOnOff    = 0x08;
        public const byte Cmd1_IrisClose      = 0x04;
        public const byte Cmd1_IrisOpen       = 0x02;
        public const byte Cmd1_FocusNear      = 0x01;

        // Command 2 비트
        public const byte Cmd2_FocusFar  = 0x80;
        public const byte Cmd2_ZoomWide  = 0x40;
        public const byte Cmd2_ZoomTele  = 0x20;
        public const byte Cmd2_TiltDown  = 0x10;
        public const byte Cmd2_TiltUp    = 0x08;
        public const byte Cmd2_PanLeft   = 0x04;
        public const byte Cmd2_PanRight  = 0x02;
        // bit0 of cmd2 = always 0

        // Extended command 2 codes (Pan/Tilt speed bytes 가 data1/data2 로 치환)
        public const byte ExtCmd2_SetPreset    = 0x03;
        public const byte ExtCmd2_ClearPreset  = 0x05;
        public const byte ExtCmd2_CallPreset   = 0x07;

        // Pelco-D 주소 범위
        public const int MinAddress = 1;
        public const int MaxAddress = 255;

        // Speed 범위
        public const int MinSpeed   = 0x00;
        public const int MaxSpeed   = 0x3F;   // 63 — Turbo 는 0xFF (별도 처리)
    }
}
```

### 5.2 `Protocols/Pelco/PelcoDCodec.cs`

```csharp
using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.Protocols.Pelco
{
    /// <summary>
    /// Pelco-D 7-byte packet builder.
    /// Format: [Sync 0xFF] [Address 1-255] [Cmd1] [Cmd2] [Pan-Speed] [Tilt-Speed] [Checksum]
    /// Checksum = (Address + Cmd1 + Cmd2 + Pan-Speed + Tilt-Speed) mod 256
    /// </summary>
    internal static class PelcoDCodec
    {
        // ----- Pan / Tilt -----
        public static byte[] PanTiltDrive(int address, PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            byte cmd2 = 0;
            switch (dir)
            {
                case PanTiltDirection.Up:        cmd2 = PelcoConstants.Cmd2_TiltUp; break;
                case PanTiltDirection.Down:      cmd2 = PelcoConstants.Cmd2_TiltDown; break;
                case PanTiltDirection.Left:      cmd2 = PelcoConstants.Cmd2_PanLeft; break;
                case PanTiltDirection.Right:     cmd2 = PelcoConstants.Cmd2_PanRight; break;
                case PanTiltDirection.UpLeft:    cmd2 = (byte)(PelcoConstants.Cmd2_TiltUp | PelcoConstants.Cmd2_PanLeft); break;
                case PanTiltDirection.UpRight:   cmd2 = (byte)(PelcoConstants.Cmd2_TiltUp | PelcoConstants.Cmd2_PanRight); break;
                case PanTiltDirection.DownLeft:  cmd2 = (byte)(PelcoConstants.Cmd2_TiltDown | PelcoConstants.Cmd2_PanLeft); break;
                case PanTiltDirection.DownRight: cmd2 = (byte)(PelcoConstants.Cmd2_TiltDown | PelcoConstants.Cmd2_PanRight); break;
                default:                         cmd2 = 0; break;   // Stop
            }
            return Build(address, 0x00, cmd2, (byte)(panSpeed & 0xFF), (byte)(tiltSpeed & 0xFF));
        }

        public static byte[] PanTiltStop(int address)
        {
            return Build(address, 0x00, 0x00, 0x00, 0x00);
        }

        // ----- Zoom -----
        public static byte[] ZoomDrive(int address, ZoomDirection dir, int speed)
        {
            // Pelco-D 는 Zoom 속도 별도 미지원 — speed 인자는 무시 (Pan/Tilt 자리에 0)
            byte cmd2 = 0;
            switch (dir)
            {
                case ZoomDirection.Tele: cmd2 = PelcoConstants.Cmd2_ZoomTele; break;
                case ZoomDirection.Wide: cmd2 = PelcoConstants.Cmd2_ZoomWide; break;
                default: cmd2 = 0; break;
            }
            return Build(address, 0x00, cmd2, 0x00, 0x00);
        }

        public static byte[] ZoomStop(int address) { return PanTiltStop(address); }   // 0x00 0x00 = all stop

        // ----- Focus -----
        public static byte[] FocusDrive(int address, FocusDirection dir, int speed)
        {
            byte cmd1 = 0, cmd2 = 0;
            switch (dir)
            {
                case FocusDirection.Far:  cmd2 = PelcoConstants.Cmd2_FocusFar; break;
                case FocusDirection.Near: cmd1 = PelcoConstants.Cmd1_FocusNear; break;
                default: break;
            }
            return Build(address, cmd1, cmd2, 0x00, 0x00);
        }

        public static byte[] FocusStop(int address) { return PanTiltStop(address); }

        // ----- Preset (Memory) -----
        public static byte[] PresetSet(int address, int presetNumber)
        {
            // FF [addr] 0x00 0x03 0x00 [preset#] [csum]
            return Build(address, 0x00, PelcoConstants.ExtCmd2_SetPreset, 0x00, (byte)(presetNumber & 0xFF));
        }

        public static byte[] PresetRecall(int address, int presetNumber)
        {
            // FF [addr] 0x00 0x07 0x00 [preset#] [csum]
            return Build(address, 0x00, PelcoConstants.ExtCmd2_CallPreset, 0x00, (byte)(presetNumber & 0xFF));
        }

        public static byte[] PresetClear(int address, int presetNumber)
        {
            return Build(address, 0x00, PelcoConstants.ExtCmd2_ClearPreset, 0x00, (byte)(presetNumber & 0xFF));
        }

        // ----- 내부: 7바이트 packet + 체크섬 빌드 -----
        private static byte[] Build(int address, byte cmd1, byte cmd2, byte data1, byte data2)
        {
            byte addr = (byte)(address < 1 ? 1 : (address > 255 ? 255 : address));
            byte csum = (byte)((addr + cmd1 + cmd2 + data1 + data2) % 256);
            return new byte[]
            {
                PelcoConstants.Sync,
                addr,
                cmd1,
                cmd2,
                data1,
                data2,
                csum
            };
        }
    }
}
```

### 5.3 Transport 재사용 표

| Pelco-D over | 기존 transport 재사용 | 추가 변경 |
|--------------|----------------------|----------|
| RS-232/422/485 | `SerialViscaTransport` | `WaitForReply=false` 플래그 추가 (§4) |
| TCP | `TcpViscaTransport` | `WaitForReply=false` 만 set (raw byte send/receive 그대로 작동) |
| UDP | `UdpViscaTransport` (RawMode=true) | §3.2 `RawMode` 플래그 추가 (header wrap 비활성) |

→ **신규 transport 클래스 없음** — 기존 인프라 100% 재사용. 클래스 이름 "Visca" 는 historical 이며 protocol-agnostic 임을 코드 주석에 명시.

### 5.4 `CameraCapabilities.PelcoDGeneric()` 정적 팩토리

```csharp
public static CameraCapabilities PelcoDGeneric()
{
    return new CameraCapabilities
    {
        // Pan/Tilt speed: Pelco-D 표준 0x00-0x3F (63)
        PanSpeedMin = 0x00, PanSpeedMax = 0x3F, PanSpeedDefault = 0x20,
        TiltSpeedMin = 0x00, TiltSpeedMax = 0x3F, TiltSpeedDefault = 0x20,
        // Zoom/Focus 속도 별도 없음 — UI 편의상 동일 범위 노출 (codec 무시)
        ZoomSpeedMin = 0, ZoomSpeedMax = 7, ZoomSpeedDefault = 4,
        FocusSpeedMin = 0, FocusSpeedMax = 7, FocusSpeedDefault = 4,

        DefaultTransport = CameraTransportKind.Rs232,
        SupportedBaudRates = new List<int> { 2400, 4800, 9600, 19200, 38400 },
        DefaultBaudRate = 9600,                    // Q4 결정
        DefaultIpPort = 4001,                       // v1.1: vendor 컨벤션 default — 사용자가 web UI 보고 입력 (필수)
        DefaultAddress = 1,                         // Pelco-D 주소 (1-255)

        PresetCount = 64,                           // Pelco-D 표준 1-32 (vendor 별 1-256), 보수적 64
        OsdSupported = false,                       // Pelco-D 는 OSD 명령 표준 없음
        ExpectReply = false,                        // Q3 결정 — fire-and-forget
        MaxAddress = 255                            // Pelco-D 주소 1-255 (VISCA 1-7 보다 넓음)
    };
}
```

> **v1.1 변경**: `DefaultIpPort = 4001` (PTZOptics raw VISCA-over-TCP 컨벤션과 별개로 Pelco-D over UDP/TCP 일반 default). `PelcoDCameraService.ConnectAsync` 에서 `Port <= 0 ? 4001 : Port` fallback 과 정합. `MaxAddress = 255` 명시 추가 (UI Address 클램프에서 사용 — §7).

### 5.5 `Services/PelcoDCameraService.cs`

```csharp
using System;
using System.Threading.Tasks;
using Xeno.Framework.Camera.Core;
using Xeno.Framework.Camera.Protocols.Pelco;
using Xeno.Framework.Camera.Transports;

namespace Xeno.Framework.Camera.Services
{
    /// <summary>
    /// Generic Pelco-D camera service. Transport 종류 (Serial / UDP / TCP) 는 ConnectAsync 에서
    /// CameraTransportKind enum 보고 dispatch. Codec 은 ViscaCodec 대신 PelcoDCodec 사용.
    /// </summary>
    public sealed class PelcoDCameraService : CameraServiceBase
    {
        private SerialViscaTransport _serial;
        private IViscaIpTransport _ipTransport;

        public PelcoDCameraService()
            : base(
                new CameraInfo { Brand = "Pelco", Model = "Pelco-D Generic", Description = "Generic Pelco-D PTZ camera" },
                CameraCapabilities.PelcoDGeneric())
        {
        }

        public override async Task ConnectAsync()
        {
            EnsureNotDisposed();
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                DisposeTransports();
                switch (Transport)
                {
                    case CameraTransportKind.Rs232:
                    case CameraTransportKind.Rs422:
                    case CameraTransportKind.Rs485:
                        _serial = new SerialViscaTransport
                        {
                            PortName = ComPort,
                            BaudRate = BaudRate <= 0 ? Capabilities.DefaultBaudRate : BaudRate,
                            Parity = Parity.None,    // v1.1 — Pelco-D 표준 8N1
                            DataBits = 8,
                            StopBits = StopBits.One,
                            WaitForReply = false      // Pelco-D fire-and-forget
                        };
                        _serial.Open();
                        SetConnected(true, "Serial " + ComPort + "@" + _serial.BaudRate + " (Pelco-D)");
                        break;

                    case CameraTransportKind.UdpVisca:
                        var udp = new UdpViscaTransport
                        {
                            Host = Host,
                            Port = Port <= 0 ? 4001 : Port,    // Pelco-D over UDP 일반적 포트, vendor 마다 상이
                            WaitForReply = false,
                            RawMode = true                      // VISCA 8B 헤더 미사용
                        };
                        udp.OnLog = msg => Log(LogDirection.Info, "[UDP-Pelco] " + msg);
                        udp.Open();
                        _ipTransport = udp;
                        SetConnected(true, "UDP " + Host + ":" + udp.Port + " (Pelco-D raw)");
                        break;

                    case CameraTransportKind.TcpVisca:
                        var tcp = new TcpViscaTransport
                        {
                            Host = Host,
                            Port = Port <= 0 ? 4001 : Port,
                            WaitForReply = false
                        };
                        tcp.OnLog = msg => Log(LogDirection.Info, "[TCP-Pelco] " + msg);
                        tcp.Open();
                        _ipTransport = tcp;
                        SetConnected(true, "TCP " + Host + ":" + tcp.Port + " (Pelco-D raw)");
                        break;

                    default:
                        throw new NotSupportedException("Pelco-D unsupported transport: " + Transport);
                }
            }
            catch (Exception ex)
            {
                RaiseError("connect failed: " + ex.Message);
                SetConnected(false, "connect failed");
                throw;
            }
            finally { _gate.Release(); }
        }

        public override async Task DisconnectAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try { DisposeTransports(); SetConnected(false, "disconnected"); }
            finally { _gate.Release(); }
        }

        // ----- PT/Zoom/Focus/Preset — 모든 메서드는 PelcoDCodec 호출 후 SendAsync -----
        public override Task PanTiltDriveAsync(PanTiltDirection dir, int panSpeed, int tiltSpeed)
        {
            int p = Clamp(panSpeed, Capabilities.PanSpeedMin, Capabilities.PanSpeedMax);
            int t = Clamp(tiltSpeed, Capabilities.TiltSpeedMin, Capabilities.TiltSpeedMax);
            return SendAsync(PelcoDCodec.PanTiltDrive(Address, dir, p, t), "PT Drive " + dir);
        }
        public override Task PanTiltStopAsync()             => SendAsync(PelcoDCodec.PanTiltStop(Address), "PT Stop");
        public override Task ZoomDriveAsync(ZoomDirection dir, int speed)   => SendAsync(PelcoDCodec.ZoomDrive(Address, dir, speed), "Zoom " + dir);
        public override Task ZoomStopAsync()                => SendAsync(PelcoDCodec.ZoomStop(Address), "Zoom Stop");
        public override Task FocusDriveAsync(FocusDirection dir, int speed) => SendAsync(PelcoDCodec.FocusDrive(Address, dir, speed), "Focus " + dir);
        public override Task FocusStopAsync()               => SendAsync(PelcoDCodec.FocusStop(Address), "Focus Stop");
        // v1.1 — preset 번호 안전 가드 (Capabilities.PresetCount 까지 클램프)
        public override Task RecallPresetAsync(int n)
        {
            int p = Clamp(n, 1, Capabilities.PresetCount);
            return SendAsync(PelcoDCodec.PresetRecall(Address, p), "Preset Recall " + p);
        }
        public override Task SetPresetAsync(int n)
        {
            int p = Clamp(n, 1, Capabilities.PresetCount);
            return SendAsync(PelcoDCodec.PresetSet(Address, p), "Preset Set " + p);
        }

        // OSD: Pelco-D 표준 미지원 — 무시 (또는 향후 vendor extension)
        public override Task OsdOnAsync()     { Log(LogDirection.Info, "OSD On not supported by Pelco-D"); return Task.CompletedTask; }
        public override Task OsdOffAsync()    { Log(LogDirection.Info, "OSD Off not supported by Pelco-D"); return Task.CompletedTask; }
        public override Task OsdSelectAsync() { Log(LogDirection.Info, "OSD Select not supported by Pelco-D"); return Task.CompletedTask; }
        public override Task OsdBackAsync()   { Log(LogDirection.Info, "OSD Back not supported by Pelco-D"); return Task.CompletedTask; }

        private async Task SendAsync(byte[] payload, string label)
        {
            EnsureNotDisposed();
            bool serialOk = _serial != null && _serial.IsOpen;
            bool ipOk = _ipTransport != null && _ipTransport.IsOpen;
            if (!serialOk && !ipOk) { RaiseError(label + " : not connected"); return; }

            await _gate.WaitAsync().ConfigureAwait(false);
            _busy = true;
            try
            {
                Log(LogDirection.Tx, label + " : " + ToHex(payload));
                if (serialOk)
                    await _serial.SendAsync(payload).ConfigureAwait(false);
                else
                    await _ipTransport.SendCommandAsync(payload).ConfigureAwait(false);
                // 응답 처리 없음 — Pelco-D fire-and-forget
            }
            catch (Exception ex) { RaiseError(label + " failed: " + ex.Message); }
            finally { _busy = false; _gate.Release(); }
        }

        private void DisposeTransports()
        {
            try { _serial?.Dispose(); } catch { }
            try { _ipTransport?.Dispose(); } catch { }
            _serial = null;
            _ipTransport = null;
        }

        public override void Dispose()
        {
            DisposeTransports();
            base.Dispose();
        }
    }
}
```

---

## 6. Scope 3: Canon CR-N300 모델

### 6.1 `CameraCapabilities.CanonCrN300()` 정적 팩토리

```csharp
public static CameraCapabilities CanonCrN300()
{
    var caps = SrgIp();                 // Sony VISCA-IP 표준 그대로 시작
    caps.DefaultTransport = CameraTransportKind.UdpVisca;
    caps.DefaultIpPort = 52381;          // 사용자 환경 확정
    caps.PresetCount = 16;               // Q6 — Sony 표준 추정, 실측 후 micro-task
    caps.ExpectReply = true;
    // Brand/Model 은 factory.Create 에서 별도 주입
    return caps;
}
```

### 6.2 Factory 등록

```diff
 public IList<CameraInfo> GetAvailableModels()
 {
     return new List<CameraInfo>
     {
         new CameraInfo { Brand = "Sony", Model = "EVI-H100", Description = "Sony EVI-H100 (RS-232/RS-422 VISCA)" },
         new CameraInfo { Brand = "Sony", Model = "SRG-300H", Description = "Sony SRG-300H (VISCA over IP)" },
         new CameraInfo { Brand = "FR",   Model = "FR-H50SN", Description = "FR-H50SN (VISCA over IP, OEM firmware)" },
+        new CameraInfo { Brand = "Canon",Model = "CR-N300",  Description = "Canon CR-N300 (VISCA over IP)" },
+        new CameraInfo { Brand = "Pelco",Model = "Pelco-D Generic", Description = "Generic Pelco-D camera (RS-232/422/485 + UDP/TCP)" }
     };
 }

 public CameraCapabilities GetCapabilities(string model)
 {
     switch (model)
     {
         case "EVI-H100":         return CameraCapabilities.EviH100();
         case "SRG-300H":         return CameraCapabilities.SrgIp();
         case "FR-H50SN":         return CameraCapabilities.FrH50Sn();
+        case "CR-N300":          return CameraCapabilities.CanonCrN300();
+        case "Pelco-D Generic":  return CameraCapabilities.PelcoDGeneric();
         default: throw new NotSupportedException("Unknown camera model: " + model);
     }
 }

 public ICameraService Create(string model)
 {
     switch (model)
     {
         case "EVI-H100":
             return new Services.EviH100CameraService();
         case "SRG-300H":
             return new Services.ViscaIpCameraService(
                 new CameraInfo { Brand = "Sony", Model = "SRG-300H", Description = "Sony SRG-300H PTZ (VISCA over IP)" },
                 CameraCapabilities.SrgIp());
         case "FR-H50SN":
             return new Services.ViscaIpCameraService(
                 new CameraInfo { Brand = "FR", Model = "FR-H50SN", Description = "FR-H50SN PTZ (VISCA over IP, OEM)" },
                 CameraCapabilities.FrH50Sn());
+        case "CR-N300":
+            return new Services.ViscaIpCameraService(
+                new CameraInfo { Brand = "Canon", Model = "CR-N300", Description = "Canon CR-N300 PTZ (VISCA over IP)" },
+                CameraCapabilities.CanonCrN300());
+        case "Pelco-D Generic":
+            return new Services.PelcoDCameraService();   // info/caps 는 ctor 에서 자동
         default:
             throw new NotSupportedException("Unknown camera model: " + model);
     }
 }
```

---

## 7. UI 변경 — `CameraControl.cs`

Pelco-D 주소 범위가 1-255 로 VISCA (1-7) 보다 넓음. UI 의 Address TextBox 에 모델별 max 동적 적용.

### 7.1 `CameraCapabilities` 에 `MaxAddress` 추가 (선택)

```csharp
public int MaxAddress { get; set; } = 7;     // 기본 VISCA 범위
```

각 정적 팩토리에서:
- `EviH100()`/`SrgIp()`/`FrH50Sn()`/`CanonCrN300()`: 변경 없음 (기본 7)
- `PelcoDGeneric()`: `caps.MaxAddress = 255`

### 7.2 UI Address 검증 (Leave 핸들러)

```csharp
slot.TxtAddress.Leave += (s, e) =>
{
    int a;
    int parsed = int.TryParse(slot.TxtAddress.Text, out a) ? a : 1;
    int max = 7;
    if (slot.Service != null) max = slot.Service.Capabilities.MaxAddress;
    else if (!string.IsNullOrEmpty(slot.Config.Model) && _factory != null)
        max = _factory.GetCapabilities(slot.Config.Model).MaxAddress;
    slot.Config.Address = Math.Max(1, Math.Min(parsed, max));
    if (slot.Config.Address.ToString() != slot.TxtAddress.Text)
        slot.TxtAddress.Text = slot.Config.Address.ToString();
};
```

→ 모델 콤보가 Pelco-D Generic 이면 1-255 입력 허용, VISCA 모델이면 1-7 로 자동 클램프.

---

## 8. 테스트 전략

### 8.1 정적 코덱 검증

| 명령 | 입력 | 기대 출력 |
|------|------|----------|
| `PelcoDCodec.PanTiltDrive(1, Right, 0x20, 0x00)` | addr=1, dir=Right, p=0x20 | `FF 01 00 02 20 00 23` |
| `PelcoDCodec.PanTiltStop(1)` | addr=1 | `FF 01 00 00 00 00 01` |
| `PelcoDCodec.ZoomDrive(1, Tele, 0)` | addr=1, dir=Tele | `FF 01 00 20 00 00 21` |
| `PelcoDCodec.PresetSet(1, 5)` | addr=1, n=5 | `FF 01 00 03 00 05 09` |
| `PelcoDCodec.PresetRecall(1, 5)` | addr=1, n=5 | `FF 01 00 07 00 05 0D` |

체크섬 식 검증: `0x01 + 0x00 + 0x02 + 0x20 + 0x00 = 0x23` ✓

### 8.2 회귀 테스트 (UDP fix)

| # | 시나리오 | 검증 |
|---|---------|------|
| R1 | FR-H50SN UDP 연결 + PT Drive | TX → 카메라 동작, RX timeout (silent firmware 동일) |
| R2 | EVI-H100 RS-232 PT Drive/Stop | 회귀 0 |
| R3 | TCP 기반 카메라 (FR-H50SN TCP 모드) | 회귀 0 |

### 8.3 신규 동작 검증

| # | 시나리오 | 검증 |
|---|---------|------|
| N1 | Canon CR-N300 (192.168.1.131:52381) UDP | TX → RX `90 41 FF` (ACK) 정상 수신, late Completion drain |
| N2 | Pelco-D RS-232 (사용자 카메라, 9600) PT Drive | 카메라 회전 |
| N3 | Pelco-D RS-232 PT Stop | 카메라 정지 |
| N4 | Pelco-D Preset Set 1 + Recall 1 | Set → 위치 저장, Recall → 해당 위치 복귀 |

### 8.4 빌드 검증

- Debug + Release 0 경고 0 오류
- Pelco-D 신규 4 파일 추가 후에도 회귀 0

---

## 9. 구현 순서 (Do 단계 권장)

1. **Step 1: UDP fix** (1h) — `UdpViscaTransport.cs` Connect 제거, source IP 매칭, RawMode 플래그. 빌드 통과 + FR-H50SN 회귀 검증
2. **Step 2: Canon CR-N300 모델 등록** (0.3h) — `Capabilities.CanonCrN300()` + Factory 3 케이스. 실기 검증 (192.168.1.131)
3. **Step 3: Pelco-D 코덱 + 상수** (1h) — `Protocols/Pelco/PelcoConstants.cs`, `PelcoDCodec.cs` + 정적 코덱 단위 검증
4. **Step 4: SerialViscaTransport WaitForReply 추가** (0.3h)
5. **Step 5: PelcoDCameraService** (1h) — Connect dispatch (Serial/UDP/TCP), SendAsync fire-and-forget
6. **Step 6: Capabilities + Factory + UI Address max** (0.5h)
7. **Step 7: csproj 4 신규 파일 등록 + 빌드 검증** (0.2h)
8. **Step 8: 실기 검증** (사용자 환경) — Canon CR-N300 + Pelco-D RS-232

총 예상 **~4.3h** (camera-remote-control round-1 의 11.5h 대비 작은 범위 — 기존 인프라 100% 재사용 효과).

---

## 10. 위험 재평가 (Plan §7 대비)

| # | Plan 위험 | Design 단계 보완 |
|---|----------|------------------|
| R1 | Old program log 단순 표기 차이 가능성 | §3.3 회귀 테스트 R1 (FR-H50SN) 으로 unconnected pattern 안전성 입증 후 진행. 만약 회귀 발생 시 즉시 rollback 가능 (1 메서드 변경) |
| R2 | Pelco-D vendor 별 변형 | "Pelco-D Generic" 1개로 표준 7바이트만 구현, vendor 특화는 향후 Capabilities 정적 팩토리 1개 추가로 흡수 (§5.4 패턴) |
| R3 | Pelco-D 응답 처리 | `ExpectReply=false` 기본 + UI 수동 toggle 미제공 (필요 시 v3) |
| R4 | Pelco-D Address 1-255 vs VISCA 1-7 | §7.1 `MaxAddress` capability + §7.2 UI 동적 클램프로 흡수 |
| R5 | UdpVisca/TcpVisca 리네임 | Q5 결정으로 보류 — 본 사이클 영향 없음 |
| R6 | CR-N300 매뉴얼 미보유 | Sony VISCA 표준 추정 + 실측 후 micro-task |
| D1 | UDP fix 회귀 | §3.3, §8.2 회귀 절차 필수 — Step 1 직후 FR/EVI 양쪽 검증 |

---

## 11. Open Items

### v1.0 위임 항목 (변동 없음 / v1.1 결정)

| # | 항목 | 상태 (v1.1, 2026-05-06) |
|---|------|------------------------|
| OD1 | Pelco-D over UDP vendor-default 포트 (4001 가정) | **결정**: `DefaultIpPort = 4001` 채택 (§5.4 표 정정 완료). 사용자가 카메라 web UI 보고 UI 의 Port 필드에 입력 (필수). |
| OD2 | Pelco-D Preset 최대 번호 64 추정 (vendor 별 32-256) | 미해결 — 보수 값 64 유지. 사용자 카메라 매뉴얼 확보 시 micro-task. 영향 없음 (실측은 1/2 만 사용) |
| OD3 | Pelco-D over TCP wire framing — raw 7바이트 외 vendor framing 가능성 | 미해결 — 사용자 환경 RS-232 검증으로 충분, TCP 변형은 이슈 발생 시 후속 |
| OD4 | UI Address TextBox placeholder text | 미해결 — 시각 hint, 동작 무관 |

### v1.1 신규 항목 (실측 발견)

| # | 항목 | 비고 |
|---|------|------|
| **OD5** | **멀티-Canon (또는 같은 vendor 모델 N대) 동시 연결 시 hub demux 동작** | 실측은 Canon 1 + FR 1 케이스만 검증. **같은 vendor 모델 2대 동시** (예: Canon CR-N300 × 2) 는 source IP 가 다르면 demux 정상이지만 미검증. 향후 시나리오 발생 시 1회 확인 |
| **OD6** | **Hub local 52381 점유 실패 시 (다른 프로세스가 이미 점유) ephemeral fallback 동작** | 코드 fallback 구현됨 (`ViscaUdpHub.cs:113-121`), 단 fallback 시 모든 reply 가 못 오는 위험. 실 환경에서 발생 시 명시 경고 가능 |

위 항목 모두 본 v1.1 설계의 클래스 구조에 영향 없음.

---

**문서 상태**: Approved (v1.1, 2026-05-06)

### v1.1 Round-2 흡수 변경 요약

본 v1.1 갱신은 v1.0 Design 승인 후 Do/Check 단계에서 도출된 4건의 implementation-ahead 변경을 단방향 동기화한 결과:

1. **§3.4 (신규)** — `ViscaUdpHub` 싱글턴 + 멀티-카메라 demux 다이어그램 + 시그니처 + Hub RxLoop + UdpViscaTransport 통합 코드
2. **§3.5 (신규)** — RawMode vs Non-RawMode transport 내부 분기 표
3. **§3.3 (갱신)** — 회귀 R1 expected (silent → reply, FR-H50SN UDP 도 정상 동작 확정), R6 신규 (멀티-카메라 무간섭)
4. **§5.4 (갱신)** — `DefaultIpPort = 4001` (본문 0 → 4001, §5.5 코드 블록과 정합), `MaxAddress = 255` 명시
5. **§5.5 (갱신)** — Serial Parity/DataBits/StopBits (8N1) 명시, RecallPreset/SetPreset Clamp 가드 추가

전체 변경의 동기·증거·매칭 분석은 `docs/03-analysis/pelco-d-and-udp-fix.analysis.md` (round-2) 참조.
