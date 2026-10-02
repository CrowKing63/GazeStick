# GazeStick — 뷰포트 기하 수정 계획 (해상도 갱신 + 멀티 디스플레이)

Beam Eye Tracker SDK에 전달하는 뷰포트 기하(ViewportGeometry) 관련 버그 2건의 수정 계획.

**현상:**
- 가상 디스플레이로 해상도가 바뀌는 동안 GazeStick이 실행 중이면, 정규화 시선 좌표가 옛 해상도 기준으로 계산되어 오버레이와 스틱 영점이 어긋남.
- 멀티 디스플레이 환경에서 뷰포트가 전체 가상 화면(virtual screen)을 덮어, 실제 게임 화면 중심과 스틱 영점이 안 맞음 (예: 오버레이가 화면 중앙일 때 스틱이 오른쪽 아래를 향함).

**조사 결과 요약 (2026-10-02):**
- `StickMapper`는 정규화 좌표(0~1) 위에서만 계산하며 해상도 무관, 좌우 대칭. 데드존/커브/민감도/스무딩 모두 대칭이라 이 쪽에서 비대칭 영점 오프셋이 나올 수 없음 → **변경 불필요**.
- `BeamTrackingService.Start()`가 `ViewportGeometry((0,0), (SM_CXSCREEN, SM_CYSCREEN))`를 시작 시 **한 번만** 고정. 이후 갱신 호출이 전무.
- `Point00 = (0,0)` 하드코딩 + `GetSystemMetrics`는 **모든 모니터 합집합**(Windows Virtual Screen) 크기를 반환 → 멀티 디스플레이에서 뷰포트가 실제 화면보다 커짐.
- 벤더링된 SDK 래퍼(`Services/BeamEyeTrackerApi.cs`)에 `EW_BET_API_UpdateViewportGeometry` P/Invoke가 빠져 있음 (공식 SDK에는 존재, `beam-sdk/csharp/src/Eyeware/BeamEyeTracker.cs` 875줄).

---

## 1. 목표와 범위

| 항목 | 내용 |
|---|---|
| 버그 A | 해상도/모니터 구성 변경 감지 → 뷰포트 기하 갱신 |
| 버그 B | 뷰포트를 "전체 가상 화면"이 아닌 **사용자가 고른 단일 디스플레이** 기준으로 계산 |
| 범위 외 | StickMapper, VirtualPadService, 설정 UI의 기존 슬라이더 — 손대지 않음 |
| 좌표 체계 | 통합 화면(unified screen) = Windows Virtual Screen, 논리 픽셀. WinForms `Screen.Bounds`와 `GetSystemMetrics`가 같은 공간(논리 픽셀)이라 혼용해도 일관됨. 100% 스케일 환경에서 DPI 요인은 배제 확인済み(현재 96dpi). |

---

## 2. 설계 명세

### 2-1. 뷰포트 기하 계산 (순수 함수)

```
ComputeViewportGeometry(targetDisplay: Screen? ) -> ViewportGeometry
  - targetDisplay == null 또는 "primary" → Screen.PrimaryScreen.Bounds
  - 그 외 → 이름(\\.\DISPLAYn)으로 매칭된 Screen, 없으면 primary 폴백
  - Point00 = (Bounds.Left, Bounds.Top)
  - Point11 = (Bounds.Right, Bounds.Bottom)   // 통합 화면 좌표
```

- 기존 `(0,0)` 하드코딩 제거. `Point00`가 항상 선택된 디스플레이의 원점이 되도록 변경.
- 단일 디스플레이가 (0,0)에 있으면 기존 동작과 동일 → QHD 단독 환경은 회귀 없음.

### 2-2. 변경 감지

**부하 분석 (사전 확인):**
- `GetSystemMetrics`는 user32 경량 호출(마이크로초). `Screen.AllScreens`는 WinForms 내부 WM_DISPLAYCHANGE 기반 캐시를 반환해 재열거 비용이 거의 없음.
- GazeStick은 이미 16ms마다 Beam 데이터를 폴링하므로, 초당 1회 기하 체크는 비교상 무시 가능 수준. → 폴링 자체의 CPU 부담은 실측상 문제 아님.

**주 방식: 이벤트 + 폴백 (하이브리드)**
1. **이벤트(주):** 기존 `HotkeyWindow` WndProc에 `WM_DISPLAYCHANGE`(0x007E) 처리 추가. 모니터 구성/해상도 변경 시 Windows가 모든 윈도우에 이 메시지를 브로드캐스트하므로 가상 디스플레이(VibeShine/Sunshine 계열) 핫플러그에도 도달함. 아무것도 안 하는 시간에는 비용 0.
2. **폴백(안전망):** 10초 간격 폴링으로 이벤트 누락을 방어 (드라이버별 메시지 전달 차이, 원격 데스크톱 세션 등). 체크 내용은 `GetSystemMetrics(0/1)` + `Screen.AllScreens` 개수·Bounds.
3. **디바운스:** 이벤트 또는 폴백이 변경을 감지하면 500ms(마지막 변경 기준) 대기 후 1회만 갱신. 가상 디스플레이 전환 시퀀스에서 모니터 제거→추가가 연달아 오므로 필수.

**검증 포인트 (M1에서 확인):** VibeShine 전환 시 WM_DISPLAYCHANGE가 실제로 도달하는지 로그로 확인. 안 와도 폴백이 10초 내 감지하므로 동작은 보장, 응답만 느려지는 차이.

### 2-3. 갱신 경로

