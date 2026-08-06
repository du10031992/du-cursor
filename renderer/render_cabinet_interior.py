#!/usr/bin/env python3
"""
Render noi that tu dien - bo tri gon gang tren thanh DIN tu block thiet bi that (AI PNG).
- Dong goi thiet bi theo nhom (nguon / phan phoi / dieu khien)
- Xep sat trai, chen module blank phia duoi moi hang
- 3 den bao pha L1/L2/L3 tren cua tu (thuc te)
"""

import os
import re
from dataclasses import dataclass, field
from typing import List, Optional, Tuple

from PIL import Image, ImageDraw, ImageEnhance, ImageFont

from render_cabinet import (
    CabinetSpec,
    Device,
    Bay,
    parse_json,
    parse_csv,
    make_demo,
    find_device_image,
    paste_device_image,
    DEVICE_MODULES,
    SCRIPT_DIR,
)

BG = (235, 237, 240)
CAB_GRAY = (188, 192, 198)
CAB_DARK = (155, 160, 168)
CAB_INNER = (210, 214, 220)
DIN_SILVER = (200, 205, 212)
DUCT_GRAY = (120, 125, 132)
BLANK_MOD = (248, 249, 252)
C_RED = (220, 45, 45)
C_YELLOW = (245, 195, 25)
C_BLUE = (35, 95, 210)
C_NEUTRAL = (45, 85, 200)
WIRE_R, WIRE_Y, WIRE_B = (200, 40, 40), (230, 180, 30), (40, 80, 200)

MODULE_W = 18
SCALE = 4
MX = MODULE_W * SCALE
MY = 118
RAIL_H = 10
ROW_GAP = 14
MARGIN = 36
DOOR_W = 100
DUCT_W = 32
LABEL_H = 16
TERM_H = 52


@dataclass
class DinRow:
    label: str
    devices: List[Device] = field(default_factory=list)


