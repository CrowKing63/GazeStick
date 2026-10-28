# Microsoft Store 제출 가이드 (Windows 데스크톱 앱)

이 문서는 Win32/WPF 데스크톱 앱을 **Microsoft Store**에 올리는 전체 과정과, AltKey 1.1.12 제출에서 실제로 부딪혀 해결한 함정들을 정리한 것이다. GazeStick 등 다른 프로젝트에도 그대로 적용한다.

---

## 0. 전제 조건

- **Microsoft Store 개발자 계정**이 등록되어 있어야 한다(개인/기업). 등록 시 계정마다 **Publisher ID(GUID)**가 배정된다 — 이 값이 아래에서 핵심이다.
- 앱은 **`.msix` 패키지**로 만들어져야 한다. Win32/WPF 데스크톱 앱도 MSIX로 포장할 수 있다(`runFullTrust` capability 사용).
- 소스가 GitHub에 있고, **CI가 같은 패키지를 재현**할 수 있게 되어 있으면 유지보수가 편하다(AltKey는 `scripts/package-msix.ps1` + `.github/workflows/release.yml`).

---

## 1. 전체 흐름 (High-level)

```
[소스 준비] → [MSIX 패키지 생성+서명] → [스토어 목록 자산 준비] → [Partner Center에 제품 등록+업로드] → [심사] → [발매]
```

각 단계를 아래에서 상세히 한다. **4번 "함정 정리"가 가장 중요** — 여기서 시간을 가장 많이 잃는다.

---

## 2. 단계별 상세

### Step 1 — MSIX 패키지 만들기

#### 1-1. `AppxManifest.xml` 작성 (이게 제일 중요)
MSIX의 매니페스트는 스토어 유효성 검사의 대부분을 좌우한다. 필수 항목:

