# 연속 점프 밟힘 검사

## 수정

기존에는 PlayerController가 착지 직후 예약 점프를 실행해 IsGrounded를 false로 바꾼 다음 IceStepTracker가 접지를 검사하면 밟힘이 누락됐다. 타일 검사를 컨트롤러의 접지 갱신 직후, 점프 처리 전에 호출하도록 바꿨다. 트래커가 별도의 FixedUpdate를 실행하지 않아 스크립트 실행 순서에도 의존하지 않는다.

실제 점프와 재착지를 구분한다. 짧은 점프는 기존 블럭의 0.5초 이음새 중복 방지 시간보다 빨리 착지할 수 있으므로, 착지 충격은 그 시간 제한을 통과한다. 걷기 중 이음새 중복 방지·서 있기 시간·공중 통과·안전지대·호스트 얼음 권한은 유지한다.

## 실행

원본 씬을 수정하지 않고 기존 복사 프로젝트에서 Unity 6000.6.3f1 에디터의 물리 시뮬레이션을 실행한다. 게임 빌드는 하지 않는다.

`JumpContactRunner.cs`를 복사본 Assets/Scripts, `JumpContactEntry.cs`를 Assets/Scripts/Editor에 복사하고 `-batchmode -nographics -executeMethod JumpContactEntry.Run`으로 실행한다. 기존 온라인 규칙·시리즈 규칙 검사도 함께 실행한다.

검사 결과: `jump-contact-results.txt`, 에디터 로그: `jump-contact-editor.log`.

2026-10-07 최종 실행: 물리 검사 10개 중 실패 0개, 기존 온라인 규칙 42개·5판 규칙 23개 통과. 준비 중 보호, 서로 다른 새 칸에서 즉시 점프 3회, 실제 물리의 같은 칸 연속 점프 3회와 금 진행, 공중 통과 제외, 서 있기 중복 방지, 안전 칸 보호, 클라이언트 임의 얼음 변경 방지를 확인했다. 사용자 실제 플레이·수정 버전 Relay 양쪽 실행은 별도 확인이 필요하다.

기존 `Builds/Web/LION_ICE-WebGL-20261007.zip`과 Windows 배포 파일에는 이번 수정이 포함되지 않는다. 사용자 빌드 요청 시 새 배포 파일을 만든다.
