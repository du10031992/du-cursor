#!/usr/bin/env python3
"""
MEP Cabinet Renderer — Render mặt trước tủ điện từ BOQ (CSV/JSON) sang PNG.
Chạy độc lập hoặc gọi từ plugin AutoCAD (qua Process.Start).

Usage:
    python3 render_cabinet.py --input devices.csv --output cabinet.png
    python3 render_cabinet.py --input devices.json --output cabinet.png
    python3 render_cabinet.py --demo --output demo_cabinet.png
"""

import argparse
import csv
import json
import math
import os
import sys
from dataclasses import dataclass, field
from typing import List, Optional

try:
    from PIL import Image, ImageDraw, ImageFont
    PIL_OK = True
except ImportError:
    PIL_OK = False
    print("ERROR: pip install pillow", file=sys.stderr)
    sys.exit(1)


# ─── Constants ──────────────────────────────────────────────────────────────
MODULE_W = 18          # 1 DIN module = 18mm → 18px at 1:1 scale
SCALE = 3              # px per mm  (1mm = 3px)
MX = MODULE_W * SCALE  # px width per module slot = 54
MY = 90                # row height px
RAIL_H = 12
CABINET_PAD_X = 60
CABINET_PAD_Y = 50
BAY_LABEL_W = 110
MODULES_PER_ROW = 24   # default max modules per bay row
HEADER_H = 80
FOOTER_H = 60
BAY_MARGIN = 20

# Colors
C_BG = (30, 35, 42)
C_PANEL_BG = (50, 55, 65)
C_CABINET_BODY = (68, 68, 70)
C_CABINET_EDGE = (100, 100, 105)
C_CABINET_DOOR = (80, 80, 82)
C_DIN_RAIL = (180, 180, 185)
C_BAY_BG = (55, 58, 68)
C_BAY_BORDER = (90, 90, 100)
C_BAY_LABEL_BG = (38, 44, 56)
C_SLOT_EMPTY = (45, 48, 55)
C_SLOT_BORDER = (75, 78, 88)

C_TITLE = (255, 255, 255)
C_SUBTITLE = (180, 195, 210)
C_LABEL = (210, 215, 225)
C_SMALL = (150, 160, 175)
C_ACCENT = (0, 180, 255)
C_GREEN = (60, 200, 100)
C_RED = (220, 60, 60)
C_YELLOW = (240, 180, 30)
C_ORANGE = (240, 130, 30)

DEVICE_COLORS = {
    "MCB":        (45, 130, 200),
    "MCCB":       (30, 100, 160),
    "ELCB":       (60, 150, 100),
    "CONTACTOR":  (160, 80, 200),
    "RELAY":      (180, 120, 40),
    "TIMER":      (100, 140, 180),
    "METER":      (40, 160, 160),
    "PILOT":      (220, 60, 60),
    "SWITCH":     (80, 160, 80),
    "SURGE":      (200, 100, 50),
    "BUSBAR":     (120, 120, 40),
    "OTHER":      (100, 100, 110),
}

DEVICE_MODULES = {
    "MCB 1P": 1, "MCB 2P": 2, "MCB 3P": 3, "MCB 4P": 4,
    "MCCB":   4, "ELCB":   2, "CONTACTOR": 3, "RELAY": 2,
    "TIMER":  2, "METER":  6, "PILOT": 1, "SWITCH": 1,
    "SURGE":  2, "BUSBAR": 6, "TRANSFORMER": 8,
}


