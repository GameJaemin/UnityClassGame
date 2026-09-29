# 복셀 사자 캐릭터 (Blender 5.x)
# 실행:
#   blender --background --factory-startup --python make_lion.py -- --fbx <Lion.fbx> --blend <Lion.blend> --preview <폴더>

import os
import sys

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from voxel_common import Part, mx, run  # noqa: E402

# 폴가이즈 느낌의 밝은 파스텔 톤 (sRGB)
PALETTE = {
    "Fur":      (1.00, 0.78, 0.35),
    "Mane":     (0.95, 0.50, 0.16),
    "Cream":    (1.00, 0.93, 0.76),
    "Nose":     (0.33, 0.17, 0.12),
    "Eye":      (0.08, 0.08, 0.10),
    "EyeShine": (1.00, 1.00, 1.00),
    "Blush":    (1.00, 0.62, 0.66),
}


def build_parts():
    parts = []

    # 다리: 짧고 통통하게, 발바닥은 크림색
    for side, (x0, x1) in (("L", (1, 3)), ("R", (mx(3), mx(1)))):
        leg = Part(f"Leg_{side}")
        leg.box(x0, x1, -1, 1, 0, 2, "Fur")
        leg.box(x0, x1, -2, -2, 0, 0, "Cream")  # 발끝
        leg.box(x0, x1, -1, 1, 0, 0, "Cream")
        parts.append(leg)

    # 몸통: 모서리를 깎아 둥글게, 배는 크림색
    body = Part("Body")
    body.box(-5, 4, -3, 3, 3, 8, "Fur")
    body.remove(lambda x, y, z: x in (-5, 4) and y in (-3, 3))
    body.remove(lambda x, y, z: z in (3, 8) and (x in (-5, 4) or y in (-3, 3)))
    body.box(-3, 2, -3, -3, 4, 7, "Cream")
    parts.append(body)

    # 팔: 몸통 옆에 붙고 손은 크림색
    for side, (x0, x1) in (("L", (5, 6)), ("R", (mx(6), mx(5)))):
        arm = Part(f"Arm_{side}")
        arm.box(x0, x1, -1, 0, 4, 7, "Fur")
        arm.box(x0, x1, -1, 0, 4, 4, "Cream")
        parts.append(arm)

    # 머리: 큰 머리 + 갈기 + 귀 + 얼굴
    head = Part("Head")
    head.box(-5, 4, -4, 3, 9, 16, "Fur")
    head.remove(lambda x, y, z: x in (-5, 4) and y == -4)
    head.remove(lambda x, y, z: z == 9 and (x in (-5, 4) or y in (-4, 3)))

    # 갈기: 얼굴 테두리를 두르는 껍질 (옆, 뒤, 위)
    for x in range(-6, 6):
        for y in range(-3, 5):
            for z in range(9, 18):
                outer = (x in (-6, 5)) + (y == 4) + (z == 17)
                if outer == 1:
                    head.voxels[(x, y, z)] = "Mane"

    # 얼굴 테두리 갈기: 정면에서 얼굴을 둥글게 감싼다 (이마 앞머리, 턱수염 포함)
    for z in range(9, 18):
        head.voxels[(-6, -4, z)] = "Mane"
        head.voxels[(5, -4, z)] = "Mane"
        head.voxels[(-5, -4, z)] = "Mane"
        head.voxels[(4, -4, z)] = "Mane"
    for x in range(-6, 6):
        head.voxels[(x, -4, 17)] = "Mane"
        head.voxels[(x, -3, 17)] = "Mane"
    for x in range(-4, 4):
        head.voxels[(x, -4, 16)] = "Mane"   # 앞머리
        head.voxels[(x, -4, 9)] = "Mane"    # 턱수염
    head.box(-3, 2, -5, -5, 9, 9, "Mane")

    # 삐죽삐죽한 털 끝 (옆, 위)
    for z in (10, 12, 14, 16):
        head.box(-7, -7, -4, -2, z, z, "Mane")
        head.box(6, 6, -4, -2, z, z, "Mane")
    for x in range(-6, 6, 2):
        head.box(x, x, -4, -2, 18, 18, "Mane")

    # 갈기 뭉치 (옆으로, 위로) + 뒤통수 털 질감
    for x in (-7, 6):
        head.box(x, x, 0, 2, 11, 13, "Mane")
    head.box(-1, 0, 1, 2, 18, 18, "Mane")
    for x in range(-5, 5):
        for z in range(10, 17):
            if (x * 7 + z * 3) % 5 == 0 and (x + z) % 2 == 0:
                head.voxels[(x, 5, z)] = "Mane"

    # 귀 (안쪽 분홍)
    for x0 in (2, mx(3)):
        head.box(x0, x0 + 1, -1, -1, 18, 19, "Fur")
        head.paint(x0 if x0 > 0 else x0 + 1, -1, 18, "Blush")

    # 눈 (2x2, 안쪽 위에 하이라이트)
    for x0 in (1, mx(2)):
        head.box(x0, x0 + 1, -4, -4, 13, 14, "Eye")
    head.paint(1, -4, 14, "EyeShine")
    head.paint(mx(1), -4, 14, "EyeShine")

    # 볼터치
    head.paint(3, -4, 11, "Blush")
    head.paint(mx(3), -4, 11, "Blush")

    # 주둥이와 코 (얼굴 앞으로 한 칸 튀어나옴)
    head.box(-2, 1, -5, -5, 10, 11, "Cream")
    head.box(-1, 0, -5, -5, 12, 12, "Nose")
    parts.append(head)

    # 꼬리: 뒤로 뻗다가 위로 꺾이고 끝에 갈기색 털뭉치
    tail = Part("Tail")
    tail.box(-1, 0, 4, 5, 5, 5, "Fur")
    tail.box(-1, 0, 6, 6, 5, 7, "Fur")
    tail.box(-1, 0, 6, 7, 8, 9, "Mane")
    parts.append(tail)

    return parts


run("Lion", PALETTE, build_parts)
