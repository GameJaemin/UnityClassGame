# 혼자 하기 5판 시리즈 검증

실제 Main 씬과 플레이어/NPC 프리팹을 사용한다. 원본 Assets·Packages·ProjectSettings를 복사한 별도 프로젝트에 검증 코드를 넣는다.

- `SoloSeriesRunner.cs`: Assets/Scripts에 복사. UNITY_EDITOR 또는 DEVELOPMENT_BUILD에서만 컴파일한다.
- `SoloSeriesEntry.cs`: Assets/Scripts/Editor에 복사.
- 에디터: Unity 6000.6.3f1 `-batchmode -nographics -projectPath <복사본> -executeMethod SoloSeriesEntry.Run -smokeOutput <비어 있는 출력 폴더> -logFile <로그>`로 실행한다. Play 모드 진입 후 종료는 러너가 담당하므로 -quit를 넣지 않는다.
- EXE: `../online-regression/MainRegressionBuild.cs`를 복사본 Editor에 넣고 BuildOutput.txt에 테스트 EXE 출력 경로를 저장해 Development 빌드를 만든다. 테스트 EXE를 `-batchmode -nographics -soloSeriesTest -smokeOutput <비어 있는 출력 폴더> -logFile <로그>`로 실행한다.
- 테스트 창은 숨긴 상태로 실행한다. PlayerPrefs는 CodexTests 제품에 격리한다. 배포본에서는 러너를 제외한다.

검사: 캐릭터 선택 UI에서 호랑이를 선택 → 자동 맵 선택·카운트다운·다섯 판 → NPC 스킨 유지 → 합산 17/14/11/8점 → 우승 횟수 1회 → 최종 UI 유지/6판 차단 → 타이틀 복귀 → 새 시리즈 점수 초기화 → 중도 나가기로 시리즈 취소.

매 판 지정 참가자 한 명을 남겨 순위를 재현하므로 자연스러운 경기 시간·조작감 검사는 아니다. 그래픽 없이 버튼의 등록된 이벤트를 실행하며 실제 포인터 클릭/화면 렌더 확인은 별도로 필요하다. 결과 화면 검사에서 씬이 전환된 뒤의 새 대기 화면을 이전 라운드 결과로 검사하지 않도록 현재 씬/라운드를 다시 확인한다.
