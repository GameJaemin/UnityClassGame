# 클라이언트 입력 지연·웹 처리량 개선 검증

## 원인과 수정

기존 클라이언트는 자신의 캐릭터도 물리가 멈춘 원격 표시 상태였다. 입력을 호스트에 보내고, 호스트 위치가 돌아온 뒤 보간하므로 자신의 이동 시작에도 왕복 지연과 표시 지연이 붙었다.

자신의 캐릭터는 동일한 PlayerController 물리로 매 프레임 입력을 적용하고 즉시 이동한다. 호스트 응답은 현재 위치를 바로 덮지 않고 실제 처리 입력 순번에 대응하는 과거 로컬 위치와 비교한다. 아직 호스트에 도착하지 않은 이동은 보존하고 오차는 나눠 보정한다. 큰 위치 차이·최초 응답·부활은 위치와 이력을 초기화한다. 이는 위치 이력 보정이며 물리 전체를 되감아 재실행하는 방식은 아니다.

입력은 50Hz 최신 순번 전송으로 바꾸고 버튼 누름 횟수를 후속 패킷에도 포함한다. 이동 패킷 하나가 사라져도 최신 상태가 복구되며 버튼이 중복 실행되지 않는다. 순번 확인은 수신 순간이 아니라 Update에서 입력을 소비하고 FixedUpdate에서 실제 물리 처리를 마친 뒤 기록한다. 호스트가 확정한 기절·밀림·파워업·사망·부활·결과는 클라이언트에 적용하고, 로컬 예측이 얼음 파괴나 상대 타격을 확정하지 않게 했다.

Web에서만 화면 픽셀 배율/렌더 해상도·그림자·HDR·MSAA를 조정하고 얼음 파편 수/물리를 줄인다. 원격 연출의 Renderer 검색 결과와 임시 목록을 재사용한다. Windows 원본 품질 에셋·씬은 수정하지 않는다.

통신 형식 변경으로 OnlineRules.Protocol은 4다. 기존 배포 Web/Windows의 프로토콜 3과 섞어 실행할 수 없다.

Relay 종료 중 안전지대가 그리드 삭제 뒤에도 이전 IceBlock 경로를 매 프레임 참조하는 오류를 발견했다. IceGrid.Clear에서 경로를 먼저 해제하고 삭제된 칸을 건너뛴다. 이 오류 반복도 클라이언트 처리량을 늘릴 수 있어 함께 수정했다.

## 재현

Unity 6000.6.3f1 복사 프로젝트의 Main 씬에서 검사한다. 빌드는 만들지 않는다.

- `LatencyPredictionEntry.cs`를 복사본 Assets/Scripts/Editor에, `LatencyPredictionRunner.cs`를 Assets/Scripts에 복사한다. `-batchmode -nographics -executeMethod LatencyPredictionEntry.Run`으로 실제 물리와 위치 이력 검사를 실행한다.
- Relay 검사는 두 독립 복사 프로젝트를 에디터의 StandaloneWindows64 대상으로 실행한다. `LatencyRelayEntry.Run`, 호스트만 `-smokeHost`, 공통 `-smokeOutput <결과 폴더>`, 서로 다른 `-onlineProfile`을 지정한다. Main 씬과 실제 Authentication/Sessions/Relay를 사용한다.
- 접속 후 입력 및 위치 채널의 수신을 각각 180ms 미루고 10개 중 1개를 버린다. 이는 애플리케이션 메시지 전달 지연/손실이며 WebSocket의 실제 TCP 재전송이나 브라우저 프레임 비용을 흉내 낸 것은 아니다. 제어/월드 채널은 지연시키지 않는다.
- 배치 에디터의 합성 키가 무시되지 않도록 검사 전용 InputSettings에서 초점과 Game 뷰에 상관없이 키 이벤트를 받는다. 제품의 입력 설정은 변경하지 않는다.

## 결과 (2026-10-07)

- 최종 소스의 실제 물리/이력/권한/안전지대 정리·재생성 검사 18개: 실패 0개. `latency-prediction-results.txt`, `latency-prediction-final-editor.log`.
- 기존 온라인 규칙 42개·5판 규칙 23개: 통과. 최종 소스 컴파일 오류 없음.
- 실제 두 에디터 Relay 검사: 입력과 위치 응답 각각 편도 180ms 지연, 각 채널 10% 손실. 합성 D+Space 입력 후 100ms에 위치 변화 약 0.598m, 키/이동 액션/점프 액션과 로컬 예측 물리 활성 확인. 이동 거리는 점프의 수직 이동도 포함한다. 응답 시간 자체를 정밀 계측한 결과가 아니라 100ms 시점에 반응 여부를 검사한 결과다.
- 양쪽 최초 준비 각 1회, 중간 로비 복귀 없이 5판, 합산 17/14/11/8점 일치: 통과. `latency-relay-final-verified/response.txt`, `host-result.txt`, `client-result.txt`.
- 이 Relay 실행의 종료에서 안전지대 참조 오류를 찾았고, 위 18개 최종 물리 검사에 삭제/재생성 회귀를 추가해 통과했다. 안전지대 정리 추가 이후 Relay 5판 전체를 다시 실행하지는 않았다.
- 그래픽 없는 에디터에서 URP 렌더 타깃 오류와 Unity 검색 인덱스 오류가 발생하므로 이 로그를 브라우저 렌더 검증으로 취급하지 않는다. 이 검사는 입력·물리·네트워크 흐름 검사다. 서비스 로그와 임시 방 코드는 공개 배포하지 않는다.

## 배포 범위

기존 itch.io와 ZIP은 수정 전 버전이다. 사용자 요청 전 새 빌드/업로드는 하지 않았다. 실제 브라우저 FPS·렌더 품질·WSS 네트워크와 다른 PC의 체감은 새 빌드에서 확인해야 한다. 로컬 입력의 호스트 왕복 대기는 제거하지만 상대에게 보이는 시각과 호스트 충돌/승패 확정에는 실제 네트워크 지연이 남는다.

참고: [Unity 클라이언트 선행 처리 문서](https://mp-docs.dl.it.unity3d.com/netcode/2.2.0/advanced-topics/client-anticipation/), [Unity Web 캔버스 해상도 문서](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/develop/canvas-size).
