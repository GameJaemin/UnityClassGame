UI 폴리싱 검증 — Unity 6000.6.3f1

사용자 프로젝트를 Assets/Packages/ProjectSettings만 복사한 별도 프로젝트에서 검사한다.
BatchUIPolishValidation.cs를 복사본 Assets/Scripts/Editor에 넣고 다음 인자로 Unity를 실행한다.

-batchmode -quit -projectPath <복사본 경로> -executeMethod BatchUIPolishValidation.Run -logFile <로그 경로>

그래픽 렌더가 필요하므로 -nographics는 사용하지 않는다. 이 검사에는 BuildPipeline 호출이 없다.
검사: 기존 멀티 규칙 28개, 입력 richText/IME 설정·명암 대비·재질, 모델/화살표/카메라 연결,
좌우 순환·연결 중 잠금, 모델 초상화 저장, 재적용 시 닉네임 위치 보존, 모든 기존 HUD 위치 보존.
docs/screenshots/ui-polish.png는 Unity의 모드 선택 UI 렌더 결과다.
docs/screenshots/ui-polish-lobby.png는 같은 UI에 예시 참가자 문구를 넣어 촬영한 배치 확인용 렌더다.
실제 인터넷 접속이나 한글 IME 키 입력을 이 검사로 대체하지 않는다.

원본 Main 씬은 검사에서 변경하지 않는다. Play를 끈 뒤 LionIceRun > UI 폴리싱으로 저장한다.
게임 빌드는 사용자 요청 시에만 수행한다.
