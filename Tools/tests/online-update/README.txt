업데이트 저장/빌드 검사 (2026-10-06)

원본 Assets, Packages, ProjectSettings를 별도 Unity 프로젝트로 복사한 뒤
BatchUpdateBuild.cs를 복사본 Assets/Editor에 넣고,
복사본 BuildOutput.txt에 출력 EXE의 절대 경로를 저장한다.
Unity 6000.6.3f1 -batchmode -nographics -quit -projectPath <복사본> -executeMethod BatchUpdateBuild.Run -logFile <로그>로 실행한다.
이 검사는 복사본 씬/프리팹에 업데이트 메뉴를 적용한다. 원본에 직접 넣지 않는다.

검사: 기존 멀티 규칙 28개 + 표현 저장 9개.
씬/멀티 프리팹 수치, 이름표 글꼴/참조 직렬화, 닉네임 정규화,
내 캐릭터/AI 표시, 사용자 이름표 위치 보존, 중복 생성 방지.
실제 화면과 네트워크에서의 준비/밀치기 체감은 별도 확인한다.