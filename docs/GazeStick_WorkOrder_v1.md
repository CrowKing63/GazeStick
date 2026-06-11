# GazeStick — 코딩 에이전트 작업 지시서 v1.0

웹캠 시선 추적 → Xbox 가상 패드 오른쪽 스틱 변환 유틸리티 (Windows)

---

## 1. 프로젝트 개요

GazeStick은 Beam Eye Tracker SDK로 수신한 시선 좌표를 ViGEmBus 가상 Xbox 360 컨트롤러의 오른쪽 스틱(RX/RY) 입력으로 변환하는 Windows 접근성 유틸리티다. 전용 시선 추적 하드웨어 없이 웹캠만으로 게임패드 카메라 조작을 대체하는 것이 목표다.

**전제 조건:**
- Beam Eye Tracker 앱이 설치 및 백그라운드 실행 중일 것
- ViGEmBus 드라이버가 설치되어 있을 것

**언어:** C# (.NET 8, WinForms — 트레이 상주 앱 구조)

---

## 2. 아키텍처

앱은 네 개의 서비스로 구성된다. 각 서비스는 독립적으로 교체 가능하도록 인터페이스로 분리한다.

| 서비스 | 역할 |
|---|---|
| `TrackingService` | Beam SDK 폴링, 시선 X/Y 좌표(0.0~1.0) 수신. Beam 앱 연결 상태를 이벤트로 노출. |
| `StickMapper` | 좌표 → 스틱 값 변환. 데드존 / 감도 / 스무딩 적용. 순수 함수 구조 권장. |
| `VirtualPad` | ViGEm Xbox 360 가상 컨트롤러 생성 및 관리. 슬롯 번호 고정 로직 포함. |
| `TrayApp` | 시스템 트레이 상주 UI. 팝업 패널, 설정 관리, 전역 단축키 등록. |

### 2-1. 시작 시퀀스

앱이 실행되면 아래 순서로 초기화한다.

1. ViGEm 드라이버 연결 확인 → 가상 패드 생성 (고정 슬롯, 아래 참조)
2. Beam SDK 연결 시도 개시 (백그라운드 폴링, 재시도 루프)
3. 트레이 아이콘 표시 — 주황 상태 (Beam 대기 중)
4. Beam 연결 성공 시 추적 시작, 아이콘 녹색으로 전환

> **NOTE:** Beam 앱은 별도로 자동 시작되므로 GazeStick이 직접 Beam 앱을 실행할 필요는 없다. 연결될 때까지 폴링만 하면 된다.

### 2-2. 가상 패드 슬롯 고정

ViGEmBus는 부팅/연결 순서에 따라 가상 패드에 Xbox 컨트롤러 인덱스(슬롯 번호, 1~4)를 동적으로 배정한다. 이 번호가 매번 달라지면 reWASD 등 외부 리매핑 툴의 프로필이 꼬인다. 따라서 다음 방식으로 슬롯을 고정한다.

1. 첫 실행 시: 현재 사용 중이지 않은 가장 낮은 번호 슬롯을 자동 선택
2. 해당 슬롯 번호를 설정 파일에 저장
3. 이후 실행 시: 저장된 슬롯 번호로 패드 생성 시도
4. 충돌 발생 시 (해당 슬롯이 이미 사용 중): 사용 가능한 다음 슬롯으로 폴백, 트레이 툴팁에 경고 표시

사용자는 팝업 패널에서 슬롯 번호를 수동으로 변경할 수 있다.

---

## 3. 핵심 로직 — StickMapper

StickMapper는 다음 순서로 시선 좌표를 스틱 값으로 변환한다. 모든 계산은 float(32비트)로 처리한다.

### 3-1. 중립점 편차 계산

```
dx = gazeX - 0.5   // -0.5 ~ +0.5
dy = gazeY - 0.5   // -0.5 ~ +0.5
```

### 3-2. 데드존 적용

반지름 기반 원형 데드존을 사용한다. 단순 축별 데드존보다 자연스럽다.

```
distance = sqrt(dx² + dy²)

if (distance < deadzone) → stickX = 0, stickY = 0, 종료

// 데드존 경계에서 값이 튀지 않도록 rescale
scale = (distance - deadzone) / (1.0 - deadzone)
nx = (dx / distance) * scale
ny = (dy / distance) * scale
```

### 3-3. 감도 적용 및 클램프

```
nx = clamp(nx * sensitivity, -1.0, +1.0)
ny = clamp(ny * sensitivity, -1.0, +1.0)
```

### 3-4. 스무딩 (지수 이동 평균)

이전 프레임 값과 보간한다. smoothing 값이 0이면 스무딩 없음, 1.0이면 움직임 없음이다.

```
outputX = prevX * smoothing + nx * (1 - smoothing)
outputY = prevY * smoothing + ny * (1 - smoothing)
```

