"""다운로드한 CC0 원본에서 게임용 후보를 복사하고 목록·무결성을 확인한다."""
from pathlib import Path
import csv
import hashlib
import json
import shutil
import struct
import wave
import html
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Tools/audio/SourcePacks'
DEST = ROOT / 'LionIceRun/Assets/Audio'
SOURCES = {
    'impact': ('Kenney', 'https://kenney.nl/assets/impact-sounds'),
    'interface': ('Kenney', 'https://kenney.nl/assets/interface-sounds'),
    'jingles': ('Kenney', 'https://kenney.nl/assets/music-jingles'),
    'rpg': ('Kenney', 'https://kenney.nl/assets/rpg-audio'),
    'digital': ('Kenney', 'https://kenney.nl/assets/digital-audio'),
    'snow': ('CleytonKauffman', 'https://opengameart.org/content/snow-theme'),
    'happy': ('OwlishMedia', 'https://opengameart.org/content/happy-clappy-loop'),
    'joyfully': ('MintoDog', 'https://opengameart.org/content/joyfully'),
    'ocean': ('Thimras', 'https://opengameart.org/content/ocean-splash'),
}
CLIPS = []

def select(pack, original, target, purpose):
    CLIPS.append((pack, original, target, purpose))

for i in range(3):
    select('impact', f'Audio/footstep_snow_{i:03}.ogg', f'SFX/Movement/snow_step_{i+1:02}.ogg', '눈·얼음 위 발소리 변형')
    select('impact', f'Audio/impactGlass_light_{i:03}.ogg', f'SFX/Ice/ice_crack_{i+1:02}.ogg', '얼음 금감 후보: 유리의 가벼운 충돌')
    select('impact', f'Audio/impactGlass_heavy_{i:03}.ogg', f'SFX/Ice/ice_break_{i+1:02}.ogg', '얼음 완전 파손 후보: 유리의 강한 충돌')
    select('impact', f'Audio/impactPunch_heavy_{i:03}.ogg', f'SFX/Collision/knockback_{i+1:02}.ogg', '회전 막대·피스톤의 강한 타격 후보')
for i in range(2):
    select('rpg', f'Audio/creak{i+1}.ogg', f'SFX/Ice/ice_warning_{i+1:02}.ogg', '깨지기 전 삐걱거림 후보')
    select('impact', f'Audio/impactSoft_heavy_{i:03}.ogg', f'SFX/Collision/landing_{i+1:02}.ogg', '착지·넘어짐의 둔탁한 충돌')
    select('impact', f'Audio/impactMetal_medium_{i:03}.ogg', f'SFX/Gimmicks/machine_hit_{i+1:02}.ogg', '기계 장치 충돌')
    select('digital', f'Audio/phaseJump{i+1}.ogg', f'SFX/Movement/jump_{i+1:02}.ogg', '가벼운 아케이드 점프 후보')
select('rpg', 'Audio/knifeSlice.ogg', 'SFX/Movement/dive_whoosh.ogg', '다이브·회전 막대 스침 후보')
select('rpg', 'Audio/metalLatch.ogg', 'SFX/Gimmicks/piston_click.ogg', '피스톤 돌출·복귀의 기계 걸쇠 후보')
for original, target, purpose in [
    ('click_001', 'ui_click', '버튼 누름'), ('select_001', 'ui_select', '캐릭터·맵 선택'),
    ('back_001', 'ui_back', '뒤로 가기'), ('confirmation_001', 'ui_confirm', '준비 완료·확정'),
    ('error_001', 'ui_error', '참가 오류·불가능한 동작'), ('tick_001', 'roulette_tick', '맵 룰렛 카드 이동'),
    ('pluck_001', 'countdown_tick', '3·2·1 및 다음 판 5초 안내 후보'),
    ('maximize_001', 'round_start', '경기 시작 후보'), ('open_001', 'panel_open', '창 열기'),
]:
    select('interface', f'Audio/{original}.ogg', f'SFX/UI/{target}.ogg', purpose)
for i, name in enumerate(['pickup', 'shield', 'super_jump', 'repair'], 1):
    select('digital', f'Audio/powerUp{i}.ogg', f'SFX/Items/{name}.ogg', f'아이템 {name} 획득·발동 후보')
