#!/usr/bin/env python3
"""
Blender headless scene builder — render tu dien 3D photorealistic (Cycles / V-Ray).
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector, Euler


MODULE_MM = 18.0
DEVICE_H_MM = 88.0
RAIL_H_MM = 7.0
ROW_GAP_MM = 16.0
DUCT_W_MM = 30.0


def parse_args():
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []
    out = {"input": None, "output": "cabinet_blender.png", "engine": "cycles", "samples": 256}
    i = 0
    while i < len(argv):
        if argv[i] == "--input" and i + 1 < len(argv):
            out["input"] = argv[i + 1]
            i += 2
        elif argv[i] == "--output" and i + 1 < len(argv):
            out["output"] = argv[i + 1]
            i += 2
        elif argv[i] == "--engine" and i + 1 < len(argv):
            out["engine"] = argv[i + 1].lower()
            i += 2
        elif argv[i] == "--samples" and i + 1 < len(argv):
            out["samples"] = int(argv[i + 1])
            i += 2
        else:
            i += 1
    if not out["input"]:
        raise SystemExit("Can --input layout.json")
    return out


def mm(v):
    return v / 1000.0


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, base_color, roughness=0.45, metallic=0.0, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (*base_color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def make_image_mat(name, img_path):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    mat.blend_method = "CLIP"
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    if img_path and os.path.exists(img_path):
        tex.image = bpy.data.images.load(img_path, check_existing=True)
        tex.image.colorspace_settings.name = "sRGB"
        tex.image.alpha_mode = "STRAIGHT"
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.38
    try:
        bsdf.inputs["Specular IOR Level"].default_value = 0.35
    except KeyError:
        bsdf.inputs["Specular"].default_value = 0.35
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.shadow_method = "CLIP"
    return mat


def add_box(name, sx, sy, sz, loc, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (sx / 2, sy / 2, sz / 2)
    if mat:
        obj.data.materials.append(mat)
    return obj


def add_plane_textured(name, w, h, loc, img_path):
    """Plane mat huong +Y (ve phia camera)."""
    bpy.ops.mesh.primitive_plane_add(size=1, location=loc, rotation=Euler((math.radians(-90), 0, 0), "XYZ"))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (w / 2, h / 2, 1)
    mat = make_image_mat(name + "_mat", img_path)
    obj.data.materials.append(mat)
    return obj


def add_emissive_sphere(name, loc, color, strength=18.0, radius_mm=7.0):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=mm(radius_mm), location=loc)
    obj = bpy.context.active_object
    obj.name = name
    mat = make_mat(name + "_mat", color, roughness=0.15, emission=color, emission_strength=strength)
    obj.data.materials.append(mat)
    return obj


def parse_cabinet_dims(size_str):
    h, w, d = 600.0, 500.0, 225.0
    import re
    mh = re.search(r"H(\d+)", size_str or "", re.I)
    mw = re.search(r"W(\d+)", size_str or "", re.I)
    md = re.search(r"D(\d+)", size_str or "", re.I)
    if mh:
        h = float(mh.group(1))
    if mw:
        w = float(mw.group(1))
    if md:
        d = float(md.group(1))
    return w, h, d


def build_cabinet_shell(w_mm, h_mm, d_mm):
    mat_shell = make_mat("CabShell", (0.68, 0.70, 0.73), roughness=0.58)
    mat_inner = make_mat("CabInner", (0.78, 0.80, 0.83), roughness=0.72)
    mat_duct = make_mat("WireDuct", (0.42, 0.44, 0.47), roughness=0.62)
    mat_rail = make_mat("DINRail", (0.75, 0.77, 0.80), roughness=0.22, metallic=0.92)

    cx, cy, cz = 0, 0, mm(h_mm / 2)
    t = mm(2.5)

    # Back panel (mounting plate)
    add_box("BackPanel", mm(w_mm - 30), mm(3), mm(h_mm - 50),
            (cx, cy - mm(d_mm / 2 - 6), cz), mat_inner)

    # Shell frame
    add_box("Top", mm(w_mm), mm(d_mm), t, (cx, cy, cz + mm(h_mm / 2 - 1)), mat_shell)
    add_box("Bottom", mm(w_mm), mm(d_mm), t, (cx, cy, cz - mm(h_mm / 2 - 1)), mat_shell)
    add_box("Left", t, mm(d_mm), mm(h_mm), (cx - mm(w_mm / 2 - 1), cy, cz), mat_shell)
    add_box("Right", t, mm(d_mm), mm(h_mm), (cx + mm(w_mm / 2 - 1), cy, cz), mat_shell)

    # Door (open ~48 deg)
    hinge_x = cx - mm(w_mm / 2)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(hinge_x + mm(w_mm * 0.42), cy + mm(d_mm * 0.42), cz))
    door = bpy.context.active_object
    door.name = "Door"
    door.scale = (mm(w_mm * 0.84) / 2, mm(5) / 2, mm(h_mm * 0.88) / 2)
    door.data.materials.append(mat_shell)
    door.rotation_euler = Euler((0, math.radians(48), 0), "XYZ")

    # Wire ducts
    duct_h = h_mm - 70
    add_box("DuctL", mm(DUCT_W_MM), mm(16), mm(duct_h),
            (cx - mm(w_mm / 2 - DUCT_W_MM - 18), cy - mm(d_mm / 2 - 18), cz), mat_duct)
    add_box("DuctR", mm(DUCT_W_MM), mm(16), mm(duct_h),
            (cx + mm(w_mm / 2 - DUCT_W_MM - 18), cy - mm(d_mm / 2 - 18), cz), mat_duct)

    return mat_rail


def build_devices(layout, w_mm, h_mm, d_mm, mat_rail):
    row_mods = layout.get("row_modules", 18)
    rows = layout.get("rows", [])
    rail_len = row_mods * MODULE_MM

    y_face = -mm(d_mm / 2 - 22)  # mat phang thiet bi, huong +Y
    z_top = mm(h_mm - 95)
    row_step = mm(DEVICE_H_MM + RAIL_H_MM + ROW_GAP_MM + 18)

    for row_idx, row in enumerate(rows):
        z_row = z_top - row_idx * row_step
        rail_z = z_row - mm(DEVICE_H_MM / 2 + RAIL_H_MM / 2)

        add_box(
            f"Rail_{row_idx}",
            mm(rail_len), mm(RAIL_H_MM), mm(RAIL_H_MM),
            (0, y_face - mm(4), rail_z),
            mat_rail,
        )

        slot = 0
        for dev in row.get("devices", []):
            modules = dev.get("modules", 1)
            dev_w_mm = modules * MODULE_MM
            x_center_mm = -rail_len / 2 + slot * MODULE_MM + dev_w_mm / 2
            tex = dev.get("texture")
            loc = (mm(x_center_mm), y_face, z_row)
            if tex and os.path.exists(tex):
                add_plane_textured(
                    f"Dev_{row_idx}_{slot}",
                    mm(dev_w_mm - 0.5),
                    mm(DEVICE_H_MM - 1),
                    loc,
                    tex,
                )
            slot += modules


def build_phase_lights(w_mm, h_mm, d_mm):
    colors = [(0.92, 0.12, 0.10), (0.96, 0.76, 0.10), (0.12, 0.38, 0.88)]
    labels = ["L1", "L2", "L3"]
    base_x = -mm(w_mm / 2 - 28)
    base_y = mm(d_mm * 0.38)
    base_z = mm(h_mm - 75)
    for i, (col, lbl) in enumerate(zip(colors, labels)):
        add_emissive_sphere(f"Phase_{lbl}", (base_x, base_y + mm(i * 24 - 24), base_z), col, strength=22.0)


def setup_camera(w_mm, h_mm, d_mm):
    cam_data = bpy.data.cameras.new("CabCamera")
    cam_data.lens = 32
    cam_data.clip_start = mm(1)
    cam_data.clip_end = mm(5000)
    cam = bpy.data.objects.new("CabCamera", cam_data)
    bpy.context.collection.objects.link(cam)
    bpy.context.scene.camera = cam

    target = Vector((0, -mm(d_mm * 0.05), mm(h_mm * 0.48)))
    dist = mm(max(w_mm, h_mm) * 1.55)
    cam.location = Vector((dist * 0.42, dist * 0.72, mm(h_mm * 0.52)))
    direction = target - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_lights():
    bpy.ops.object.light_add(type="AREA", location=(0.6, 0.5, 0.95))
    key = bpy.context.active_object
    key.data.energy = 220
    key.data.size = 0.9

    bpy.ops.object.light_add(type="AREA", location=(-0.55, 0.35, 0.75))
    fill = bpy.context.active_object
    fill.data.energy = 85
    fill.data.size = 1.2
    fill.data.color = (0.88, 0.92, 1.0)

    bpy.ops.object.light_add(type="SPOT", location=(0.1, 0.7, 1.1))
    rim = bpy.context.active_object
    rim.data.energy = 180
    rim.data.spot_size = math.radians(55)

    world = bpy.context.scene.world
    if not world:
        world = bpy.data.worlds.new("World")
        bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.55, 0.57, 0.60, 1.0)
        bg.inputs["Strength"].default_value = 0.25


def setup_render(output_path, engine_name, samples, width, height):
    scene = bpy.context.scene
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = output_path

    vray_ok = False
    if engine_name.lower() == "vray":
        for addon in ("vray_blender", "VRayBlender", "vray_for_blender"):
            try:
                bpy.ops.preferences.addon_enable(module=addon)
                if addon in bpy.context.preferences.addons:
                    scene.render.engine = "VRAY"
                    vray_ok = True
                    break
            except Exception:
                pass
        if not vray_ok:
            print("V-Ray addon khong tim thay — fallback Cycles")

    if not vray_ok:
        scene.render.engine = "CYCLES"
        scene.cycles.samples = samples
        scene.cycles.use_denoising = False
        scene.cycles.device = "CPU"
        scene.cycles.max_bounces = 10
        scene.view_settings.view_transform = "Filmic"
        scene.view_settings.exposure = 0.6
        scene.view_settings.look = "Medium High Contrast"


def main():
    args = parse_args()
    with open(args["input"], encoding="utf-8") as f:
        layout = json.load(f)

    clear_scene()
    w_mm, h_mm, d_mm = parse_cabinet_dims(layout.get("size", ""))
    mat_rail = build_cabinet_shell(w_mm, h_mm, d_mm)
    build_devices(layout, w_mm, h_mm, d_mm, mat_rail)
    build_phase_lights(w_mm, h_mm, d_mm)
    setup_camera(w_mm, h_mm, d_mm)
    setup_lights()

    render_cfg = layout.get("render", {})
    setup_render(
        os.path.abspath(args["output"]),
        args["engine"],
        args.get("samples") or render_cfg.get("samples", 256),
        render_cfg.get("width", 1920),
        render_cfg.get("height", 1280),
    )

    print(f"Rendering {args['output']} engine={bpy.context.scene.render.engine} samples={bpy.context.scene.cycles.samples}...")
    bpy.ops.render.render(write_still=True)
    print(f"OK {args['output']}")


if __name__ == "__main__":
    main()
