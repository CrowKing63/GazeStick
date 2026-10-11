# GazeStick — 프리셋 기능 + 팝업 2열 컴팩트 리디자인 작업 지시서

프리셋(3개)으로 데드존/감도/스무딩/블링크 클램프/커브를 저장·선택하는 기능 추가, 그리고 세로로 길어진 팝업 패널을 조절기 항목만 2열로 재배치해 축소하는 리디자인.

**언어:** C# (.NET 8, WinForms — `UI/PopupPanel.cs`, `Models/AppSettings.cs`, `UI/TrayApplicationContext.cs`)

---

## 1. 범위

### 1-1. 프리셋 기능
- 조절 대상: **Deadzone, Sensitivity, Smoothing, BlinkClampThreshold, CurveType, CurvePower** (6개). InvertY·단축키·자동시작·타겟 디스플레이는 프리셋에 포함하지 않는다.
- 프리셋 3개. 기본 선택은 **첫 번째(Preset 1)**. 셋 다 초기값은 기존 기본값.
- 사용자가 어떤 프리셋이 선택된 상태에서 수치를 수정하면 **그 즉시 해당 프리셋에 저장**(실시간, 별도 저장 버튼 없음).
- 프리셋 선택 시 그 프리셋의 값들이 조절기로 즉시 반영된다.

### 1-2. 팝업 리디자인 (2열 컴팩트)
- 현재: 너비 392 × 높이 ~920(디자인 픽셀), 단일 열. 조절기 4개 + 커브 섹션이 세로로 쌓여 창이 길다.
- 목표: **조절기(NumericAdjuster) 항목만 2열 그리드로** 재배치, 하단 전체 너비 버튼(InvertY/자동시작/단축키 — 라벨이 길어 축소 불가)은 그대로 유지.
- 기대 효과: 높이 ~920 → **~760 (약 17% 감소)**. 너비는 392 유지.

두 작업은 같은 파일(`PopupPanel.cs`)을 건드리므로 한 번에 묶어 실행한다.

---

## 2. 데이터 모델 (AppSettings)

### 2-1. 신규 필드

```csharp
public sealed class Preset
{
    public double Deadzone { get; set; } = 0.10;
    public double Sensitivity { get; set; } = 2.0;
    public double Smoothing { get; set; } = 0.30;
    public double BlinkClampThreshold { get; set; } = 0.0;
    public CurveType Curve { get; set; } = CurveType.Linear;
    public double CurvePower { get; set; } = 2.0;
}
```

`AppSettings`에 추가:

| 필드 | 타입 | 기본값 | 비고 |
|---|---|---|---|
| `Presets` | `Preset[]` (길이 3) | 모두 기존 기본값 | JSON에 키가 없으면 생성자 초기값 유지 → 구버전 설정 파일 하위호환 자동 |
| `ActivePreset` | `int` | `0` | 0~2. 로드 시 `Math.Clamp(0, 2)` |

### 2-2. 저장 구조: "탑레벨 = 라이브 상태"
기존 `Deadzone` 등 탑레벨 필드를 **라이브 값**으로 그대로 둔다. `StickMapper.Map(gaze, settings)` 서명은 안 건드린다.

- **값 변경 이벤트 발생 시**: 기존처럼 탑레벨 필드 갱신 + **동시에 `Presets[ActivePreset]`의 해당 필드도 갱신**. 둘 다 같은 `AppSettings` 객체이므로 `SettingsManager.Save()` 호출은 1회로 유지.
- **프리셋 선택 시**: `Presets[i]` → 탑레벨 6필드로 복사, `ActivePreset = i`, 저장.

### 2-3. 마이그레이션 (기존 사용자 보호)
업데이트 전 설정 파일에 이미 커스텀 값(예: Deadzone=0.2)이 있으면, 새 빌드 첫 실행 시 그 값이 프리셋 기본값으로 사라지지 않도록:

- `SettingsManager.Load()`에서 deserialize **전에** 원본 JSON 문자열에 `"Presets"` 키가 있는지 확인.
- 키가 없는 파일(기존 사용자)이면 로드 후 **`Presets[0]`을 현재 탑레벨 6개 값으로 시드**. → "이전 설정 = Preset 1"이 됨.