# ─── Data Model ─────────────────────────────────────────────────────────────
@dataclass
class Device:
    name: str
    device_type: str = "OTHER"
    in_a: float = 0
    voltage: float = 0
    poles: int = 1
    curve: str = "C"
    qty: int = 1
    manufacturer: str = ""
    code: str = ""
    note: str = ""
    modules: int = 1
    color: tuple = field(default_factory=lambda: (100, 100, 110))

    def __post_init__(self):
        self.modules = self._calc_modules()
        self.color = self._get_color()

    def _calc_modules(self) -> int:
        dtype = self.device_type.upper().strip()
        for key, m in DEVICE_MODULES.items():
            if dtype.startswith(key.upper()):
                return m
        if "MCB" in dtype:
            return max(1, self.poles)
        if "MCCB" in dtype:
            return 4
        if "CONTACTOR" in dtype:
            return 3
        return 1

    def _get_color(self) -> tuple:
        dtype = self.device_type.upper()
        for key, c in DEVICE_COLORS.items():
            if key in dtype:
                return c
        return DEVICE_COLORS["OTHER"]

    @property
    def label(self) -> str:
        parts = [self.device_type]
        if self.poles > 1 or "MCB" in self.device_type.upper():
            parts.append(f"{self.poles}P")
        if self.in_a > 0:
            parts.append(f"{int(self.in_a)}A")
        return " ".join(parts)


@dataclass
class Bay:
    name: str = "NGĂN"
    label: str = ""
    devices: List[Device] = field(default_factory=list)
    max_modules: int = MODULES_PER_ROW

    @property
    def total_modules(self):
        return sum(d.modules * d.qty for d in self.devices)

    @property
    def used_modules(self):
        return self.total_modules

    @property
    def rows_needed(self) -> int:
        return max(1, math.ceil(self.total_modules / self.max_modules))


@dataclass
class CabinetSpec:
    name: str = "DB-T1A"
    cabinet_type: str = "Tủ phân phối"
    size: str = "600x800x200"
    floor_level: str = "FL+1.400"
    bays: List[Bay] = field(default_factory=list)

    @property
    def total_modules(self):
        return sum(b.total_modules for b in self.bays)


# ─── Parsers ────────────────────────────────────────────────────────────────
def parse_csv(path: str) -> CabinetSpec:
    spec = CabinetSpec()
    current_bay = Bay(name="NGĂN 1", label="Phân phối")
    spec.bays.append(current_bay)

    with open(path, encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        for row in reader:
            row = {k.strip(): v.strip() for k, v in row.items() if k}
            bay_name = row.get("bay", row.get("ngan", ""))
            if bay_name and bay_name != current_bay.name:
                current_bay = Bay(name=bay_name, label=row.get("bay_label", bay_name))
                spec.bays.append(current_bay)

            name = row.get("name", row.get("ten", row.get("ten_thiet_bi", "")))
            dtype = row.get("type", row.get("loai", row.get("device_type", name)))
            try:
                in_a = float(row.get("in_a", row.get("in", row.get("dong", 0)) or 0))
            except ValueError:
                in_a = 0
            try:
                qty = int(row.get("qty", row.get("sl", row.get("so_luong", 1)) or 1))
            except ValueError:
                qty = 1
            try:
                poles = int(row.get("poles", row.get("pha", 1)) or 1)
            except ValueError:
                poles = 1

            dev = Device(
                name=name or dtype,
                device_type=dtype,
                in_a=in_a,
                poles=poles,
                qty=qty,
                manufacturer=row.get("manufacturer", row.get("nha_san_xuat", "")),
                code=row.get("code", row.get("ma", "")),
                note=row.get("note", row.get("ghi_chu", "")),
            )
            if dev.name:
                current_bay.devices.append(dev)

    return spec


def parse_json(path: str) -> CabinetSpec:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)

    # Support both array (list of cabinets) and object (single cabinet)
    if isinstance(data, list):
        data = data[0] if data else {}

    spec = CabinetSpec(
        name=data.get("name", "DB"),
        cabinet_type=data.get("type", "Tủ phân phối"),
        size=data.get("size", "600x800x200"),
        floor_level=data.get("floor", "FL+1.400"),
    )

    for bd in data.get("bays", []):
        bay = Bay(name=bd.get("name", "NGĂN"), label=bd.get("label", ""))
        for dd in bd.get("devices", []):
            dev = Device(
                name=dd.get("name", dd.get("type", "")),
                device_type=dd.get("type", "OTHER"),
                in_a=float(dd.get("in_a", 0)),
                voltage=float(dd.get("voltage", 230)),
                poles=int(dd.get("poles", 1)),
                curve=dd.get("curve", "C"),
                qty=int(dd.get("qty", 1)),
                manufacturer=dd.get("manufacturer", ""),
                code=dd.get("code", ""),
                note=dd.get("note", ""),
            )
            bay.devices.append(dev)
        spec.bays.append(bay)

    return spec