for original, name, purpose in [
    ('00', 'round_win', '라운드 우승 후보'), ('01', 'round_result', '라운드 결과 후보'),
    ('02', 'series_win', '5판 합산 우승 후보'), ('03', 'series_result', '시리즈 결과 후보'),
]:
    select('jingles', f'Audio/Pizzicato jingles/jingles_PIZZI{original}.ogg', f'Jingles/{name}.ogg', purpose)
select('ocean', 'ocean_splash.ogg', 'SFX/Collision/ocean_splash.ogg', '바다 낙하 물보라')
select('snow', 'Snow Theme/BGM_Snow_Theme_Loop.ogg', 'Music/menu_snow_loop.ogg', '겨울 테마 타이틀·로비 음악 후보')
select('happy', 'happy.wav', 'Music/playful_round_loop.wav', '밝은 피아노 경기 음악 후보')
select('joyfully', 'joyfully.ogg', 'Music/party_round_loop.ogg', '170 BPM 파티·기믹 경기 음악 후보')

# Ogg 페이지 CRC 및 Vorbis 식별 패킷·마지막 granule로 컨테이너와 길이를 검사한다.
CRC_TABLE = []
for n in range(256):
    c = n << 24
    for _ in range(8):
        c = ((c << 1) ^ (0x04C11DB7 if c & 0x80000000 else 0)) & 0xffffffff
    CRC_TABLE.append(c)

def inspect(path):
    data = path.read_bytes()
    if path.suffix == '.wav':
        with wave.open(str(path), 'rb') as w:
            channels, rate, frames = w.getnchannels(), w.getframerate(), w.getnframes()
            assert len(w.readframes(frames)) == frames * channels * w.getsampwidth()
            return channels, rate, frames / rate
    offset, packet, identification, frames = 0, bytearray(), None, 0
    serial = None
    eos = False
    while offset < len(data):
        assert data[offset:offset+4] == b'OggS' and data[offset+4] == 0, path
        count = data[offset+26]
        laces = data[offset+27:offset+27+count]
        length = 27 + count + sum(laces)
        page = bytearray(data[offset:offset+length])
        assert len(page) == length, path
        expected = struct.unpack_from('<I', page, 22)[0]
        page[22:26] = b'\0' * 4
        crc = 0
        for value in page:
            crc = ((crc << 8) ^ CRC_TABLE[((crc >> 24) ^ value) & 255]) & 0xffffffff
        assert crc == expected, f'CRC: {path}'
        page_serial = struct.unpack_from('<I', page, 14)[0]
        serial = page_serial if serial is None else serial
        assert serial == page_serial, '연결된 복수 스트림은 지원하지 않음'
        granule = struct.unpack_from('<Q', page, 6)[0]
        if granule != 0xffffffffffffffff:
            frames = max(frames, granule)
        eos |= bool(page[5] & 4)
        body = 27 + count
        for size in laces:
            packet.extend(page[body:body+size])
            body += size
            if size < 255:
                if identification is None:
                    assert packet[:7] == b'\x01vorbis', path
                    identification = bytes(packet)
                packet.clear()
        offset += length
    assert eos and identification and frames > 0, path
    channels = identification[11]
    rate = struct.unpack_from('<I', identification, 12)[0]
    return channels, rate, frames / rate

