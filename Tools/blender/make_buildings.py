# 복셀 건축물 (Blender 5.x) - 맵 주변 배경 장식용, 애니메이션 없음
# 실행:
#   blender --background --factory-startup --python make_buildings.py -- [--fbx-dir <폴더>] [--palette <Buildings_palette.txt>] [--blend <Buildings.blend>] [--preview <폴더>] [--reference <Lion.blend>]
#
# 복셀 1칸 = 0.2m (voxel_props.PROP_V). 원점 = 바닥 가운데, 정면 = Blender -Y (Unity +Z).
# 색은 모든 건축물이 같은 색상표(Buildings_*)를 써서 Unity에서 머티리얼을 함께 쓴다.

import math
import os
import sys

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from voxel_common import mx  # noqa: E402
from voxel_props import Prop, noise, run_props  # noqa: E402

PALETTE = {
    "Snow":      (0.96, 0.98, 1.00),
    "SnowShade": (0.80, 0.88, 0.96),
    "Ice":       (0.60, 0.85, 0.97),
    "IceLight":  (0.78, 0.93, 1.00),
    "IceDeep":   (0.38, 0.68, 0.90),
    "Wood":      (0.74, 0.49, 0.29),
    "WoodDark":  (0.50, 0.31, 0.18),
    "Roof":      (0.90, 0.32, 0.34),
    "Paint":     (0.98, 0.97, 0.94),
    "Stone":     (0.58, 0.63, 0.71),
    "StoneDark": (0.42, 0.47, 0.56),
    "Light":     (1.00, 0.84, 0.38),
    "Navy":      (0.16, 0.20, 0.32),
    "Pink":      (0.98, 0.47, 0.70),
    "Purple":    (0.60, 0.50, 0.92),
    "Yellow":    (1.00, 0.80, 0.22),
    "Fish":      (0.62, 0.74, 0.84),
}


def ax(x):
    """좌우 대칭 무늬용: 왼쪽 복셀을 오른쪽 짝의 x로 바꿈"""
    return x if x >= 0 else mx(x)


