#!/usr/bin/env python3
import json
import os
import sys

import bpy

# Import helpers from build_scene by exec
src = open(os.path.join(os.path.dirname(__file__), "build_scene.py"), encoding="utf-8").read()
# only take helpers before main
helpers = src.split("def main(")[0]
ns = {"bpy": bpy, "json": json, "math": __import__("math"), "os": os, "sys": sys}
exec(helpers, ns)

layout = json.load(open(sys.argv[sys.argv.index("--") + 2], encoding="utf-8-sig"))
img = ns["build_layout_image"](layout, 600, 800)
img.filepath_raw = "/tmp/layout_only.png"
img.file_format = "PNG"
img.save()
print("OK", img.filepath_raw, img.size)