def make_demo_spec() -> CabinetSpec:
    spec = CabinetSpec(name="DB-T1A", cabinet_type="Tủ phân phối", size="600x800x200", floor_level="FL+1.400")

    bay1 = Bay(name="NGĂN 1", label="Nguồn vào")
    bay1.devices = [
        Device("MCCB tổng", "MCCB", in_a=100, poles=3, qty=1, manufacturer="Schneider"),
        Device("MCB dự phòng", "MCB 1P", in_a=32, poles=1, qty=2),
    ]

    bay2 = Bay(name="NGĂN 2", label="Phân phối")
    bay2.devices = [
        Device("MCB nhánh 1", "MCB 1P", in_a=10, poles=1, qty=4),
        Device("MCB nhánh 2", "MCB 1P", in_a=16, poles=1, qty=4),
        Device("MCB nhánh 3", "MCB 1P", in_a=20, poles=1, qty=3),
        Device("MCB 3P", "MCB 3P", in_a=16, poles=3, qty=2),
        Device("MCB 3P lớn", "MCB 3P", in_a=32, poles=3, qty=1),
    ]

    bay3 = Bay(name="NGĂN 3", label="Điều khiển")
    bay3.devices = [
        Device("Contactor K1", "CONTACTOR", in_a=25, poles=3, qty=2, manufacturer="LS"),
        Device("Relay nhiệt", "RELAY", in_a=10, poles=0, qty=2),
        Device("Timer", "TIMER", in_a=0, poles=0, qty=1),
        Device("Đồng hồ A", "METER", in_a=0, poles=3, qty=1),
    ]

    spec.bays = [bay1, bay2, bay3]
    return spec


