# GazeStick — 오픈소스 웹캠 시선 추적 백엔드 (폴백) 계획

Beam 구독·하드웨어가 없는 사용자를 위한 **오픈소스 웹캠 시선 추적**을 두 번째 `ITrackingService`로 추가하는 확장안. 기본값은 Beam 그대로 유지하며, 웹캠 백엔드는 설정으로 opt-in 하는 **저가 폴백**이다.

**언어:** C# (.NET 8, WinForms — `Services/`, `UI/`, `Models/`)
**상태:** **폐기 (M0 판정, 2026-10-30).** M0 스파이크에서 MobileOne S0를 실측한 결과, 게임 실사용 체감이 Beam 대비 부족(실사용 무리)해 폐기로 결정. 원인은 웹캠 전용(무 IR) 방식의 구조적 정확도 천장(~2–3°)이며, 다른 경량 모델(ProxylessGaze·WebEyeTrack)로도 이 갭을 못 닫음. throwaway 스파이크 브랜치는 삭제하고 마스터에 이식하지 않음. 본 문서는 조사·판정 기록으로 보존한다.

---

## 1. 포지셔닝 (이 문서의 전제)

- **대체가 아니라 폴백.** 웹캠 전용(무 IR, 무 하드웨어 캘리브) 방식의 실사용 정확도 상한은 **약 2–3°**다. Beam/IR은 1° 이내. 이 갭은 소프트웨어로 못 닫는다.
- 따라서 웹캠 백엔드는 "구독·하드웨어가 없는 환경에서 대략적 스틱 제어가 가능한 저가 옵션"으로 포지셔닝한다. "Beam 대체"라는 표현은 README·UI 어디에도 쓰지 않는다.
- **Go/No-Go는 M0 스파이크가 판정.** 프로토타입 정확도를 사용자가 Beam과 A/B로 직접 비교한 뒤 유지/폐기를 결정한다 (7절).

### 1-1. 왜 기존 MediaPipe 기하학적 방식(Playability 등)은 못 썼나
기존 웹캠 툴 대부분이 **iris-ratio(기하학적)** 방식이다 — 홍채가 눈 안 어느 쪽에 갔는지 2D 비율로 추정. 헤드 포즈·조명·거리 변화에 취약하고 사용자별 보정이 약하다. 사용자가 Playability에서 "빔 대비 메리트 없음"을 느낀 원인이 이 지점이다.

### 1-2. 진짜 업그레이드: 딥러닝 회귀 모델
기하학적 계산 대신 **딥 시선 회귀(ONNX) 모델**로 (x,y) 화면 좌표 또는 방향을 직접 예측하고, **사용자별 캘리브레이션(Ridge Regression 등)**으로 보정한다. 이 경로가 기하학 대비 갭을 좁혀 준다. C#에서 `Microsoft.ML.OnnxRuntime`으로 직접 돌리면 된다 — 구식 `Mediapipe.Net` 바인딩 의존 없이 ConditioningControlPanel이 증명한 견고한 경로다.

---

## 2. 후보 모델 분석 (C# 포트 가능 + 라이선스)

모두 **ONNX**를 내므로 C#에서 ONNX Runtime으로 직접 추론 가능.

| 후보 | 라이선스 | 형태 | 보고된 정확도 | C# 적합성 |
|---|---|---|---|---|
| **yakhyo MobileGaze / MobileOne S0** | **MIT** ✅ | ONNX ~4.8MB, 사전학습, 실시간 | Gaze360 MAE ~12.5°(비교정 벤치마크) → 실사용+캘리브로 대폭 개선 | **높음** — ONNX Runtime 직접, 바인딩 불필요 |
| **WebEyeTrack (BlazeGaze)** | **MIT** ✅ | 경량 CNN, few-shot 개인화 | MacGaze 검증: 4점 캘리브 시 평균 8.1% | 중 — 모델 MIT이나 TF/JS 기반이라 ONNX 변환 필요 |
| **ProxylessGaze (MIT-Han-Lab)** | **MIT** ✅ | ONNX, CPU ~80FPS | "satisfactory" (ETH-XGaze 학습) | 높음 — ONNX Runtime |
| UnitEye (참고용) | GPL 3.0 ⚠️ | Unity/Barracuda | ~2.4° RMSE (C920, 캘리브 포함) | **부적합** — copyleft + Unity 전용 |
| UniGaze (참고용) | 비영리 ⚠️ | PyTorch SOTA | WACV 2026 SOTA | **부적합** — 라이선스 + 무거움 |

