#!/usr/bin/env python3
"""
MEP Cabinet Renderer v3 — Composite real device images (PNG) vào layout tủ.
Tự động map loại thiết bị → ảnh PNG → scale → paste vào đúng vị trí DIN.

Usage:
    python3 render_cabinet_v3.py --input TD-01_full.json --output cabinet.png
    python3 render_cabinet_v3.py --input TD-01_full.csv  --output cabinet.png
    python3 render_cabinet_v3.py --demo                  --output cabinet.png
"""

import argparse, csv, json, math, os, sys
from dataclasses import dataclass, field
from typing import List, Optional

try:
    from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance, ImageChops
except ImportError:
    print("pip install pillow"); sys.exit(1)

# ── Layout constants (px) ─────────────────────────────────────────────────
MODULE_W   = 18           # mm per DIN slot
SCALE      = 5            # px/mm → 1 module = 90px wide
MX         = MODULE_W * SCALE   # 90px per module
MY         = 160          # device image height px
RAIL_H     = 16
MODS_ROW_DEFAULT = 12
HDR_H      = 80
FTR_H      = 55
LEFT_W     = 210          # project info sidebar width
BAY_LBL_W  = 130          # bay name column
BAY_PAD_X  = 30
BAY_PAD_Y  = 40
BAY_GAP    = 18

# ── Colors ────────────────────────────────────────────────────────────────
BG             = (24, 26, 32)
CABINET_OUTER  = (55, 57, 60)
CABINET_INNER  = (62, 65, 68)
CABINET_SHELF  = (48, 50, 54)
CABINET_EDGE   = (38, 40, 44)
DIN_RAIL_C     = (185, 190, 196)
SLOT_EMPTY     = (70, 72, 78)
SLOT_NUM_C     = (120, 124, 135)
BAY_NAME_C     = (80, 210, 120)
BAY_LABEL_C    = (170, 178, 195)
BAY_BG         = (52, 55, 62)
ACCENT         = (0, 175, 255)
ACCENT2        = (0, 210, 100)
TEXT1          = (240, 242, 246)
TEXT2          = (180, 185, 196)
TEXT3          = (120, 126, 140)
C_GREEN        = (0, 195, 80)
C_YELLOW       = (240, 185, 20)
C_RED          = (215, 50, 50)
SIDEBAR_BG     = (20, 22, 28)
SIDEBAR_LN     = (35, 38, 46)


# ── Device PNG map ────────────────────────────────────────────────────────
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEVICE_IMG_DIRS = [
    os.path.join(SCRIPT_DIR, "devices"),
    os.path.join(SCRIPT_DIR, "..", "samples", "cabinet", "devices"),
    os.path.join(os.path.dirname(SCRIPT_DIR), "samples", "cabinet", "devices"),
]

DEVICE_PNG_MAP = {
    "MCCB":       ["device_mccb_schneider.png", "device_mccb.png"],
    "MCB 1P":     ["device_mcb1p_schneider.png", "device_mcb1p.png"],
    "MCB 2P":     ["device_mcb1p_schneider.png"],
    "MCB 3P":     ["device_mcb3p_schneider.png", "device_mcb3p.png"],
    "MCB 4P":     ["device_mcb3p_schneider.png"],
    "ELCB":       ["device_mcb3p_schneider.png"],
    "CONTACTOR":  ["device_contactor_ls.png", "device_contactor.png"],
    "RELAY":      ["device_relay_ls.png", "device_relay.png"],
    "TIMER":      ["device_timer_schneider.png", "device_timer.png"],
    "METER":      ["device_meter_pm5560.png", "device_meter.png"],
}

DEVICE_MODULES = {
    "MCB 1P": 1, "MCB 2P": 2, "MCB 3P": 3, "MCB 4P": 4,
    "MCCB": 4, "ELCB": 2,
    "CONTACTOR": 4, "RELAY": 3,
    "TIMER": 4, "METER": 6, "PILOT": 1,
    "SWITCH": 1, "SURGE": 2, "BUSBAR": 6,
}


def find_device_image(device_type: str) -> Optional[Image.Image]:
    """Tìm ảnh PNG cho loại thiết bị. Trả None nếu không có."""
    dtype = device_type.upper().strip()
    candidates = None
    for key, filenames in DEVICE_PNG_MAP.items():
        if dtype.startswith(key) or key in dtype:
            candidates = filenames
            break
    if not candidates:
        return None

    for img_dir in DEVICE_IMG_DIRS:
        for fname in candidates:
            path = os.path.join(img_dir, fname)
            if os.path.exists(path):
                try:
                    img = Image.open(path).convert("RGBA")
                    return img
                except Exception:
                    continue
    return None


