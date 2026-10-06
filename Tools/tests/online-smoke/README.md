# 방 코드 멀티 검증

> 아래 두 라운드 수동 복귀 검사는 2026-10-06 초기 구현의 기록이다. 최신 자동 5판 시리즈 검증은 `../online-series/README.md`를 따른다. 최신 흐름에서는 경기 중 수동 로비 복귀/맵 멈추기가 차단되므로 이 예전 실행 시나리오는 그대로 사용하지 않는다.

이 폴더의 C# 파일은 원본 게임 씬을 수정하지 않는 **별도 Unity 검증 프로젝트용**이다. 원본 `Assets`에 넣지 않는다.

## 재현 방법

1. Unity 6000.6.3f1용 빈 검증 프로젝트를 만든다. 원본 `Assets/Scripts`, `Assets/StreamingAssets/Maps`, `Assets/TextMesh Pro`와 `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`을 복사한다. 기존 UGS 프로젝트 연결이 유지되므로 실제 서비스에 접속한다.
2. `BatchOnlineRunner.cs`를 검증 프로젝트의 `Assets`, `BatchOnlineBuild.cs`를 `Assets/Editor`에 복사한다.
3. Unity를 `-batchmode -nographics -quit -projectPath <검증 프로젝트> -executeMethod BatchOnlineBuild.Run -logFile <빌드 로그>`로 실행한다. `SmokePlayer/OnlineSmoke.exe`가 생성된다.
4. 비어 있는 결과 폴더를 만든다. 위 EXE를 `-batchmode -nographics -smokeHost -onlineProfile smokehost -smokeOutput <결과 폴더> -logFile <host.log>`와 `-batchmode -nographics -onlineProfile smokeclient -smokeOutput <동일 결과 폴더> -logFile <client.log>`로 각각 실행한다. Windows에서 숨긴 창으로 실행한다.
5. `host-result.txt`, `client-result.txt`와 각 `*-round0.txt`, `*-round1.txt`를 확인한다. 방 코드가 기록된 `room-code.txt` 및 서비스 로그는 공개하거나 커밋하지 않는다.

실제 비공개 로비와 Relay를 사용한다. 테스트용 익명 프로필을 따로 사용하며, 종료 시 방을 떠난다. 캐릭터 모델과 UI를 제외한 단순 물리 검증 씬이므로 스킨 모델 누락 경고는 예상된다.

## 검사 범위

- 코드 참가 → 두 명 준비 → 방장 룰렛 선택 → 공통 맵 로딩/카운트다운
- 두 사람과 두 봇의 생성, 양쪽 이동과 얼음 상태
- 방장 강제 낙하와 10초 후 부활의 양쪽 상태
- 같은 연결로 로비 복귀 후 두 번째 라운드
- 두 번째 경기 종료와 네 명 순위 전달

별도 메뉴 `LionIceRun > 친구 멀티 규칙 검사`는 코드/닉네임·입력 순번·맵 해시·공통 맵·전체 맵의 안전한 4인 시작 칸·NGO 버퍼를 검사한다.

이 테스트는 한 PC의 두 인스턴스 검증이다. 실제 화면, 서로 다른 인터넷의 2~4인 플레이, 지연/손실, 점프·밀치기 동시 입력, 시련/아이템, 솔로 복귀는 별도로 확인한다.
