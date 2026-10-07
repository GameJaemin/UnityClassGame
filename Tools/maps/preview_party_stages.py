"""맵 JSON 기반 비교 그림. 게임 화면 캡처가 아닌 평면 배치도다."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root = Path(__file__).resolve().parents[2]
names = ['seesaw_summit', 'blink_boulevard', 'bounce_carnival', 'hammer_junction', 'trick_garden', 'sky_picnic']
colors = dict(seesaw='#d686ff', blink='#ff995e', launcher='#ffda38', pendulum='#ff517f', move='#35dbba', sweeper='#ff517f')
labels = dict(seesaw='시소', blink='반짝 다리', launcher='발사대', pendulum='왕복 해머', move='왕복 셔틀', sweeper='회전 막대')
image = Image.new('RGB', (1230, 1020), '#10203c'); draw = ImageDraw.Draw(image)
bold = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 26)
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 18)
draw.text((30, 16), 'LION ICE · 새 파티 스테이지 6개 — 평면 배치도', font=bold, fill='white')
draw.text((30, 60), '파랑: 얼음 · 초록: 이동 안전 시작 칸 · 흰 원: 출발 위치 · 색 테두리: 기믹 범위', font=font, fill='#bed8f0')
for i, name in enumerate(names):
    m = json.loads((root / f'LionIceRun/Assets/StreamingAssets/Maps/{name}.json').read_text(encoding='utf-8'))
    ox, oy = 35+i%3*405, 122+i//3*442
    draw.text((ox, oy), m['name'], font=bold, fill='white')
    bx, by, size = ox, oy+46, 22
    for z, row in enumerate(m['rows']):
        for x, kind in enumerate(row):
            if kind != '.':
                draw.rectangle((bx+x*size+1, by+z*size+1, bx+(x+1)*size-1, by+(z+1)*size-1), fill='#42d694' if kind=='S' else '#87cfee')
    for g in m['gimmicks']:
        x, y = bx+(g['x']+.5)*size, by+(14-g['z']+.5)*size
        w, h = g['width']*size, g['depth']*size
        draw.rectangle((x-w/2, y-h/2, x+w/2, y+h/2), outline=colors[g['kind']], width=4)
    for p in [m['playerStart']]+m['botStarts']:
        x, y = bx+(p['x']+.5)*size, by+(14-p['y']+.5)*size
        draw.ellipse((x-6,y-6,x+6,y+6), fill='white', outline='#10203c', width=2)
    draw.text((ox, by+340), ' + '.join(labels[k] for k in dict.fromkeys(g['kind'] for g in m['gimmicks'])), font=font, fill='#d4e6f9')
path = root / 'docs/screenshots/party-stages-overview.png'
path.parent.mkdir(parents=True, exist_ok=True); image.save(path)
print(path)
