#!/usr/bin/env python3
"""
Blender Cycles — render tu dien.
Pipeline da verify:
  1) Ghep anh thiet bi (PNG) len tam layout bang pixel blit
  2) Dan len plane + khung tu kim loai
  3) Camera nhin ro tam layout (khong bi trang xoa)
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector, Euler


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = {"input": None, "output": "out.png", "samples": 64, "engine": "cycles"}
    i = 0
    while i < len(argv):
        if argv[i] in ("--input", "--output", "--engine") and i + 1 < len(argv):
            out[argv[i][2:]] = argv[i + 1]
            i += 2
        elif argv[i] == "--samples" and i + 1 < len(argv):
            out["samples"] = int(argv[i + 1])
            i += 2
        else:
            i += 1
    if not out["input"]:
        raise SystemExit("need --input")
    return out


def mat_principled(name, color, rough=0.5, metal=0.0, emit=None, es=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    b = nodes.new("ShaderNodeBsdfPrincipled")
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        k = "Emission Color" if "Emission Color" in b.inputs else "Emission"
        b.inputs[k].default_value = (*emit, 1)
        if "Emission Strength" in b.inputs:
            b.inputs["Emission Strength"].default_value = es
    links.new(b.outputs["BSDF"], out.inputs["Surface"])
    return m


def cube(name, sx, sy, sz, loc, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = (sx / 2, sy / 2, sz / 2)
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(material)
    return o


def load_rgba(path, tw, th):
    img = bpy.data.images.load(os.path.abspath(path), check_existing=True)
    sw, sh = img.size
    src = list(img.pixels)
    out = [0.0] * (tw * th * 4)
    for y in range(th):
        sy = int((1 - (y + 0.5) / th) * (sh - 1))
        for x in range(tw):
            sx = int((x + 0.5) / tw * (sw - 1))
            si = (sy * sw + sx) * 4
            di = (y * tw + x) * 4
            out[di:di + 4] = src[si:si + 4]
    return out


def blit(dst, dw, dh, src, sw, sh, x0, y0):
    for y in range(sh):
        dy = y0 + y
        if dy < 0 or dy >= dh:
            continue
        for x in range(sw):
            dx = x0 + x
            if dx < 0 or dx >= dw:
                continue
            si = (y * sw + x) * 4
            di = (dy * dw + dx) * 4
            # skip catalog white background
            if src[si] > 0.93 and src[si + 1] > 0.93 and src[si + 2] > 0.93:
                continue
            dst[di:di + 4] = src[si:si + 4]


def build_layout_image(layout, px_w=900, px_h=1200):
    pixels = [0.78, 0.80, 0.82, 1.0] * (px_w * px_h)
    rows = layout.get("rows", [])
    row_mods = int(layout.get("row_modules", 18))
    margin = 50
    usable_w = px_w - margin * 2
    mod_w = usable_w / max(row_mods, 1)
    row_h = 200
    top = px_h - 70

    for x0 in (12, px_w - 48):
        for y in range(40, px_h - 30):
            for x in range(x0, x0 + 32):
                i = (y * px_w + x) * 4
                pixels[i:i + 3] = [0.34, 0.36, 0.38]

    for ri, row in enumerate(rows):
        y_row = top - ri * (row_h + 50) - row_h
        for y in range(y_row - 10, y_row - 2):
            for x in range(margin, px_w - margin):
                i = (y * px_w + x) * 4
                pixels[i:i + 3] = [0.84, 0.85, 0.87]
        slot = 0
        for dev in row.get("devices", []):
            mods = max(1, int(dev.get("modules") or 1))
            dw = max(20, int(mods * mod_w) - 6)
            dh = row_h - 16
            x0 = margin + int(slot * mod_w) + 2
            y0 = y_row + 6
            tex = dev.get("texture")
            if tex and os.path.isfile(tex):
                src = load_rgba(tex, dw, dh)
                blit(pixels, px_w, px_h, src, dw, dh, x0, y0)
                print("BLIT", os.path.basename(tex), "at", x0, y0, dw, dh)
            slot += mods

    img = bpy.data.images.new("CabinetLayout", px_w, px_h, alpha=True)
    img.pixels = pixels
    img.pack()
    return img


def main():
    args = parse_args()
    with open(args["input"], encoding="utf-8-sig") as f:
        layout = json.load(f)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    layout_img = build_layout_image(layout)

    # Emission material — khong bi anh sang lam trang xoa
    m = bpy.data.materials.new("layoutMat")
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emi = nodes.new("ShaderNodeEmission")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = layout_img
    links.new(tex.outputs["Color"], emi.inputs["Color"])
    emi.inputs["Strength"].default_value = 1.0
    links.new(emi.outputs["Emission"], out.inputs["Surface"])

    # Board
    bpy.ops.mesh.primitive_plane_add(location=(0, 0, 0))
    board = bpy.context.active_object
    board.name = "Board"
    board.scale = (0.45, 0.60, 1)
    bpy.ops.object.transform_apply(scale=True)
    board.data.materials.append(m)

    # Metal frame around board (depth)
    shell = mat_principled("shell", (0.40, 0.42, 0.45), 0.5, 0.4)
    cube("frameL", 0.03, 0.08, 1.22, (-0.48, -0.03, 0), shell)
    cube("frameR", 0.03, 0.08, 1.22, (0.48, -0.03, 0), shell)
    cube("frameT", 0.99, 0.08, 0.03, (0, -0.03, 0.615), shell)
    cube("frameB", 0.99, 0.08, 0.03, (0, -0.03, -0.615), shell)
    cube("back", 0.99, 0.02, 1.22, (0, -0.07, 0), shell)

    # Phase LEDs — tren khung, khong de len thiet bi
    for i, c in enumerate(((0.95, 0.12, 0.1), (0.95, 0.8, 0.1), (0.15, 0.4, 0.95))):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.018, location=(-0.30 + i * 0.07, 0.06, 0.68))
        s = bpy.context.active_object
        s.data.materials.append(mat_principled(f"led{i}", c, 0.2, 0, c, 8))

    # Camera: giong render_layout_only (da verify thiet bi hien ro)
    # Board nam mat phang XY, normal +Z; camera nhin xuong -Z.
    cam_d = bpy.data.cameras.new("cam")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = 1.40
    cam = bpy.data.objects.new("cam", cam_d)
    bpy.context.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    cam.location = (0.05, -0.08, 2.0)
    cam.rotation_euler = (0, 0, 0)

    bpy.ops.object.light_add(type="AREA", location=(0.6, -0.5, 1.5))
    L = bpy.context.active_object
    L.data.energy = 15
    L.data.size = 1.2

    world = bpy.data.worlds.new("W")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.14, 0.15, 0.17, 1)
    bg.inputs[1].default_value = 0.5

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = int(args["samples"])
    sc.cycles.use_denoising = False
    sc.cycles.device = "CPU"
    # Portrait — khop ti le board (0.45 x 0.60)
    sc.render.resolution_x = int(layout.get("render", {}).get("width", 1200))
    sc.render.resolution_y = int(layout.get("render", {}).get("height", 1600))
    sc.render.image_settings.file_format = "PNG"
    sc.render.filepath = os.path.abspath(args["output"])
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.exposure = 0.0

    print("RENDER", sc.render.filepath, "samples", sc.cycles.samples)
    bpy.ops.render.render(write_still=True)
    if not os.path.isfile(sc.render.filepath):
        raise SystemExit("no output")
    print("OK", sc.render.filepath)


if __name__ == "__main__":
    main()
