# 복셀 호랑이 캐릭터 (Blender 5.x)
# 실행:
#   blender --background --factory-startup --python make_tiger.py -- --fbx <Tiger.fbx> --blend <Tiger.blend> --preview <폴더>
#
# 사자와 같은 부위 이름·관절 위치를 써서 애니메이션을 함께 쓸 수 있다.

import os
import sys

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from voxel_common import Part, mx, run  # noqa: E402

# 선명한 주황 털 + 검은 줄무늬 + 흰 주둥이·배 (sRGB)
PALETTE = {
    "Fur":      (1.00, 0.58, 0.18),
    "Stripe":   (0.20, 0.15, 0.14),
    "White":    (1.00, 0.97, 0.92),
    "Nose":     (0.93, 0.50, 0.52),
    "Eye":      (0.08, 0.08, 0.10),
    "EyeShine": (1.00, 1.00, 1.00),
    "Blush":    (1.00, 0.62, 0.66),
}


def build_parts():
    parts = []

    # 다리: 흰 발, 발목 위 줄무늬 한 줄
    for side, (x0, x1) in (("L", (1, 3)), ("R", (mx(3), mx(1)))):
        leg = Part(f"Leg_{side}")
        leg.box(x0, x1, -1, 1, 0, 2, "Fur")
        leg.box(x0, x1, -2, -2, 0, 0, "White")
        leg.box(x0, x1, -1, 1, 0, 0, "White")
        leg.box(x0, x1, -1, 1, 2, 2, "Stripe")
        parts.append(leg)

    # 몸통: 흰 배, 옆구리와 등에 세로 줄무늬
    body = Part("Body")
    body.round_box(-5, 4, -3, 3, 3, 8, "Fur")
    body.box(-3, 2, -3, -3, 4, 7, "White")
    for x in (-5, 4):
        for y in (-1, 1):
            for z in range(5, 8):
                body.paint(x, y, z, "Stripe")
    for x in (-3, 2):
        for z in range(5, 8):
            body.paint(x, 3, z, "Stripe")
    for x in (-1, 0):
        for z in range(4, 7):
            body.paint(x, 3, z, "Stripe")
    parts.append(body)

    # 팔: 흰 손, 팔 가운데 줄무늬 한 줄
    for side, (x0, x1) in (("L", (5, 6)), ("R", (mx(6), mx(5)))):
        arm = Part(f"Arm_{side}")
        arm.box(x0, x1, -1, 0, 4, 7, "Fur")
        arm.box(x0, x1, -1, 0, 4, 4, "White")
        arm.box(x0, x1, -1, 0, 6, 6, "Stripe")
        parts.append(arm)

    # 머리: 둥근 머리, 흰 볼털과 주둥이, 이마와 옆머리 줄무늬
    head = Part("Head")
    head.round_box(-5, 4, -4, 3, 9, 16, "Fur")

    # 옆으로 삐져나온 흰 볼털
    for x in (-6, 5):
        head.box(x, x, -3, -2, 10, 12, "White")
    head.box(-4, -3, -4, -4, 10, 11, "White")
    head.box(2, 3, -4, -4, 10, 11, "White")

    # 이마 줄무늬 (가운데 한 줄 + 양옆 두 줄)
    head.paint(-1, -4, 16, "Stripe")
    head.paint(0, -4, 16, "Stripe")
    for x in (-3, 2):
        head.paint(x, -4, 15, "Stripe")
        head.paint(x, -4, 16, "Stripe")
    for y in range(-3, 1):
        head.paint(-1, y, 16, "Stripe")
        head.paint(0, y, 16, "Stripe")

    # 옆머리와 뒤통수 줄무늬
    for x in (-5, 4):
        for y in (-1, 1):
            for z in range(12, 15):
                head.paint(x, y, z, "Stripe")
    for z in (11, 14):
        for x in range(-3, 3):
            head.paint(x, 3, z, "Stripe")

    # 귀: 앞은 주황 + 안쪽 흰색, 뒤는 검정
    for x0, inner, outer in ((2, 2, 3), (mx(3), mx(2), mx(3))):
        head.box(x0, x0 + 1, -1, -1, 17, 18, "Fur")
        head.box(x0, x0 + 1, 0, 0, 17, 18, "Stripe")
        head.remove(lambda x, y, z, o=outer: x == o and z == 18)
        head.paint(inner, -1, 17, "White")

    # 눈 (2x2, 안쪽 위에 하이라이트) + 눈 위 흰 점
    for x0 in (1, mx(2)):
        head.box(x0, x0 + 1, -4, -4, 13, 14, "Eye")
    head.paint(1, -4, 14, "EyeShine")
    head.paint(mx(1), -4, 14, "EyeShine")
    head.paint(2, -4, 15, "White")
    head.paint(mx(2), -4, 15, "White")

    # 볼터치
    head.paint(3, -4, 12, "Blush")
    head.paint(mx(3), -4, 12, "Blush")

    # 주둥이와 분홍 코
    head.box(-2, 1, -5, -5, 10, 11, "White")
    head.box(-1, 0, -5, -5, 12, 12, "Nose")
    parts.append(head)

    # 꼬리: 길게 뒤로 뻗다가 위로, 줄무늬 두 줄과 검은 끝
    tail = Part("Tail")
    tail.box(-1, 0, 4, 6, 5, 5, "Fur")
    tail.box(-1, 0, 7, 7, 5, 9, "Fur")
    tail.box(-1, 0, 5, 5, 5, 5, "Stripe")
    tail.box(-1, 0, 7, 7, 7, 7, "Stripe")
    tail.box(-1, 0, 7, 7, 9, 9, "Stripe")
    parts.append(tail)

    return parts


run("Tiger", PALETTE, build_parts)
