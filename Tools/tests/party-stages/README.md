# 추가 파티 스테이지 검사

게임 빌드 없이 복사 프로젝트의 실제 Main 씬을 Unity Play 모드로 검사한다. `PartyStageEntry.cs`는 Assets/Scripts/Editor, `PartyStageRunner.cs`는 Assets/Scripts에 복사하고 `-batchmode -nographics -executeMethod PartyStageEntry.Run`을 사용한다. 러너가 종료하므로 `-quit`는 넣지 않는다.

새 6개 맵의 71개 검사 통과: 기믹 종류/생성 개수·얼음 개수·4인 시작 칸·온라인 시작 배정·이동 안전 경로·룰렛 미리보기·시소 기울기와 접촉점 운반·반짝 발판의 렌더/콜라이더/AI 경로 제외·재등장 시 파괴 상태 유지와 얼음 복구·복제 PC 주기·발사대 실제 접촉 점프/기절 없음·해머 실제 접촉 밀침/기절. 기존 온라인 규칙 42개·5판 규칙 23개도 통과.

초기 발사대 검사에서는 이전 해머의 기절 상태를 다음 맵까지 유지한 검사 준비 오류가 있었다. 맵마다 플레이어 위치/상태를 초기화한 뒤 통과했다. 기록: `results-2026-10-07.txt`, 컴파일/실행 로그: `validation.log`.

실제 화면과 조작감, 실제 Relay 2인 동기화는 별도로 확인해야 한다. 원본 Main 씬 YAML과 사용자 BigMap은 변경하지 않는다.
