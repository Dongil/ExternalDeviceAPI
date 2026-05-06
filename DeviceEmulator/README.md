# DeviceEmulator

PTZ 카메라 / 메트릭스 등 외부 장비의 **반대편 역할** (수신·응답) 을
수행하는 standalone 에뮬레이터.

`Xeno.Framework.Camera` 라이브러리의 inverse parser
(`ViscaCommandParser`, `PelcoDCommandParser`, `ViscaReplyBuilder`) 를 사용해
컨트롤러가 보낸 byte 시퀀스를 의미 단위로 표시하고 자동으로 ACK/Completion 응답을 송신.

## 사전 요구

- Windows 10/11
- .NET Framework 4.8 (대부분 사전 설치됨)

## v1 지원 모델 (5종)

| Brand | Model | Transports |
|-------|-------|-----------|
| Sony | EVI-H100 | Serial (RS-232/422) |
| Sony | SRG-300H | UDP, TCP |
| FR | FR-H50SN | TCP, UDP |
| Canon | CR-N300 | UDP |
| Pelco | Pelco-D Generic | Serial, UDP, TCP |

PN-8080 / Videohub 메트릭스는 v2 사이클에서 추가 예정.

## 빌드

1. VS2022 에서 `DeviceEmulator/DeviceEmulator.sln` 열기
2. `Release` 구성 선택 후 솔루션 빌드 (Ctrl+Shift+B)
3. 결과물: `DeviceEmulator/DeviceEmulator/bin/Release/`
   - `DeviceEmulator.exe`
   - `Xeno.Framework.Camera.dll`

## 배포 (다른 머신으로)

1. `bin/Release/` 폴더 통째로 zip
2. 다른 머신에 압축 풀기
3. `DeviceEmulator.exe` 더블클릭

## 사용법

1. **Device Type** 콤보 → "Camera" 선택
2. **Model** 콤보 → 시뮬레이트할 모델 (예: `Sony SRG-300H`)
3. **Transport** 콤보 → 통신 방식 (Serial / UDP / TCP) — 모델별 지원 항목만 표시
4. (Serial) **COM Port + Baud** 선택  / (IP) **Local Port** 입력
   - UDP 기본 52381, TCP 기본 5678
   - UDP 인 경우 `Sony reply convention` 체크 시 응답 dest port = 52381 of source IP
     (해제 시 source port mirror — legacy 컨트롤러 검증용)
5. **Listen** 클릭 → 상태 라벨이 `● Listening` 으로 변경
6. 컨트롤러가 본 머신 IP:Port 로 접속/송신
7. 로그 패널에 수신 packet 과 자동 응답이 표시
8. **Stop** 으로 중단

## 옵션

| 옵션 | 동작 |
|------|------|
| **Latency (ms)** | 응답 송신 전 지연 (0~1000) |
| **Inject NAK** | 다음 명령에 ACK 대신 VISCA Error (0x60 0x02 Syntax) 응답 |
| **Inject Timeout** | 다음 명령에 응답 자체를 안 함 |
| **Inject Malformed** | 다음 명령에 garbage byte 응답 |
| **1회용** | 체크 시 inject 옵션이 다음 명령에 1회만 적용 |

## 멀티-인스턴스

`DeviceEmulator.exe` 를 여러 번 실행해도 OS 가 별도 process 로 인식.
단 같은 port (예: UDP 52381) 를 두 instance 가 동시 점유 불가 →
두 번째 Listen 시 SocketException. 인스턴스마다 다른 port 를 지정하여 우회.

## 통합 테스트 패턴

| # | 시나리오 |
|---|---------|
| I1 | DeviceEmulator (CR-N300, UDP 52381) Listen + CameraController (CR-N300, UDP 192.168.x.y) → PT/Zoom 명령 송신 → emulator 가 RX 표시 + ACK/Completion 송신 → controller 가 ACK 정상 수신 |
| I2 | DeviceEmulator (Pelco-D, Serial COM2) + CameraController (Pelco-D, Serial COM1, com0com 페어) → PT 패킷이 emulator 측에서 byte-correct 표시 (응답은 Pelco-D 표준상 없음) |
| I3 | DeviceEmulator (FR-H50SN, TCP 5678) + CameraController (FR-H50SN, TCP) → PT/Zoom/Preset 시퀀스 |
| I4 | Inject NAK 토글 후 controller 명령 → controller 측 ERR 로그 정상 |
| I5 | Latency 500ms 설정 후 controller 명령 → controller 측 응답 지연 인식 |
