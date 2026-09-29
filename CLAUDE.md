# LionIceRun

폴가이즈 느낌의 솔로 3D 게임. 복셀 동물 캐릭터가 펭귄 얼음깨기(폴가이즈 Thin Ice 방식) 맵 위에서 아이템을 모두 모으면 성공, 바다에 떨어지면 실패.
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
- 컴파일/실행은 직접 못 한다. 결과는 사용자 스크린샷이나 Console 오류로 확인한다.

## 주요 코드 (`LionIceRun/Assets/Scripts/`)

| 파일 | 역할 |
|---|---|
| `IceGrid.cs` | 가로x세로 얼음 그리드 생성(기본 6x6). 블럭 윗면 y=0. `CellTop(cell)`, `StartCell`, `Blocks`, 바다 위치(`seaDepth` 12m) |
| `QuarterViewCamera.cs` | 그리드 크기·화면비에 맞춰 고정 쿼터뷰 자동 배치 |
| `PlayerController.cs` | Rigidbody 이동. 상태 `Normal / Diving / KnockedDown`, `KnockDown(초)`로 조작 불가 처리(시련 기절에도 사용), 가변 점프, 다이브(Ctrl) |
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
- 빈도·등장 조건: 예) 아이템 5개 이상일 때 8초마다
- 애니메이션: 준비, 이동, 퇴장 등
```
blueprint의 시련 표에 있는 항목은 표 내용을 기본값으로 쓴다. 큰 작업은 모델 → 애니메이션 → 로직 순으로 나눠 확인받는다.

시련 원칙: 모든 시련은 예고가 있어 피할 수 있고, 아이템을 모을수록 하나씩 늘어나며, 남은 아이템에 갈 수 없게 만들지 않는다 (아이템 블럭은 랜덤 금 가기·고드름 대상 제외, 기절 중에는 오래 서 있기 타이머 정지).
