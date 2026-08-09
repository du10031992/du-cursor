#!/usr/bin/env python3
"""
MEP Pipe System Renderer — minh hoa he nuoc / PCCC (Pillow).

Usage:
  python3 render_pipe_system.py --system water --demo --output water.png
  python3 render_pipe_system.py --system fire --input mep_fire_export.json --output fire.png
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
from typing import Any, Dict, List, Optional, Tuple

try:
    from PIL import Image, ImageDraw, ImageFilter, ImageFont
except ImportError:
    print("pip install pillow")
    sys.exit(1)

W, H = 1600, 1000
MARGIN = 48
SIDEBAR_W = 360
CANVAS_PAD = 40

THEMES = {
    "water": {
        "title": "HE NUOC",
        "subtitle": "So do ong + phan tich (TCVN 4513)",
        "bg_top": (18, 48, 72),
        "bg_bot": (232, 244, 250),
        "pipe": (20, 120, 190),
        "pipe_hi": (90, 190, 235),
        "accent": (0, 140, 200),
        "panel": (255, 255, 255),
        "text": (20, 40, 55),
        "muted": (90, 110, 125),
        "fitting": (15, 95, 155),
        "grid": (190, 215, 230),
        "badge": (0, 130, 180),
    },
    "fire": {
        "title": "HE PCCC",
        "subtitle": "Sprinkler / hydrant + phan tich (QCVN 06 / NFPA 13)",
        "bg_top": (72, 22, 22),
        "bg_bot": (255, 240, 236),
        "pipe": (190, 40, 40),
        "pipe_hi": (235, 110, 90),
        "accent": (200, 50, 40),
        "panel": (255, 255, 255),
        "text": (55, 25, 25),
        "muted": (125, 90, 90),
        "fitting": (160, 30, 30),
        "grid": (235, 200, 195),
        "badge": (180, 40, 35),
    },
}


def load_font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "C:\\Windows\\Fonts\\arialbd.ttf" if bold else "C:\\Windows\\Fonts\\arial.ttf",
        "C:\\Windows\\Fonts\\segoeuib.ttf" if bold else "C:\\Windows\\Fonts\\segoeui.ttf",
    ]
    for path in candidates:
        if os.path.isfile(path):
            try:
                return ImageFont.truetype(path, size)
            except OSError:
                pass
    return ImageFont.load_default()


def demo_payload(system: str) -> Dict[str, Any]:
    if system == "fire":
        return {
            "system": "fire",
            "name": "DEMO-PCCC",
            "title": "He PCCC — Demo",
            "segments": [
                {
                    "points": [
                        {"x": 0, "y": 0},
                        {"x": 8000, "y": 0},
                        {"x": 8000, "y": 5000},
                        {"x": 2000, "y": 5000},
                        {"x": 2000, "y": 2500},
                    ],
                    "width": 65,
                    "layer": "MEP_FIRE",
                },
                {
                    "points": [
                        {"x": 8000, "y": 2500},
                        {"x": 12000, "y": 2500},
                    ],
                    "width": 50,
                    "layer": "MEP_FIRE",
                },
            ],
            "fittings": [
                {"name": "ELBOW", "kind": "elbow90", "x": 8000, "y": 0, "rotation_deg": 0},
                {"name": "ELBOW", "kind": "elbow90", "x": 8000, "y": 5000, "rotation_deg": 90},
                {"name": "TEE", "kind": "tee", "x": 8000, "y": 2500, "rotation_deg": 0},
                {"name": "SPK1", "kind": "sprinkler", "x": 4000, "y": 0, "rotation_deg": 0},
                {"name": "SPK2", "kind": "sprinkler", "x": 6000, "y": 0, "rotation_deg": 0},
                {"name": "SPK3", "kind": "sprinkler", "x": 10000, "y": 2500, "rotation_deg": 0},
                {"name": "HCT", "kind": "hydrant", "x": 2000, "y": 2500, "rotation_deg": 0},
                {"name": "DET", "kind": "detector", "x": 5000, "y": 5000, "rotation_deg": 0},
            ],
            "analysis": {
                "items": [
                    {"title": "Chieu dai ong", "value": 21.0, "unit": "m", "note": "Demo polyline"},
                    {"title": "Luu luong sprinkler", "value": 80.0, "unit": "L/min", "note": "K=80, P=1.0 bar"},
                    {"title": "Mat do phun", "value": 5.0, "unit": "L/min·m²", "note": "Q=600 / A=120"},
                    {"title": "Bon du tru", "value": 90.0, "unit": "m³", "note": "1500 L/min × 60 phut"},
                ]
            },
        }

    return {
        "system": "water",
        "name": "DEMO-WATER",
        "title": "He nuoc — Demo",
        "segments": [
            {
                "points": [
                    {"x": 0, "y": 0},
                    {"x": 6000, "y": 0},
                    {"x": 6000, "y": 4000},
                    {"x": 10000, "y": 4000},
                    {"x": 10000, "y": 1500},
                ],
                "width": 50,
                "layer": "MEP_WATER",
            },
            {
                "points": [
                    {"x": 6000, "y": 2000},
                    {"x": 3000, "y": 2000},
                    {"x": 3000, "y": 3500},
                ],
                "width": 40,
                "layer": "MEP_WATER",
            },
        ],
        "fittings": [
            {"name": "CO90", "kind": "elbow90", "x": 6000, "y": 0, "rotation_deg": 0},
            {"name": "TEE", "kind": "tee", "x": 6000, "y": 2000, "rotation_deg": 0},
            {"name": "VAN1", "kind": "valve", "x": 3000, "y": 0, "rotation_deg": 0},
            {"name": "GIAM", "kind": "reducer", "x": 8000, "y": 4000, "rotation_deg": 0},
            {"name": "NUT", "kind": "cap", "x": 3000, "y": 3500, "rotation_deg": 90},
        ],
        "analysis": {
            "items": [
                {"title": "Chieu dai ong", "value": 16.5, "unit": "m", "note": "Demo polyline"},
                {"title": "Toc do nuoc trong ong", "value": 1.274, "unit": "m/s", "note": "Q=2.5 L/s, DN=50mm"},
                {"title": "Ton that cot nuoc", "value": 1.85, "unit": "m", "note": "f=0.02, L=30m"},
                {"title": "Luu luong", "value": 2.5, "unit": "L/s", "note": "Thiet ke mau"},
            ]
        },
    }


def collect_bounds(data: Dict[str, Any]) -> Tuple[float, float, float, float]:
    xs: List[float] = []
    ys: List[float] = []
    for seg in data.get("segments") or []:
        for p in seg.get("points") or []:
            xs.append(float(p["x"]))
            ys.append(float(p["y"]))
    for f in data.get("fittings") or []:
        xs.append(float(f.get("x", 0)))
        ys.append(float(f.get("y", 0)))
    if not xs:
        return 0.0, 0.0, 1000.0, 1000.0
    return min(xs), min(ys), max(xs), max(ys)


def make_mapper(
    bounds: Tuple[float, float, float, float],
    rect: Tuple[int, int, int, int],
):
    minx, miny, maxx, maxy = bounds
    rx0, ry0, rx1, ry1 = rect
    dx = max(maxx - minx, 1.0)
    dy = max(maxy - miny, 1.0)
    rw = max(rx1 - rx0, 1)
    rh = max(ry1 - ry0, 1)
    scale = min(rw / dx, rh / dy) * 0.88
    ox = rx0 + (rw - dx * scale) / 2
    oy = ry0 + (rh - dy * scale) / 2

    def map_pt(x: float, y: float) -> Tuple[float, float]:
        # CAD Y up -> image Y down
        return ox + (x - minx) * scale, oy + (maxy - y) * scale

    return map_pt, scale


def draw_background(img: Image.Image, theme: Dict[str, Any]) -> None:
    draw = ImageDraw.Draw(img)
    top = theme["bg_top"]
    bot = theme["bg_bot"]
    for y in range(H):
        t = y / (H - 1)
        # soft vertical wash + lighter work area
        if y < 110:
            c = tuple(int(top[i] * (1 - t * 0.35) + bot[i] * (t * 0.35)) for i in range(3))
        else:
            u = (y - 110) / (H - 110)
            c = tuple(int(top[i] * (1 - u) * 0.15 + bot[i] * (0.85 + 0.15 * u)) for i in range(3))
        draw.line([(0, y), (W, y)], fill=c)

    # subtle diagonal sheen
    overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    for i in range(0, W + H, 28):
        od.line([(i, 0), (i - H, H)], fill=(255, 255, 255, 10), width=1)
    img.alpha_composite(overlay)


def draw_header(draw: ImageDraw.ImageDraw, theme: Dict[str, Any], data: Dict[str, Any]) -> None:
    title_font = load_font(34, bold=True)
    sub_font = load_font(16)
    small = load_font(13)
    draw.text((MARGIN, 28), theme["title"], fill=(255, 255, 255), font=title_font)
    draw.text((MARGIN, 70), theme["subtitle"], fill=(230, 235, 240), font=sub_font)
    name = data.get("name") or data.get("title") or ""
    draw.text((W - MARGIN - 280, 40), str(name), fill=(255, 255, 255), font=small)


def draw_grid(draw: ImageDraw.ImageDraw, rect: Tuple[int, int, int, int], theme: Dict[str, Any]) -> None:
    x0, y0, x1, y1 = rect
    step = 48
    for x in range(x0, x1, step):
        draw.line([(x, y0), (x, y1)], fill=theme["grid"], width=1)
    for y in range(y0, y1, step):
        draw.line([(x0, y), (x1, y)], fill=theme["grid"], width=1)
    draw.rounded_rectangle([x0, y0, x1, y1], radius=12, outline=theme["accent"], width=2)


def draw_pipe(
    draw: ImageDraw.ImageDraw,
    pts: List[Tuple[float, float]],
    width_px: float,
    theme: Dict[str, Any],
) -> None:
    if len(pts) < 2:
        return
    # outer glow / casing
    draw.line(pts, fill=theme["pipe"], width=max(8, int(width_px + 6)), joint="curve")
    draw.line(pts, fill=theme["pipe_hi"], width=max(3, int(width_px * 0.45)), joint="curve")
    # flow arrows
    for i in range(len(pts) - 1):
        x0, y0 = pts[i]
        x1, y1 = pts[i + 1]
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        ang = math.atan2(y1 - y0, x1 - x0)
        s = 10
        a1 = (mx - s * math.cos(ang - 0.45), my - s * math.sin(ang - 0.45))
        a2 = (mx, my)
        a3 = (mx - s * math.cos(ang + 0.45), my - s * math.sin(ang + 0.45))
        draw.polygon([a1, a2, a3], fill=theme["accent"])


def draw_fitting(
    draw: ImageDraw.ImageDraw,
    kind: str,
    x: float,
    y: float,
    theme: Dict[str, Any],
    scale: float,
) -> None:
    r = max(10, min(22, 14 * (scale / 0.08 if scale > 0 else 1)))
    kind = (kind or "fitting").lower()
    c = theme["fitting"]
    if kind == "elbow90":
        draw.arc([x - r, y - r, x + r, y + r], 0, 90, fill=c, width=4)
        draw.ellipse([x - 4, y - 4, x + 4, y + 4], fill=c)
    elif kind == "tee":
        draw.line([(x - r, y), (x + r, y)], fill=c, width=4)
        draw.line([(x, y), (x, y - r)], fill=c, width=4)
    elif kind == "valve":
        draw.polygon([(x - r, y), (x, y - r * 0.7), (x + r, y), (x, y + r * 0.7)], outline=c, width=3)
    elif kind == "reducer":
        draw.polygon([(x - r, y - r * 0.5), (x + r, y - r * 0.25), (x + r, y + r * 0.25), (x - r, y + r * 0.5)], outline=c, width=3)
    elif kind == "cap":
        draw.ellipse([x - r * 0.7, y - r * 0.7, x + r * 0.7, y + r * 0.7], outline=c, width=3)
    elif kind == "sprinkler":
        draw.ellipse([x - r * 0.55, y - r * 0.55, x + r * 0.55, y + r * 0.55], outline=c, width=3)
        draw.line([(x, y), (x, y + r)], fill=c, width=2)
        draw.line([(x - r * 0.4, y + r * 0.7), (x + r * 0.4, y + r * 0.7)], fill=c, width=2)
    elif kind == "hydrant":
        draw.rectangle([x - r * 0.5, y - r, x + r * 0.5, y + r * 0.4], outline=c, width=3)
        draw.line([(x, y - r), (x, y - r * 1.4)], fill=c, width=3)
    elif kind == "detector":
        draw.ellipse([x - r, y - r, x + r, y + r], outline=c, width=3)
        draw.ellipse([x - r * 0.35, y - r * 0.35, x + r * 0.35, y + r * 0.35], fill=c)
    else:
        draw.ellipse([x - 6, y - 6, x + 6, y + 6], fill=c)


def draw_sidebar(
    draw: ImageDraw.ImageDraw,
    theme: Dict[str, Any],
    data: Dict[str, Any],
    rect: Tuple[int, int, int, int],
) -> None:
    x0, y0, x1, y1 = rect
    draw.rounded_rectangle([x0, y0, x1, y1], radius=14, fill=theme["panel"], outline=theme["accent"], width=2)
    title_font = load_font(20, bold=True)
    body = load_font(14)
    small = load_font(12)
    draw.text((x0 + 20, y0 + 18), "PHAN TICH", fill=theme["accent"], font=title_font)

    segs = len(data.get("segments") or [])
    fits = len(data.get("fittings") or [])
    draw.text((x0 + 20, y0 + 52), f"Doan ong: {segs}", fill=theme["text"], font=body)
    draw.text((x0 + 20, y0 + 74), f"Phu kien: {fits}", fill=theme["text"], font=body)

    y = y0 + 110
    items = ((data.get("analysis") or {}).get("items")) or []
    for item in items[:8]:
        title = str(item.get("title") or "Chi so")
        value = item.get("value")
        unit = str(item.get("unit") or "")
        note = str(item.get("note") or "")
        draw.rounded_rectangle([x0 + 16, y, x1 - 16, y + 78], radius=10, fill=(248, 250, 252), outline=theme["grid"], width=1)
        draw.text((x0 + 28, y + 10), title, fill=theme["muted"], font=small)
        draw.text((x0 + 28, y + 30), f"{value} {unit}".strip(), fill=theme["text"], font=title_font)
        if note:
            draw.text((x0 + 28, y + 56), note[:42], fill=theme["muted"], font=small)
        y += 90

    legend_y = min(y + 10, y1 - 160)
    draw.text((x0 + 20, legend_y), "CHU THICH", fill=theme["accent"], font=title_font)
    legend = [
        ("elbow90", "Co 90"),
        ("tee", "Chu T"),
        ("valve", "Van"),
        ("sprinkler", "Sprinkler"),
        ("hydrant", "Chua chay"),
        ("detector", "Dau bao"),
    ]
    ly = legend_y + 34
    for kind, label in legend:
        draw_fitting(draw, kind, x0 + 34, ly + 8, theme, 0.1)
        draw.text((x0 + 56, ly), label, fill=theme["text"], font=small)
        ly += 22


def render(data: Dict[str, Any], output: str) -> None:
    system = (data.get("system") or "water").lower()
    if system not in THEMES:
        system = "water"
    theme = THEMES[system]

    img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    draw_background(img, theme)
    draw = ImageDraw.Draw(img)
    draw_header(draw, theme, data)

    plot = (MARGIN, 110, W - SIDEBAR_W - MARGIN - 20, H - MARGIN)
    side = (W - SIDEBAR_W - MARGIN + 10, 110, W - MARGIN, H - MARGIN)

    # card under plot
    draw.rounded_rectangle([plot[0] - 8, plot[1] - 8, plot[2] + 8, plot[3] + 8], radius=16, fill=(255, 255, 255, 235))
    draw_grid(draw, plot, theme)

    bounds = collect_bounds(data)
    mapper, scale = make_mapper(bounds, (plot[0] + CANVAS_PAD, plot[1] + CANVAS_PAD, plot[2] - CANVAS_PAD, plot[3] - CANVAS_PAD))

    # soft shadow pass for pipes
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    for seg in data.get("segments") or []:
        pts = [mapper(float(p["x"]), float(p["y"])) for p in (seg.get("points") or [])]
        if len(pts) >= 2:
            wpx = max(6.0, min(18.0, float(seg.get("width") or 50) * 0.12))
            sd.line(pts, fill=(0, 0, 0, 55), width=int(wpx + 10), joint="curve")
    shadow = shadow.filter(ImageFilter.GaussianBlur(4))
    img.alpha_composite(shadow)
    draw = ImageDraw.Draw(img)

    for seg in data.get("segments") or []:
        pts = [mapper(float(p["x"]), float(p["y"])) for p in (seg.get("points") or [])]
        wpx = max(6.0, min(18.0, float(seg.get("width") or 50) * 0.12))
        draw_pipe(draw, pts, wpx, theme)

    for f in data.get("fittings") or []:
        x, y = mapper(float(f.get("x", 0)), float(f.get("y", 0)))
        draw_fitting(draw, str(f.get("kind") or "fitting"), x, y, theme, scale)

    draw_sidebar(draw, theme, data, side)

    # footer
    foot = load_font(12)
    draw.text((MARGIN, H - 28), "MepPanel · Pillow visual render · khong Blender", fill=theme["muted"], font=foot)

    out = img.convert("RGB")
    out.save(output, "PNG", optimize=True)
    print(f"OK {system} render -> {output} ({out.size[0]}x{out.size[1]})")


def main() -> None:
    parser = argparse.ArgumentParser(description="MEP Water/PCCC pipe system renderer")
    parser.add_argument("--system", choices=("water", "fire"), default="water")
    parser.add_argument("--input", "-i", help="JSON export from AutoCAD")
    parser.add_argument("--output", "-o", default="pipe_system.png")
    parser.add_argument("--demo", action="store_true")
    args = parser.parse_args()

    if args.demo or not args.input:
        data = demo_payload(args.system)
    else:
        with open(args.input, "r", encoding="utf-8-sig") as f:
            data = json.load(f)
        if not data.get("system"):
            data["system"] = args.system

    # CLI system flag wins for theme when demo
    if args.demo:
        data["system"] = args.system

    render(data, args.output)


if __name__ == "__main__":
    main()
