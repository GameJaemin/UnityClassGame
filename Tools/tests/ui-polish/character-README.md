# 캐릭터 선택 회귀 검사

현재 Main 씬 복사본의 Unity Play 모드에서 검사한다. `CharacterSelectionEntry.cs`는 Assets/Scripts/Editor, `CharacterSelectionRunner.cs`는 Assets/Scripts에 복사하고 `-batchmode -nographics -executeMethod CharacterSelectionEntry.Run`을 사용한다. 빌드는 하지 않는다.

원본 씬의 일반 진입은 버튼 레이캐스트·좌우 순환·선택 모델·스킨 저장/솔로 반영 12개 검사를 통과했다. 따라서 사용자 환경의 원인을 정상 초기 진입 실패로 확정하지 않았다.

수정 후 19개 검사 통과: 화면 재진입, 연결 손실 후 복구, 중복 리스너 방지, 연결 중 잠금, 불완전 모델 선택 시 드롭다운 유지, 정상 시작에서 선택 스킨 저장/솔로 반영, `EditorUtility.RequestScriptReload()`로 실제 Play 중 코드 재로딩을 수행한 뒤 선택 버튼과 선택 값 저장. 결과는 `character-results-2026-10-07.txt`, 로그는 `character-reload.log`.

캐릭터 버튼을 Awake에만 연결하던 것을 OnEnable/화면 열기에서 중복 없이 복구하도록 변경했다. 모드·로비 버튼 연결도 OnEnable에서 복구한다. 이 검사는 온라인 경기 중 코드 재로딩 후 네트워크 세션 전체 복원까지 보장하는 검사가 아니다. 원본 씬 YAML은 변경하지 않았다.
