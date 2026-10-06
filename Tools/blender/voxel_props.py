# 복셀 소품·건축물 공통 코드 (Blender 5.x)
# 캐릭터(voxel_common.py)와 달리 뼈대·애니메이션이 없는 정적 메시를 만든다.
# 소품 스크립트(make_buildings.py 등)가 PALETTE와 Prop 목록을 만들고 run_props()를 부른다.
#
# 건축물은 복셀 1칸 = PROP_V(0.2m). 캐릭터(0.1m)보다 두 배 굵어서 장난감 블럭처럼 보이고 면 수도 적다.
# 원점 = 바닥면 가운데, 정면은 Blender -Y (Unity +Z).

import bpy
import bmesh
import math
import os
from mathutils import Vector

from voxel_common import NEIGHBORS, make_materials, parse_args, srgb_to_linear

PROP_V = 0.2


def noise(x, y, z, seed=0):
    """좌표마다 항상 같은 0~1 값 (무작위 무늬를 다시 만들어도 똑같이)"""
    h = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791) ^ (seed * 2654435761)
    h = (h ^ (h >> 13)) * 1274126177
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


class Prop:
    def __init__(self, name):
        self.name = name
        self.voxels = {}

    # ---- 채우기 ----
    def box(self, x0, x1, y0, y1, z0, z1, color):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    self.voxels[(x, y, z)] = color

    def cylinder(self, cx, cy, r, z0, z1, color, r_top=None):
        """세로 원기둥 (r_top을 주면 위로 갈수록 반지름이 바뀌는 원뿔대). 중심은 실수 좌표."""
        r_top = r if r_top is None else r_top
        for z in range(z0, z1 + 1):
            t = 0 if z1 == z0 else (z - z0) / (z1 - z0)
            rr = r + (r_top - r) * t
            n = int(math.ceil(rr)) + 1
            for x in range(int(cx) - n, int(cx) + n + 1):
                for y in range(int(cy) - n, int(cy) + n + 1):
                    if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= rr * rr:
                        self.voxels[(x, y, z)] = color

    def ring(self, cx, cy, r_out, r_in, z0, z1, color):
        for z in range(z0, z1 + 1):
            n = int(math.ceil(r_out)) + 1
            for x in range(int(cx) - n, int(cx) + n + 1):
                for y in range(int(cy) - n, int(cy) + n + 1):
                    d2 = (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2
                    if r_in * r_in < d2 <= r_out * r_out:
                        self.voxels[(x, y, z)] = color

    def dome(self, cx, cy, z0, r, color):
        """바닥 z0에 놓인 반구 (속이 꽉 참: 바깥 면만 메시가 되므로 비용 없음)"""
        n = int(math.ceil(r)) + 1
        for x in range(int(cx) - n, int(cx) + n + 1):
            for y in range(int(cy) - n, int(cy) + n + 1):
                for z in range(z0, z0 + n + 1):
                    if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 + (z - z0 + 0.5) ** 2 <= r * r:
                        self.voxels[(x, y, z)] = color

    # ---- 고치기 ----
    def remove(self, predicate):
        for key in [k for k in self.voxels if predicate(*k)]:
            del self.voxels[key]

    def recolor(self, predicate, color):
        """조건에 맞는 기존 복셀의 색만 바꾼다. predicate(x, y, z, 현재색)"""
        for key, c in list(self.voxels.items()):
            if predicate(*key, c):
                self.voxels[key] = color

    def paint_front(self, xs, zs, color, depth=1):
        """정면(-Y)에서 보이는 첫 복셀부터 depth칸을 칠한다 (둥근 벽에 문·창문 그리기)"""
        for x in xs:
            for z in zs:
                ys = [y for (vx, y, vz) in self.voxels if vx == x and vz == z]
                if ys:
                    y0 = min(ys)
                    for y in range(y0, y0 + depth):
                        if (x, y, z) in self.voxels:
                            self.voxels[(x, y, z)] = color

    def cap_snow(self, color, only=None):
        """각 기둥(x, y)의 맨 윗 복셀을 눈으로 덮는다. only: 덮을 원래 색 목록"""
        tops = {}
        for (x, y, z) in self.voxels:
            if z > tops.get((x, y), -10**9):
                tops[(x, y)] = z
        for (x, y), z in tops.items():
            if only is None or self.voxels[(x, y, z)] in only:
                self.voxels[(x, y, z)] = color


def build_prop_mesh(prop, materials):
    """바깥으로 드러난 면만 만드는 정적 복셀 메시. 원점 = 바닥 가운데."""
    used = sorted(set(prop.voxels.values()))
    index_of = {name: i for i, name in enumerate(used)}
    bm = bmesh.new()
    verts = {}

    def vert(x, y, z):
        key = (x, y, z)
        if key not in verts:
            verts[key] = bm.verts.new((x * PROP_V, y * PROP_V, z * PROP_V))
        return verts[key]

    for (x, y, z), color in prop.voxels.items():
        for (dx, dy, dz), corners in NEIGHBORS:
            if (x + dx, y + dy, z + dz) in prop.voxels:
                continue
            face = bm.faces.new([vert(x + cx, y + cy, z + cz) for cx, cy, cz in corners])
            face.material_index = index_of[color]

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    mesh = bpy.data.meshes.new(f"{prop.name}Mesh")
    bm.to_mesh(mesh)
    bm.free()
    for name in used:
        mesh.materials.append(materials[name])
    obj = bpy.data.objects.new(prop.name, mesh)
    return obj


def export_static_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        bake_anim=False,
    )