records = []
for pack, original, target, purpose in CLIPS:
    source = SOURCE / (original if pack in ('happy', 'joyfully', 'ocean') else f'{pack}/{original}')
    output = DEST / target
    output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, output)
    processing = '원본 그대로 복사'
    if pack == 'happy':
        # 원본의 32비트 float WAV를 Unity에 쓰기 편한 16비트 PCM으로 변환한다.
        raw = source.read_bytes()
        assert raw[:4] == b'RIFF' and raw[8:12] == b'WAVE'
        pos, fmt, samples = 12, None, None
        while pos + 8 <= len(raw):
            size = struct.unpack_from('<I', raw, pos + 4)[0]
            chunk = raw[pos+8:pos+8+size]
            if raw[pos:pos+4] == b'fmt ':
                fmt = struct.unpack_from('<HHIIHH', chunk)
            if raw[pos:pos+4] == b'data':
                samples = np.frombuffer(chunk, dtype='<f4')
            pos += 8 + size + size % 2
        assert fmt and fmt[0] == 3 and fmt[5] == 32 and samples is not None
        assert np.isfinite(samples).all()
        peak = float(np.max(np.abs(samples)))
        gain = min(1.0, 0.999 / peak) if peak else 1.0
        pcm = np.rint(samples * gain * 32767).astype('<i2')
        with wave.open(str(output), 'wb') as w:
            w.setnchannels(fmt[1])
            w.setframerate(fmt[2])
            w.setsampwidth(2)
            w.writeframes(pcm.tobytes())
        processing = f'32비트 float WAV → 16비트 PCM, gain={gain:.6f}, 길이·샘플레이트 유지'
    channels, rate, duration = inspect(output)
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    source_digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if pack != 'happy':
        assert digest == source_digest
    author, url = SOURCES[pack]
    records.append(dict(file=target, purpose=purpose, author=author, source_page=url,
                        source_file=str(source.relative_to(ROOT)).replace('\\', '/'),
                        license='CC0-1.0', downloaded='2026-10-07', channels=channels,
                        sample_rate=rate, duration_seconds=round(duration, 4),
                        bytes=output.stat().st_size, sha256=digest,
                        source_sha256=source_digest, processing=processing))

licenses = DEST / 'Licenses'
licenses.mkdir(parents=True, exist_ok=True)
for pack in ('impact', 'interface', 'jingles', 'rpg', 'digital'):
    shutil.copyfile(SOURCE / pack / 'License.txt', licenses / f'Kenney-{pack}.txt')
shutil.copyfile(SOURCE / 'snow/Snow Theme/readme.txt', licenses / 'SnowTheme-original.txt')
shutil.copyfile(SOURCE / 'CC0-1.0.txt', licenses / 'CC0-1.0.txt')
(DEST / 'audio-manifest.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
with (DEST / 'audio-manifest.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=records[0].keys())
    writer.writeheader()
    writer.writerows(records)

parts = ['<!doctype html><html lang="ko"><meta charset="utf-8"><title>LION ICE 사운드 후보</title>',
         '<style>body{background:#101d39;color:#eef3ff;font:16px sans-serif;max-width:1050px;margin:40px auto}h1{color:#ffdf42}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:12px}article{padding:16px;background:#21375b;border-radius:12px}audio{width:100%}small{color:#b9cee6}a{color:#91dcff}</style>',
         '<h1>LION ICE 무료 사운드 후보</h1><p>CC0 · 2026-10-07 다운로드. 재생 버튼으로 비교하세요. 게임 이벤트 연결과 볼륨 조정은 아직 하지 않았습니다.</p>',
         '<p>얼음 효과음은 유리 충돌·삐걱거림을 활용한 후보입니다. 재생은 한 번에 하나씩 눌러 주세요.</p>']
for group in ('Music', 'Jingles', 'SFX/Ice', 'SFX/Movement', 'SFX/Collision', 'SFX/Gimmicks', 'SFX/Items', 'SFX/UI'):
    parts.append(f'<h2>{group}</h2><div class="grid">')
    for r in records:
        if str(Path(r['file']).parent).replace('\\', '/') != group:
            continue
        parts.append(f'<article><b>{html.escape(r["purpose"])}</b><p><small>{r["file"]} · {r["duration_seconds"]:.2f}초</small></p><audio controls preload="none" src="../../LionIceRun/Assets/Audio/{r["file"]}"></audio><p><a href="{r["source_page"]}">{r["author"]} · 원본 출처</a></p></article>')
    parts.append('</div>')
parts.append('</html>')
(ROOT / 'Tools/audio/preview.html').write_text('\n'.join(parts), encoding='utf-8')
report = f'PASS: {len(records)}개 파일, Ogg CRC/Vorbis 헤더·WAV 프레임·길이·SHA256 기록. HappyClappyLoop는 PCM16 변환, 나머지는 원본 일치\n'
report += '\n'.join(f'{r["file"]}: {r["duration_seconds"]:.4f}s, {r["sample_rate"]}Hz, {r["channels"]}ch' for r in records)
(ROOT / 'Tools/audio/verification.txt').write_text(report + '\n', encoding='utf-8')
print(report)