def remove_white_background(img: Image.Image, threshold=240) -> Image.Image:
    """Loại bỏ nền trắng / sáng thành trong suốt (RGBA)."""
    img = img.convert("RGBA")
    data = img.getdata()
    new_data = []
    for r, g, b, a in data:
        if r > threshold and g > threshold and b > threshold:
            new_data.append((r, g, b, 0))
        else:
            new_data.append((r, g, b, a))
    img.putdata(new_data)
    return img


def paste_device_image(canvas: Image.Image, device_img: Image.Image,
                       x: int, y: int, w: int, h: int):
    """Scale thiết bị vào slot và paste lên canvas, giữ tỉ lệ."""
    pad = 4
    target_w = w - pad * 2
    target_h = h - pad * 2

    ratio = min(target_w / device_img.width, target_h / device_img.height)
    nw = int(device_img.width * ratio)
    nh = int(device_img.height * ratio)
    resized = device_img.resize((nw, nh), Image.LANCZOS)

    # Remove white bg
    resized = remove_white_background(resized, threshold=235)

    # Center in slot
    ox = x + pad + (target_w - nw) // 2
    oy = y + pad + (target_h - nh) // 2

    if canvas.mode == "RGB":
        bg = Image.new("RGB", canvas.size, CABINET_INNER)
        tmp = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        tmp.paste(resized, (ox, oy), resized)
        canvas.paste(tmp.convert("RGB"), (0, 0), tmp.split()[3])
    else:
        canvas.paste(resized, (ox, oy), resized)


# ── Data model ────────────────────────────────────────────────────────────
@dataclass
class Device:
    name: str
    device_type: str = "OTHER"
    model: str = ""
    in_a: float = 0
    poles: int = 1
    qty: int = 1
    manufacturer: str = ""
    code: str = ""
    modules: int = 1
    note: str = ""

    def __post_init__(self):
        if self.modules <= 0:
            self.modules = self._calc_modules()

    def _calc_modules(self) -> int:
        t = self.device_type.upper()
        for k, m in DEVICE_MODULES.items():
            if t.startswith(k.upper()): return m
        if "MCB" in t: return max(1, self.poles)
        if "MCCB" in t: return 4
        return 2


@dataclass
class Bay:
    name: str = "NGĂN"
    label: str = ""
    devices: List[Device] = field(default_factory=list)
    row_modules: int = MODS_ROW_DEFAULT

    @property
    def rows_needed(self):
        return max(1, math.ceil(self.used_modules / self.row_modules))

    @property
    def used_modules(self):
        return sum(d.modules * d.qty for d in self.devices)


@dataclass
class CabinetSpec:
    name: str = "TD-01"
    project: str = ""
    cabinet_type: str = "Tủ phân phối"
    voltage: str = "400 VAC"
    in_rated: float = 100
    size: str = "600x800x250"
    floor: str = ""
    created: str = ""
    bays: List[Bay] = field(default_factory=list)


# ── Parsers ───────────────────────────────────────────────────────────────
def parse_json(path: str) -> CabinetSpec:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    if isinstance(data, list):
        data = data[0] if data else {}

    spec = CabinetSpec(
        name=data.get("name", "TD"),
        project=data.get("project", ""),
        cabinet_type=data.get("type", "Tủ phân phối"),
        voltage=data.get("voltage", "400 VAC"),
        in_rated=float(data.get("in_rated", 100)),
        size=data.get("size", "600x800x250"),
        floor=data.get("floor", ""),
        created=data.get("created", ""),
    )

    for bd in data.get("bays", []):
        bay = Bay(
            name=bd.get("name", "NGĂN"),
            label=bd.get("label", ""),
            row_modules=int(bd.get("row_modules", bd.get("modules_per_row", MODS_ROW_DEFAULT))),
        )
        for dd in bd.get("devices", []):
            qty = int(dd.get("qty", 1))
            for _ in range(qty):
                dev = Device(
                    name=dd.get("name", ""),
                    device_type=dd.get("type", "OTHER"),
                    model=dd.get("model", ""),
                    in_a=float(dd.get("in_a", 0)),
                    poles=int(dd.get("poles", 1)),
                    qty=1,
                    manufacturer=dd.get("manufacturer", ""),
                    code=dd.get("code", ""),
                    modules=int(dd.get("modules", 0)),
                    note=dd.get("note", ""),
                )
                bay.devices.append(dev)
        spec.bays.append(bay)
    return spec


