"""파티 서바이벌 스테이지 6개. 기존 사용자 맵은 수정하지 않는다."""
import json
import uuid
from pathlib import Path

FOLDER = Path(__file__).resolve().parents[2] / 'LionIceRun/Assets/StreamingAssets/Maps'
def device(kind, x, z, width, depth, **values):
    return dict(kind=kind, x=x, z=z, width=width, depth=depth, **values)

def save(file, name, theme, description, rectangles, devices):
    cells = [['.' for _ in range(15)] for _ in range(15)]
    for left, bottom, right, top in rectangles:
        for z in range(bottom, top + 1):
            for x in range(left, right + 1):
                cells[z][x] = 'I'
    # 두 대피 구역은 기믹 밖의 정적 얼음에 두고, 이동 안전 경로의 시작점으로 쓴다.
    for x, z in [(2, 5), (12, 9)]:
        assert cells[z][x] == 'I'
        cells[z][x] = 'S'
    spawns = [(2, 2), (12, 2), (2, 12), (12, 12)]
    for x, z in spawns:
        assert cells[z][x] == 'I'
    payload = dict(version=1, name=name, theme=theme, description=description, width=15, depth=15,
                   rows=[''.join(row) for row in reversed(cells)], playerStart=dict(x=2, y=2),
                   botStarts=[dict(x=x, y=z) for x, z in spawns[1:]], gimmicks=devices)
    path = FOLDER / (file + '.json')
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    print(file, name, len(devices), 'devices')

ring = [(1, 1, 3, 13), (11, 1, 13, 13), (4, 1, 10, 3), (4, 11, 10, 13)]
save('seesaw_summit', '시소 눈마루', 'candy', '반대 타이밍으로 기울어지는 두 시소. 중앙에서 균형을 잡거나 넓은 외곽으로 돌아가요.',
     ring + [(4, 5, 10, 6), (4, 8, 10, 9)],
     [device('seesaw', 7, 5.5, 7, 2, period=7, tiltAngle=18), device('seesaw', 7, 8.5, 7, 2, period=7, phase=3.5, tiltAngle=18)])
save('blink_boulevard', '반짝 다리 거리', 'clock', '빨간 경고등이 켜지면 다리가 잠시 사라져요. 위아래 다리가 교대로 열리니 타이밍을 골라요.',
     ring + [(4, 5, 10, 6), (4, 8, 10, 9), (7, 7, 7, 7)],
     [device('blink', 7, 5.5, 7, 2, period=8, activeDuration=5, phase=0), device('blink', 7, 8.5, 7, 2, period=8, activeDuration=5, phase=4)])
save('bounce_carnival', '통통 점프 축제', 'candy', '노란 발사대를 밟으면 위로 튀어 올라요. 중앙 회전 막대를 뛰어넘고 좁은 착지 칸을 고르세요.',
     ring + [(4, 6, 10, 8), (6, 4, 8, 10), (5, 5, 5, 5), (9, 9, 9, 9)],
     [device('launcher', 4, 7, 1, 1, liftSpeed=8), device('launcher', 10, 7, 1, 1, liftSpeed=8),
      device('launcher', 7, 4, 1, 1, liftSpeed=7), device('launcher', 7, 10, 1, 1, liftSpeed=7),
      device('sweeper', 7, 7, 5, 0.3, speed=28, hitSpeed=9)])
save('hammer_junction', '해머 교차로', 'factory', '네 방향에서 흔들리는 큰 해머. 해머가 멀어지는 순간 교차로를 건너고 대피 칸으로 빠져요.',
     ring + [(4, 6, 10, 8), (6, 4, 8, 10)],
     [device('pendulum', 5, 7, 1.2, 1, period=5, tiltAngle=60, angle=0, hitSpeed=10),
      device('pendulum', 9, 7, 1.2, 1, period=5, phase=2.5, tiltAngle=60, angle=0, hitSpeed=10),
      device('pendulum', 7, 5, 1.2, 1, period=6, tiltAngle=55, angle=90, hitSpeed=9),
      device('pendulum', 7, 9, 1.2, 1, period=6, phase=3, tiltAngle=55, angle=90, hitSpeed=9)])
# 폭넓은 양쪽 출발 섬을 좁은 두 경로와 중앙 피난 섬으로 연결한다.
islands = [(1, 1, 3, 13), (11, 1, 13, 13), (4, 2, 10, 2), (4, 12, 10, 12),
           (4, 5, 6, 6), (8, 8, 10, 9), (6, 7, 8, 7), (7, 4, 7, 10)]
save('trick_garden', '장난감 균형 정원', 'garden', '시소 지름길과 반짝 다리, 중앙 해머를 함께 피하는 생존 정원. 길 끝의 작은 섬에서 쉬어 가요.',
     islands, [device('seesaw', 5, 5.5, 3, 2, period=6, tiltAngle=22),
               device('blink', 9, 8.5, 3, 2, period=7, activeDuration=4.5),
               device('pendulum', 7, 7, 1, 1, period=5.5, tiltAngle=50, angle=90)])
save('sky_picnic', '구름 위 피크닉', 'lagoon', '왕복 셔틀과 시소 섬을 갈아타요. 중앙 발사대에서 점프해 돌아오는 다리의 타이밍을 맞춰요.',
     ring + [(5, 5, 7, 7), (8, 8, 10, 10), (7, 8, 7, 10)],
     [device('move', 6, 6, 3, 3, dx=1, period=7), device('seesaw', 9, 9, 3, 3, period=8, tiltAngle=20),
      device('launcher', 7, 9, 1, 1, liftSpeed=7)])
