# 이동 안전지대 검사

게임 빌드 없이 복사 프로젝트의 실제 Main 씬을 Play 모드로 실행한다. `MovingSafetyEntry.cs`는 Assets/Scripts/Editor, `MovingSafetyRunner.cs`는 Assets/Scripts에 복사한다. `-batchmode -nographics -executeMethod MovingSafetyEntry.Run`을 사용하며 러너가 종료하므로 `-quit`는 넣지 않는다.

검사: 10초 유지·2초 색 보간, 안전 시작 칸이 있는 기존 12개 맵의 두 경로·인접 순환·경로 겹침 방지·10초 직전 보호·10초 이동·파괴 칸 복구/콜라이더·떠난 칸 금감·다음 칸 파괴 방어·경로 반복. 복제 PC의 예측 시각으로 보호 칸을 바꾸지 않는 것과 호스트 스냅샷 적용 뒤 안전/일반 칸의 원격 파괴 판정도 확인한다. S가 없는 사용자 BigMap은 새 안전 칸을 임의로 만들지 않는다. 기존 온라인/5판 규칙도 함께 검사한다.

원본 씬·사용자 맵 JSON은 변경하지 않는다. 런타임에 IceGrid가 MovingSafeZones를 생성하므로 별도 메뉴/Inspector 작업이 필요 없다. 실제 화면의 색 변화·조작감과 실제 Relay 연결은 이 그래픽 없는 검사와 별도로 확인해야 한다.

2026-10-07: 232개 검사 실패 0개. 기존 온라인 36개·5판 23개 규칙 검사도 통과. 기록은 `results-2026-10-07.txt`, 에디터 컴파일/실행 로그는 `validation.log`. 처음 검사에서는 S가 없는 사용자 BigMap까지 두 안전 경로가 있다고 가정하여 중단됐으며, S 없는 맵을 유지하는 조건으로 보완한 뒤 전체 통과했다.