def parse_csv(path: str) -> CabinetSpec:
    spec = CabinetSpec()
    current_bay = None

    with open(path, encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        for row in reader:
            row = {k.strip(): v.strip() for k, v in row.items() if k}
            bay_name = row.get("bay", row.get("ngan", ""))
            if current_bay is None or (bay_name and bay_name != current_bay.name):
                current_bay = Bay(
                    name=bay_name or "NGĂN 1",
                    label=row.get("bay_label", ""),
                    row_modules=int(row.get("row_modules", MODS_ROW_DEFAULT)),
                )
                spec.bays.append(current_bay)

            try: qty = int(row.get("qty", row.get("sl", 1)) or 1)
            except: qty = 1
            try: ia = float(row.get("in_a", row.get("in", 0)) or 0)
            except: ia = 0
            try: poles = int(row.get("poles", row.get("pha", 1)) or 1)
            except: poles = 1
            try: mods = int(row.get("modules", 0) or 0)
            except: mods = 0

            name = row.get("ten_thiet_bi", row.get("name", ""))
            dtype = row.get("device_type", row.get("loai", row.get("type", name)))

            for _ in range(qty):
                dev = Device(
                    name=name or dtype,
                    device_type=dtype,
                    model=row.get("model", ""),
                    in_a=ia,
                    poles=poles,
                    qty=1,
                    manufacturer=row.get("manufacturer", row.get("nha_san_xuat", "")),
                    code=row.get("code", row.get("ma", "")),
                    modules=mods,
                    note=row.get("ghi_chu", row.get("note", "")),
                )
                if dev.name:
                    current_bay.devices.append(dev)

    return spec


def make_demo() -> CabinetSpec:
    spec = CabinetSpec("TD-01","TD-3P-100A","Tủ điện phân phối và điều khiển",
                       "400 VAC",100,"600x800x250","FL+1.400","24/05/2025")
    b1 = Bay("NGĂN 1","Nguồn vào",row_modules=12)
    b1.devices=[
        Device("MCCB tổng","MCCB","EasyPact CVS100F",100,3,1,"Schneider","LV510347",4),
        Device("MCB dự phòng","MCB 1P","Easy9",32,1,1,"Schneider","EZ9F34132",1),
        Device("MCB dự phòng","MCB 1P","Easy9",32,1,1,"Schneider","EZ9F34132",1),
    ]
    b2 = Bay("NGĂN 2","Phân phối",row_modules=24)
    b2.devices=[
        *[Device("MCB đèn","MCB 1P","Easy9",10,1,1,"Schneider","EZ9F34110",1) for _ in range(4)],
        *[Device("MCB ổ cắm","MCB 1P","Easy9",16,1,1,"Schneider","EZ9F34116",1) for _ in range(4)],
        *[Device("MCB AC","MCB 1P","Easy9",20,1,1,"Schneider","EZ9F34120",1) for _ in range(4)],
        *[Device("MCB 3P","MCB 3P","Easy9",16,3,1,"Schneider","EZ9F34316",3) for _ in range(2)],
        Device("MCB 3P 32A","MCB 3P","iC60N",32,3,1,"Schneider","A9F74332",3),
    ]
    b3 = Bay("NGĂN 3","Điều khiển",row_modules=24)
    b3.devices=[
        *[Device("Contactor","CONTACTOR","GMC-25",25,3,1,"LS","MC-025a",4) for _ in range(2)],
        *[Device("Relay nhiệt","RELAY","MT-32",25,3,1,"LS","MT-32/3H",3) for _ in range(2)],
        Device("Timer","TIMER","RE17RAMU",0,0,1,"Schneider","RE17RAMU",4),
        Device("Đồng hồ A","METER","PM5560",100,3,1,"Schneider","METSEPM5560",6),
    ]
    spec.bays=[b1,b2,b3]
    return spec


# ── Renderer ──────────────────────────────────────────────────────────────
def lerp(a, b, t): return tuple(int(a[i]*(1-t)+b[i]*t) for i in range(len(a)))

class CabinetRenderer:
    def __init__(self, spec: CabinetSpec):
        self.spec = spec
        self._load_fonts()
        self._preload_device_images()

    def _load_fonts(self):
        paths_bold   = ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]
        paths_normal = ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]
        paths_mono   = ["/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf"]
        def try_font(paths, size):
            for p in paths:
                if os.path.exists(p):
                    return ImageFont.truetype(p, size)
            return ImageFont.load_default()

        self.f_title = try_font(paths_bold, 22)
        self.f_sub   = try_font(paths_normal, 16)
        self.f_bay   = try_font(paths_bold, 14)
        self.f_dev   = try_font(paths_normal, 11)
        self.f_sm    = try_font(paths_normal, 9)
        self.f_mono  = try_font(paths_mono, 10)

    def _preload_device_images(self):
        self.dev_img_cache = {}
        types_needed = set(d.device_type for b in self.spec.bays for d in b.devices)
        for dt in types_needed:
            img = find_device_image(dt)
            if img:
                self.dev_img_cache[dt] = img

    def _calc_sizes(self):
        # Compute canvas size from bays
        max_mods = max((b.row_modules for b in self.spec.bays), default=24)
        content_w = LEFT_W + BAY_PAD_X + BAY_LBL_W + max_mods * MX + BAY_PAD_X
        bay_heights = []
        for bay in self.spec.bays:
            bh = bay.rows_needed * (MY + RAIL_H + 4) + 48
            bay_heights.append(bh)

        total_bay_h = sum(bay_heights) + BAY_GAP * len(self.spec.bays) + BAY_PAD_Y * 2
        self.img_w = max(1400, content_w + 40)
        self.img_h = HDR_H + total_bay_h + FTR_H + 10
        self.bay_heights = bay_heights
        self.cab_x = LEFT_W + 16
        self.cab_y = HDR_H + 6
        self.cab_w = self.img_w - self.cab_x - 16
        self.cab_h = total_bay_h + 10

    def render(self) -> Image.Image:
        self._calc_sizes()
        img = Image.new("RGB", (self.img_w, self.img_h), BG)
        draw = ImageDraw.Draw(img)

        self._draw_bg(draw)
        self._draw_sidebar(draw)
        self._draw_header(draw)
        self._draw_cabinet(draw)
        self._draw_bays(img, draw)
        self._draw_footer(draw)

        # Subtle final enhancement
        img = ImageEnhance.Contrast(img).enhance(1.05)
        return img

    def _draw_bg(self, draw: ImageDraw.ImageDraw):
        for y in range(self.img_h):
            t = y / self.img_h
            c = lerp(BG, (30, 32, 40), t)
            draw.line([0, y, self.img_w, y], fill=c)

    def _draw_header(self, draw: ImageDraw.ImageDraw):
        for y in range(HDR_H):
            t = y / HDR_H
            c = lerp((15, 17, 24), (22, 25, 34), t)
            draw.line([LEFT_W, y, self.img_w, y], fill=c)
        draw.line([LEFT_W, HDR_H - 1, self.img_w, HDR_H - 1], fill=ACCENT, width=2)

        # App icon
        x0 = LEFT_W + 12
        draw.rectangle([x0, 12, x0 + 38, 50], fill=(0, 120, 190), outline=(0, 160, 230), width=2)
        draw.text((x0 + 5, 20), "DB", fill=TEXT1, font=self.f_bay)

        # Title
        draw.text((LEFT_W + 58, 10), "CẤU HÌNH TỦ ĐIỆN", fill=TEXT1, font=self.f_title)
        draw.text((LEFT_W + 58, 40), f"{self.spec.cabinet_type}  |  {self.spec.name}  |  {self.spec.size} mm",
                  fill=TEXT2, font=self.f_sub)

        # Toolbar simulation
        tools = ["Mới", "Mở", "Lưu", "Xuất PDF", "Xuất Excel", "Kiểm tra", "Báo cáo"]
        tx = LEFT_W + 12
        ty = HDR_H + 3
        for tool in tools:
            tw = len(tool) * 8 + 12
            draw.rectangle([tx, ty, tx + tw, ty + 26], fill=(35, 38, 48), outline=(55, 58, 70))
            draw.text((tx + 6, ty + 6), tool, fill=TEXT2, font=self.f_sm)
            tx += tw + 4

    def _draw_sidebar(self, draw: ImageDraw.ImageDraw):
        for y in range(self.img_h):
            t = y / self.img_h
            c = lerp(SIDEBAR_BG, (24, 26, 34), t)
            draw.line([0, y, LEFT_W, y], fill=c)
        draw.line([LEFT_W - 1, 0, LEFT_W - 1, self.img_h], fill=(40, 43, 54), width=2)

        # Header
        draw.rectangle([0, 0, LEFT_W, HDR_H], fill=(15, 17, 24))
        draw.line([0, HDR_H - 1, LEFT_W, HDR_H - 1], fill=ACCENT, width=2)
        draw.text((12, 20), "DỰ ÁN", fill=ACCENT, font=self.f_bay)

        # Project info
        fields = [
            ("Tên dự án:", self.spec.project or "TD-3P-100A"),
            ("Mã tủ điện:", self.spec.name),
            ("Điện áp:", self.spec.voltage),
            ("Tần số:", "50 Hz"),
            ("Dòng định mức:", f"{int(self.spec.in_rated)} A"),
            ("Ngày tạo:", self.spec.created or "2025"),
        ]
        sy = HDR_H + 12
        for label, value in fields:
            draw.text((10, sy), label, fill=TEXT3, font=self.f_sm)
            draw.text((10, sy + 13), value, fill=TEXT1, font=self.f_dev)
            draw.line([8, sy + 28, LEFT_W - 8, sy + 28], fill=SIDEBAR_LN)
            sy += 34

        # Navigation menu
        menus = [
            ("Tổng quan", False),
            ("Cấu hình tủ điện", True),
            ("Sơ đồ một sợi", False),
            ("Báo cáo vật tư", False),
            ("Danh mục thiết bị", False),
        ]
        my = sy + 12
        for label, active in menus:
            bg_c = (30, 35, 50) if active else SIDEBAR_BG
            accent_c = ACCENT if active else SIDEBAR_BG
            draw.rectangle([4, my, LEFT_W - 4, my + 32], fill=bg_c)
            draw.line([4, my, 4, my + 32], fill=accent_c, width=3)
            draw.text((16, my + 9), label, fill=TEXT1 if active else TEXT2, font=self.f_dev)
            my += 38

        # Schneider logo at bottom
        logo_y = self.img_h - 80
        draw.rectangle([20, logo_y, LEFT_W - 20, logo_y + 40],
                       fill=(0, 108, 48), outline=(0, 140, 60))
        draw.text((28, logo_y + 8), "Schneider", fill=(255, 255, 255), font=self.f_bay)
        draw.text((28, logo_y + 26), "Electric", fill=(180, 220, 180), font=self.f_sm)

    def _draw_cabinet(self, draw: ImageDraw.ImageDraw):
        cx, cy = self.cab_x, self.cab_y
        cw, ch = self.cab_w, self.cab_h

        # Cabinet outer body (shadow)
        for d in range(8, 0, -1):
            shade = tuple(min(255, BG[i] + d * 3) for i in range(3))
            draw.rectangle([cx + d, cy + d, cx + cw + d, cy + ch + d], outline=shade)

        # Cabinet body
        for y in range(ch):
            t = y / ch
            c = lerp(CABINET_OUTER, CABINET_EDGE, t * 0.3)
            draw.line([cx, cy + y, cx + cw, cy + y], fill=c)

        draw.rectangle([cx, cy, cx + cw, cy + ch], outline=CABINET_EDGE, width=4)

        # Inner door frame
        draw.rectangle([cx + 8, cy + 8, cx + cw - 8, cy + ch - 8],
                       outline=(45, 47, 52), width=3)
        draw.rectangle([cx + 12, cy + 12, cx + cw - 12, cy + ch - 12],
                       fill=CABINET_INNER, outline=(40, 42, 48), width=1)

        # Hinges
        for hy in [cy + 40, cy + ch - 40]:
            draw.rectangle([cx - 6, hy - 12, cx + 6, hy + 12], fill=(130, 133, 138), outline=(100, 103, 108), width=1)
            draw.ellipse([cx - 3, hy - 3, cx + 3, hy + 3], fill=(80, 83, 88))

        # Lock
        lx = cx + cw - 14
        ly = cy + ch // 2
        draw.ellipse([lx - 8, ly - 10, lx + 8, ly + 10], fill=(150, 153, 158), outline=(100, 103, 108))
        draw.ellipse([lx - 4, ly - 4, lx + 4, ly + 4], fill=(80, 83, 88))

    def _draw_bays(self, img: Image.Image, draw: ImageDraw.ImageDraw):
        content_x = self.cab_x + 16 + BAY_PAD_X
        cy = self.cab_y + 16 + BAY_PAD_Y

        for bay_idx, bay in enumerate(self.spec.bays):
            rows = bay.rows_needed
            bay_h = self.bay_heights[bay_idx]
            bay_mods = bay.row_modules
            bay_w = BAY_LBL_W + bay_mods * MX

            # Bay shelf background
            draw.rectangle([content_x, cy, content_x + bay_w, cy + bay_h],
                           fill=CABINET_SHELF, outline=(38, 40, 46), width=2)

            # Bay label column
            draw.rectangle([content_x, cy, content_x + BAY_LBL_W - 6, cy + bay_h],
                           fill=(30, 33, 42), outline=(45, 48, 58))
            draw.text((content_x + 8, cy + 12), bay.name, fill=BAY_NAME_C, font=self.f_bay)
            draw.text((content_x + 8, cy + 32), f"({bay.label})", fill=BAY_LABEL_C, font=self.f_sm)

            # Usage bar
            used = bay.used_modules
            total = rows * bay_mods
            pct = min(1.0, used / total) if total > 0 else 0
            bar_x = content_x + 6
            bar_y = cy + bay_h - 16
            bar_w = BAY_LBL_W - 16
            draw.rectangle([bar_x, bar_y, bar_x + bar_w, bar_y + 8],
                           fill=(40, 43, 52), outline=(60, 63, 72))
            bar_color = C_GREEN if pct < 0.7 else (C_YELLOW if pct < 0.9 else C_RED)
            if int(bar_w * pct) > 0:
                draw.rectangle([bar_x, bar_y, bar_x + int(bar_w * pct), bar_y + 8], fill=bar_color)
            draw.text((bar_x, bar_y - 13), f"{used}/{total}", fill=TEXT3, font=self.f_sm)

            # Device rows
            dev_x = content_x + BAY_LBL_W
            row_y = cy + 8

            for row in range(rows):
                ry = row_y + row * (MY + RAIL_H + 4)
                # Shelf plate
                for y in range(MY):
                    t = y / MY
                    shade = lerp((68, 70, 75), (60, 62, 67), t)
                    draw.line([dev_x, ry + y, dev_x + bay_mods * MX, ry + y], fill=shade)

                # Empty module slots
                for sl in range(bay_mods):
                    sx = dev_x + sl * MX
                    draw.rectangle([sx + 1, ry + 1, sx + MX - 2, ry + MY - 2],
                                   fill=SLOT_EMPTY, outline=(55, 57, 64), width=1)
                    num = row * bay_mods + sl + 1
                    draw.text((sx + MX // 2 - 8, ry + MY // 2 - 5), f"{num:02d}",
                              fill=SLOT_NUM_C, font=self.f_sm)

                # DIN rail
                rail_y = ry + MY
                for y in range(RAIL_H):
                    t = y / RAIL_H
                    c = lerp((195, 200, 208), (165, 170, 178), t)
                    draw.line([dev_x, rail_y + y, dev_x + bay_mods * MX, rail_y + y], fill=c)
                draw.line([dev_x, rail_y, dev_x + bay_mods * MX, rail_y], fill=(140, 145, 152))
                draw.line([dev_x, rail_y + RAIL_H - 1, dev_x + bay_mods * MX, rail_y + RAIL_H - 1],
                          fill=(140, 145, 152))
                # DIN holes
                for s in range(0, bay_mods * MX, MX):
                    hx = dev_x + s + MX // 2
                    hy = rail_y + RAIL_H // 2
                    draw.ellipse([hx - 3, hy - 2, hx + 3, hy + 2], fill=DIN_RAIL_C, outline=(140, 145, 152))

            # Place device images
            cur_row = 0
            cur_slot = 0
            for dev in bay.devices:
                m = dev.modules
                if cur_slot + m > bay_mods:
                    cur_row += 1
                    cur_slot = 0
                if cur_row >= rows:
                    break

                dx = dev_x + cur_slot * MX
                dy = row_y + cur_row * (MY + RAIL_H + 4)
                dw = m * MX
                dh = MY

                dev_img = self.dev_img_cache.get(dev.device_type)
                if dev_img:
                    paste_device_image(img, dev_img, dx, dy, dw, dh)
                    # Label dưới thiết bị
                    lbl = f"{int(dev.in_a)}A" if dev.in_a else dev.device_type[:6]
                    lx_lbl = dx + dw // 2 - len(lbl) * 4
                    draw.text((lx_lbl, dy + dh - 18), lbl,
                              fill=(220, 225, 235), font=self.f_sm)
                else:
                    # Fallback: colored block
                    draw.rectangle([dx + 2, dy + 2, dx + dw - 2, dy + dh - 2],
                                   fill=(80, 83, 92), outline=(100, 103, 112))
                    draw.text((dx + 4, dy + dh // 2 - 6), dev.device_type[:8],
                              fill=TEXT1, font=self.f_sm)

                # Frame around placed device (slot separator lines)
                draw.line([dx + dw, dy, dx + dw, dy + dh], fill=(45, 47, 54), width=1)
                cur_slot += m

            cy += bay_h + BAY_GAP

    def _draw_footer(self, draw: ImageDraw.ImageDraw):
        fy = self.img_h - FTR_H
        for y in range(FTR_H):
            c = lerp((15, 17, 24), (20, 22, 30), y / FTR_H)
            draw.line([0, fy + y, self.img_w, fy + y], fill=c)
        draw.line([0, fy, self.img_w, fy], fill=(40, 43, 54))

        # DIN usage bar
        total_used = sum(b.used_modules for b in self.spec.bays)
        total_slots = sum(b.rows_needed * b.row_modules for b in self.spec.bays)
        pct = total_used / total_slots if total_slots > 0 else 0

        bar_x = LEFT_W + 16
        bar_y = fy + 14
        bar_w = self.img_w - LEFT_W - 32
        bar_h = 14

        draw.text((bar_x, bar_y - 14), "Mức độ sử dụng thanh DIN:", fill=TEXT2, font=self.f_dev)
        draw.rectangle([bar_x, bar_y, bar_x + bar_w, bar_y + bar_h],
                       fill=(35, 38, 46), outline=(55, 58, 68))

        filled_w = int(bar_w * pct)
        seg = 8
        for i in range(0, filled_w, seg):
            seg_w = min(seg - 1, filled_w - i)
            c = C_GREEN if pct < 0.7 else (C_YELLOW if pct < 0.9 else C_RED)
            draw.rectangle([bar_x + i, bar_y + 2, bar_x + i + seg_w, bar_y + bar_h - 2],
                           fill=c)

        usage_txt = f"{total_used} / {total_slots} module ({int(pct*100)}%)"
        draw.text((bar_x + bar_w // 2 - len(usage_txt) * 4, bar_y + 1),
                  usage_txt, fill=TEXT1, font=self.f_dev)

        # Status
        draw.text((LEFT_W + 16, fy + FTR_H - 22), "Trạng thái:", fill=TEXT3, font=self.f_sm)
        draw.ellipse([LEFT_W + 76, fy + FTR_H - 20, LEFT_W + 86, fy + FTR_H - 10], fill=C_GREEN)
        draw.text((LEFT_W + 90, fy + FTR_H - 22), "Đã lưu", fill=C_GREEN, font=self.f_sm)

        # Version right
        ver_txt = "Phiên bản: 1.0.0.0"
        draw.text((self.img_w - len(ver_txt) * 7 - 10, fy + FTR_H - 22),
                  ver_txt, fill=TEXT3, font=self.f_sm)


# ── CLI ───────────────────────────────────────────────────────────────────
def main():
    parser = argparse.ArgumentParser(description="MEP Cabinet Renderer v3 — Composite device images")
    parser.add_argument("--input", "-i")
    parser.add_argument("--output", "-o", default="cabinet.png")
    parser.add_argument("--demo", action="store_true")
    args = parser.parse_args()

    if args.demo or not args.input:
        spec = make_demo()
    elif args.input.lower().endswith(".json"):
        spec = parse_json(args.input)
    else:
        spec = parse_csv(args.input)

    renderer = CabinetRenderer(spec)
    img = renderer.render()
    img.save(args.output, "PNG", dpi=(150, 150))
    print(f"OK {args.output}  ({img.width}x{img.height}px)")


if __name__ == "__main__":
    main()
