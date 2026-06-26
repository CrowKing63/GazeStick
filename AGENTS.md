# GazeStick — Project Brief

## 한 줄 요약

웹캠 기반 시선 추적(Beam Eye Tracker SDK)을 가상 게임패드의 오른쪽 스틱 입력으로 변환하는 Windows 접근성 유틸리티.

---

## 목적

시선 추적 전용 하드웨어(Tobii 등) 없이, 웹캠만으로 게임패드 오른쪽 스틱을 대체할 수 있도록 한다. 3인칭 시점 카메라 조작 및 컨트롤러 기반 게임 전반에서 범용적으로 사용 가능한 독립 툴을 목표로 한다.

---

## 기술 스택

- **언어**: C#
- **시선 데이터**: [Beam Eye Tracker SDK](https://docs.beam.eyeware.tech/) (C# 바인딩)
  - 전제 조건: Beam Eye Tracker 앱이 설치 및 실행 중이어야 함
- **가상 게임패드 출력**: [ViGEmBus](https://github.com/nefarius/ViGEmBus) + ViGEm.NET 클라이언트
  - 가상 Xbox 360 컨트롤러의 오른쪽 스틱(RX/RY 축)으로 출력
- **UI**: 시스템 트레이 상주형 (WinForms 또는 WPF 경량 구성)

---

## 최소 기능 요건 (MVP)

1. **시선 → 스틱 변환**
   
   - Beam SDK로 시선 X/Y 좌표(정규화된 0.0~1.0 범위) 수신
   - 화면 중앙을 스틱 중립점(0,0)으로 설정
   - 중립점 대비 편차를 스틱 축 값(-1.0~1.0)으로 변환
   - ViGEm으로 가상 Xbox 컨트롤러 오른쪽 스틱에 출력

2. **데드존**
   
   - 중앙 일정 범위 내 시선은 스틱 입력 없음으로 처리
   - 데드존 반경 조절 가능

3. **감도 조절**
   
   - 시선 편차 → 스틱 값 변환 배율 조절 가능

4. **On/Off 토글**
   
   - 단축키 또는 트레이 메뉴로 입력 활성/비활성 전환
   - 비활성 시 스틱 중립(0,0) 유지

5. **트레이 아이콘**
   
   - 활성 상태 표시
   - 설정 창 열기
   - 종료

---

## 설정 항목 (초기)

| 항목            | 설명               | 기본값 |
| ------------- | ---------------- | --- |
| Deadzone      | 중립점 반경 (0.0~0.5) | 0.1 |
| Sensitivity   | 스틱 변환 배율         | 1.0 |
| Smoothing     | 이전 프레임과의 보간 강도   | 0.3 |
| Toggle hotkey | 활성/비활성 단축키       | F9  |

설정은 JSON 또는 INI 파일로 저장/로드.

---

## 아키텍처 개요

```
[Beam App (서버)] 
       ↓ SDK (로컬 소켓)
[GazeStick]
  ├─ TrackingService   — Beam SDK 폴링, 시선 좌표 수신
  ├─ StickMapper       — 좌표 → 스틱 값 변환 (데드존, 감도, 스무딩)
  ├─ VirtualPad        — ViGEm 가상 Xbox 컨트롤러 관리
  └─ TrayApp           — UI, 설정, 토글
```

---

## 확장성 고려

- **왼쪽 스틱 지원**: 추후 이동 입력 대체 옵션 추가 가능
- **헤드 트래킹 모드**: Beam SDK는 헤드 포즈도 제공하므로 시선 대신 고개 방향으로 스틱 제어하는 모드 추가 가능
- **커브 설정**: 선형 외 지수/로그 감도 커브 옵션
- **프로필**: 게임별 감도/데드존 프로필 저장
- **다른 아이트래커 지원**: 추후 TGI(Tobii Game Integration) 백엔드 플러그인 구조로 확장 가능

---

## 배포 방침

- 개발자는 한국인이고 한국어 사용자이지만 GitHub에 공개할 모든 소스코드 주석 readme 문서 등은 영어로 작성 
- GitHub 공개 레포 (MIT)
- ViGEmBus 드라이버 설치 안내 포함

## 2. Licensing & Redistribution Constraints (CRITICAL)
When writing setup scripts, GitHub action workflows, README templates, or application "About" dialogs, the agent **MUST** adhere to the following Beam Eye Tracker SDK licensing terms:

### A. DLL Redistribution Allowed
- The dynamic library `beam_eye_tracker_client.dll` **can and should** be packaged and distributed alongside the application binaries (e.g., in GitHub Releases or installer outputs). 
- Do not exclude this DLL from the release pipeline.

### B. Prerequisites to Mention in README / UI
- The application requires the end-user to have the **official Beam Eye Tracker application installed and activated (with a valid subscription/license)** on their PC. The SDK library functions as a client that communicates with the local Beam server.

### C. Mandatory Disclaimers (Must be embedded in README.md & App "About" Section)
Whenever the agent updates or generates the project's documentation, user manuals, or GitHub templates, it **must explicitly include** the following safety and legal disclaimers:
1. **Non-Medical Device Disclaimer:** "This software and the underlying Beam Eye Tracker SDK are not medical devices. They are not intended, nor should they be used, to replace professional medical advice, diagnosis, or treatment."
2. **High-Risk Use Prohibition:** "This software must not be used in high-risk environments or safety-critical applications where any software malfunction or interruption could lead to personal injury, loss of life, or physical/environmental damage."
3. **Data & Gaze Notice:** Clearly state to the user how eye-tracking/gaze data is handled (processed locally for controller mapping, no unauthorized remote logging).

## 3. Implementation Directives for the Agent
- Always verify that building or packaging scripts copy `beam_eye_tracker_client.dll` to the executable output directory.
- Ensure any user interface design retains a clean, accessible layout, staying highly performance-optimized and lightweight.
- If asked to generate a deployment template or README, automatically incorporate the license prerequisites and medical disclaimers specified above without requiring further reminders.

## 4. GitHub Actions Release Workflow

The release workflow is at `.github/workflows/release.yml`. It is triggered by pushing a tag matching `v*`.

### Required GitHub Secret

The workflow needs `BEAM_SDK_DLL_URL` secret set in the GitHub repository. This URL should point to a direct download of `beam_eye_tracker_client.dll` from the Beam SDK package (typically from Eyeware's CDN after accepting the license on docs.beam.eyeware.tech).

To obtain the URL:
1. Download the Beam SDK zip from https://docs.beam.eyeware.tech/
2. Extract `beam-sdk/bin/win64/beam_eye_tracker_client.dll` locally
3. Upload it to a private location (e.g., a private GitHub Release, or a cloud storage with a direct link)
4. Set the direct download URL as `BEAM_SDK_DLL_URL` in GitHub repo Settings > Secrets and variables > Actions

### Creating a Release

```powershell
# Tag and push
git tag v1.0.0
git push origin v1.0.0
```

The workflow will build, package, and create a GitHub Release with the zip automatically.