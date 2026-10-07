# UI 정리·폰트 통일 검사

EXE 빌드 없이 Assets/Packages/ProjectSettings를 복사한 검사 전용 Unity 프로젝트를 사용한다. 원본 Main 씬 YAML을 직접 편집하지 않는다.

`UIConsistencyValidation.cs`를 복사본 Assets/Scripts/Editor에 넣고 Unity 6000.6.3f1에 `-batchmode -nographics -quit -executeMethod UIConsistencyValidation.Run`을 전달한다.

검사 범위: 원본 씬의 전체 TMP 텍스트(입력창·드롭다운·비활성 템플릿 포함) 폰트와 기울임, 구형 멈추기 버튼 제거와 참조 해제, 자동 선택 안내, 모든 기존 RectTransform 위치 유지, 메뉴 재적용·씬 저장·재열기, 버튼 없는 Awake·시리즈 없는 솔로 자동 감속·타이틀 복귀, 기존 온라인/5판 규칙.

2026-10-07 검사 통과: 실제 Main 씬의 104개 텍스트·기존 배치·버튼 삭제·재적용/씬 저장·자동 감속·타이틀 복귀·기존 온라인/5판 규칙 59개. 컴파일 오류 없이 종료 코드 0. 로그: `consistency-validation.log`, 요약: `consistency-results-2026-10-07.txt`. 실제 화면·포인터/한글 입력은 그래픽 없는 검사로 대체하지 않는다.

원본 게임은 다음 실행 때 `UIConsistency`에서 자동으로 정리된다. 에디터에서도 저장된 상태로 보려면 Play를 끄고 **LionIceRun > UI 정리·폰트 통일**을 실행한다. 기존 **UI 전체 업데이트**에도 동일한 정리를 포함했다. 이미지에 그려진 로고 글자는 TMP 폰트 변경 대상이 아니다.
