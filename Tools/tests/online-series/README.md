# 온라인 5판 시리즈 검증

Unity 6000.6.3f1의 **에디터 Play 모드**에서 실제 Sessions/Relay 접속을 검사한다. 게임 EXE/ZIP이나 BuildPipeline 호출은 없다.

1. 원본 프로젝트의 Assets·Packages·ProjectSettings를 각각 다른 임시 프로젝트 두 곳에 복사한다.
2. `SeriesEditorEntry.cs`는 각 복사본의 `Assets/Scripts/Editor`, `SeriesPlayRunner.cs`는 `Assets/Scripts`에 복사한다. 원본 게임 Assets에 넣지 않는다.
3. 공유 임시 출력 폴더를 만든다. Unity를 `-batchmode -nographics -projectPath <복사본> -executeMethod SeriesEditorEntry.Run -onlineProfile <개별 테스트 프로필> -smokeOutput <공유 폴더> -logFile <개별 로그>`로 각각 실행한다. 방장에만 `-smokeHost`를 추가한다. `-quit`는 시작 인자에 넣지 않는다. Play 모드 종료 후 테스트가 에디터를 종료한다.
4. `host-result.txt`, `client-result.txt`의 PASS와 최종 점수 일치를 확인한다. 테스트 방은 종료할 때 떠난다. 방 코드·서비스 로그·익명 프로필 캐시는 공개하거나 커밋하지 않는다.

에디터 진입 전에 기존 멀티 규칙 검사 28개, 시리즈 규칙 검사 23개, UI 저장 연결/메뉴 재적용/기존 HUD 위치 보존을 검사한다.

실제 네트워크 검사에서는 두 사람과 두 봇이 처음에 한 번만 준비하고 자동 룰렛·맵 로딩·카운트다운을 거쳐 다섯 판을 진행한다. 호스트가 각 판의 생존자를 번갈아 남겨 결과를 재현한다. 자동 흐름 중 로비 복귀 없음, 온라인 멈추기 버튼 숨김, 기존 준비 유지, 최종 합산 17·14·11·8점의 양쪽 일치를 확인한다.

원본 Main 씬은 검사에서 수정하지 않는다. 복사본의 회사/제품 이름을 변경해 테스트 우승 횟수가 원본 PlayerPrefs에 섞이지 않게 한다. 모델을 제외한 테스트 참가자이므로 스킨 모델 누락 경고는 예상된다. 새 복사 프로젝트의 Unity 검색 인덱서 초기화에서 에디터 내부 예외가 있었으나 Play·네트워크 검증은 완료됐다.

순수 규칙 검사에는 중복/잘못된 순위 거부, 5판 합산, 마지막 판 승자와 합산 우승자가 다른 경우, 반복/오래된 스냅샷, 공동 순위, 2인 점수, 새 시리즈 초기화를 포함한다.

실제 게임 UI/서로 다른 PC/연결 끊김은 별도 플레이 확인이 필요하다. 원본 씬에 새 UI를 저장하려면 Play를 끄고 **LionIceRun > 온라인 5판 시리즈 UI 업데이트**를 실행한다. **친구 멀티 세팅**에도 포함된다.
