# LionIceRun

폴가이즈 느낌의 솔로 3D 서바이벌 게임. 복셀 동물 캐릭터가 펭귄 얼음깨기(폴가이즈 Thin Ice 바탕) 맵 위에서 얼마나 오래 버티는지 겨룬다. 바다에 떨어지면 끝나고 버틴 시간이 기록(최고 기록은 PlayerPrefs)이 된다. 시련은 버틴 시간에 따라 늘어난다.
전체 계획과 진행 상황은 `blueprint.md`가 기준이다.

## 작업 규칙

- 사용자와의 대화, 문서, 코드 주석은 한국어로 쓴다. 코드 식별자는 영어.
- 할 일을 끝내거나 규칙이 바뀌면 **`blueprint.md`와 `blueprint.html`을 같이 고친다**. HTML은 고친 뒤 Artifact로 다시 게시한다 (기존 주소: https://claude.ai/artifact/365Pc5zVb3RJoA5kd9QTbp, 새 대화에서는 `url`로 지정해서 게시).
  - HTML 할 일 목록은 `<script>`의 `STAGES` 배열이다. 완료 항목은 `{ text: "...", done: true }`로 적는다.
  - 사용자가 직접 확인한 항목만 체크한다. 코드만 작성하고 테스트 전이면 체크하지 않는다.
- 시련(방해 요소)은 후순위. 핵심 시스템(blueprint 1~10단계)을 먼저 만든다.
- git: 이 PC에는 git CLI가 없다. 사용자가 직접 커밋하므로, 요청하면 **마지막 커밋 이후 바뀐 파일 목록 + 커밋 제목/설명**을 정리해 준다. `.git/objects`를 직접 읽어 비교할 수 있다. 기존 `.gitignore`, `.gitattributes`는 덮어쓰지 않는다.

## 개발 환경

- Unity 6000.6.3f1, Universal 3D(URP), 새 Input System만 사용 (`Input.GetKey` 금지, `InputSystem.actions`의 `Player/Move`, `Player/Jump` 사용)
- Blender 5.2: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`
- Unity 프로젝트: `LionIceRun/`, 메인 씬 `Assets/Scenes/Main.unity`

## Unity 작업 방식

- Unity 에디터가 열려 있는 상태에서 씬·프리팹 YAML을 직접 고치지 않는다. 대신 **에디터 메뉴 스크립트**(`Assets/Scripts/Editor/StepN*.cs`, 메뉴 `LionIceRun > ...`)를 만들고 사용자가 실행한다. 메뉴는 여러 번 실행해도 안전하게(기존 것 재사용) 만든다.
- 이미 씬에 있는 컴포넌트는 예전 값이 저장되어 있어서, 코드 기본값을 바꿔도 반영되지 않는다. 기본값을 바꾸면 사용자에게 Inspector에서 바꿀 값을 표로 알려준다. 필드 이름을 바꾸면 새 기본값이 들어간다.
- 기존 재질의 셰이더를 바꿀 때는 새 재질 값으로 초기화한다 (`EditorUtility.CopySerialized`). 예전 셰이더에 저장된 같은 이름의 값(예: `_Glossiness` = 0)이 남아 새 셰이더에서 엉뚱하게 쓰인다. 직접 만드는 셰이더 속성 이름은 URP Lit과 겹치지 않게 짓는다.
- 컴파일/실행은 직접 못 한다. 결과는 사용자 스크린샷이나 Console 오류로 확인한다.

## 주요 코드 (`LionIceRun/Assets/Scripts/`)

| 파일 | 역할 |
|---|---|
| `IceGrid.cs` | 가로x세로 얼음 그리드 생성(기본 6x6). 블럭 윗면 y=0. `CellTop(cell)`, `StartCell`, `Blocks`, 바다 위치(`seaDepth` 12m) |
| `QuarterViewCamera.cs` | 시작 전에는 그리드 전체가 보이는 거리, 게임 시작 시 `playDistance`로 부드럽게 줌인하며 플레이어를 따라감 (높이는 안 따라감) |
| `PlayerController.cs` | Rigidbody 이동. 상태 `Normal / Diving / KnockedDown`, `KnockDown(초)`로 조작 불가 처리(시련 기절에도 사용), 가변 점프, 다이브(Ctrl) |
| `BotManager.cs` / `BotBrain.cs` | AI 봇. 몸은 플레이어와 같은 부품을 코드로 조립하고 `PlayerController.SetExternalInput`으로 조종. 칸 단위 길 찾기(깨진 칸 1개는 점프). 바다에 빠지면 다시 떨어져 들어옴. **플레이어를 찾을 때는 `useExternalInput == false`인 쪽을 고른다** |
| `Trials/*.cs` | 시련. `TrialBase`(버틴 시간 기준 시작·간격, 첫 등장 안내, `Targets()`=플레이어+봇, `Hit()`=방패 판정) + `SnowballTrial`, `WindTrial`, `IcicleTrial`, `PenguinTrial`, 기절 별 `StunStars`. 새 시련은 `TrialBase`를 상속하고 `Reset()`에 기본 등장 시간을 넣는다 |
| `RepairItem.cs` / `RepairItemSpawner.cs` | 파워업 아이템 4종 (이름은 처음 얼음 복구만 있던 흔적). `PlayerPowerUps`가 방패·슈퍼 점프 상태를 가짐 |
| `PlayerSkin.cs` | 선택한 스킨 FBX를 Player 아래 `Model`로 붙이고 캡슐 렌더러 숨김 |
| `CharacterAnimator.cs` | Animator 파라미터 `Speed`, `WalkAnimSpeed`, `Grounded`, `VerticalSpeed`, `Down` 전달 |

거리 설계: 블럭 1.5m + 간격 0.1m. 깨진 블럭 1칸(약 1.7m)은 건널 수 있고 2칸(약 3.3m)은 어떤 동작으로도 못 건너야 한다. 점프·다이브 수치를 바꿀 때 이 조건을 지킨다.

## 복셀 캐릭터 파이프라인 (`Tools/blender/`)

- `voxel_common.py`: 복셀 → 메시, 공통 뼈대(`PIVOTS`), 리그 생성, FBX 내보내기, 미리보기 렌더
- `voxel_anim.py`: 공통 클립 Idle(48f 반복) / Walk(16f 반복) / Jump(12f 유지) / Fall(12f 반복) / Dive(10f 유지), 24fps
- `make_<캐릭터>.py`: `PALETTE`(sRGB)와 `build_parts()`만 정의하고 `run(이름, PALETTE, build_parts)` 호출
- `build_all.ps1`: 모든 캐릭터를 다시 만들고 FBX·색상표를 `LionIceRun/Assets/Models/Characters/`로 바로 출력, 미리보기 이미지·영상 생성
  - 실행: `powershell -ExecutionPolicy Bypass -File Tools\blender\build_all.ps1`
- 이후 Unity 메뉴 **LionIceRun > Step 8 캐릭터 가져오기**: Generic 리그, 클립 이름(`Rig|Walk`→`Walk`)·반복, 색상표로 URP 머티리얼 연결, `Assets/Animations/Character.controller` 생성, Player 연결

규칙:
- 복셀 1칸 = 0.1m, 키 약 2m(캡슐 높이), 정면은 Blender -Y (Unity +Z).
- **플레이어 스킨은 같은 부위 이름·관절 위치(`PIVOTS`)를 쓴다** (Root, Body, Head, Arm_L/R, Leg_L/R, Tail). 오브젝트 이름은 항상 `Rig`/`Mesh`. 그래야 사자 클립을 모든 스킨이 공유한다.
- 좌우 대칭은 `mx(x)` 사용. 둥근 몸은 `Part.round_box`.
- 새 캐릭터는 미리보기(`<이름>_preview.png`)를 먼저 보여주고 확인받은 뒤 Unity에 연결한다.
- 방해꾼(물범, 펭귄 등)처럼 몸 구조가 다른 캐릭터는 스킨 뼈대를 억지로 쓰지 말고 전용 `PIVOTS`와 전용 클립을 만든다. 공통 코드(`voxel_common.py`)는 그대로 쓴다.

현재 스킨: 사자(기본), 북극곰(파란 목도리), 호랑이.

## UI 파이프라인

- 사용자가 준 UI 시트 원본: `D:\UnityClassGame\UI\UI (1~8).png` (UI (8)은 UI (4)와 같은 파일). 시트 2는 화면 시안, 시트 3은 글자가 박힌 버튼·배너, 시트 4는 아이콘·빈 버튼.
- `Tools/ui/slice_ui.ps1`이 투명 배경 기준으로 요소를 잘라 `Assets/UI/Sprites/`에 저장하고 원본을 `Assets/UI/Source/`로 복사한다. 새 요소가 필요하면 이 스크립트에 `Save` 줄을 추가한다.
- 시트에는 예전 목표(아이템 수집) 요소("모든 아이템 획득!", "3/6")가 섞여 있으니 쓰지 않는다. 숫자·시간처럼 바뀌는 글자는 빈 패널 + 아이콘 + 실제 Text로 조합한다.
- 글자는 **TextMeshPro**만 쓴다 (기본 Text + Outline 효과는 테두리가 깨져 보여서 쓰지 않음). 글꼴은 `Assets/UI/Fonts/MalgunGothicBold SDF.asset` (Windows 맑은 고딕 Bold를 OS에서 읽는 DynamicOS 방식), 재질은 같은 폴더의 Outlined(흰 글자 + 두꺼운 남색 테두리 + 아래 그림자) / OutlinedThin / Plain(밝은 바탕 위 남색 글자).
- UI 스프라이트는 압축 끔 + 밉맵 켬 (큰 원본을 작게 줄여 그리면 깨지기 때문), 늘려 쓰는 배경은 9-slice. 시트에 빈 형태로 없는 배경(흰 배지, 반투명 남색 패널)은 `Tools/ui/PanelGen.cs`로 그린다.
- 화면 구성은 **Fall Guys UI**(https://www.gameuidatabase.com/gameData.php?id=1499)를 따른다: 화면 끝에 붙은 평행사변형 띠(목표 분홍, 숫자 남색, 스테이지 제목 초록, 새 기록 금색), 작은 제목 + 기울어진 분홍 배지, 화면을 가로지르는 사건 띠("시작!", "탈락!"), 반투명 키 칩, 자홍 제목 상자, 기울인 굵은 글자. 버튼·아이콘 그림은 사용자 UI 시트를 쓴다.
- UI 입력은 새 Input System용 `InputSystemUIInputModule`을 쓴다.
- **사용자가 GameUI에서 직접 고친 배치(위치·크기·회전·글자 크기·정렬)는 보존한다.** Step 7 UI 세팅은 다시 만들 때 요소 이름 경로로 값을 되돌려 놓는다. 그래서 UI 코드에서 **요소 이름을 바꾸면 그 요소의 사용자 배치가 사라진다** → 이름은 되도록 유지하고, 새 요소는 새 이름으로 추가한다. 코드로 바꾼 배치를 반영하려면 사용자가 "Step 7 UI 세팅 (배치 초기화)"를 써야 한다고 안내한다.

## PowerShell 스크립트 주의

- **`.ps1` 파일은 반드시 UTF-8 BOM으로 저장한다.** Windows PowerShell 5.1은 BOM 없는 UTF-8을 한국어 코드페이지로 읽어서, 한글 주석이 다음 줄을 삼켜 코드가 조용히 실행되지 않는다. Write 도구로 만든 뒤 BOM을 붙인다.

## 요청 양식

### 스킨 추가
```
[스킨 추가] 판다
- 특징: 검은 눈 주위, 검은 귀·팔·다리, 나머지 흰색
- 소품: (선택)
- 참고: 북극곰처럼 둥근 머리
```
진행: `make_<이름>.py` 작성 → 미리보기 → 확인 → `build_all.ps1`에 추가·실행 → Step 8 메뉴에 캐릭터 추가(`Characters` 배열, `PlayerSkin.Skin` enum)

### 방해꾼(시련) 추가
```
[방해꾼 추가] 펭귄 슬라이딩
- 생김새:
- 등장: 어디서 나타나 어떻게 움직이는지
- 예고: 피할 수 있게 미리 보여주는 방법 (필수)
- 플레이어에게: 밀려남 / 기절(KnockDown 초) 등
- 블럭에게: 영향 없음 / 단계 진행 등
- 빈도·등장 조건: 예) 60초 버틴 뒤부터 8초마다
- 애니메이션: 준비, 이동, 퇴장 등
```
blueprint의 시련 표에 있는 항목은 표 내용을 기본값으로 쓴다. 큰 작업은 모델 → 애니메이션 → 로직 순으로 나눠 확인받는다.

시련 원칙: 모든 시련은 예고가 있어 피할 수 있고, 버틴 시간이 길어질수록 하나씩 늘어나며, 서 있는 블럭을 예고 없이 바로 깨뜨리지 않는다 (기절 중에는 오래 서 있기 타이머 정지).
