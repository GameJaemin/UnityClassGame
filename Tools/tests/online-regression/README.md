# 온라인 타이틀·5판 회귀 검증

원본을 수정하지 않은 복사 프로젝트의 실제 `Assets/Scenes/Main.unity`와 모델·프리팹·UI를 사용한다. 규칙 검사만 통과하는 단순 테스트 씬과 구분한다.

## 실행

1. 원본 Assets·Packages·ProjectSettings를 두 임시 프로젝트에 복사한다. `MainRegressionRunner.cs`를 Assets/Scripts, `MainRegressionEntry.cs`를 Assets/Scripts/Editor에 넣는다.
2. Unity 6000.6.3f1을 각각 `-batchmode -nographics -projectPath <복사본> -executeMethod MainRegressionEntry.Run -onlineProfile <개별 프로필> -smokeOutput <비어 있는 공유 출력 폴더> -logFile <개별 로그>`로 실행한다. 호스트만 `-smokeHost`를 추가한다. 기존 Hub 라이선스 IPC가 필요하면 `-licensingIpc LicenseClient-User`를 추가한다.
3. EXE 검사는 복사본 Assets/Scripts/Editor에 `MainRegressionBuild.cs`도 넣고 BuildOutput.txt에 테스트 EXE 출력 경로를 저장한다. `-batchmode -nographics -quit -executeMethod MainRegressionBuild.Run`으로 Development 빌드를 만든다.
4. 테스트 EXE를 `-batchmode -nographics -mainOnlineRegression -onlineProfile <개별 프로필> -smokeOutput <비어 있는 공유 출력 폴더> -logFile <개별 로그>`로 두 개 실행한다. 호스트만 `-smokeHost`를 추가한다. 숨긴 창으로 실행한다.
5. 양쪽 result.txt, trace.txt, 프로세스 종료 코드를 확인한다. 방 코드·서비스 로그·익명 인증 정보는 커밋하지 않는다.

## 검사 범위

- 실제 Sessions/Relay 연결, 최초 준비 1회, 5판 자동 맵 선택·로딩·카운트다운·경기·결과.
- 지정 생존자를 번갈아 남겨 점수 17·14·11·8의 양쪽 일치 확인. 자연스러운 전체 경기 시간이나 조작 체감 측정은 아니다.
- 준비 명령 직후의 로딩 완료 응답도 연타 제한에 막히지 않는지 확인.
- 5판 후 결과 버튼을 통해 로비로 돌아가도 두 참가자 연결 유지.
- 다음 시리즈에서 호스트 Busy 상태 중 클라이언트가 퇴장하면 처리 완료 후 시리즈 취소/로비 복귀.
- 나가기 후 모드 화면의 타이틀 버튼을 통해 룰렛 숨김·시작 버튼 복원·네트워크 종료 확인.
- 타이틀 시작 버튼의 실제 등록 이벤트, 방 재생성/나가기.
- 온라인 이후 솔로 전환·맵 선택·감속·카운트다운·경기 시작.

검증용 PlayerPrefs는 CodexTests/MainOnlineRegression에 격리한다. Runtime 검사 코드는 UNITY_EDITOR 또는 DEVELOPMENT_BUILD에서만 컴파일하며, EXE는 명시적인 테스트 인자가 있을 때만 실행한다. 배포 빌드는 별도 복사 프로젝트에서 검사 러너를 제거하고 `Tools/release/BatchReleaseBuild.cs`로 생성한다.

실제 포인터 클릭/화면 렌더, 서로 다른 PC·인터넷·지연·패킷 손실은 별도 플레이 확인이 필요하다.