# ---------------------------------------------------------------- 이글루
def igloo():
    """눈 벽돌 반구 + 앞쪽 굴 입구 + 얼음 창문 + 작은 분홍 깃발. 지름 약 4.4m, 높이 약 3m"""
    p = Prop("Igloo")
    R = 11
    p.dome(0, 0, 0, R, "Snow")

    # 굴 입구 (정면 -Y로 튀어나옴), 위 모서리 둥글게
    p.box(-3, 2, -14, -7, 0, 5, "Snow")
    p.remove(lambda x, y, z: y <= -7 and z == 5 and x in (-3, 2))

    # 눈 벽돌 줄눈: 3칸마다 가로줄, 층마다 엇갈린 세로줄
    def joint(x, y, z, c):
        if c != "Snow" or z == 0:
            return False
        if z % 3 == 2:
            return True
        if y <= -8:  # 굴은 세로줄을 x 기준으로
            return (ax(x) + (z // 3) % 2 * 2) % 4 == 3
        if z >= 9:
            return False
        r = math.hypot(x + 0.5, y + 0.5)
        u = math.atan2(y + 0.5, x + 0.5) * r / 4.0 + (z // 3) % 2 * 0.5
        return (u % 1.0) < 0.24
    p.recolor(joint, "SnowShade")

    # 입구: 안쪽으로 파고 깊은 곳은 어둡게, 위쪽은 아치
    p.remove(lambda x, y, z: -2 <= x <= 1 and -14 <= y <= -10 and z <= 3)
    p.remove(lambda x, y, z: -1 <= x <= 0 and -14 <= y <= -10 and z == 4)
    p.box(-2, 1, -9, -9, 0, 3, "Navy")
    p.box(-1, 0, -9, -9, 4, 4, "Navy")

    # 오른쪽 옆 얼음 창문
    p.recolor(lambda x, y, z, c: x >= 7 and -2 <= y <= 1 and 4 <= z <= 6, "Ice")

    # 꼭대기 깃발
    p.box(0, 0, 0, 0, 10, 15, "WoodDark")
    p.box(1, 3, 0, 0, 13, 15, "Pink")
    p.remove(lambda x, y, z: x == 3 and y == 0 and z in (13, 15))  # 깃발 끝 삼각형
    return p


# ---------------------------------------------------------------- 등대
def lighthouse():
    """바위섬 위 빨강·흰 줄무늬 등대. 전망대 난간, 노란 불빛, 빨간 지붕. 높이 약 8.8m"""
    p = Prop("Lighthouse")

    # 바위섬: 울퉁불퉁한 2단
    p.cylinder(0, 0, 9.0, 0, 1, "Stone")
    p.cylinder(0, 0, 7.5, 2, 2, "Stone")
    p.remove(lambda x, y, z: math.hypot(x + 0.5, y + 0.5) > (7.5 if z == 1 else 6.0) and noise(x, y, z, 1) < 0.45)
    p.recolor(lambda x, y, z, c: c == "Stone" and noise(x, y, z, 2) > 0.62, "StoneDark")

    # 탑: 아래 넓고 위로 갈수록 좁아짐, 5칸 줄무늬
    p.cylinder(0, 0, 5.5, 3, 30, "Paint", r_top=4.0)
    p.recolor(lambda x, y, z, c: c == "Paint" and 3 <= z <= 30 and ((z - 3) // 5) % 2 == 1, "Roof")

    # 문과 작은 창문 (정면)
    p.paint_front(range(-1, 1), range(3, 7), "Navy")
    p.paint_front(range(-1, 1), range(13, 15), "Navy")
    p.paint_front(range(-1, 1), range(22, 24), "Navy")

    # 전망대 바닥 + 난간 (기둥은 한 칸 건너)
    p.cylinder(0, 0, 6.5, 31, 31, "Navy")
    p.ring(0, 0, 6.5, 5.5, 32, 33, "Navy")
    p.remove(lambda x, y, z: z == 32 and math.hypot(x + 0.5, y + 0.5) > 5.5 and (ax(x) + ax(y)) % 2 == 1)

    # 등불 방: 노란 유리 + 남색 창살 4개
    p.cylinder(0, 0, 3.5, 32, 36, "Light")
    p.recolor(lambda x, y, z, c: c == "Light" and (z == 32 or x in (-1, 0) or y in (-1, 0)) and
              math.hypot(x + 0.5, y + 0.5) > 2.5, "Navy")

    # 지붕: 넓은 챙 + 뾰족한 원뿔 + 꼭지
    p.cylinder(0, 0, 4.5, 37, 37, "Roof")
    p.cylinder(0, 0, 3.8, 38, 41, "Roof", r_top=0.8)
    p.box(-1, 0, -1, 0, 42, 43, "Navy")
    return p


# ---------------------------------------------------------------- 낚시 오두막
def fishing_hut():
    """나무 기둥 위 판자 바닥, 통나무 오두막, 눈 덮인 빨간 지붕, 굴뚝, 생선 건조대, 통"""
    p = Prop("FishingHut")

    # 기둥 + 판자 바닥
    for x in (-10, -1, 8):
        for y in (-9, 7):
            p.box(x, x + 1, y, y + 1, 0, 1, "WoodDark")
    p.box(-10, 9, -9, 8, 2, 3, "Wood")
    p.recolor(lambda x, y, z, c: z == 3 and ax(x) % 3 == 0, "WoodDark")

    # 통나무 벽: 3칸마다 짙은 줄, 모서리 기둥
    p.box(-7, 6, -3, 6, 4, 14, "Wood")
    p.recolor(lambda x, y, z, c: 4 <= z <= 14 and -3 <= y <= 6 and -7 <= x <= 6 and
              (z % 3 == 0 or (x in (-7, 6) and y in (-3, 6))), "WoodDark")

    # 맞배지붕 (용마루는 x 방향): 한 층씩 안으로
    for k in range(7):
        p.box(-8, 7, -5 + k, 8 - k, 15 + k, 15 + k, "Roof")
    p.cap_snow("Snow", only=["Roof"])
    p.recolor(lambda x, y, z, c: c == "Snow" and z <= 16 and noise(x, y, z, 3) < 0.5, "Roof")  # 처마 끝은 눈이 듬성듬성
    # 처마 끝 고드름
    for x in range(-8, 8):
        if noise(x, 0, 0, 4) < 0.45:
            p.box(x, x, -5, -5, 14 - (1 if noise(x, 1, 0, 4) < 0.4 else 0), 14, "Ice")

    # 굴뚝
    p.box(2, 3, 3, 4, 15, 23, "Stone")
    p.box(1, 4, 2, 5, 24, 24, "StoneDark")
    p.box(2, 3, 3, 4, 25, 25, "Snow")

    # 문(손잡이), 창문(따뜻한 불빛 + 나무틀), 옆 창문
    p.box(-2, 0, -3, -3, 4, 10, "WoodDark")
    p.box(0, 0, -4, -4, 7, 7, "Yellow")
    p.box(2, 5, -4, -4, 7, 11, "WoodDark")
    p.remove(lambda x, y, z: y == -4 and 3 <= x <= 4 and 8 <= z <= 10)
    p.box(3, 4, -3, -3, 8, 10, "Light")
    p.box(6, 6, 1, 3, 8, 10, "Light")

    # 생선 건조대 (왼쪽 앞)
    p.box(-9, -9, -8, -8, 4, 10, "Wood")
    p.box(-9, -9, -4, -4, 4, 10, "Wood")
    p.box(-9, -9, -8, -4, 10, 10, "Wood")
    for y in (-7, -5):
        p.box(-9, -9, y, y, 7, 9, "Fish")
        p.box(-9, -9, y, y, 6, 6, "Navy")  # 꼬리

    # 통 (오른쪽 앞)
    p.cylinder(7.0, -6.0, 1.6, 4, 7, "Wood")
    p.recolor(lambda x, y, z, c: c == "Wood" and z in (4, 7) and math.hypot(x + 0.5 - 7, y + 0.5 + 6) <= 1.7 and z >= 4, "WoodDark")

    # 뒤쪽 눈 더미
    p.dome(-5.5, 6.5, 4, 2.6, "Snow")
    return p


# ---------------------------------------------------------------- 얼음 성
def ice_castle():
    """얼음 벽돌 성: 네 모서리 탑(보라 원뿔 지붕, 분홍 깃발), 성벽 톱니, 성문, 가운데 본성과 분홍 첨탑"""
    p = Prop("IceCastle")
    p.box(-13, 12, -11, 12, 0, 0, "IceDeep")

    # 성벽 + 톱니
    p.box(-8, 7, -9, -7, 1, 12, "Ice")   # 앞
    p.box(-8, 7, 8, 10, 1, 12, "Ice")    # 뒤
    p.box(-12, -10, -6, 7, 1, 12, "Ice")  # 왼쪽
    p.box(9, 11, -6, 7, 1, 12, "Ice")     # 오른쪽
    for x in range(-8, 8):
        if ax(x) % 2 == 0:
            p.box(x, x, -9, -7, 13, 13, "Ice")
            p.box(x, x, 8, 10, 13, 13, "Ice")
    for y in range(-6, 8):
        if y % 2 == 0:
            p.box(-12, -10, y, y, 13, 13, "Ice")
            p.box(9, 11, y, y, 13, 13, "Ice")

    # 성문 아치 (앞 성벽을 뚫음)
    p.remove(lambda x, y, z: -9 <= y <= -7 and ((-2 <= x <= 1 and 1 <= z <= 6) or (-1 <= x <= 0 and z == 7)))

    # 본성 + 톱니 + 문·창문
    p.box(-6, 5, -2, 6, 1, 17, "Ice")
    for x in range(-6, 6):
        for y in range(-2, 7):
            edge = x in (-6, 5) or y in (-2, 6)
            if edge and (ax(x) + y) % 2 == 0:
                p.box(x, x, y, y, 18, 18, "Ice")
    p.box(-2, 1, -2, -2, 1, 4, "Navy")
    p.box(-1, 0, -2, -2, 5, 5, "Navy")
    for x in (-4, 3):
        p.box(min(x, x + (1 if x > 0 else -1)), max(x, x + (1 if x > 0 else -1)), -2, -2, 10, 12, "Navy")

    # 얼음 벽돌 무늬: 2층씩 엇갈린 4칸 벽돌을 밝은 얼음으로 섞음
    def brick(x, y, z, c):
        if c != "Ice":
            return False
        course = z // 2
        block = (ax(x) + y + (course % 2) * 2) // 4
        return noise(block, course, 0, 5) > 0.55
    p.recolor(brick, "IceLight")

    # 본성 첨탑
    p.cylinder(0, 2, 3.0, 18, 22, "Ice")
    p.cylinder(0, 2, 3.6, 23, 23, "Pink")
    p.cylinder(0, 2, 3.0, 24, 31, "Pink", r_top=0.5)
    p.box(0, 0, 2, 2, 28, 35, "Navy")
    p.box(1, 3, 2, 2, 33, 35, "Yellow")

    # 네 모서리 탑: 원기둥 + 보라 원뿔 지붕 + 바깥쪽으로 나부끼는 깃발
    for cx in (-10.0, 10.0):
        for cy in (-7.0, 8.0):
            p.cylinder(cx, cy, 3.6, 1, 20, "Ice")
            p.recolor(lambda x, y, z, c, cx=cx, cy=cy: c in ("Ice", "IceLight") and z % 4 == 0 and
                      math.hypot(x + 0.5 - cx, y + 0.5 - cy) <= 3.7, "IceDeep")  # 탑 띠
            p.cylinder(cx, cy, 4.4, 21, 21, "Purple")
            p.cylinder(cx, cy, 3.8, 22, 29, "Purple", r_top=0.6)
            px = 9 if cx > 0 else mx(9)
            py = int(math.floor(cy))
            p.box(px, px, py, py, 26, 33, "Navy")  # 원뿔 끝이 가늘어 깃대를 안쪽까지 박음
            if cx > 0:
                p.box(px + 1, px + 3, py, py, 31, 33, "Pink")
            else:
                p.box(px - 3, px - 1, py, py, 31, 33, "Pink")
    return p


def build_props():
    return [igloo(), lighthouse(), fishing_hut(), ice_castle()]


run_props("Buildings", PALETTE, build_props)
