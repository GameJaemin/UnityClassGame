# 복셀 펭귄 방해꾼 (Blender 5.x) - 시련 "펭귄 슬라이딩"
# 실행:
#   blender --background --factory-startup --python make_penguin.py -- --fbx <Penguin.fbx> --palette <Penguin_palette.txt> --blend <Penguin.blend> --preview <폴더>
#
# 플레이어 스킨과 몸 구조가 달라 전용 뼈대(PENGUIN_PIVOTS)를 쓴다.
# 키 약 1.4m (플레이어 절반보다 조금 큼), 통통한 달걀 체형. 배로 엎드려 미끄러질 때 플레이어 캡슐 아래쪽에 부딪힌다.

import os
import sys

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from voxel_anim import pose, rot  # noqa: E402
from voxel_common import Part, mx, run  # noqa: E402

# 부위 이름 -> (관절 위치(복셀 단위), 부모)
PENGUIN_PIVOTS = {
    "Foot_L": ((2, -1, 1), None),
    "Foot_R": ((-2, -1, 1), None),
    "Body":   ((0, 0, 1), None),
    "Head":   ((0, 0, 9), "Body"),
    "Wing_L": ((4, 0, 8), "Body"),
    "Wing_R": ((-4, 0, 8), "Body"),
}

PALETTE = {
    "Back":     (0.16, 0.19, 0.27),
    "Belly":    (1.00, 1.00, 0.97),
    "Beak":     (1.00, 0.62, 0.16),
    "Feet":     (1.00, 0.55, 0.12),
    "Eye":      (0.06, 0.06, 0.08),
    "Blush":    (1.00, 0.62, 0.70),
}


def build_parts():
    parts = []

    # 발: 주황색 납작한 발, 앞으로 길게
    for side, (x0, x1) in (("L", (1, 2)), ("R", (mx(2), mx(1)))):
        foot = Part(f"Foot_{side}", PENGUIN_PIVOTS)
        foot.box(x0, x1, -4, 0, 0, 0, "Feet")
        foot.remove(lambda x, y, z, o=(x1 if x0 > 0 else x0): x == o and y == -4)  # 발끝 바깥 모서리 둥글게
        parts.append(foot)

    # 몸통: 통통한 달걀 모양, 등은 짙은 남색, 배는 흰색으로 볼록
    body = Part("Body", PENGUIN_PIVOTS)
    body.round_box(-4, 3, -3, 3, 1, 8, "Back")
    # 위아래로 한 번 더 깎아 달걀처럼
    body.remove(lambda x, y, z: z in (1, 8) and (x in (-3, 2) and y in (-2, 2)))
    body.box(-3, 2, -3, -3, 2, 7, "Belly")
    body.box(-2, 1, -4, -4, 3, 6, "Belly")  # 볼록한 배
    body.box(-1, 0, 4, 4, 1, 2, "Back")     # 짧은 꼬리
    parts.append(body)

    # 날개(지느러미): 몸 옆에 붙은 납작한 판, 끝은 조금 좁게
    for side, x in (("L", 4), ("R", mx(4))):
        wing = Part(f"Wing_{side}", PENGUIN_PIVOTS)
        wing.box(x, x, -1, 1, 3, 7, "Back")
        wing.remove(lambda xx, y, z: z == 3 and y in (-1, 1))
        parts.append(wing)

    # 머리: 둥근 머리, 흰 얼굴 안에 큰 눈, 주황 부리
    head = Part("Head", PENGUIN_PIVOTS)
    head.round_box(-4, 3, -3, 2, 9, 13, "Back")
    head.box(-3, 2, -3, -3, 10, 12, "Belly")
    # 흰 얼굴 안쪽에 까만 2칸 눈 (하이라이트를 넣으면 얼굴색과 섞여 가는 줄처럼 보임)
    for x in (1, mx(1)):
        head.box(x, x, -3, -3, 11, 12, "Eye")
    head.paint(2, -3, 10, "Blush")
    head.paint(mx(2), -3, 10, "Blush")
    head.box(-1, 0, -4, -4, 10, 10, "Beak")
    head.box(-1, 0, -5, -5, 10, 10, "Beak")
    parts.append(head)

    return parts


# ---- 애니메이션 (뼈 축 규칙은 voxel_anim.py 참고: X+ 앞으로 숙임, Z+ 바깥으로 벌림, location Y 위아래) ----

# 엎드린 자세: 발끝을 축으로 앞으로 눕고, 몸이 땅에 묻히지 않게 Root를 띄운다
LYING_HEIGHT = 0.42


def ready():
    # 예고: 1초 동안 좌우로 뒤뚱거리며 날개를 파닥이고 발을 구름
    keys = []
    for f, s in ((0, 0), (6, 1), (12, 0), (18, -1), (24, 0)):
        lift = abs(s)
        keys.append((f, {
            "Root": pose(loc=(0, 0.05 * lift, 0)),
            "Body": rot(0, 0, 10 * s),
            "Head": rot(0, 0, -6 * s),
            "Wing_L": rot(0, 0, 15 + 30 * lift),
            "Wing_R": rot(0, 0, -15 - 30 * lift),
            "Foot_L": rot(-25 if s > 0 else 0),
            "Foot_R": rot(-25 if s < 0 else 0),
        }))
    return 24, True, keys


def sliding_pose(flap):
    return {
        "Root": pose(rot_xyz=(85, 0, 0), loc=(0, LYING_HEIGHT, 0)),
        "Body": rot(0, 0, 4 * flap),
        "Head": rot(-45),
        "Wing_L": rot(0, 0, 25 + 10 * flap),
        "Wing_R": rot(0, 0, -25 + 10 * flap),
        "Foot_L": rot(60),
        "Foot_R": rot(60),
    }


def slide():
    # 배로 엎드려 미끄러지며 날개를 비행기처럼 펴고 살짝 흔듦
    keys = [(f, sliding_pose(s)) for f, s in ((0, 0), (3, 1), (6, 0), (9, -1), (12, 0))]
    return 12, True, keys


def exit_dive():
    # 퇴장: 미끄러지던 자세에서 머리부터 바다로 뛰어듦 (로직에서 아래로 떨어뜨림), 마지막 자세 유지
    diving = {
        "Root": pose(rot_xyz=(135, 0, 0), loc=(0, LYING_HEIGHT, 0)),
        "Body": rot(0),
        "Head": rot(-10),
        "Wing_L": rot(0, 0, 10),
        "Wing_R": rot(0, 0, -10),
        "Foot_L": rot(80),
        "Foot_R": rot(80),
    }
    keys = [(0, sliding_pose(0)), (10, diving), (16, diving)]
    return 16, False, keys


PENGUIN_CLIPS = {"Ready": ready, "Slide": slide, "Exit": exit_dive}

run("Penguin", PALETTE, build_parts, clips=PENGUIN_CLIPS)