**배제 근거:** GPL/AGPL(copyleft)은 MIT인 GazeStick에 섞이면 오염된다. 비영리(UniGaze)는 공개 배포 불가.

### 2-1. 1차 프로토타입 모델 선정
**yakhyo MobileOne S0**를 1차 후보로 정한다 — MIT, 사전학습 ONNX 번들, 실시간, 바인딩 불필요. 대안은 ProxylessGaze / WebEyeTrack. 최종 선택은 M0 스파이크에서 실제 정확도로 판정한다.

> **NOTE:** 모델 파일(온닉스 바이너리)은 레포에 커밋하지 않고, CI/패킹 시 다운로드·번들하거나 NuGet으로 관리한다. MIT 라이선스이므로 ATTRIBUTION(출처 표기)만 유지하면 된다.

---

## 3. 아키텍처 — `WebcamTrackingService`

기존 추상화(`ITrackingService`)에 두 번째 구현을 추가한다. 출력은 **해상도 무관 정규화 좌표**(`GazePoint`)이므로 `StickMapper`/`VirtualPad`는 그대로 재사용된다.

```
[웹캠 캡처]
   ↓ 프레임
[얼굴/눈 크롭]      — MediaPipe FaceMesh(기존 참조) 또는 RetinaFace/YOLO ONNX (M0에서 택1)
   ↓ crop
[딥 시선 ONNX 모델]  — MobileOne S0 등, (x,y) 화면 좌표 또는 방향 예측
   ↓
[사용자별 캘리브레이션] — Ridge Regression: N개 샘플 → 정규화 화면 좌표 보정
   ↓
GazePoint (0.0~1.0)  → 기존 StickMapper → VirtualPad (변경 없음)
```

`WebcamTrackingService : ITrackingService`:
- `Start()`/`Stop()`: 캡처 세션 + 추론 루프 시작/종료.
- `GazeUpdated(GazePoint)`: 프레임당 정규화 시선 좌표 발화.
- `ConnectionStateChanged(...)`: "웹캠 준비됨 / 얼굴 감지 실패 / 캘리브 필요" 상태 노출 (Beam의 연결 이벤트와 동일한 계약).
- 스레딩·라이프사이클은 `BeamTrackingService` 패턴에 맞춘다.

---

## 4. 백엔드 선택 & 리소스 관리 (이중 지출 방지)

현재 오케스트레이터(`TrayApplicationContext`)는 트래커를 **하나만** 인스턴스화한다 (`_tracker = new BeamTrackingService()`). 이 구조를 유지하면 "두 백엔드가 동시에 살아 리소스를 이중으로 쓰는" 상태가 존재하지 않는다.

- **설정 `trackingBackend`**: `beam`(기본) / `webcam`.
- 앱 시작 시 선택된 백엔드 **하나만** 생성·`Start()`. 안 쓰는 백엔드는 인스턴스 자체가 없으므로 카메라/추론 리소스를 전혀 쓰지 않는다.
- **F9(입력 토글)**: 활성/비활성만 제어, 백엔드를 죽이지 않음 (기존 동작 유지).
- **백엔드 완전 해제**: 웹캠 선택 상태에서 `Stop()` 시 캡처 세션 + ONNX Runtime이 완전히 종료되어 리소스 0.

> 즉 Beam 사용자는 아무 설정도 안 건드리면 기존과 동일(웹캠 리소스 0), 웹캠 사용자는 Beam 앱이 없어도 동작한다. "이중 지출"은 구조적으로 불가능하다.

