# 복셀 캐릭터 공통 애니메이션 (Blender 5.x)
# 모든 캐릭터가 같은 뼈 이름을 쓰므로 여기서 만든 클립을 그대로 공유한다.
#
# 뼈는 모두 관절에서 위(+Z)를 향한다. 뼈 로컬 축 기준:
#   X 회전 +  : 앞으로 숙임 (매달린 팔·다리는 끝이 뒤로 감)
#   Y 회전    : 좌우로 돌리기 (꼬리 흔들기)
#   Z 회전 +  : 몸 바깥쪽 +X 방향으로 기울임 (Arm_L을 +로 올리면 바깥으로 벌어짐)
#   location Y: 위아래

import math

import bpy

FPS = 24
BONES = ["Root", "Body", "Head", "Arm_L", "Arm_R", "Leg_L", "Leg_R", "Tail"]


def rot(x=0.0, y=0.0, z=0.0):
    return {"rot": (x, y, z)}


def pose(rot_xyz=None, loc=None, scale=None):
    p = {}
    if rot_xyz is not None:
        p["rot"] = rot_xyz
    if loc is not None:
        p["loc"] = loc
    if scale is not None:
        p["scale"] = scale
    return p


def idle():
    keys = []
    for f, t in ((0, 0.0), (24, 1.0), (48, 0.0)):
        keys.append((f, {
            "Body": pose(scale=(1 + 0.015 * t, 1 + 0.035 * t, 1 + 0.015 * t)),
            "Head": rot(-3 * t),
            "Arm_L": rot(0, 0, 4 + 3 * t),
            "Arm_R": rot(0, 0, -4 - 3 * t),
        }))
    for f, a in ((0, 0), (12, 15), (24, 0), (36, -15), (48, 0)):
        keys.append((f, {"Tail": rot(0, a, 0)}))
    return 48, True, keys


def walk():
    # 폴가이즈처럼 종종걸음 + 좌우로 뒤뚱
    leg, arm, roll = 35, 30, 6
    keys = []
    for f, s in ((0, 1), (8, -1), (16, 1)):
        keys.append((f, {
            "Root": pose(loc=(0, 0, 0)),
            "Leg_L": rot(leg * s),
            "Leg_R": rot(-leg * s),
            "Arm_L": rot(-arm * s, 0, 6),
            "Arm_R": rot(arm * s, 0, -6),
            "Body": rot(8, 0, roll * s),
            "Head": rot(-4, 0, -roll * s * 0.6),
            "Tail": rot(10, 20 * s, 0),
        }))
    for f in (4, 12):
        keys.append((f, {
            "Root": pose(loc=(0, 0.06, 0)),
            "Leg_L": rot(0),
            "Leg_R": rot(0),
            "Arm_L": rot(0, 0, 6),
            "Arm_R": rot(0, 0, -6),
            "Body": rot(8, 0, 0),
            "Head": rot(-4, 0, 0),
            "Tail": rot(10, 0, 0),
        }))
    return 16, True, keys


def jump():
    # 찌그러졌다가 쭉 늘어나며 두 팔을 번쩍, 마지막 자세 유지
    keys = [
        (0, {
            "Body": pose(scale=(1.12, 0.85, 1.12)),
            "Arm_L": rot(0, 0, 20), "Arm_R": rot(0, 0, -20),
            "Leg_L": rot(0), "Leg_R": rot(0),
        }),
        (3, {
            "Body": pose(scale=(0.92, 1.12, 0.92)),
            "Arm_L": rot(-35, 0, 110), "Arm_R": rot(-35, 0, -110),
            "Leg_L": rot(-25), "Leg_R": rot(15),
        }),
        (6, {
            "Body": pose(scale=(1, 1.05, 1)),
            "Arm_L": rot(-25, 0, 100), "Arm_R": rot(-25, 0, -100),
            "Leg_L": rot(-20), "Leg_R": rot(10),
        }),
        (12, {
            "Body": pose(scale=(1, 1.05, 1)),
            "Arm_L": rot(-25, 0, 100), "Arm_R": rot(-25, 0, -100),
            "Leg_L": rot(-20), "Leg_R": rot(10),
        }),
    ]
    return 12, False, keys


def fall():
    # 팔을 허우적거리고 다리를 버둥거림
    keys = []
    for f, s in ((0, 1), (6, -1), (12, 1)):
        keys.append((f, {
            "Body": pose(rot_xyz=(-5, 0, 0), scale=(0.97, 1.05, 0.97)),
            "Head": rot(-8),
            "Arm_L": rot(-40 * s, 0, 100 - 15 * s),
            "Arm_R": rot(40 * s, 0, -100 - 15 * s),
            "Leg_L": rot(15 * s),
            "Leg_R": rot(-15 * s),
            "Tail": rot(0, 25 * s, 0),
        }))
    return 12, True, keys


def dive():
    # 발끝을 축으로 앞으로 몸을 던지고 두 팔을 앞으로 쭉, 마지막 자세 유지
    flying = {
        "Root": rot(80),
        "Body": pose(scale=(0.95, 1.08, 0.95)),
        "Head": rot(-25),
        "Arm_L": rot(-170, 0, 15),
        "Arm_R": rot(-170, 0, -15),
        "Leg_L": rot(20),
        "Leg_R": rot(25),
        "Tail": rot(-20),
    }
    keys = [
        (0, {name: pose(rot_xyz=(0, 0, 0)) for name in flying}),
        (4, flying),
        (10, flying),
    ]
    return 10, False, keys


CLIPS = {"Idle": idle, "Walk": walk, "Jump": jump, "Fall": fall, "Dive": dive}


def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)


def insert(pb, prop, frame):
    pb.keyframe_insert(prop, frame=frame, group=pb.name)


def create_clips(rig):
    """CLIPS를 rig의 액션으로 만든다. 반환: {이름: (길이, 반복 여부)}"""
    rig.animation_data_create()
    info = {}
    for name, build in CLIPS.items():
        length, loop, keys = build()
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        reset_pose(rig)

        # 모든 뼈를 처음과 끝 프레임에 기본 자세로 먼저 찍어서, 클립마다 모든 뼈가 키를 갖게 한다
        for frame in (0, length):
            for pb in rig.pose.bones:
                for prop in ("location", "rotation_euler", "scale"):
                    insert(pb, prop, frame)

        for frame, bones in keys:
            for bone, p in bones.items():
                pb = rig.pose.bones[bone]
                if "rot" in p:
                    pb.rotation_euler = [math.radians(a) for a in p["rot"]]
                    insert(pb, "rotation_euler", frame)
                if "loc" in p:
                    pb.location = p["loc"]
                    insert(pb, "location", frame)
                if "scale" in p:
                    pb.scale = p["scale"]
                    insert(pb, "scale", frame)
            reset_pose(rig)

        action.use_frame_range = True
        action.frame_start = 0
        action.frame_end = length
        info[name] = (length, loop)

    rig.animation_data.action = None
    reset_pose(rig)
    return info


def action_fcurves(action):
    try:
        return list(action.fcurves)
    except AttributeError:
        curves = []
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    curves.extend(bag.fcurves)
        return curves


def assign_action(obj, action):
    obj.animation_data_create()
    obj.animation_data.action = action
    if hasattr(obj.animation_data, "action_slot") and len(action.slots) > 0:
        obj.animation_data.action_slot = action.slots[0]
