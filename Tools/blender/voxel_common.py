# 복셀 캐릭터 공통 코드 (Blender 5.x)
# 캐릭터 스크립트(make_lion.py 등)가 PALETTE와 build_parts()를 정의하고 run()을 부른다.
#
# 복셀 1칸 = 0.1m, 캐릭터는 -Y 방향(Blender 정면)을 바라본다. Unity로 내보내면 +Z를 바라본다.
# 부위마다 오브젝트를 나누고 원점을 관절 위치에 둬서 Unity Animation 창에서 회전만으로 움직일 수 있게 한다.
# 모든 캐릭터는 같은 부위 이름과 관절 위치(PIVOTS)를 써서 애니메이션을 함께 쓸 수 있게 한다.

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector

V = 0.1

# 공통 뼈대: 부위 이름 -> (관절 위치(복셀 단위), 부모)
PIVOTS = {
    "Leg_L": ((2.5, 0.5, 3), None),
    "Leg_R": ((-2.5, 0.5, 3), None),
    "Body":  ((0, 0.5, 3), None),
    "Arm_L": ((5, 0, 8), "Body"),
    "Arm_R": ((-5, 0, 8), "Body"),
    "Head":  ((0, 0, 9), "Body"),
    "Tail":  ((0, 4, 5.5), "Body"),
}


def mx(x):
    """x 좌표 좌우 대칭 (복셀 x는 [x, x+1] 구간이라 -x-1이 반대편)"""
    return -x - 1


class Part:
    def __init__(self, name):
        self.name = name
        self.pivot, self.parent = PIVOTS[name]
        self.voxels = {}

    def box(self, x0, x1, y0, y1, z0, z1, color):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    self.voxels[(x, y, z)] = color

    def round_box(self, x0, x1, y0, y1, z0, z1, color):
        """네 세로 모서리와 위아래 테두리를 깎은 둥근 상자"""
        self.box(x0, x1, y0, y1, z0, z1, color)
        self.remove(lambda x, y, z: x in (x0, x1) and y in (y0, y1))
        self.remove(lambda x, y, z: z in (z0, z1) and (x in (x0, x1) or y in (y0, y1)))

    def paint(self, x, y, z, color):
        if (x, y, z) in self.voxels:
            self.voxels[(x, y, z)] = color

    def remove(self, predicate):
        for key in [k for k in self.voxels if predicate(*k)]:
            del self.voxels[key]


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def make_materials(prefix, palette):
    materials = {}
    for name, rgb in palette.items():
        mat = bpy.data.materials.new(f"{prefix}_{name}")
        linear = tuple(srgb_to_linear(c) for c in rgb) + (1.0,)
        mat.diffuse_color = linear
        try:
            mat.use_nodes = True
        except AttributeError:
            pass
        bsdf = mat.node_tree.nodes.get("Principled BSDF") if mat.node_tree else None
        if bsdf:
            bsdf.inputs["Base Color"].default_value = linear
            bsdf.inputs["Roughness"].default_value = 0.6
        materials[name] = mat
    return materials


NEIGHBORS = [
    ((1, 0, 0), [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)]),
    ((-1, 0, 0), [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)]),
    ((0, 1, 0), [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)]),
    ((0, -1, 0), [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]),
    ((0, 0, 1), [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]),
    ((0, 0, -1), [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)]),
]


def build_mesh(part, materials):
    """바깥으로 드러난 면만 만드는 복셀 메시. 좌표는 관절(원점) 기준."""
    used = sorted(set(part.voxels.values()))
    index_of = {name: i for i, name in enumerate(used)}
    px, py, pz = part.pivot

    bm = bmesh.new()
    verts = {}

    def vert(x, y, z):
        key = (x, y, z)
        if key not in verts:
            verts[key] = bm.verts.new(((x - px) * V, (y - py) * V, (z - pz) * V))
        return verts[key]

    for (x, y, z), color in part.voxels.items():
        for (dx, dy, dz), corners in NEIGHBORS:
            if (x + dx, y + dy, z + dz) in part.voxels:
                continue
            face = bm.faces.new([vert(x + cx, y + cy, z + cz) for cx, cy, cz in corners])
            face.material_index = index_of[color]

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    mesh = bpy.data.meshes.new(part.name)
    bm.to_mesh(mesh)
    bm.free()
    for name in used:
        mesh.materials.append(materials[name])

    obj = bpy.data.objects.new(part.name, mesh)
    obj.location = Vector(part.pivot) * V
    return obj