---

## 5. 캘리브레이션 UX (신규 비용)

기존 MVP에 없던 것. 모든正经한 웹캠 트래커에서 **사용자별 캘리브레이션**이 필수다. GazeStick은 현재 캘리브 흐름이 없어 새로 추가해야 한다.

### 5-1. 방식
**Ridge Regression**: 사용자가 N개 지점(권장 5–9점)을 순서대로 바라보며 각 시점의 (모델 출력, 목표 화면 좌표) 쌍을 수집 → 정규화 화면 좌표로 매핑하는 회귀 계수를 학습한다. C#에서 구현이 간단하다.

### 5-2. 흐름
1. 팝업에서 "캘리브레이션" 시작 → 안내(지점 순서).
2. 각 지점: 카운트다운 후 샘플 캡처.
3. 완료 → 계수 저장, `GazePoint` 보정 적용.
4. **재캘리브 트리거**: 카메라 위치/거리/조명이 크게 바뀌면 정확도 저하 → 툴팁으로 재캘리브 권고.

### 5-3. 저장
캘리브 계수는 `%AppData%\GazeStick\calibration.json`에 저장 (사용자별, 한 번 세팅). 웹캠 백엔드 선택 시 로드.

> **NOTE:** 캘리브 UX는 UI 변경이므로 Debug exe에서 사용자가 직접 보고 나서 커밋한다 (스킬 규칙).

---

## 6. 설정 파일 확장

```json
{
  "trackingBackend": "beam",
  "calibrationPath": "%AppData%/GazeStick/calibration.json"
}
```

| 키 | 기본값 | 설명 |
|---|---|---|
| `trackingBackend` | `"beam"` | `beam`(기본) / `webcam`. 구버전 파일(키 없음)은 `beam` → 하위호환 자동. |

기존 6개 프리셋·데드존·감도 등은 웹캠 백엔드와 **공유**한다 (StickMapper 계약이 동일하므로).

---

## 7. Go/No-Go 판정 & 폐기 기준 (M0 스파이크)

**M0 스파이크**가 전체 확정의 관문이다. 이 단계에서 throwaway 디버그 콘솔로 엔드투엔드 파이프라인을 돌리고, 사용자가 Beam과 A/B 비교한다.

### 7-1. 스파이크 산출물
- **스탠드얼론 시각 테스트 하네스** (온라인 게임패드 테스터 느낌): 웹캠 피드를 표시하고, 예측 시선 점을 실시간으로 점(dot)으로 겹쳐 그리는 작은 throwaway WinForms 창.
- ViGEm·StickMapper·설정 저장·백엔드 선택 등 GazeStick 통합 **전부 생략**. 정확도 체감만 목적.
- (선택) 빠른 캘리브 버튼: N점 샘플로 보정 전/후 차이 확인. 첫 확인은 무캘리브 원본 출력으로 충분.

### 7-2. A/B 테스트 (사용자 직접)
같은 게임, 같은 자세·조명에서 Beam과 웹캠 백엔드를 번갈아 사용.

### 7-3. 폐기 기준
- **카메라 회전(3인칭) 체감이 Beam의 ~70% 이상** → 폴백으로 가치 있음, M1 이후로 진행.
- **조준/정밀 스틱에서 "Beam이 아니면 안 된다"가 느껴짐** → **폐기**. 웹캠은 저가 폴백 포지션이라 이线下을 못 넘으면 의미 없음.
- 폐기 시 M0 throwaway 코드만 브랜치에 남기고 마스터에는 이식하지 않는다.

---

## 8. NuGet & 의존성

| 패키지 | 용도 |
|---|---|
| `Microsoft.ML.OnnxRuntime` | 딥 시선 모델 추론 (마이크로소프트 유지) |
| `OpenCvSharp` (또는 Windows.Media.Capture) | 웹캠 캡처. M0에서 둘 중 택1 (ConditioningControlPanel은 OpenCvSharp 증명) |
| 시선 ONNX 모델 파일 | yakhyo MobileOne S0 등 (MIT, 번들/다운로드 — 레포 커밋 금지) |

