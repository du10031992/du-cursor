#!/usr/bin/env python3
"""
Wrapper goi Blender headless de render tu dien 3D (Cycles / V-Ray).
Fallback ve Pillow photoreal neu Blender khong co.
"""

import json
import os
import shutil
import subprocess
import sys
import tempfile
from typing import List, Optional, Tuple

from render_cabinet import (
    CabinetSpec,
    Device,
    SCRIPT_DIR,
    find_device_image_path,
)
from render_cabinet_interior import (
    build_din_layout,
    device_modules,
    make_db01_demo,
)

BLENDER_SCRIPT = os.path.join(SCRIPT_DIR, "blender", "build_scene.py")


def find_blender() -> Optional[str]:
    """Tim Blender executable (Windows / Linux / macOS)."""
    for name in ("blender", "blender.exe"):
        path = shutil.which(name)
        if path:
            return path

    candidates = []
    if sys.platform == "win32":
        pf = os.environ.get("ProgramFiles", r"C:\Program Files")
        pf86 = os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")
        for base in (pf, pf86):
            bf = os.path.join(base, "Blender Foundation")
            if os.path.isdir(bf):
                for entry in sorted(os.listdir(bf), reverse=True):
                    exe = os.path.join(bf, entry, "blender.exe")
                    if os.path.isfile(exe):
                        candidates.append(exe)
    elif sys.platform == "darwin":
        candidates.append("/Applications/Blender.app/Contents/MacOS/Blender")
    else:
        candidates.extend(("/usr/bin/blender", "/snap/bin/blender"))

    for c in candidates:
        if os.path.isfile(c):
            return c
    return None


def export_blender_layout(
    spec: CabinetSpec,
    engine: str = "cycles",
    samples: int = 256,
    width: int = 1920,
    height: int = 1280,
) -> dict:
    """Chuyen CabinetSpec -> JSON layout cho Blender."""
    name, size, row_mods, din_rows = build_din_layout(spec)
    rows_out = []
    for din_row in din_rows:
        devs_out = []
        for dev in din_row.devices:
            tex = find_device_image_path(dev.device_type)
            devs_out.append({
                "name": dev.name or dev.device_type,
                "type": dev.device_type,
                "modules": device_modules(dev),
                "texture": tex,
                "in_a": dev.in_a,
            })
        rows_out.append({"label": din_row.label, "devices": devs_out})

    return {
        "name": name,
        "size": size,
        "row_modules": row_mods,
        "devices_dir": os.path.join(SCRIPT_DIR, "devices"),
        "rows": rows_out,
        "render": {
            "engine": engine,
            "samples": samples,
            "width": width,
            "height": height,
        },
    }


def render_with_blender(
    spec: CabinetSpec,
    output_path: str,
    engine: str = "cycles",
    samples: int = 256,
    width: int = 1920,
    height: int = 1280,
    timeout_sec: int = 600,
) -> Tuple[bool, str]:
    """
    Render bang Blender. Tra (ok, message).
    """
    blender = find_blender()
    if not blender:
        return False, "Blender chua cai — tai tu https://www.blender.org/download/"

    if not os.path.isfile(BLENDER_SCRIPT):
        return False, f"Thieu script: {BLENDER_SCRIPT}"

    layout = export_blender_layout(spec, engine, samples, width, height)
    out_abs = os.path.abspath(output_path)
    os.makedirs(os.path.dirname(out_abs) or ".", exist_ok=True)

    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False, encoding="utf-8") as tf:
        json.dump(layout, tf, ensure_ascii=False, indent=2)
        layout_path = tf.name

    cmd = [
        blender,
        "--background",
        "--factory-startup",
        "--python", BLENDER_SCRIPT,
        "--",
        "--input", layout_path,
        "--output", out_abs,
        "--engine", engine,
        "--samples", str(samples),
    ]

    print(f"Blender render ({engine}, {samples} samples) — co the mat 1-3 phut...")
    print(" ".join(cmd))

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout_sec,
            cwd=SCRIPT_DIR,
        )
    except subprocess.TimeoutExpired:
        os.unlink(layout_path)
        return False, f"Blender timeout sau {timeout_sec}s"
    finally:
        try:
            os.unlink(layout_path)
        except OSError:
            pass

    if result.returncode != 0:
        err = (result.stderr or result.stdout or "")[-800:]
        return False, f"Blender loi (code {result.returncode}): {err}"

    if not os.path.isfile(out_abs):
        return False, "Blender chay xong nhung khong tao file output"

    return True, out_abs


def render_blender_or_fallback(
    spec: CabinetSpec,
    output_path: str,
    engine: str = "cycles",
    samples: int = 256,
    fallback_quality: str = "photoreal",
) -> str:
    """Thu Blender truoc; fallback Pillow neu khong co Blender."""
    ok, msg = render_with_blender(spec, output_path, engine=engine, samples=samples)
    if ok:
        print(f"OK {msg} [blender/{engine}]")
        return "blender"

    print(f"WARN: {msg}")
    print(f"Fallback -> Pillow ({fallback_quality})")
    from render_cabinet_interior import render_interior
    from PIL import Image

    img = render_interior(spec, quality=fallback_quality)
    img.save(output_path, "PNG", dpi=(150, 150))
    print(f"OK {output_path} ({img.width}x{img.height}px) [fallback/{fallback_quality}]")
    return "fallback"