# ─── Renderer ───────────────────────────────────────────────────────────────
class CabinetRenderer:
    def __init__(self, spec: CabinetSpec, modules_per_row: int = MODULES_PER_ROW):
        self.spec = spec
        self.modules_per_row = modules_per_row
        for bay in spec.bays:
            bay.max_modules = modules_per_row
        self._load_fonts()

    def _load_fonts(self):
        sizes = [28, 22, 17, 13, 11]
        self.fonts = {}
        for s in sizes:
            try:
                self.fonts[s] = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", s)
            except Exception:
                self.fonts[s] = ImageFont.load_default()

        self.font_title = self.fonts[28]
        self.font_sub = self.fonts[22]
        self.font_bay = self.fonts[17]
        self.font_dev = self.fonts[13]
        self.font_small = self.fonts[11]

    def _calc_layout(self):
        self.bay_row_count = [b.rows_needed for b in self.spec.bays]
        total_rows = sum(self.bay_row_count)
        bay_count = len(self.spec.bays)
        content_w = BAY_LABEL_W + self.modules_per_row * MX + CABINET_PAD_X * 2
        content_h = (total_rows * (MY + RAIL_H) + bay_count * (BAY_MARGIN + 30) + CABINET_PAD_Y * 2)
        self.img_w = max(1100, content_w + 40)
        self.img_h = HEADER_H + content_h + FOOTER_H + 40
        self.cabinet_x = 20
        self.cabinet_y = HEADER_H + 10
        self.cabinet_w = content_w
        self.cabinet_h = content_h

    def render(self) -> Image.Image:
        self._calc_layout()
        img = Image.new("RGB", (self.img_w, self.img_h), C_BG)
        draw = ImageDraw.Draw(img)

        self._draw_header(draw)
        self._draw_cabinet_body(draw)
        self._draw_bays(draw, img)
        self._draw_footer(draw)
        self._draw_legend(draw)

        return img

    def _draw_header(self, draw: ImageDraw.ImageDraw):
        draw.rectangle([0, 0, self.img_w, HEADER_H], fill=(25, 30, 40))
        draw.rectangle([0, HEADER_H - 2, self.img_w, HEADER_H], fill=C_ACCENT)

        title = f"  CẤU HÌNH TỦ ĐIỆN  —  {self.spec.name}"
        draw.text((20, 12), title, fill=C_TITLE, font=self.font_title)
        draw.text((20, 46), f"{self.spec.cabinet_type}  |  {self.spec.size} mm  |  {self.spec.floor_level}", fill=C_SUBTITLE, font=self.font_sub)

        mods = sum(b.total_modules for b in self.spec.bays)
        total_slots = len(self.spec.bays) * self.modules_per_row * max(b.rows_needed for b in (self.spec.bays or [Bay()]))
        draw.text((self.img_w - 340, 12), f"Tổng module: {mods}", fill=C_LABEL, font=self.font_sub)
        draw.text((self.img_w - 340, 42), f"Số ngăn: {len(self.spec.bays)}", fill=C_SMALL, font=self.font_dev)

    def _draw_cabinet_body(self, draw: ImageDraw.ImageDraw):
        x0, y0 = self.cabinet_x, self.cabinet_y
        x1, y1 = x0 + self.cabinet_w, y0 + self.cabinet_h
        for dx in range(5, 0, -1):
            shade = tuple(max(0, c - dx * 10) for c in C_CABINET_BODY)
            draw.rectangle([x0 + dx, y0 + dx, x1 + dx, y1 + dx], fill=shade)
        draw.rectangle([x0, y0, x1, y1], fill=C_CABINET_DOOR, outline=C_CABINET_EDGE, width=3)

    def _draw_bays(self, draw: ImageDraw.ImageDraw, img: Image.Image):
        cx = self.cabinet_x + CABINET_PAD_X
        cy = self.cabinet_y + CABINET_PAD_Y

        for bay_idx, bay in enumerate(self.spec.bays):
            rows = bay.rows_needed
            bay_h = rows * (MY + RAIL_H) + 30
            bay_w = BAY_LABEL_W + self.modules_per_row * MX

            draw.rectangle([cx, cy, cx + bay_w, cy + bay_h], fill=C_BAY_BG, outline=C_BAY_BORDER, width=2)
            draw.rectangle([cx, cy, cx + BAY_LABEL_W - 4, cy + bay_h], fill=C_BAY_LABEL_BG, outline=C_BAY_BORDER, width=1)
            draw.text((cx + 8, cy + 10), f"{bay.name}", fill=C_ACCENT, font=self.font_bay)
            if bay.label:
                draw.text((cx + 8, cy + 34), bay.label, fill=C_SMALL, font=self.font_small)

            self._draw_row_slots(draw, img, bay, cx + BAY_LABEL_W, cy + 10, rows)

            used = bay.used_modules
            total = rows * self.modules_per_row
            pct = used / total if total > 0 else 0
            bar_x = cx + 8
            bar_y = cy + bay_h - 22
            bar_w = BAY_LABEL_W - 16
            draw.rectangle([bar_x, bar_y, bar_x + bar_w, bar_y + 10], fill=C_SLOT_EMPTY, outline=C_SLOT_BORDER, width=1)
            filled_c = C_GREEN if pct < 0.7 else (C_YELLOW if pct < 0.9 else C_RED)
            draw.rectangle([bar_x, bar_y, bar_x + int(bar_w * pct), bar_y + 10], fill=filled_c)
            draw.text((bar_x, bar_y - 14), f"{used}/{total} mod", fill=C_SMALL, font=self.font_small)

            cy += bay_h + BAY_MARGIN

    def _draw_row_slots(self, draw: ImageDraw.ImageDraw, img: Image.Image, bay: Bay, rx: int, ry: int, rows: int):
        for row in range(rows):
            rail_y = ry + row * (MY + RAIL_H)
            draw.rectangle([rx, rail_y + MY, rx + self.modules_per_row * MX, rail_y + MY + RAIL_H], fill=C_DIN_RAIL, outline=(140, 140, 145), width=1)
            for slot in range(self.modules_per_row):
                sx = rx + slot * MX
                draw.rectangle([sx + 1, rail_y + 1, sx + MX - 1, rail_y + MY - 1], fill=C_SLOT_EMPTY, outline=C_SLOT_BORDER, width=1)
                num = row * self.modules_per_row + slot + 1
                draw.text((sx + MX // 2 - 8, rail_y + MY // 2 - 6), f"{num:02d}", fill=C_SLOT_BORDER, font=self.font_small)

        cur_row = 0
        cur_slot = 0
        for dev in bay.devices:
            for _ in range(dev.qty):
                m = dev.modules
                if cur_slot + m > self.modules_per_row:
                    cur_row += 1
                    cur_slot = 0
                if cur_row >= rows:
                    break

                dx = rx + cur_slot * MX
                dy = ry + cur_row * (MY + RAIL_H)
                self._draw_device_block(draw, img, dev, dx, dy, m)
                cur_slot += m

    def _draw_device_block(self, draw: ImageDraw.ImageDraw, img: Image.Image, dev: Device, dx: int, dy: int, mods: int):
        w = mods * MX - 2
        h = MY - 2
        c = dev.color
        c_dark = tuple(max(0, x - 40) for x in c)
        c_light = tuple(min(255, x + 60) for x in c)

        for i in range(h):
            t = i / h
            row_color = tuple(int(c[j] * (1 - t * 0.3) + c_dark[j] * t * 0.3) for j in range(3))
            draw.line([(dx + 1, dy + 1 + i), (dx + w, dy + 1 + i)], fill=row_color)

        draw.rectangle([dx + 1, dy + 1, dx + w, dy + h], outline=c_light, width=2)

        if w > 50:
            self._draw_device_face(draw, dev, dx + 1, dy + 1, w, h)

    def _draw_device_face(self, draw: ImageDraw.ImageDraw, dev: Device, x: int, y: int, w: int, h: int):
        dtype = dev.device_type.upper()

        if "MCB" in dtype or "MCCB" in dtype or "ELCB" in dtype:
            toggle_w = max(10, min(20, w - 8))
            toggle_h = max(20, min(40, h - 20))
            tx = x + (w - toggle_w) // 2
            ty = y + 8
            draw.rectangle([tx, ty, tx + toggle_w, ty + toggle_h], fill=(240, 240, 245), outline=(60, 60, 70), width=2)
            draw.rectangle([tx + 2, ty + 2, tx + toggle_w - 2, ty + toggle_h // 2], fill=(220, 60, 50), outline=(180, 40, 40), width=1)
            draw.rectangle([tx + 2, ty + toggle_h // 2, tx + toggle_w - 2, ty + toggle_h - 2], fill=(60, 100, 200), outline=(40, 80, 180), width=1)

        elif "CONTACTOR" in dtype:
            cw = max(14, w - 12)
            ch = max(20, h - 24)
            cx2 = x + (w - cw) // 2
            cy2 = y + 10
            draw.rectangle([cx2, cy2, cx2 + cw, cy2 + ch], fill=(100, 50, 160), outline=(140, 90, 210), width=2)
            for i in range(3):
                px = cx2 + 4 + i * (cw - 8) // 3
                draw.ellipse([px, cy2 + 4, px + 6, cy2 + 10], fill=(200, 200, 50))

        elif "RELAY" in dtype:
            rw = max(10, w - 8)
            rh = max(16, h - 24)
            draw.rectangle([x + 4, y + 8, x + 4 + rw, y + 8 + rh], fill=(160, 100, 30), outline=(210, 150, 60), width=2)
            draw.ellipse([x + rw // 2 - 4, y + 8 + rh // 2 - 4, x + rw // 2 + 4, y + 8 + rh // 2 + 4], fill=(220, 180, 60))

        elif "METER" in dtype or "DONG HO" in dtype:
            mw = max(16, w - 6)
            mh = max(20, h - 20)
            draw.rectangle([x + 3, y + 6, x + 3 + mw, y + 6 + mh], fill=(10, 30, 60), outline=(0, 180, 255), width=2)
            mid_x = x + 3 + mw // 2
            mid_y = y + 6 + mh // 2
            draw.arc([mid_x - 12, mid_y - 10, mid_x + 12, mid_y + 10], 200, 340, fill=(0, 200, 255), width=2)
            draw.line([mid_x, mid_y, mid_x + 8, mid_y - 5], fill=(0, 255, 150), width=2)

        # Label
        label = dev.label if len(dev.label) <= 8 else dev.label[:7] + "."
        draw.text((x + 2, y + h - 16), label, fill=(240, 245, 255), font=self.font_small)

    def _draw_footer(self, draw: ImageDraw.ImageDraw):
        fy = self.img_h - FOOTER_H
        draw.rectangle([0, fy, self.img_w, self.img_h], fill=(22, 26, 34))
        draw.rectangle([0, fy, self.img_w, fy + 2], fill=C_ACCENT)

        all_devices = [(d, bay.name) for bay in self.spec.bays for d in bay.devices]
        total_mods = sum(d.modules * d.qty for d, _ in all_devices)
        total_devices = sum(d.qty for d, _ in all_devices)
        used_slots = sum(b.rows_needed * b.max_modules for b in self.spec.bays)

        stats = (f"  {self.spec.name}  |  {total_devices} thiết bị  |  "
                 f"{total_mods} module đã dùng / {used_slots} tổng  |  "
                 f"Render: MEP Drawing Tool v0.16")
        draw.text((20, fy + 12), stats, fill=C_LABEL, font=self.font_dev)
        draw.text((20, fy + 34), "  Lưu ý: Bản vẽ tham khảo — xác nhận với kỹ sư trước khi thi công.", fill=C_SMALL, font=self.font_small)

    def _draw_legend(self, draw: ImageDraw.ImageDraw):
        items = [
            ("MCB", DEVICE_COLORS["MCB"]),
            ("MCCB", DEVICE_COLORS["MCCB"]),
            ("Contactor", DEVICE_COLORS["CONTACTOR"]),
            ("Relay", DEVICE_COLORS["RELAY"]),
            ("Meter", DEVICE_COLORS["METER"]),
        ]
        lx = self.cabinet_x + self.cabinet_w + 30
        ly = self.cabinet_y + 20
        draw.text((lx, ly), "LEGEND", fill=C_ACCENT, font=self.font_bay)
        ly += 28
        for label, color in items:
            draw.rectangle([lx, ly, lx + 24, ly + 16], fill=color, outline=(180, 180, 190), width=1)
            draw.text((lx + 30, ly + 1), label, fill=C_LABEL, font=self.font_dev)
            ly += 24

        ly += 20
        draw.text((lx, ly), "TRẠNG THÁI", fill=C_ACCENT, font=self.font_bay)
        ly += 28
        for label, color in [("Bình thường (< 70%)", C_GREEN), ("Đầy (~70-90%)", C_YELLOW), ("Quá tải (>90%)", C_RED)]:
            draw.rectangle([lx, ly, lx + 24, ly + 10], fill=color)
            draw.text((lx + 30, ly), label, fill=C_SMALL, font=self.font_small)
            ly += 18


# ─── CLI ────────────────────────────────────────────────────────────────────
def main():
    parser = argparse.ArgumentParser(description="MEP Cabinet Renderer")
    parser.add_argument("--input", "-i", help="File CSV hoặc JSON khối lượng thiết bị")
    parser.add_argument("--output", "-o", default="cabinet.png", help="Đường dẫn file PNG đầu ra")
    parser.add_argument("--demo", action="store_true", help="Render tủ demo")
    parser.add_argument("--modules", type=int, default=24, help="Số module mỗi hàng (mặc định 24)")
    args = parser.parse_args()

    if args.demo or not args.input:
        spec = make_demo_spec()
    elif args.input.lower().endswith(".json"):
        spec = parse_json(args.input)
    else:
        spec = parse_csv(args.input)

    renderer = CabinetRenderer(spec, modules_per_row=args.modules)
    img = renderer.render()
    img.save(args.output, "PNG", dpi=(150, 150))
    print(f"OK {args.output}  ({img.width}x{img.height}px)")


if __name__ == "__main__":
    main()