> **NOTE:** 카메라 캡처 방식(OpenCvSharp vs Windows.Media.Capture)과 얼굴 크롭 방식(MediaPipe FaceMesh vs RetinaFace/YOLO ONNX)은 M0 스파이크에서 실제 성능·설립 비용으로 택1한다. 이 문서에선 둘 다 후보로 둔다.

---

## 9. 마일스톤

| 단계 | 내용 | 산출물 |
|---|---|---|
| **M0 (스파이크, Go/No-Go)** | 스탠드얼론 시각 테스트 하네스: 웹캠→크롭→ONNX→(선택 캘리브)→화면 위 시선 점 오버레이. 1차 모델(MobileOne S0) 실측 | throwaway 창 + A/B 체감 결과. **여기서 유지/폐기 판정** (7절) |
| **M1** | `WebcamTrackingService : ITrackingService` 정식 구현 (스레딩, 이벤트, 라이프사이클 — Beam 패턴 일치) | 웹캠 백엔드가 `GazeUpdated` 발화, StickMapper로 스틱 출력 확인 |
| **M2** | `trackingBackend` 설정 + 오케스트레이터 와이어링 (하나만 인스턴스, 기본 beam, opt-in webcam) + 리소스 완전 해제 | Beam 사용자는 기존과 동일(웹캠 리소스 0), 웹캠 선택 시 Beam 불필요 동작 |
| **M3** | 캘리브레이션 UX (5절): 안내 폼, 샘플 수집, 계수 저장/로드, 재캘리브 권고 | 한 번 세팅 후 재시작해도 캘리브 유지, UI는 사용자 시각 검증 통과 |
| **M4** | Debug exe 빌드 → 사용자 A/B 최종 검증 게이트 (스킬 규칙) | 10절 체크리스트 통과 후 마스터 이식 검토 |

---

## 10. 수동 검증 체크리스트 (사용자 직접 실행)

1. 기본 상태(설정 안 건드리기): Beam 연결 시 기존과 **완전히 동일**하게 동작, 웹캠 리소스 미사용 (작업 관리자에서 카메라/ONNX 프로세스 없음).
2. `trackingBackend=webcam` 전환 → 캘리브레이션 흐름 실행(5–9점) → 완료 후 스틱이 시선 방향으로 기울어짐.
3. 캘리브 후 앱 재시작 → 캘리브 계수 자동 로드, 다시 세팅 불필요.
4. 카메라 위치 크게 변경 → 툴팁에 재캘리브 권고 표시.
5. **A/B**: 같은 3인칭 회전에서 Beam 대비 체감 ~70% 이상이면 폴백으로 가치 인정.
6. **정밀 조준**에서 "Beam이 아니면 안 된다"가 느껴지면 → 폐기 (M4 미통과).
7. 웹캠 백엔드 `Stop()` 시 캡처 세션 + ONNX Runtime 완전 종료 (리소스 0 확인).
8. F9 토글은 입력만 제어하고 백엔드를 죽이지 않음 (기존 동작 유지).
9. README·About에 "웹캠 = 저가 폴백, Beam 대체 아님" + 데이터 처리 안내(로컬 처리, 원격 로깅 없음) 명시.

---

## 11. 범위 외 (이번 작업에서 안 함)
- 헤드 트래킹 모드, 감도 커브, 게임별 프로필 — 기존 향후 확장 항목 그대로.
- 웹캠 전용 IR 하드웨어 지원 (이 계획은 무 IR 웹캠만).
- Tobii Game Integration 백엔드 — 별도 계획.

---

*GazeStick 웹캠 시선 추적 백엔드(폴백) 계획 — 이 문서를 기준으로 M0 스파이크부터 시작한다. Go/No-Go는 7절 판정 기준에 따라 사용자 A/B 테스트로 결정한다.*
