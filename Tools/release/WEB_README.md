# itch.io 웹 빌드

Unity 6000.6.3f1의 Web Build Support 모듈이 필요하다. Windows와 Web는 같은 소스를 사용한다.

- `MapData`: Web 플레이어는 빌드에 묶인 JSON 원문/파일 목록을 Resources로 읽고, Windows와 에디터는 기존 StreamingAssets 파일을 읽는다. 파싱·정규화·해시·보관 맵 필터는 공통이다.
- `WebBuildSupport`: Web 빌드 직전에 현재 모든 맵을 자동으로 묶고, OFL 한글 글꼴을 포함하며 gzip 및 Decompression Fallback을 설정한다. 기존 씬 배치는 수정하지 않는다.
- `OnlineGameSession`: Web는 WSS 및 WebSocket, Windows/에디터는 DTLS를 자동 선택한다. 생성과 참가 모두 NetworkOptions로 지정한다. Web의 탭별 게스트 프로필은 같은 브라우저 두 탭의 인증 충돌을 막는다.
- `UIConsistency`: Web는 포함된 Noto Sans CJK KR Bold를 사용한다. 원래 UI의 테두리·그림자 재질 설정을 유지하고 atlas를 웹 글꼴로 교체한다. Windows는 맑은 고딕 Bold를 유지한다.
- `Assets/WebGLTemplates/LionIceWeb`: iframe 크기에 맞춰 화면을 채우며 로딩 표시·전체 화면 버튼을 제공한다.
- `OnlineActor` / `OnlineMotionHistory`: 클라이언트 자신의 이동·점프·다이브·밀치기 동작을 로컬에서 즉시 계산한다. 호스트가 실제 물리에 처리한 입력 순번의 과거 위치와 비교해 오차를 보정하며, 얼음·타격·부활·승패는 호스트가 확정한다. 최신 입력은 50Hz 비신뢰 순번 전송, 버튼 누름 순번은 다음 패킷에도 반복한다.
- `WebRuntimePerformance`: Web에서만 60fps 목표, 화면 픽셀 배율 1, URP 렌더 배율 0.85·MSAA 2·그림자 거리 24m/캐스케이드 1·HDR 해제. 얼음 조각은 최대 96개, 블럭당 최대 2개, Rigidbody 대신 연출용 이동을 사용한다. 실제 FPS는 새 브라우저 빌드에서 확인한다.
- 웹 배포 씬의 라운드 표시는 얼음 HUD 아래에, 경기 중 나가기 버튼은 시간/우승 HUD 아래에 놓는다. 원본 씬·Windows HUD 배치는 유지한다.

## 빌드

일반 Unity 빌드에서는 Build Profiles의 Web를 선택하면 자동 적용된다. Windows로 돌아갈 때 소스/통신 설정을 직접 변경하지 않는다. 플랫폼별 프로필에서 빌드 대상만 선택한다.

원본 에디터를 유지하려면 Assets·Packages·ProjectSettings를 임시 프로젝트로 복사하고 `BatchWebBuild.cs`를 복사본 Assets/Scripts/Editor에 넣는다. 복사본 `BuildOutput.txt`에 출력 폴더 절대 경로를 쓴다.

Unity를 `-batchmode -quit -buildTarget WebGL -projectPath <복사본> -executeMethod BatchWebBuild.Run -logFile <로그>`로 실행한다. 기존 라이선스 IPC가 필요하면 `-licensingIpc LicenseClient-User`를 추가한다.

온라인 규칙 42개·5판 23개·디스크/웹 맵 해시 일치 검사를 먼저 수행한다. 브라우저의 실제 렌더링과 Relay 연결은 별도로 확인한다.

## itch.io 업로드

현재 업로드용 파일: `Builds/Web/LION_ICE-WebGL-20261007.zip`. 이 ZIP은 연속 점프 밟힘 수정·클라이언트 입력 예측·웹 성능 개선·새 UI/설정/감정표현/채팅 **이전** 빌드(프로토콜 3)다. 소스를 수정해도 기존 ZIP은 갱신되지 않으며, 사용자 빌드 요청 후 새 ZIP으로 교체한다. 수정 소스는 프로토콜 4이므로 새 Web/Windows 참가자는 모두 같은 수정 버전을 사용해야 한다.

표지 이미지: `docs/marketing/itch-cover-v1.png`를 페이지의 Cover image에 별도로 올린다.

출력 폴더의 **내용물**을 ZIP으로 묶는다. ZIP 최상위에 `index.html`, `Build/`, `StreamingAssets/`가 있어야 한다.

1. itch.io 프로젝트의 Kind of project를 **HTML**로 선택한다.
2. ZIP을 업로드하고 **This file will be played in the browser**를 선택한다.
3. 화면 크기 1280×720, 전체 화면 허용으로 설정한다. SharedArrayBuffer 설정은 이 단일 스레드 빌드에 필요하지 않다.
4. 실제 게시 페이지에서 방 생성·참가·5판·타이틀 복귀를 확인한다. Windows와 혼합 참가할 때는 게임 프로토콜과 맵 해시가 일치해야 한다.

`index.html`을 파일 탐색기에서 직접 열지 않는다. 로컬 검사는 HTTP 서버로 제공한다.

## 새 UI와 한글 입력 확인

실행하면 새 UI가 자동 적용된다. 다이브 기본 키는 Shift이며 타이틀의 시작 버튼 아래 **설정**에서 조작 키·익명 표시·채팅 표시를 저장할 수 있다. 경기에서 T를 누르고 마우스를 끈 뒤 놓으면 감정표현, 온라인 방에서 Enter를 누르면 채팅 입력이다. 채팅은 최대 120자·1초 간격, 감정표현은 1.2초 간격으로 호스트가 검증한다.

닉네임 기본값은 빈칸이며 비워두고 시작하면 선택 동물 이름을 사용한다. 설정의 **맵 선택 (초)**에 1~15초를 직접 입력한다(기본 5초, 소수 허용, 기존 저장값 유지). 변경은 다음 맵 선택부터 적용하고 온라인은 방장 설정을 따른다. 온라인 동기화 시작 여유 0.5초·맵 로딩·경기 카운트다운은 별도다.

`WebTextInput.cs`와 `Assets/Plugins/WebGL/IceTextInput.jslib`는 브라우저 입력창에서 한글을 조합하고 TMP에 전달한다. 템플릿의 전체 화면은 입력창을 포함하는 body를 사용한다. 이벤트 모의 검사에서 조합 중 Enter 억제·마지막 글자·포커스/리사이즈 정리를 확인했지만 실제 WebGL 브라우저 통합 검사는 새 빌드 뒤에 수행해야 한다. 한글 닉네임/방 코드/채팅, 조합 중 Enter, 일반/전체 화면 전환, 입력창을 닫은 뒤 WASD/Shift/T 복구를 확인한다. 검증 기록은 `Tools/tests/experience-ui/validation.md`.

## 글꼴 출처

[Noto Sans CJK KR Bold 원본](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/Korean/NotoSansCJKkr-Bold.otf), [SIL OFL 1.1](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE). 원본 글꼴은 `Assets/UI/Fonts/Web/`, 라이선스는 같은 폴더 `OFL.txt`에 보관한다. 배포 ZIP에도 라이선스를 포함한다.