def _setup_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = 0.6
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
    cam_data.type = "ORTHO"
    camera = bpy.data.objects.new("PreviewCamera", cam_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-200, -200, 0), (200, -200, 0), (200, 200, 0), (-200, 200, 0)], [], [(0, 1, 2, 3)])
    ground_obj = bpy.data.objects.new("Ground", ground)
    mat = bpy.data.materials.new("GroundMaterial")
    mat.diffuse_color = (srgb_to_linear(0.80), srgb_to_linear(0.91), srgb_to_linear(0.98), 1)
    ground.materials.append(mat)
    scene.collection.objects.link(ground_obj)
    return scene, camera


def _aim(camera, center, size, azimuth=-35, elevation=30):
    """center를 비스듬히 내려다보는 직교 카메라. azimuth 0 = 정면(-Y)."""
    a, e = math.radians(azimuth), math.radians(elevation)
    direction = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    camera.location = Vector(center) + direction * 60
    camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = size
    camera.data.clip_end = 500


def _scale_reference(blend_path):
    """크기 비교용 사자 (Lion.blend의 Mesh). 없으면 2m 분홍 기둥."""
    if blend_path and os.path.exists(blend_path):
        try:
            with bpy.data.libraries.load(blend_path) as (src, dst):
                dst.objects = [n for n in src.objects if n == "Mesh"]
            obj = dst.objects[0]
            obj.modifiers.clear()
            obj.parent = None
            bpy.context.scene.collection.objects.link(obj)
            return obj
        except Exception as e:  # 미리보기용이므로 실패해도 계속
            print("Lion reference load failed:", e)
    mesh = bpy.data.meshes.new("Reference")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Reference", mesh)
    obj.scale = (0.6, 0.6, 2.0)
    obj.location = (0, 0, 1.0)
    mat = bpy.data.materials.new("ReferenceMaterial")
    mat.diffuse_color = (srgb_to_linear(0.95), srgb_to_linear(0.45), srgb_to_linear(0.65), 1)
    mesh.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def render_prop_previews(objects, folder, reference_blend=None):
    """건축물마다 한 장(옆에 사자를 세워 크기 비교) + 전체를 한 줄로 세운 한 장"""
    scene, camera = _setup_scene()
    os.makedirs(folder, exist_ok=True)
    ref = _scale_reference(reference_blend)

    def bounds(obj):
        xs = [v.co.x for v in obj.data.vertices]
        ys = [v.co.y for v in obj.data.vertices]
        zs = [v.co.z for v in obj.data.vertices]
        return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)

    # 개별
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    for obj in objects:
        for other in objects:
            other.hide_render = other is not obj
        x0, x1, y0, y1, z0, z1 = bounds(obj)
        ref.location.x = x0 - 1.0
        ref.location.y = (y0 + y1) / 2
        size = max(x1 - x0 + 2.0, y1 - y0, z1 - z0) * 1.3
        _aim(camera, ((x0 + x1) / 2 - 0.5, (y0 + y1) / 2, (z1 - z0) * 0.45), size, azimuth=-20)
        scene.render.filepath = os.path.join(folder, f"{obj.name.lower()}.png")
        bpy.ops.render.render(write_still=True)

    # 전체: 왼쪽부터 나란히, 맨 왼쪽에 사자
    gap = 2.0
    cursor = 0.0
    ref.location = (cursor, 0, ref.location.z if ref.name == "Reference" else 0)
    cursor += 1.5
    top = 0.0
    for obj in objects:
        obj.hide_render = False
        x0, x1, y0, y1, z0, z1 = bounds(obj)
        obj.location.x = cursor - x0
        cursor += (x1 - x0) + gap
        top = max(top, z1)
    width = cursor - gap
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 820
    _aim(camera, (width / 2 - 0.5, 0, top * 0.42), width * 1.08, azimuth=-20, elevation=24)
    scene.render.filepath = os.path.join(folder, "buildings_all.png")
    bpy.ops.render.render(write_still=True)


def run_props(collection_name, palette, build_props):
    """인자: [--fbx-dir <폴더>] [--palette <경로>] [--blend <경로>] [--preview <폴더>] [--reference <Lion.blend>]"""
    args = parse_args()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()

    materials = make_materials(collection_name, palette)
    collection = bpy.data.collections.new(collection_name)
    bpy.context.scene.collection.children.link(collection)

    objects = []
    for prop in build_props():
        obj = build_prop_mesh(prop, materials)
        collection.objects.link(obj)
        objects.append(obj)
        print("PROP_DONE", prop.name, len(prop.voxels), "voxels", len(obj.data.polygons), "faces")

    if "fbx-dir" in args:
        for obj in objects:
            export_static_fbx(obj, os.path.join(args["fbx-dir"], f"{obj.name}.fbx"))
    if "palette" in args:
        with open(args["palette"], "w", encoding="utf-8") as f:
            for color, rgb in palette.items():
                f.write(f"{collection_name}_{color} {rgb[0]:.3f} {rgb[1]:.3f} {rgb[2]:.3f}\n")
    if "blend" in args:
        os.makedirs(os.path.dirname(args["blend"]), exist_ok=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=args["blend"])
    if "preview" in args:
        render_prop_previews(objects, args["preview"], args.get("reference"))