| 항목 | 값 | 비고 |
|---|---|---|
| `Identity Name` | `<Publisher접두사>.<앱명>` | 예: `CrowKing63.AltKey`. 접두사는 계정에서 유도됨 |
| `Identity Publisher` | **`CN=<GUID>`** | ⚠️ 계정에 등록된 Publisher ID. `CN=CrowKing63` 같은 사람이 알아본 이름이 **아님** (함정 #1) |
| `Identity Version` | `x.y.z.0` | 앱 버전, 예: `1.1.12.0` |
| `TargetDeviceFamily` | `Name="Windows.Desktop" MinVersion="10.0.17763.0"` | ⚠️ `MinVersion`은 **10.0.17134.0보다 커야** 함 (함정 #2) |
| `Capabilities` | `runFullTrust` 등 | 데스크톱 앱은 `runFullTrust` 필수 (함정 #5) |

매니페스트 소스는 리포지토리에 **원본으로 보관**한다(AltKey: `msix/AppxManifest.xml`). CI가 이걸로 패키지를 만든다.

#### 1-2. 패키지 팩킹
- `MakeAppx.exe pack` (Windows App Certification Kit)로 `.msix` 생성.
- AltKey에는 `scripts/package-msix.ps1`이 이걸 자동화한다.
- **버전 치환 regex 함정** (함정 #3): 매니페스트의 `Version="..."`을 앱 버전으로 바꿀 때, regex가 `MinVersion=`/`MaxVersion=`까지 같이 바꿔버리면 안 됨. `Identity`의 `Version=` **만** 정확히 타겟팅해야 한다.

#### 1-3. 서명
- `.msix`는 서명된 상태여야 한다. 개발자 인증서로 서명한다(AltKey: scratch에 `sign-msix*.ps1`).
- 스토어 제출용으로는 **제출 시 Partner Center가 다시 서명**하는 방식도 있으나, 로컬에서 유효성 검사를 통과하게 하려면 자체 서명이 필요하다.

#### 1-4. CI 재현 확인
- `.github/workflows/release.yml`이 같은 매니페스트/스クリプト로 패키지를 만들어 **GitHub Release에 `.msix`를 첨부**하게 해두면, 로컬과 스토어 제출용 패키지가 항상 일치한다.

---

### Step 2 — 스토어 목록 자산 준비

| 자산 | 요구사항 | 비고 |
|---|---|---|
| **앱 아이콘** | 300×300 PNG, **투명 배경** | 기존 고해상도 아이콘을 `HighQualityBicubic`으로 다운샘플링. 모서리 알파=0 확인 |
| **스토어 스크린샷** | 여러 장 (마케팅용) | 앱 실행 화면 캡처 |
| **짧은 설명** | ~35자 안쪽 | 타일/검색 노출용 한 줄 |
| **긴 설명** | 4000자 이내 | 기능·특징. 사실에 기반 (개인정보 없음 등) |
| **개인 정보 취급 방침** | **필수** — 텍스트 또는 URL | ⚠️ "아니오"로만 해결 안 됨 (함정 #4) |
| **법주 + 하위 법주** | **필수** | ⚠️ 하위 법주 비어 있으면 저장 조용히 롤백 (함정 #6) |
| **runFullTrust 근거** | **필수** 텍스트 | capability가 왜 필요한지 구체적으로 (함정 #5) |

---

### Step 3 — Partner Center 제출

1. **제품 등록**: 앱 이름, 법주/하위 법주 선택.
2. **속성(Properties)**:
   - 개인 정보 취급 방침 (텍스트/URL 제공).
   - Support info (웹사이트 등, 옵션).
   - 디스플레이 모드, 제품 선언(체크박스), 시스템 요구 사항(대부분 옵션).
3. **패키지 업로드**: `.msix` 파일 업로드.
4. **Microsoft Store 목록**: 이름, 설명, 아이콘, 스크린샷.
5. **연령별 등급** 입력.
6. **제출(Submit)**: 심사 요청.

> ⚠️ **저장 롤백 주의**: 필수 칸이 비어 있으면 빨간 오류 없이 **폼 전체가 조용히 롤백**된다. "저장했는데 안 바뀌었다"면 반드시 **비어 있는 필수(*) 칸**을 먼저 찾으라. (함정 #6)

---

### Step 4 — 심사

- `runFullTrust`는 **제한된 기능**이라 승인 절차가 필요하지만, 이건 정상적이다(차단 요소 아님).
- Microsoft 심사에 들어감. 통과하면 자동으로 capability 부여 + 발매.

---

## 3. 함정 정리 (가장 중요 — 시간 대부분 여기서 잃음)

| # | 함정 | 증상 | 해결 |
|---|---|---|---|
| **1** | **Publisher가 사람 이름** | "잘못된 패키지 게시자 이름: CN=CrowKing63 (필요한 이름: CN=<GUID>)" + "패밀리 이름" 불일치 | 매니페스트 `Publisher`를 **계정 GUID**(`CN=4359A7FD-...`)로 변경. 파트너 센터 계정 정보에서 GUID 확인 |
| **2** | **MinVersion이 너무 낮음** | "Windows MinVersion <= 10.0.17134.0을 대상으로 하는 패키지를 업로드할 수 없습니다" | `TargetDeviceFamily MinVersion`을 **10.0.17763.0** 이상으로 |
| **3** | **버전 regex가 Min/MaxVersion까지 깨뜨림** | 팩킹 후 매니페스트의 `MinVersion`이 앱 버전(1.1.12.0)으로 변해 #2 오류 발생 | 치환 regex를 `Identity`의 ` Version="` **앞공백 앵커**로 제한해 `MinVersion`/`MaxVersion`은 건드리지 않게 |
| **4** | **개인정보 방침 "아니오"가 안 통함** | "개인정보 미사용" 선택+저장해도 다시 URL 제공 상태로 롤백 | 선언한 기능(runFullTrust 등) 때문에 **방침이 강제**됨. `텍스트 제공`으로 바꿔 사실에 맞는 짧은 방침 텍스트를 입력 (호스팅 불필요) |
| **5** | **runFullTrust 근거 미입력** | 제출 시 "runFullTrust가 필요한 이유" 필수 질문 | 구체적 API(`SendInput`, 전역 훅, 백그라운드/트레이, 전역 단축키) + "핵심 기능에 필수"를 명시 |
| **6** | **하위 법주 비어 있으면 조용히 롤백** | 저장했는데 상태가 안 바뀌고, 이전으로 복귀 | **비어 있는 필수(*) 칸**(특히 하위 법주)을 채운 뒤 저장. 빨간 오류가 없어도 롤백의 원인 |

---

## 4. AltKey에서 재사용할 아티팩트

- `msix/AppxManifest.xml` — 스토어용 매니페스트 원본 (Publisher GUID, MinVersion=17763 반영됨)
- `scripts/package-msix.ps1` — 팩킹+버전 치환 자동화 (regex 수정 포함)
- `.github/workflows/release.yml` — CI가 같은 `.msix`를 만들어 GitHub Release에 첨부
- `msix/Assets/` — MSIX 로고 (`Square150x150Logo.png`, `Square44x44Logo.png`, `StoreLogo.png`)
- 스토어 아이콘 300×300 (투명) — 기존 고해상도 아이콘 다운샘플링

---

## 5. GazeStick에 적용할 때 체크리스트

GazeStick을 Store에 올릴 때 아래만 다시 하면 된다:

- [ ] `AppxManifest.xml` 생성:
  - [ ] `Identity Name` = `<Publisher접두사>.GazeStick`
  - [ ] `Identity Publisher` = **계정 GUID** (`CN=<GUID>`, 사람 이름 아님) — AltKey와 같은 계정이면 같은 GUID
  - [ ] `Identity Version` = GazeStick 현재 버전
  - [ ] `TargetDeviceFamily MinVersion` = **10.0.17763.0**
  - [ ] `runFullTrust` capability 포함 (GazeStick도 카메라/전역 입력 등 full-trust 필요)
- [ ] `package-msix.ps1` 같은 팩킹 스크립트 (버전 regex 함정 #3 주의)
- [ ] 300×300 **투명** 아이콘 (GazeStick 고해상도 아이콘 다운샘플링)
- [ ] 스토어 스크린샷 + 짧은/긴 설명
- [ ] 개인 정보 취급 방침 **텍스트** (카메라 사용 여부 등 사실에 기반 — GazeStick은 웹캠을 쓰므로 "카메라 영상은 로컬에서만 처리" 명시 필요)
- [ ] runFullTrust 근거 텍스트
- [ ] 법주 + **하위 법주** 모두 채움 (롤백 방지)
- [ ] Partner Center에 제품 등록 → 패키지 업로드 → 목록 → 등급 → 제출

> **GazeStick 특이사항**: 웹캠(눈 추적)을 사용하므로 개인정보 방침에 "카메라 영상은 기기 안에서만 처리되며 외부로 전송되지 않는다"는 문구를 반드시 넣어야 한다. AltKey와 달리 데이터 관련 설명이 달라진다.

---

## 6. 한 줄 요약

> **Publisher=계정 GUID, MinVersion=17763, 버전 regex는 Identity만, 개인정보 방침은 텍스트로, 하위 법주는 꼭 채우고, runFullTrust 근거는 구체적으로.** 이 여섯 가지만 지키면 Store 제출은 통한다.
