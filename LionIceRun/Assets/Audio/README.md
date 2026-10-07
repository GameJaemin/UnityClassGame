# LION ICE 무료 사운드 후보

2026-10-07 공식 배포 페이지에서 다운로드했다. 총 **43개: BGM 3개, 결과 징글 4개, 효과음 36개**. 모두 배포자가 **CC0 1.0**으로 공개한 자료다. 무료·상업용 게임에서 복사·수정·배포할 수 있으며 저작자 표시는 의무가 아니다. 선택적으로 아래 제작자를 크레딧에 표기할 수 있다.

이번 작업은 다운로드·선별·정리까지다. 게임 이벤트 연결, AudioSource/AudioMixer 설정, 실제 게임 청취·볼륨 조정은 아직 하지 않았다. 씬·프리팹은 변경하지 않았고 게임 빌드도 하지 않았다.

## 들어 보기

`D:/UnityClassGame/Tools/audio/preview.html`을 브라우저로 열면 용도별 재생 버튼이 나온다. Unity Project 창에서 개별 AudioClip을 선택해 미리 들어도 된다. 용도는 제작자 설명·원본 분류를 바탕으로 추천한 후보이며, 최종 음악 취향과 효과음 적합성은 청취 후 조정한다.

## 용도별 추천

| 폴더/파일 | 추천 위치 | 개수 |
|---|---|---:|
| `Music/menu_snow_loop.ogg` | 겨울 테마 타이틀·로비. 벨·차임이 있는 차분한 음악 | 1 |
| `Music/playful_round_loop.wav` | 일반 경기. 밝고 경쾌한 피아노 루프 | 1 |
| `Music/party_round_loop.ogg` | 회전목마·공장 같은 기믹 경기. 170 BPM 파티 음악 | 1 |
| `Jingles/` | 라운드 결과·5판 합산 결과. 피치카토 징글 | 4 |
| `SFX/Ice/` | 금감 3개·파손 3개·흔들림 경고 2개 | 8 |
| `SFX/Movement/` | 눈 위 발소리 3개·점프 2개·다이브 1개 | 6 |
| `SFX/Collision/` | 강한 밀침 3개·착지 2개·바다 물보라 1개 | 6 |
| `SFX/Gimmicks/` | 기계 충돌 2개·피스톤 걸쇠 1개 | 3 |
| `SFX/Items/` | 획득·방패·슈퍼 점프·얼음 복구 | 4 |
| `SFX/UI/` | 버튼·선택·뒤로·확정·오류·룰렛·카운트다운·시작·창 열기 | 9 |

얼음 금감/파손 후보는 **유리 충돌**, 경고 후보는 **삐걱거림** 원본이다. 실제 얼음 녹음으로 표시하지 않는다. 다이브는 원본 `knifeSlice`의 스침 효과, 피스톤은 `metalLatch`의 기계 걸쇠 효과를 활용한 후보다. 결과 징글의 우승·일반 결과 구분도 게임용 후보 이름이며 원본의 공식 구분이 아니다.

발소리·얼음·충돌 변형은 무작위로 골라 반복감을 줄이는 용도다. 연결 시 효과음 중첩 제한, 거리 감쇠, 카운트다운과 결과 징글 사이 음악 볼륨 조정이 필요하다. 특히 여러 얼음 블럭이 동시에 깨질 때 모든 소리를 겹쳐 재생하지 않도록 한다.

## 출처와 라이선스

| 배포 자료 | 제작자 | 라이선스 | 공식 페이지 |
|---|---|---|---|
| Impact Sounds | Kenney | CC0 | https://kenney.nl/assets/impact-sounds |
| Interface Sounds | Kenney | CC0 | https://kenney.nl/assets/interface-sounds |
| RPG Audio | Kenney | CC0 | https://kenney.nl/assets/rpg-audio |
| Digital Audio | Kenney | CC0 | https://kenney.nl/assets/digital-audio |
| Music Jingles | Kenney | CC0 | https://kenney.nl/assets/music-jingles |
| Snow Theme / Through the Snow | CleytonKauffman | CC0 | https://opengameart.org/content/snow-theme |
| Happy Clappy Loop | OwlishMedia | CC0 | https://opengameart.org/content/happy-clappy-loop |
| Joyfully (loop) | MintoDog | CC0 | https://opengameart.org/content/joyfully |
| Ocean splash | Thimras | CC0 | https://opengameart.org/content/ocean-splash |

`Licenses/`에 CC0 법률문과 원본 동봉 라이선스를 보관했다. 동봉 문서가 없는 OpenGameArt 자료도 위 공식 페이지의 CC0 표시를 확인했다. 원본 아카이브·전체 후보·배포 페이지 기록은 Unity Assets 밖의 `Tools/audio/SourcePacks/`, `Tools/audio/SourcePages/`에 둔다.

## 파일 검증과 변환

`audio-manifest.json` 및 엑셀로 열 수 있는 `audio-manifest.csv`에 각 파일의 추천 용도, 원본 경로, 공식 출처, 제작자, 라이선스, 길이, 샘플레이트, 채널 수, SHA256을 기록했다.

Happy Clappy Loop 원본은 32비트 float WAV이므로 게임용 파일을 **16비트 PCM WAV**로 변환했다. 길이·샘플레이트·채널 수는 유지하며 변환 정보와 원본/변환본 해시를 따로 기록했다. 나머지 42개는 원본 바이트 그대로 복사했다. Snow Theme의 실제 OGG는 48 kHz이며 소개 페이지의 44.1 kHz 설명 대신 실제 파일 수치를 목록에 기록했다.

`Tools/audio/prepare_audio.py`로 복사·변환·목록·시청 페이지를 재생성할 수 있다. Ogg 페이지 CRC/Vorbis 헤더/종료 페이지·WAV 전체 프레임·길이·원본 해시 검사를 통과했다. 기록은 `Tools/audio/verification.txt`. 이 검사는 컨테이너 무결성 검사이며 Unity 재생·음질·루프 이음새 청취 검사를 대신하지 않는다.