def load_font(size: int):
    for p in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "C:/Windows/Fonts/arial.ttf",
    ):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def parse_cabinet_width_modules(size: str, default: int = 18) -> int:
    """W500 -> 500/18 ~ 27 module; gioi han hop ly cho tu DB."""
    m = re.search(r"W(\d+)", size or "", re.I)
    if not m:
        return default
    w_mm = int(m.group(1))
    mods = max(6, w_mm // MODULE_W)
    return min(mods, 24)


def device_modules(dev: Device) -> int:
    if dev.modules and dev.modules > 0:
        return dev.modules
    t = dev.device_type.upper()
    for key, val in DEVICE_MODULES.items():
        if t.startswith(key.upper()) or key.upper() in t:
            return val
    if "MCB" in t:
        return max(1, dev.poles or 1)
    if "MCCB" in t:
        return 4
    if "CONTACTOR" in t:
        return 4
    if "RELAY" in t:
        return 3
    if "TIMER" in t:
        return 4
    if "METER" in t:
        return 6
    if "SPD" in t or "SURGE" in t:
        return 2
    return 2


def device_sort_key(dev: Device) -> Tuple[int, str]:
    t = dev.device_type.upper()
    n = (dev.name or "").upper()
    if "MCCB" in t:
        return (0, n)
    if "SPD" in t or "SURGE" in t:
        return (1, n)
    if "MCB 3P" in t or (("MCB" in t) and dev.poles >= 3):
        return (2, n)
    if "ELCB" in t or "RCBO" in t:
        return (3, n)
    if "DEN" in n or "CHIEU" in n or "LIGHT" in n or "MCB" in t and "DEN" in n:
        return (10, n)
    if "O CAM" in n or "SOCKET" in n or "OCAM" in n.replace(" ", ""):
        return (11, n)
    if "AC" in n or "DIU" in n or "HOA" in n:
        return (12, n)
    if "CONTACTOR" in t:
        return (20, n)
    if "RELAY" in t:
        return (21, n)
    if "TIMER" in t:
        return (22, n)
    if "METER" in t:
        return (23, n)
    if "MCB 1P" in t or "MCB" in t:
        return (15, n)
    return (30, n)


def classify_devices(all_devs: List[Device]) -> Tuple[List[Device], List[Device], List[Device]]:
    source, distrib, control = [], [], []
    for d in all_devs:
        t = d.device_type.upper()
        n = (d.name or "").upper()
        if "PILOT" in t or "INDICATOR" in t or "DEN BAO PHA" in n:
            continue
        if "MCCB" in t:
            source.append(d)
        elif "SPD" in t or "SURGE" in t:
            source.append(d)
        elif "CONTACTOR" in t or "RELAY" in t or "TIMER" in t or "METER" in t:
            control.append(d)
        else:
            distrib.append(d)

    source.sort(key=device_sort_key)
    distrib.sort(key=device_sort_key)
    control.sort(key=device_sort_key)
    return source, distrib, control


def make_db01_demo() -> CabinetSpec:
    """Layout gon gang cho tu DB-01 H600xW500xD225."""
    spec = CabinetSpec(
        "DB-01", "TD-3P-100A", "Tu phan phoi", "400 VAC", 100,
        "H600xW500xD225", "FL+1.400", "2025",
    )
    b1 = Bay("NGAN 1", "Nguon vao", row_modules=18)
    b1.devices = [
        Device("MCCB tong", "MCCB", "CVS100F", 100, 3, 1, "Schneider", modules=4),
        Device("Chong set", "SPD", "iPRD", 0, 0, 1, "Schneider", modules=2),
    ]
    b2 = Bay("NGAN 2", "Phan phoi", row_modules=18)
    b2.devices = (
        [Device("MCB den", "MCB 1P", "Easy9", 10, 1, 1, "Schneider", modules=1) for _ in range(6)]
        + [Device("MCB o cam", "MCB 1P", "Easy9", 16, 1, 1, "Schneider", modules=1) for _ in range(6)]
        + [Device("MCB dieu hoa", "MCB 1P", "Easy9", 20, 1, 1, "Schneider", modules=1) for _ in range(4)]
        + [Device("MCB 3P", "MCB 3P", "Easy9", 16, 3, 1, "Schneider", modules=3)]
    )
    b3 = Bay("NGAN 3", "Dieu khien", row_modules=18)
    b3.devices = [
        Device("Contactor 1", "CONTACTOR", "GMC-25", 25, 3, 1, "LS", modules=4),
        Device("Contactor 2", "CONTACTOR", "GMC-25", 25, 3, 1, "LS", modules=4),
        Device("Relay nhiet", "RELAY", "MT-32", 25, 3, 1, "LS", modules=3),
    ]
    spec.bays = [b1, b2, b3]
    return spec


def pack_into_rows(devices: List[Device], row_modules: int, max_rows: int = 99) -> List[List[Device]]:
    rows: List[List[Device]] = []
    row: List[Device] = []
    used = 0
    for dev in devices:
        m = device_modules(dev)
        if m > row_modules:
            m = row_modules
        if used + m > row_modules:
            if row:
                rows.append(row)
            if len(rows) >= max_rows:
                break
            row = [dev]
            used = m
        else:
            row.append(dev)
            used += m
    if row and len(rows) < max_rows:
        rows.append(row)
    return rows


def build_din_layout(spec: CabinetSpec) -> Tuple[str, str, int, List[DinRow]]:
    all_devs: List[Device] = []
    for bay in spec.bays:
        all_devs.extend(bay.devices)

    row_mods = parse_cabinet_width_modules(spec.size)
    for bay in spec.bays:
        if bay.row_modules and bay.row_modules <= row_mods:
            row_mods = min(row_mods, bay.row_modules)

    source, distrib, control = classify_devices(all_devs)
    if not source:
        source = [
            Device("MCCB tong", "MCCB", in_a=100, poles=3, modules=4),
            Device("SPD", "SPD", modules=2),
        ]

    rows: List[DinRow] = []
    rows.append(DinRow("Nguon vao", source))

    distrib_rows = pack_into_rows(distrib, row_mods, max_rows=3)
    labels = ["Chieu sang / o cam", "Phan phoi tiep", "Mach con lai"]
    for i, r in enumerate(distrib_rows):
        rows.append(DinRow(labels[i] if i < len(labels) else f"Hang {i+2}", r))

    if control:
        ctrl_rows = pack_into_rows(control, row_mods, max_rows=1)
        for r in ctrl_rows:
            rows.append(DinRow("Dieu khien", r))

    return spec.name or "DB-01", spec.size or "H600xW500xD225", row_mods, rows


def resolve_dev_image(dev: Device, cache: dict) -> Optional[Image.Image]:
    t = dev.device_type.upper()
    keys = []
    if "MCCB" in t:
        keys = ["MCCB"]
    elif "MCB 3P" in t or ("MCB" in t and device_modules(dev) >= 3):
        keys = ["MCB 3P", "MCB 2P", "MCB 1P"]
    elif "MCB" in t:
        keys = ["MCB 1P"]
    elif "SPD" in t or "SURGE" in t:
        keys = ["SPD", "SURGE"]
    elif "CONTACTOR" in t:
        keys = ["CONTACTOR"]
    elif "RELAY" in t:
        keys = ["RELAY"]
    elif "TIMER" in t:
        keys = ["TIMER"]
    elif "METER" in t:
        keys = ["METER"]
    for k in keys:
        if k in cache:
            return cache[k]
    img = find_device_image(dev.device_type)
    return img


def load_dev_cache() -> dict:
    cache = {}
    for key in ("MCCB", "MCB 1P", "MCB 3P", "SPD", "CONTACTOR", "RELAY", "TIMER", "METER", "PILOT"):
        im = find_device_image(key)
        if im:
            cache[key] = im
    for fname, key in (
        ("device_pilot_3phase.png", "PILOT"),
        ("device_spd.png", "SPD"),
    ):
        path = os.path.join(SCRIPT_DIR, "devices", fname)
        if os.path.exists(path):
            cache[key] = Image.open(path).convert("RGBA")
    return cache


def draw_duct(draw: ImageDraw.ImageDraw, x: int, y: int, h: int):
    draw.rectangle([x, y, x + DUCT_W, y + h], fill=DUCT_GRAY, outline=(90, 95, 100))
    sy = y + 6
    while sy < y + h - 10:
        draw.rectangle([x + 5, sy, x + DUCT_W - 5, sy + 12], fill=(95, 100, 108))
        sy += 20


def draw_blank_modules(draw: ImageDraw.ImageDraw, x: int, y: int, count: int, h: int):
    for i in range(count):
        bx = x + i * MX
        draw.rectangle([bx + 1, y + 2, bx + MX - 2, y + h - 2], fill=BLANK_MOD, outline=(200, 205, 210))


def draw_din_row(img: Image.Image, draw: ImageDraw.ImageDraw, row: DinRow,
                 x: int, y: int, row_mods: int, cache: dict) -> int:
    """Ve 1 hang DIN, tra ve chieu cao hang."""
    draw.text((x, y), row.label, fill=(55, 55, 55), font=load_font(10))
    dy = y + LABEL_H
    rail_y = dy + MY

    draw.rectangle([x, rail_y, x + row_mods * MX, rail_y + RAIL_H], fill=DIN_SILVER, outline=(150, 155, 160))

    slot = 0
    for dev in row.devices:
        m = device_modules(dev)
        if slot + m > row_mods:
            break
        dx = x + slot * MX
        png = resolve_dev_image(dev, cache)
        if png:
            paste_device_image(img, png, dx, dy, m * MX, MY)
        else:
            draw.rectangle([dx + 2, dy + 2, dx + m * MX - 2, dy + MY - 2],
                           fill=(245, 246, 248), outline=(180, 185, 190))
            draw.text((dx + 4, dy + MY // 2 - 5), dev.device_type[:6], fill=(70, 70, 70), font=load_font(9))

        tag = (dev.name or dev.device_type)[:8]
        if dev.in_a:
            tag = f"{int(dev.in_a)}A"
        draw.text((dx + 3, dy + MY - 13), tag, fill=(90, 90, 90), font=load_font(8))
        slot += m

    if slot < row_mods:
        draw_blank_modules(draw, x + slot * MX, dy, row_mods - slot, MY)

    return LABEL_H + MY + RAIL_H + ROW_GAP


def draw_phase_lights_on_door(img: Image.Image, draw: ImageDraw.ImageDraw,
                              door_x: int, door_y: int, door_w: int, door_h: int, cache: dict):
    """Den bao pha tren cua tu - dung thuc te hon rail trong tu."""
    cx = door_x + door_w // 2
    top = door_y + 36
    draw.text((door_x + 8, door_y + 10), "Den bao pha", fill=(60, 60, 60), font=load_font(10))

    pilot = cache.get("PILOT")
    pw, ph = MX * 3 - 8, MY - 28
    px = cx - pw // 2
    if pilot:
        paste_device_image(img, pilot, px, top, pw, ph)
    else:
        for i, (col, lbl) in enumerate([(C_RED, "L1"), (C_YELLOW, "L2"), (C_BLUE, "L3")]):
            ox = px + i * (pw // 3) + (pw // 3) // 2
            r = 12
            draw.ellipse([ox - r, top + 20 - r, ox + r, top + 20 + r], fill=col, outline=(50, 50, 50), width=2)
            draw.text((ox - 7, top + 20 + r + 2), lbl, fill=(40, 40, 40), font=load_font(9))

    draw.text((door_x + 8, door_y + door_h - 28), "DB-01", fill=(100, 100, 100), font=load_font(9))


def draw_terminals(draw: ImageDraw.ImageDraw, x: int, y: int, width: int):
    bw = min(160, width // 3)
    draw.rectangle([x, y, x + bw, y + 24], fill=(184, 130, 70), outline=(140, 100, 50))
    draw.text((x + 6, y + 5), "Thanh dong L1-L2-L3", fill=(255, 255, 255), font=load_font(9))

    nx = x + bw + 16
    nw = min(110, (width - bw - 32) // 2)
    draw.rectangle([nx, y, nx + nw, y + 28], fill=C_NEUTRAL, outline=(30, 60, 140))
    for i in range(5):
        draw.rectangle([nx + 6 + i * 18, y + 6, nx + 20 + i * 18, y + 22], fill=(240, 245, 255))
    draw.text((nx + 4, y + 30), "N", fill=C_NEUTRAL, font=load_font(10))

    px = nx + nw + 12
    draw.rectangle([px, y, px + nw, y + 28], fill=(50, 160, 70), outline=(30, 100, 40))
    for i in range(5):
        draw.rectangle([px + 6 + i * 18, y + 6, px + 20 + i * 18, y + 22], fill=(220, 240, 200))
    draw.text((px + 4, y + 30), "PE", fill=(30, 120, 50), font=load_font(10))


def render_interior(spec: CabinetSpec) -> Image.Image:
    name, size, row_mods, din_rows = build_din_layout(spec)
    cache = load_dev_cache()

    rail_area_w = row_mods * MX
    inner_w = rail_area_w + DUCT_W * 2 + 24
    rows_h = sum(LABEL_H + MY + RAIL_H + ROW_GAP for _ in din_rows)
    inner_h = rows_h + TERM_H + 36
    img_w = inner_w + DOOR_W + MARGIN * 2
    img_h = inner_h + MARGIN * 2 + 40

    img = Image.new("RGB", (img_w, img_h), BG)
    draw = ImageDraw.Draw(img)

    cab_x, cab_y = MARGIN, MARGIN + 28
    cab_w, cab_h = inner_w + 16, inner_h + 16

    draw.text((cab_x, cab_y - 22), f"{name} - {size}", fill=(30, 30, 30), font=load_font(15))
    draw.rectangle([cab_x, cab_y, cab_x + cab_w + DOOR_W, cab_y + cab_h], fill=CAB_GRAY, outline=CAB_DARK, width=2)

    door_x = cab_x + cab_w
    draw.rectangle([door_x, cab_y, door_x + DOOR_W, cab_y + cab_h], fill=(178, 182, 190), outline=CAB_DARK, width=2)
    draw_phase_lights_on_door(img, draw, door_x, cab_y, DOOR_W, cab_h, cache)

    ix = cab_x + 8 + DUCT_W
    iy = cab_y + 8
    draw.rectangle([cab_x + 6, cab_y + 6, cab_x + cab_w - 6, cab_y + cab_h - 6], fill=CAB_INNER, outline=(170, 175, 180))
    draw_duct(draw, cab_x + 8, cab_y + 8, cab_h - 16)
    draw_duct(draw, cab_x + cab_w - DUCT_W - 8, cab_y + 8, cab_h - 16)

    ry = iy
    for din_row in din_rows:
        rh = draw_din_row(img, draw, din_row, ix, ry, row_mods, cache)
        ry += rh

    term_y = cab_y + cab_h - TERM_H - 10
    draw_terminals(draw, ix, term_y, rail_area_w)

    for i, col in enumerate([WIRE_R, WIRE_Y, WIRE_B]):
        wx = ix - DUCT_W + 8 + i * 10
        draw.line([(wx, iy + LABEL_H), (wx, term_y)], fill=col, width=2)

    img = ImageEnhance.Contrast(img).enhance(1.03)
    img = ImageEnhance.Sharpness(img).enhance(1.08)
    return img


def main(argv=None):
    import argparse
    parser = argparse.ArgumentParser(description="Render noi that tu dien - bo tri gon gang")
    parser.add_argument("--input", "-i")
    parser.add_argument("--output", "-o", default="cabinet_interior.png")
    parser.add_argument("--demo", action="store_true")
    args = parser.parse_args(argv)

    if args.demo or not args.input:
        spec = make_db01_demo()
    elif args.input.lower().endswith(".json"):
        spec = parse_json(args.input)
    else:
        spec = parse_csv(args.input)

    img = render_interior(spec)
    img.save(args.output, "PNG", dpi=(150, 150))
    print(f"OK {args.output} ({img.width}x{img.height}px)")


if __name__ == "__main__":
    main()