### 2-4. 방어 로직
- `Presets`가 null/길이가 3 미만(손상된 파일)이면 기본 3개로 재생성, `ActivePreset`을 클램프. 접근 헬퍼 `GetActivePreset()` 한 곳으로 수렴.

---

## 3. UI — 프리셋 섹션 (PopupPanel)

### 3-1. 위치
**Target display 콤보 바로 아래**, Deadzone 조절기 위. ("타겟 디스플레이 다음" 안배.)

### 3-2. 레이아웃 (디자인 픽셀)
- 섹션 라벨 `PRESETS` (기존 `AddSectionLabel` 재사용, +22px).
- 버튼 3개 한 줄: **각 108 × 32**, x = 16 / 132 / 248 (간격 8). Xbox/DS4 버튼과 동일한 높이·색상 스키마.
- 선택된 버튼은 기존 `SetSelected`의 녹색 하이라이트(RGB 35,100,70 + 테두리 95,210,135) 재사용 → "선택됨"이 직관적으로 읽힘.
- 라벨: `Preset 1` / `Preset 2` / `Preset 3`. `AccessibleName = "Select preset N"`.

### 3-3. 이벤트
```csharp
public event Action<int>? PresetChanged;   // 선택 인덱스 0~2
```
클릭 시 `_presetIndex` 갱신 → `UpdateState()` → `PresetChanged?.Invoke(i)`. 값 반영(조절기 세팅)은 TrayApplicationContext가 핸들러에서 수행한다.

---

## 4. UI — 2열 컴팩트 레이아웃 (PopupPanel)

### 4-1. 그리드 규칙
- 콘텐츠 폭 340 → **2열: 각 164, 간격 12** (x=16 / x=192).
- 한 행 = 라벨(18px) + NumericAdjuster(폭 164), 행 높이 62 (기존과 동일).
- `AddAdjuster`를 `(int col, ref int y)` 시그니처로 확장: `col==0 → x=16`, `col==1 → x=192`.

### 4-2. 새 순서 (디자인 픽셀 y 누적)

| 블록 | y 시작 | y 증가 |
|---|---|---|
| 타이틀/상태/Beam/토글 버튼 | 14 | 128 (변경 없음) |
| `TRACKING` 라벨 | 128 | +22 → 150 |
| Target display (라벨+콤보) | 150 | +62 → 212 |
| **`PRESETS` 라벨** | 212 | +22 → 234 |
| **프리셋 버튼 3개 (1줄)** | 234 | +36 → 270 |
| 그리드 행 1: Deadzone \| Sensitivity | 270 | +62 → 332 |
| 그리드 행 2: Smoothing \| Blink clamp | 332 | +62 → 394 |
| 그리드 행 3: **Curve 버튼 \| Curve power** | 394 | +62 → 456 |
| `VIRTUAL CONTROLLER OUTPUT` 라벨 | 456 | +22 → 478 |
| Xbox/DS4 버튼 (기존 2열 유지) | 478 | +36 → 514 |
| DS4 힌트 라벨 | 514 | +26 → 540 |
| `QUICK SETTINGS` 라벨 | 540 | +22 → 562 |
| InvertY / 자동시작 / 단축키 (전체 너비 유지) | 562 | +36×3, 마지막 +40 → 674 |
| settings notice | 674 | +24 → 698 |
| Reset / Exit (기존 2열 유지) | 698 | h=32 → 종료 730 |

**폼 높이: 920 → ~760.** `CaptureDesignLayout`/`ApplyScale`은 Controls 전체를 자동 캡처하므로 DPI 스케일링 코드는 수정 불필요.

### 4-3. 세부 변경
- **Curve 섹션 라벨 삭제**: Curve 버튼(좌, 164×32)과 Curve power 조절기(우)가 그리드 행 3으로 통합 → "RESPONSE CURVE" 라벨(+22px)이 사라짐.
- **"Blink clamp (rec. 0.12)" → "Blink clamp"** 로 라벨 축소 (164px에 10pt 볼드로 안 감). 권장값은 힌트("Suppresses sudden downward gaze spikes (blinks). Recommended 0.12, 0 = off.")로 이동.
- Curve 버튼 텍스트 `"Curve: Logarithmic"`(가장 긴)이 164px에 10pt 볼드 기준 들어감(~144px).
- 하단 전체 너비 버튼 3개와 Reset/Exit는 라벨 길이 때문에 폭 축소 불가 → 그대로.

