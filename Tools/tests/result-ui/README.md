# 결과 화면 5초 이어하기 검증

EXE 빌드 없이 실제 Main 씬의 에디터 Play 모드에서 검사한다. `ResultUiEntry.cs`를 복사 프로젝트 Assets/Scripts/Editor, `ResultUiRunner.cs`를 Assets/Scripts에 넣는다.

Unity 실행: `-batchmode -nographics -projectPath <복사 프로젝트> -executeMethod ResultUiEntry.Run -smokeOutput <출력 폴더> -logFile <로그>`. 러너가 종료하므로 `-quit`는 넣지 않는다. PlayerPrefs는 CodexTests/ResultUiRegression으로 격리한다.

검사: 공유 결과 대기 설정 5초·안내 문구·결과 나가기 버튼의 화면 우측 상단 여백 24·144×44 크기·불투명도 55%·강조 글자 설정·실제 1~4판 결과에서 다음 씬으로 이동하는 대기 시간. 기존 5판 누적 점수/캐릭터 유지/최종 결과 유지/타이틀 복귀도 검사한다.

원본 씬은 변경하지 않고 새 배치를 런타임에 적용한다. `LionIceRun > 온라인 5판 시리즈 UI 업데이트` 메뉴로 결과 미리보기와 배치를 씬에 저장할 수도 있다. 그래픽 없는 검사이므로 실제 화면 렌더/포인터 조작 검사는 별도로 필요하다.
