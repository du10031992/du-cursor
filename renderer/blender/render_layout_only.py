#!/usr/bin/env python3
"""Minimal: chi render tam layout — de verify texture hien dung."""
import json
import math
import os
import sys
import bpy

helpers = open(os.path.join(os.path.dirname(__file__), "build_scene.py"), encoding="utf-8").read().split("def main(")[0]
ns = {"bpy": bpy, "json": json, "math": math, "os": os, "sys": sys, "Vector": __import__("mathutils").Vector, "Euler": __import__("mathutils").Euler}
exec(helpers, ns)

argv = sys.argv[sys.argv.index("--") + 1:]
inp = argv[argv.index("--input") + 1]
out = argv[argv.index("--output") + 1]
samples = 32
if "--samples" in argv:
    samples = int(argv[argv.index("--samples") + 1])

layout = json.load(open(inp, encoding="utf-8-sig"))
bpy.ops.wm.read_factory_settings(use_empty=True)
img = ns["build_layout_image"](layout, 900, 1200)

m = bpy.data.materials.new("L")
m.use_nodes = True
nodes, links = m.node_tree.nodes, m.node_tree.links
nodes.clear()
outn = nodes.new("ShaderNodeOutputMaterial")
emi = nodes.new("ShaderNodeEmission")
tex = nodes.new("ShaderNodeTexImage")
tex.image = img
links.new(tex.outputs["Color"], emi.inputs["Color"])
emi.inputs["Strength"].default_value = 1.0
links.new(emi.outputs["Emission"], outn.inputs["Surface"])

bpy.ops.mesh.primitive_plane_add(location=(0, 0, 0))
plane = bpy.context.active_object
plane.scale = (0.45, 0.6, 1)
bpy.ops.object.transform_apply(scale=True)
plane.data.materials.append(m)

cam_d = bpy.data.cameras.new("c")
cam_d.type = "ORTHO"
cam_d.ortho_scale = 1.3
cam = bpy.data.objects.new("c", cam_d)
bpy.context.collection.objects.link(cam)
bpy.context.scene.camera = cam
cam.location = (0, 0, 2)
cam.rotation_euler = (0, 0, 0)

sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = samples
sc.cycles.use_denoising = False
sc.render.resolution_x = 900
sc.render.resolution_y = 1200
sc.render.filepath = out
sc.view_settings.view_transform = "Standard"
bpy.ops.render.render(write_still=True)
print("OK", out)
