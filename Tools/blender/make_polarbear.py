# 복셀 북극곰 캐릭터 (Blender 5.x)
# 실행:
#   blender --background --factory-startup --python make_polarbear.py -- --fbx <PolarBear.fbx> --blend <PolarBear.blend> --preview <폴더>
#
# 사자와 같은 부위 이름·관절 위치를 써서 애니메이션을 함께 쓸 수 있다.

import os
import sys

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from voxel_common import Part, mx, run  # noqa: E402

# 하얀 털 + 얼음 맵에 어울리는 파란 목도리 (sRGB)
PALETTE = {
    "Fur":         (0.97, 0.98, 1.00),
    "Belly":       (0.86, 0.92, 0.98),
    "Muzzle":      (1.00, 0.96, 0.90),
    "Nose":        (0.12, 0.12, 0.15),
    "Eye":         (0.08, 0.08, 0.10),
    "EyeShine":    (1.00, 1.00, 1.00),
    "Blush":       (1.00, 0.62, 0.66),
    "Scarf":       (0.30, 0.62, 0.95),
    "ScarfStripe": (1.00, 1.00, 1.00),
}


def build_parts():
    parts = []

    # 다리: 사자보다 조금 더 굵게 보이도록 발끝을 앞으로
    for side, (x0, x1) in (("L", (1, 3)), ("R", (mx(3), mx(1)))):
        leg = Part(f"Leg_{side}")
        leg.box(x0, x1, -1, 1, 0, 2, "Fur")
        leg.box(x0, x1, -2, -2, 0, 0, "Fur")
        parts.append(leg)

    # 몸통: 둥글고 통통하게, 배는 살짝 푸른 흰색, 목에 파란 목도리
    body = Part("Body")
    body.round_box(-5, 4, -3, 3, 3, 8, "Fur")
    body.box(-3, 2, -3, -3, 4, 6, "Belly")
    body.box(-5, 4, -3, 3, 8, 8, "Scarf")
    body.remove(lambda x, y, z: z == 8 and x in (-5, 4) and y in (-3, 3))
    # 앞으로 늘어진 목도리 끝 (흰 줄무늬)
    body.box(1, 2, -4, -4, 5, 8, "Scarf")
    body.box(1, 2, -4, -4, 6, 6, "ScarfStripe")
    parts.append(body)

    # 팔
    for side, (x0, x1) in (("L", (5, 6)), ("R", (mx(6), mx(5)))):
        arm = Part(f"Arm_{side}")
        arm.box(x0, x1, -1, 0, 4, 7, "Fur")
        parts.append(arm)

    # 머리: 갈기 없이 둥근 머리, 앞으로 나온 주둥이, 둥근 귀
    head = Part("Head")
    head.round_box(-5, 4, -4, 3, 9, 16, "Fur")
    # 윗면을 한 번 더 깎아 동글동글하게
    head.remove(lambda x, y, z: z == 16 and (x in (-4, 3) and y in (-3, 2)))

    # 귀: 머리 위 양옆의 작은 반원, 앞면 안쪽 아래만 분홍
    for x0, inner, outer in ((2, 2, 3), (mx(3), mx(2), mx(3))):
        head.box(x0, x0 + 1, -1, 0, 17, 18, "Fur")
        head.remove(lambda x, y, z, o=outer: x == o and z == 18)
        head.paint(inner, -1, 17, "Blush")

    # 눈: 작고 동그란 1x2 (위에 하이라이트)
    for x in (2, mx(2)):
        head.box(x, x, -4, -4, 13, 14, "Eye")
        head.paint(x, -4, 14, "EyeShine")

    # 볼터치
    head.paint(3, -4, 11, "Blush")
    head.paint(mx(3), -4, 11, "Blush")

    # 주둥이: 두 칸 앞으로 길게, 끝에 큰 까만 코
    head.box(-2, 1, -5, -5, 10, 12, "Muzzle")
    head.box(-1, 0, -6, -6, 11, 12, "Muzzle")
    head.box(-1, 0, -6, -6, 12, 12, "Nose")
    head.box(-1, 0, -5, -5, 12, 12, "Nose")
    parts.append(head)

    # 꼬리: 짧고 동그란 꼬리
    tail = Part("Tail")
    tail.box(-1, 0, 4, 4, 5, 6, "Fur")
    parts.append(tail)

    return parts


run("PolarBear", PALETTE, build_parts)