### 3-5. ViGEm 출력

outputX, outputY를 Xbox 360 오른쪽 스틱(RX, RY)으로 출력한다. ViGEm.NET의 `Xbox360Report.RightThumbX/Y`는 short(-32768~32767) 범위이므로 아래와 같이 변환한다.

```csharp
report.RightThumbX = (short)(outputX * 32767);
report.RightThumbY = (short)(-outputY * 32767);  // Y축 반전 (화면 좌표 vs 스틱 좌표)
```

> **NOTE:** Y축 반전 여부는 설정에서 토글 가능하게 하면 좋다. 게임마다 다를 수 있다.

---

## 4. UI 명세

UI는 시스템 트레이 아이콘과 팝업 패널 두 가지로만 구성된다. 별도의 메인 윈도우는 없다.

### 4-1. 트레이 아이콘

| 상태 | 아이콘 | 조건 |
|---|---|---|
| ON + 연결됨 | 녹색 눈 아이콘 | 활성 상태이고 Beam 연결됨 |
| 대기 / Beam 없음 | 주황 경고 아이콘 | Beam 미연결 또는 OFF 상태 |
| 오류 | 빨간 경고 아이콘 | ViGEm 오류 등 치명적 문제 |

- 좌클릭: ON/OFF 토글
- 우클릭: 팝업 패널 열기
- 툴팁: 현재 슬롯 번호 + Beam 연결 상태 표시

### 4-2. 팝업 패널

트레이 아이콘 우클릭 시 아이콘 근처에 표시되는 작은 패널이다 (WinForms: `ToolStripDropDown` 또는 Borderless Form). 크기는 약 256 × 240px.

**헤더**
- 왼쪽: 앱 이름 "GazeStick" + 상태 아이콘
- 오른쪽: ON / OFF 토글 버튼 (pill 형태, ON이면 녹색 테두리/텍스트, OFF이면 회색)

**바디 — 파라미터 조절**

파라미터 3개를 NumericAdjuster 컨트롤로 표시한다.

NumericAdjuster 구성:
- 왼쪽: `−` 버튼 (클릭 시 step 1회 감소)
- 가운데: 현재 값 표시 영역. 마우스 드래그(좌우)로 연속 조정 가능. 하단에 "drag" 힌트 텍스트
- 오른쪽: `+` 버튼 (클릭 시 step 1회 증가)

| 항목 | 범위 | step | 기본값 | 표시 형식 |
|---|---|---|---|---|
| Deadzone | 0.00 ~ 0.50 | 0.01 | 0.10 | 소수점 2자리 |
| Sensitivity | 0.1 ~ 5.0 | 0.1 | 1.0 | 소수점 1자리 |
| Smoothing | 0.0 ~ 0.9 | 0.05 | 0.30 | 소수점 2자리 |

OFF 상태일 때 바디 전체를 opacity 40%로 흐리게 표시하고 입력을 비활성화한다.

**푸터**
- Toggle hotkey 표시 (현재 설정된 키, 우측에 배지 형태). 클릭 시 단축키 재설정 모드 진입
- Beam 연결 상태 (작은 점 + 텍스트): 연결됨 / Beam 앱 없음 / 시선 추적 없음
- 패드 슬롯 번호 표시 (예: "패드 슬롯 #2"). 클릭 시 슬롯 변경 가능
- 종료 버튼 (빨간 텍스트)

> **NOTE:** 팝업은 포커스를 잃으면(Deactivate 이벤트) 자동으로 닫힌다. ESC 키로도 닫힌다.

---

## 5. 설정 파일

설정은 JSON으로 저장한다. 경로는 `%AppData%\GazeStick\settings.json`이다. 파라미터 변경 시 즉시 저장한다 (별도 저장 버튼 없음).

```json
{
  "deadzone": 0.10,
  "sensitivity": 1.0,
  "smoothing": 0.30,
  "invertY": false,
  "toggleHotkey": "F9",
  "padSlot": 2,
  "startWithWindows": true,
  "startActive": true
}
```

| 키 | 기본값 | 설명 |
|---|---|---|
| `deadzone` | 0.10 | 원형 데드존 반지름 (정규화 좌표 기준) |
| `sensitivity` | 1.0 | 스틱 값 배율 |
| `smoothing` | 0.30 | 지수 이동 평균 계수 (0 = 스무딩 없음) |
| `invertY` | false | Y축 반전 여부 |
| `toggleHotkey` | "F9" | 전역 단축키 (System.Windows.Forms.Keys 이름 문자열) |
| `padSlot` | 자동 | ViGEm 가상 패드 슬롯 번호 (1~4). 첫 실행 시 자동 할당 후 저장. |
| `startWithWindows` | true | Windows 시작 시 자동 실행 여부 (HKCU Run 레지스트리) |
| `startActive` | true | 앱 시작 시 ON 상태로 시작할지 여부 |

