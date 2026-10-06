# Windows 빌드

원본 Unity 에디터를 유지하기 위해 Assets·Packages·ProjectSettings를 임시 프로젝트로 복사한다.
`BatchReleaseBuild.cs`를 복사본의 `Assets/Scripts/Editor`에 넣고,
복사본 루트 `BuildOutput.txt`에 새 출력 폴더의 EXE 절대 경로를 쓴다.

Unity 6000.6.3f1에서 `-batchmode -quit -projectPath <복사본> -executeMethod BatchReleaseBuild.Run -logFile <로그>`로 실행한다.
원본 Main 씬에 UI 전체 업데이트가 저장되어 있어야 한다.
기존 온라인 규칙과 5판 시리즈 규칙을 검사하고 복사본의 편집용 UI 미리보기를 닫은 뒤 Windows x64로 빌드한다.
`BuildResult.txt`와 종료 코드 및 EXE/Data 파일을 확인하고 출력 폴더를 ZIP으로 묶는다.
실행 로그와 임시 프로젝트는 커밋하지 않는다. 게임 빌드는 사용자가 요청할 때만 진행한다.