| 상황 | 동작 |
|---|---|
| Beam 연결 중 + 기하 변경 | `EW_BET_API_UpdateViewportGeometry(handle, newGeom)` 즉시 호출 (디바운스 후) |
| Beam 미연결 + 기하 변경 | 캐시만 갱신. 다음 `Start()` 시 새 기하로 API 생성 |
| 재연결(`ReconnectTimerTick` → `Start()`) | 항상 **현재** 계산된 기하로 API 재생성 (기존 코드도 Start마다 재계산하므로 자동 반영) |
| 선택한 디스플레이가 사라짐 | primary 폴백 + 트레이 배너 "디스플레이 변경됨 — primary로 전환" |
| 갱신 호출 실패 | `ErrorOccurred` → 기존 배너 경로, 캐시 유지 후 다음 폴링에서 재시도 |

### 2-4. 설정 (레거시 호환 필수)

`%AppData%\GazeStick\settings.json`에 새 키:

| 키 | 기본값 | 설명 |
|---|---|---|
| `TargetDisplay` | `""` (빈 = primary) | 모니터 장치 이름(`\\.\DISPLAY5` 등). 빈 값 또는 매칭 실패 시 primary. |

- 기존 설정 파일에 키가 없어도 정상 동작(빈 기본값).
- 팝업 패널에 디스플레이 선택 드롭다운 추가: `Screen.AllScreens`를 "이름 (WxH, primary)" 형태로 열거.

### 2-5. 벤더링 SDK 래퍼 확장

`Services/BeamEyeTrackerApi.cs`의 `API` 클래스에:

```csharp
[DllImport("beam_eye_tracker_client")]
private static extern void EW_BET_API_UpdateViewportGeometry(IntPtr apiHandle, ViewportGeometry newViewportGeometry);

public void UpdateViewportGeometry(ViewportGeometry geom) { ... ThrowIfDisposed(); ... }
```

- 공식 SDK 시그니처와 동일 (C# 래퍼 875줄 확인).

---

## 3. 파일 변경 목록

| 파일 | 변경 |
|---|---|
| `Services/BeamEyeTrackerApi.cs` | `UpdateViewportGeometry` P/Invoke + public 메서드 추가 |
| `Services/BeamTrackingService.cs` | 기하 계산 로직, 폴링 타이머(1s), 디바운스(500ms), 갱신 경로, 변경 시 이벤트(`ViewportChanged`) 노출 |
| `Models/AppSettings.cs` | `TargetDisplay` 키 (기본 `""`) |
| `Helpers/SettingsManager.cs` | 새 키 직렬화 (기존 파일 호환) |
| `UI/PopupPanel.cs` | 디스플레이 선택 드롭다운 + 변경 이벤트 |
| `UI/TrayApplicationContext.cs` | `TargetDisplay` 설정 연결, `ViewportChanged` → 트레이 텍스트/배너 |
| `tests/` | 기하 계산 단위 테스트 (primary, 오프셋 모니터, 사라진 모니터 폴백) |

---

## 4. 마일스톤

### M0 — SDK 래퍼 + 순수 함수
- **산출물:** `BeamEyeTrackerApi.cs`에 갱신 API 추가, `ComputeViewportGeometry` 단위 테스트 통과.
- **완료 기준:** `dotnet test` 그린. 기존 빌드 회귀 없음.

### M1 — 버그 A: 해상도 변경 갱신 (primary 기준)
- **산출물:** 폴링 + 디바운스 + 갱신 경로 구현, `TargetDisplay` 미지정 시 primary Bounds 사용.
- **완료 기준:** 실행 중 QHD → 가상 UHD 전환 시 스틱 영점이 새 해상도 기준으로 재대응. 로그에 갱신 이벤트 기록.

### M2 — 버그 B: 디스플레이 선택
- **산출물:** `TargetDisplay` 설정 + 팝업 드롭다운, 폴백 로직, 트레이 알림.
- **완료 기준:** 2디스플레이(물리+가상) 환경에서 각 디스플레이를 골라 영점 검증 가능. 사라진 모니터 선택 시 primary 자동 전환 + 배너.

### M3 — 마무리
- **산출물:** csproj 버전 패치 범프, 릴리스 노트(영문), `package.ps1`로 포터블 패키지 재생성.
- **완료 기준:** 커밋 → push → 태그 `v*`로 워크플로우 릴리스까지 (기존 사이클 관례).

---

## 5. 수동 검증 체크리스트 (M3 전 최종)

1. **단독 QHD, GazeStick 실행 중** VibeShine으로 물리 모니터 해제 + UHD 가상 생성 → 오버레이 중앙에서 게임패드 테스트: 스틱 영점이 중앙에 맞아야 함.
2. 같은 전환 후 좌/우 동일 거리 시선 이동 → 게임패드 테스트에서 좌/우 대칭 (오른쪽만 더 밀리는 현상 없음).
3. **물리 + 가상 2디스플레이** 동시 켜진 상태: 팝업에서 가상 디스플레이 선택 → 게임(가상) 화면 중앙 시선이 스틱 영점과 일치. 물리 선택 시 물리 화면 기준으로 일치.
4. 실행 중 해상도 다시 QHD로 복원 → 자동 갱신, 배너 1회 표시.
5. 설정 파일이 `TargetDisplay` 없는 구버전 상태에서도 정상 시작 (기본값 호환).
6. Beam 앱 재시동 후 재연결 시 새 기하가 적용됨.
7. 기존 설정(Deadzone/Sensitivity/Smoothing 등) 모두 회귀 없음, F9 토글·블링크 클램프 동작 유지.

---

*계획만 작성 — 사용자 승인 전 코딩 시작 금지.*