---

## 5. TrayApplicationContext 와이어링

`ShowPopup()`:
```csharp
_popup.PresetChanged += i => ApplyPreset(i);
```

- **ApplyPreset(i)**: `settings.ActivePreset = i`; `Presets[i]`의 6값 → 탑레벨 필드 복사; 팝업 조절기 값 세팅 (`DeadzoneValue` 등 기존 프로퍼티 재사용); `SettingsManager.Save`.
- **각 값 변경 핸들러**(Deadzone/Sensitivity/Smoothing/BlinkClamp/CurveType/CurvePower): 기존 로직에 한 줄 추가 — `settings.Presets[settings.ActivePreset].X = v;` (Save는 기존 1회 유지).
- **ShowPopup 초기화**: `_popup.PresetIndex = settings.ActivePreset` (신규 프로퍼티, 버튼 선택 상태만 세팅하고 이벤트 안 발화 — `SetTargetDisplay` 패턴과 동일).
- **ResetSettings()**: `Presets` 3개 전부 기본값으로, `ActivePreset = 0`, 팝업에 `PresetIndex = 0` 반영.

프리셋 선택은 트래킹 OFF 상태에서도 동작(설정 저장만이라 `_isActive` 게이트 없음).

---

## 6. 마일스톤

| 단계 | 내용 | 산출물 |
|---|---|---|
| **M0** | `Preset` 모델 + `AppSettings.Presets/ActivePreset` + 마이그레이션(2-3) + 방어 로직(2-4) | 설정 로드/저장 라운드트립 확인: ① 기존 형식 파일 → Preset 1에 구값 시드 ② 새 파일 → 기본값 ③ 손상된 파일 → 재생성. `tests/` 패턴에 맞춰 소형 콘솔 체크 추가(경량, 기존 테스트 프로젝트 스타일) |
| **M1** | PopupPanel 프리셋 섹션 (3-절): 버튼 3개, `PresetChanged`, 선택 상태 | Debug 빌드에서 3개 버튼 표시, Preset 1 기본 선택(녹색) |
| **M2** | TrayApplicationContext 와이어링 (5-절) + ResetSettings 확장 | 프리셋 간 값 분리/실시간 저장 동작 |
| **M3** | 2열 그리드 리디자인 (4-절): `AddAdjuster` col 파라미터, Curve 행 통합, 라벨 축소, 폼 높이 ~760 | 패널이 짧아지고 겹침 없음 |
| **M4** | Debug exe 빌드 → 사용자 시각 검증 게이트 (스킬 규칙) | 아래 체크리스트 통과 후 커밋 |

---

## 7. 수동 검증 체크리스트 (사용자 직접 실행)

1. 앱 실행 → 팝업 열기: `PRESETS` 섹션이 Target display 바로 아래에 버튼 3개, **Preset 1 녹색 선택** 상태.
2. Deadzone을 0.15로 변경 → 앱 종료 후 재실행 → 값이 Preset 1에 유지됨.
3. **Preset 2** 클릭 → 모든 값이 기본값으로 스냅. Sensitivity만 3.0으로 변경 → 재실행 후 Preset 2 선택 시 3.0 유지, **Preset 1은 0.15 그대로**.
4. Curve 버튼을 Exponential로 전환(프리셋 선택 중) → CurvePower 값이 그 프리셋에 저장됨. 다른 프리셋으로 갈 때 커브/파워가 따라감.
5. Reset settings → 3개 프리셋 모두 기본값, Preset 1 선택 상태로 복귀.
6. 레이아웃: 창이 이전보다 눈에 띄게 짧음(~17%). 2열 조절기 라벨/조절기가 겹치지 않음, "Blink clamp" 라벨 잘림 없음.
7. **DPI 검증**: UHD(250%) 모니터와 QHD 보조 모니터에서 각각 열었을 때 비율 유지, 겹침 없음.
8. **하위호환**: 업데이트 전 `settings.json` 백업 → 새 빌드 첫 실행 시 Preset 1이 이전 커스텀 값과 일치.

---

## 8. 범위 외 (이번 작업에서 안 함)
- InvertY/단축키/자동시작/타겟 디스플레이의 프리셋화.
- 프리셋 이름 변경(리네임), 추가, 삭제 UI — 고정 3개.
- OnboardingForm 변경.
