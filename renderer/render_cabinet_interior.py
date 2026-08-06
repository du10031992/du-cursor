#!/usr/bin/env python3
"""
Render noi that tu dien mo - photorealistic, dung anh thiet bi AI.
Sua loi den bao pha: luon 3 den L1/L2/L3 (do/vang/xanh), khong 1 den "L".
"""

import json
import math
import os
from dataclasses import dataclass, field
from typing import List, Optional

from PIL import Image, ImageDraw, ImageEnhance, ImageFont

# Import shared helpers tu render_cabinet
from render_cabinet import (
    CabinetSpec,
    Device,
    Bay,
    parse_json,
    parse_csv,
    make_demo,
    find_device_image,
    remove_white_background,
    paste_device_image,
    DEVICE_MODULES,
    SCRIPT_DIR,
)

# Mau sac tu that (nen sang nhu tu that)
BG = (235, 237, 240)
CAB_GRAY = (188, 192, 198)
CAB_DARK = (155, 160, 168)
CAB_INNER = (210, 214, 220)
DIN_SILVER = (200, 205, 212)
DUCT_GRAY = (120, 125, 132)
C_RED = (220, 45, 45)
C_YELLOW = (245, 195, 25)
C_BLUE = (35, 95, 210)
C_GREEN = (40, 170, 75)
C_NEUTRAL = (45, 85, 200)
WIRE_R = (200, 40, 40)
WIRE_Y = (230, 180, 30)
WIRE_B = (40, 80, 200)
WIRE_N = (30, 50, 120)
WIRE_PE = (60, 180, 50)

MODULE_W = 18
SCALE = 4
MX = MODULE_W * SCALE
MY = 130
RAIL_H = 12
MARGIN = 40
DOOR_W = 90
DUCT_W = 36


@dataclass
class InteriorLayout:
    name: str = "DB-01"
    size: str = "H600xW500xD225"
    row_modules: int = 12
    devices_top: List[Device] = field(default_factory=list)
    devices_mid: List[Device] = field(default_factory=list)
    devices_bot: List[Device] = field(default_factory=list)


def spec_to_interior(spec: CabinetSpec) -> InteriorLayout:
    layout = InteriorLayout(name=spec.name, size=spec.size)
    all_devs: List[Device] = []
    for bay in spec.bays:
        all_devs.extend(bay.devices)

    top_types = ("MCCB", "MCB 3P", "SPD", "SURGE", "SWITCH")
    for d in all_devs:
        t = d.device_type.upper()
        if any(t.startswith(x) for x in top_types) or "MCCB" in t:
            layout.devices_top.append(d)
        elif "PILOT" in t or "INDICATOR" in t or "DEN" in d.name.upper():
            pass  # ve rieng 3 den pha
        else:
            if len(layout.devices_mid) < 16:
                layout.devices_mid.append(d)
            else:
                layout.devices_bot.append(d)

    if not layout.devices_top:
        layout.devices_top = [
            Device("MCCB tong", "MCCB", in_a=100, poles=3, modules=4),
            Device("SPD", "SPD", modules=2),
        ]
    return layout


def load_font(size: int):
    for p in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "C:/Windows/Fonts/arial.ttf",
    ):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def draw_duct(draw: ImageDraw.ImageDraw, x: int, y: int, h: int):
    """Ong luon day dien doc 2 ben."""
    draw.rectangle([x, y, x + DUCT_W, y + h], fill=DUCT_GRAY, outline=(90, 95, 100))
    slot_y = y + 8
    while slot_y < y + h - 8:
        draw.rectangle([x + 6, slot_y, x + DUCT_W - 6, slot_y + 14], fill=(95, 100, 108))
        slot_y += 22