---

## 6. 전역 단축키

게임 실행 중에도 동작해야 하므로 `RegisterHotKey` (user32.dll P/Invoke) 또는 이를 래핑한 라이브러리를 사용한다.

- 기본 단축키: F9 (단일 키)
- 단축키 재설정: 팝업 푸터의 hotkey 배지 클릭 → 다음 입력된 키 조합을 캡처하여 저장
- 단축키 충돌 시 (등록 실패): 트레이 툴팁에 경고 표시, 단축키 없이 동작 유지

---

## 7. 오류 처리

| 상황 | 처리 방법 |
|---|---|
| ViGEm 드라이버 없음 | 시작 시 메시지 박스로 안내 후 종료. 설치 가이드 URL 링크 포함. |
| Beam 앱 미실행 | 트레이 주황 아이콘 유지, 백그라운드에서 2초 간격으로 재연결 시도. 앱 종료하지 않음. |
| Beam 연결 끊김 (실행 중) | 스틱 중립(0,0) 출력, 재연결 시도 재개, 아이콘 주황으로 전환. |
| 지정 패드 슬롯 충돌 | 사용 가능한 다음 슬롯으로 폴백. 툴팁에 "슬롯 #N으로 변경됨" 표시. |
| 설정 파일 손상/없음 | 기본값으로 초기화하여 동작 계속. 파일 새로 작성. |

---

## 8. Windows 자동 시작

`startWithWindows`가 true일 때 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`에 앱 경로를 등록한다. 설정 변경 시 즉시 레지스트리를 업데이트한다.

> **NOTE:** 관리자 권한 없이 HKCU에 등록하므로 UAC 문제가 없다. HKLM 사용 금지.

---

## 9. 권장 프로젝트 구조

```
GazeStick/
  GazeStick.csproj
  Program.cs                  // 진입점, 단일 인스턴스 체크
  Services/
    ITrackingService.cs
    BeamTrackingService.cs
    StickMapper.cs            // 순수 함수, 상태 없음
    VirtualPadService.cs
  Models/
    AppSettings.cs
    GazePoint.cs
    StickOutput.cs
  UI/
    TrayApplicationContext.cs  // ApplicationContext 상속
    PopupPanel.cs              // 팝업 패널 폼
    NumericAdjuster.cs         // 커스텀 컨트롤
  Helpers/
    HotkeyManager.cs           // RegisterHotKey P/Invoke
    SettingsManager.cs         // JSON 읽기/쓰기
    AutoStartManager.cs        // 레지스트리
```

---

## 10. NuGet 패키지

| 패키지 | 용도 |
|---|---|
| `Nefarius.ViGEm.Client` | ViGEm 가상 패드 생성 및 입력 출력 |
| Beam Eye Tracker SDK | 시선 좌표 수신 (DLL 직접 참조, NuGet 없음) |
| `System.Text.Json` | 설정 파일 직렬화 (BCL 포함, 별도 설치 불필요) |

> **⚠ 주의:** Beam SDK는 NuGet에 없다. [docs.beam.eyeware.tech](https://docs.beam.eyeware.tech/)에서 SDK를 직접 다운로드하고 DLL을 프로젝트에 로컬 참조로 추가한다. SDK 바이너리는 레포에 포함하지 않는다 — `.gitignore` 처리 필요.

---

## 11. MVP 완료 기준

아래 항목을 모두 충족하면 MVP 완료로 간주한다.

1. 부팅 후 GazeStick 자동 시작, Beam 연결 시 추적 자동 시작됨
2. 시선이 화면 가장자리를 향하면 Xbox 오른쪽 스틱이 해당 방향으로 기울어짐
3. 화면 중앙을 보면 스틱이 중립(0,0)으로 복귀함
4. F9로 ON/OFF 토글 시 트레이 아이콘 색상 변경됨
5. 팝업 패널에서 Deadzone / Sensitivity / Smoothing 조절 시 즉시 반영됨
6. 설정 변경 사항이 앱 재시작 후에도 유지됨
7. ViGEm 패드 슬롯 번호가 재시작해도 동일하게 유지됨
8. Beam 앱 종료 후 재실행 시 자동 재연결됨

---

## 12. 향후 확장 (MVP 제외)

MVP 이후 고려할 기능이다. 현재 코딩에서는 구현하지 않는다.

- Y축 반전 토글 UI (설정 키는 이미 포함)
- 헤드 트래킹 모드 (Beam SDK HeadPose 데이터 활용)
- 감도 커브 설정 (선형 / 지수 / 로그)
- 게임별 파라미터 프로필
- 왼쪽 스틱 출력 모드
- Tobii Game Integration 백엔드 플러그인 구조

---

*GazeStick 작업 지시서 v1.0 — 이 문서를 기준으로 구현하고, 변경 사항 발생 시 문서를 먼저 수정한다.*