def build_character(name, palette, parts):
    """부위별 복셀 메시를 하나로 합치고, 부위마다 뼈 하나가 100% 잡는 리그를 만든다.
    오브젝트 이름은 캐릭터와 상관없이 Rig / Mesh로 통일해서 Unity 애니메이션 경로를 같게 한다."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()

    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)

    # 뼈대: Root(발바닥) 아래에 부위별 뼈, 모두 관절에서 위(+Z)를 향함
    armature = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", armature)
    collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    root_bone = armature.edit_bones.new("Root")
    root_bone.head = (0, 0, 0)
    root_bone.tail = (0, 0, 0.15)
    for part in parts:
        bone = armature.edit_bones.new(part.name)
        bone.head = Vector(part.pivot) * V
        bone.tail = bone.head + Vector((0, 0, 0.15))
        bone.roll = 0
    for part in parts:
        armature.edit_bones[part.name].parent = armature.edit_bones[part.parent or "Root"]
    bpy.ops.object.mode_set(mode="OBJECT")

    # 부위 메시: 해당 뼈 이름의 버텍스 그룹에 가중치 1
    materials = make_materials(name, palette)
    meshes = []
    for part in parts:
        obj = build_mesh(part, materials)
        collection.objects.link(obj)
        group = obj.vertex_groups.new(name=part.name)
        group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
        meshes.append(obj)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    mesh.name = "Mesh"
    mesh.data.name = f"{name}Mesh"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    mesh.parent = rig
    modifier = mesh.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    return rig, mesh


def export_fbx(rig, mesh, path):
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
    )


def setup_render(scene):
    """Workbench 렌더 설정과 카메라를 한 번만 만든다."""
    if scene.camera is not None:
        return scene.camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.film_transparent = False
    # 기본 AgX는 흰색을 회색으로 누르므로 Unity 화면에 가까운 Standard로 본다
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = 0.6  # Workbench 스튜디오 조명이 어두워 흰색이 회색으로 보이는 것 보정
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "MATERIAL"
    shading.show_shadows = True
    shading.shadow_intensity = 0.35
    shading.show_cavity = True
    shading.cavity_type = "WORLD"

    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (srgb_to_linear(0.62), srgb_to_linear(0.85), srgb_to_linear(0.97))
    scene.world = world

    cam_data = bpy.data.cameras.new("PreviewCamera")
    cam_data.lens = 70
    camera = bpy.data.objects.new("PreviewCamera", cam_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def render_previews(folder, file_prefix):
    scene = bpy.context.scene
    camera = setup_render(scene)
    scene.render.resolution_x = 700
    scene.render.resolution_y = 900

    target = bpy.data.objects.new("CameraTarget", None)
    target.location = (0, 0, 1.0)
    scene.collection.objects.link(target)
    track = camera.constraints.new("TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"

    os.makedirs(folder, exist_ok=True)
    # 정면 기준 각도 (0 = 정면, 캐릭터는 -Y를 바라봄)
    views = {"front": -30, "side": 90, "back": 150}
    distance = 6.5
    for view, azimuth in views.items():
        a = math.radians(azimuth)
        camera.location = (math.sin(a) * distance, -math.cos(a) * distance, 1.0 + distance * 0.25)
        scene.render.filepath = os.path.join(folder, f"{file_prefix}_{view}.png")
        bpy.ops.render.render(write_still=True)


def render_animation_preview(rig, mesh, clips, folder, file_prefix):
    """클립마다 캐릭터를 하나씩 복제해 나란히 세우고 동시에 재생하는 영상 + 정지 컷을 만든다."""
    from voxel_anim import action_fcurves, assign_action

    scene = bpy.context.scene
    names = list(clips)

    # 미리보기에서만 짧은 클립도 반복 재생되게
    for action in bpy.data.actions:
        for curve in action_fcurves(action):
            if not any(m.type == "CYCLES" for m in curve.modifiers):
                curve.modifiers.new("CYCLES")

    spacing = 2.6
    for i, clip in enumerate(names):
        bpy.ops.object.select_all(action="DESELECT")
        rig.select_set(True)
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.object.duplicate()
        copy_rig = next(o for o in bpy.context.selected_objects if o.type == "ARMATURE")
        copy_rig.location.x = (i - (len(names) - 1) / 2) * spacing
        # 비스듬히 돌려 세워야 팔다리 흔들기와 다이브가 옆에서 보인다
        copy_rig.rotation_euler.z = math.radians(-55)
        assign_action(copy_rig, bpy.data.actions[clip])

        label = bpy.data.curves.new(f"Label_{clip}", "FONT")
        label.body = clip
        label.size = 0.45
        label.align_x = "CENTER"
        text = bpy.data.objects.new(f"Label_{clip}", label)
        text.location = (copy_rig.location.x, -1.6, 0.01)
        text_material = bpy.data.materials.new("LabelMaterial")
        text_material.diffuse_color = (0.05, 0.08, 0.15, 1)
        label.materials.append(text_material)
        scene.collection.objects.link(text)
    rig.hide_render = True
    mesh.hide_render = True

    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    ground_obj = bpy.data.objects.new("Ground", ground)
    ground_material = bpy.data.materials.new("GroundMaterial")
    ground_material.diffuse_color = (srgb_to_linear(0.80), srgb_to_linear(0.91), srgb_to_linear(0.98), 1)
    ground.materials.append(ground_material)
    scene.collection.objects.link(ground_obj)

    camera = setup_render(scene)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = len(names) * spacing + 0.4
    camera.constraints.clear()
    camera.location = (0, -12, 5.2)
    camera.rotation_euler = (math.radians(72), 0, 0)
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 640

    scene.render.fps = 24
    scene.frame_start = 0
    scene.frame_end = 95

    # 정지 컷: 몇 프레임을 뽑아 세로로 이어 붙일 수 있게 저장
    os.makedirs(folder, exist_ok=True)
    for frame in (0, 3, 6, 9):
        scene.frame_set(frame)
        scene.render.filepath = os.path.join(folder, f"{file_prefix}_anim_f{frame:02d}.png")
        bpy.ops.render.render(write_still=True)

    # 영상
    settings = scene.render.image_settings
    if hasattr(settings, "media_type"):
        settings.media_type = "VIDEO"
    settings.file_format = "FFMPEG"
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "HIGH"
    scene.render.filepath = os.path.join(folder, f"{file_prefix}_anim.mp4")
    bpy.ops.render.render(animation=True)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = {}
    for i in range(0, len(argv) - 1, 2):
        args[argv[i].lstrip("-")] = argv[i + 1]
    return args


def run(name, palette, build_parts):
    """인자: --fbx <경로> --blend <경로> --preview <폴더> [--anim-preview <폴더>]"""
    from voxel_anim import create_clips

    args = parse_args()
    rig, mesh = build_character(name, palette, build_parts())
    clips = create_clips(rig)
    if "fbx" in args:
        export_fbx(rig, mesh, args["fbx"])
    if "blend" in args:
        os.makedirs(os.path.dirname(args["blend"]), exist_ok=True)
        bpy.context.preferences.filepaths.save_version = 0  # .blend1 백업 파일을 만들지 않음
        bpy.ops.wm.save_as_mainfile(filepath=args["blend"])
    if "preview" in args:
        render_previews(args["preview"], name.lower())
    if "anim-preview" in args:
        render_animation_preview(rig, mesh, clips, args["anim-preview"], name.lower())
    print("CHARACTER_DONE", name, len(mesh.data.polygons), "faces", {k: v for k, v in clips.items()})