def draw_phase_lights(draw: ImageDraw.ImageDraw, img: Image.Image, x: int, y: int, dev_cache: dict):
    """3 den bao pha L1/L2/L3 - KHONG ve 1 den L."""
    pilot_img = dev_cache.get("PILOT")
    if pilot_img:
        paste_device_image(img, pilot_img, x, y, MX * 3, MY - 20)
    else:
        colors = [(C_RED, "L1"), (C_YELLOW, "L2"), (C_BLUE, "L3")]
        for i, (col, lbl) in enumerate(colors):
            cx = x + i * MX + MX // 2
            cy = y + MY // 2 - 10
            r = 14
            draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col, outline=(60, 60, 60), width=2)
            draw.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=(255, 255, 255, 80))
            draw.text((cx - 8, cy + r + 4), lbl, fill=(40, 40, 40), font=load_font(10))

    draw.text((x, y - 16), "Den bao pha 3P", fill=(60, 60, 60), font=load_font(10))


def draw_device_row(img: Image.Image, draw: ImageDraw.ImageDraw, devices: List[Device],
                    x: int, y: int, row_mods: int, dev_cache: dict, label: str = ""):
    if label:
        draw.text((x, y - 18), label, fill=(50, 50, 50), font=load_font(11))

    # Thanh DIN
    rail_y = y + MY
    draw.rectangle([x, rail_y, x + row_mods * MX, rail_y + RAIL_H], fill=DIN_SILVER, outline=(140, 145, 150))

    slot = 0
    for dev in devices:
        m = max(1, dev.modules)
        if slot + m > row_mods:
            break
        dx = x + slot * MX
        img_key = dev.device_type
        dev_png = dev_cache.get(img_key)
        if dev_png:
            paste_device_image(img, dev_png, dx, y, m * MX, MY)
        else:
            draw.rectangle([dx + 2, y + 2, dx + m * MX - 2, y + MY - 2],
                           fill=(240, 242, 245), outline=(160, 165, 170))
            txt = dev.device_type[:8]
            draw.text((dx + 6, y + MY // 2 - 6), txt, fill=(50, 50, 50), font=load_font(10))

        tag = dev.name[:6] if dev.name else f"{int(dev.in_a)}A"
        draw.text((dx + 4, y + MY - 14), tag, fill=(80, 80, 80), font=load_font(9))
        slot += m


def draw_terminals(draw: ImageDraw.ImageDraw, x: int, y: int):
    """Terminal N (xanh) va PE (vang-xanh)."""
    # Cu thanh dong
    draw.rectangle([x, y, x + 180, y + 28], fill=(184, 130, 70), outline=(140, 100, 50))
    draw.text((x + 4, y + 6), "Busbar", fill=(255, 255, 255), font=load_font(10))

    # N
    nx = x + 200
    draw.rectangle([nx, y, nx + 120, y + 36], fill=C_NEUTRAL, outline=(30, 60, 140))
    for i in range(6):
        draw.rectangle([nx + 8 + i * 18, y + 8, nx + 22 + i * 18, y + 28], fill=(240, 245, 255))
    draw.text((nx + 4, y + 38), "N", fill=C_NEUTRAL, font=load_font(11))

    # PE
    px = nx + 140
    draw.rectangle([px, y, px + 120, y + 36], fill=(50, 160, 70), outline=(30, 100, 40))
    for i in range(6):
        draw.rectangle([px + 8 + i * 18, y + 8, px + 22 + i * 18, y + 28], fill=(220, 240, 200))
    draw.text((px + 4, y + 38), "PE", fill=(30, 120, 50), font=load_font(11))


def render_interior(spec: CabinetSpec) -> Image.Image:
    layout = spec_to_interior(spec)
    row_mods = layout.row_modules
    inner_w = row_mods * MX + DUCT_W * 2 + 40
    inner_h = MY * 3 + RAIL_H * 3 + 120 + 80
    img_w = inner_w + DOOR_W + MARGIN * 2
    img_h = inner_h + MARGIN * 2 + 50

    img = Image.new("RGB", (img_w, img_h), BG)
    draw = ImageDraw.Draw(img)

    # Tu dong
    cab_x = MARGIN
    cab_y = MARGIN + 30
    cab_w = inner_w + 20
    cab_h = inner_h + 20
    draw.rectangle([cab_x, cab_y, cab_x + cab_w + DOOR_W, cab_y + cab_h],
                   fill=CAB_GRAY, outline=CAB_DARK, width=3)
    # Cua mo
    draw.rectangle([cab_x + cab_w, cab_y, cab_x + cab_w + DOOR_W, cab_y + cab_h],
                   fill=(175, 180, 188), outline=CAB_DARK, width=2)
    draw.text((cab_x + cab_w + 12, cab_y + cab_h // 2), "CUA", fill=(80, 80, 80), font=load_font(12))

    # Ngoai that
    ix = cab_x + 10 + DUCT_W
    iy = cab_y + 10
    draw.rectangle([cab_x + 8, cab_y + 8, cab_x + cab_w - 8, cab_y + cab_h - 8],
                   fill=CAB_INNER, outline=(160, 165, 170))

    draw_duct(draw, cab_x + 10, cab_y + 10, cab_h - 20)
    draw_duct(draw, cab_x + cab_w - DUCT_W - 10, cab_y + 10, cab_h - 20)

    # Tieu de
    title = f"{layout.name} - {layout.size}"
    draw.text((cab_x, cab_y - 24), title, fill=(30, 30, 30), font=load_font(16))

    dev_cache = {}
    for key in ("MCCB", "MCB 1P", "MCB 3P", "MCB 2P", "SPD", "SURGE", "CONTACTOR", "PILOT"):
        im = find_device_image(key)
        if im:
            dev_cache[key] = im
    # PILOT 3 phase asset
    pilot_path = os.path.join(SCRIPT_DIR, "devices", "device_pilot_3phase.png")
    if os.path.exists(pilot_path):
        dev_cache["PILOT"] = Image.open(pilot_path).convert("RGBA")
    spd_path = os.path.join(SCRIPT_DIR, "devices", "device_spd.png")
    if os.path.exists(spd_path):
        dev_cache["SPD"] = Image.open(spd_path).convert("RGBA")

    row1_y = iy
    top = layout.devices_top[:2]
    draw_device_row(img, draw, top, ix, row1_y, row_mods, dev_cache, "Nguon vao")

    # 3 den bao pha - sau MCCB/SPD
    pilot_x = ix + sum(max(1, d.modules) for d in top) * MX + 10
    if pilot_x + MX * 3 < ix + row_mods * MX:
        draw_phase_lights(draw, img, pilot_x, row1_y, dev_cache)

    row2_y = row1_y + MY + RAIL_H + 24
    draw_device_row(img, draw, layout.devices_mid[:8], ix, row2_y, row_mods, dev_cache, "Phan phoi")

    row3_y = row2_y + MY + RAIL_H + 24
    draw_device_row(img, draw, layout.devices_mid[8:16], ix, row3_y, row_mods, dev_cache)

    term_y = row3_y + MY + RAIL_H + 30
    draw_terminals(draw, ix, term_y)

    # Day noi don gian mau pha
    for i, col in enumerate([WIRE_R, WIRE_Y, WIRE_B]):
        wx = ix + 20 + i * 30
        draw.line([(wx, row1_y + MY), (wx, term_y)], fill=col, width=3)

    img = ImageEnhance.Contrast(img).enhance(1.04)
    img = ImageEnhance.Sharpness(img).enhance(1.1)
    return img


def main(argv=None):
    import argparse
    parser = argparse.ArgumentParser(description="Render noi that tu dien photorealistic")
    parser.add_argument("--input", "-i")
    parser.add_argument("--output", "-o", default="cabinet_interior.png")
    parser.add_argument("--demo", action="store_true")
    args = parser.parse_args(argv)

    if args.demo or not args.input:
        spec = make_demo()
        spec.name = "DB-01"
        spec.size = "H600xW500xD225"
    elif args.input.lower().endswith(".json"):
        spec = parse_json(args.input)
    else:
        spec = parse_csv(args.input)

    img = render_interior(spec)
    img.save(args.output, "PNG", dpi=(150, 150))
    print(f"OK {args.output} ({img.width}x{img.height}px)")


if __name__ == "__main__":
    main()
