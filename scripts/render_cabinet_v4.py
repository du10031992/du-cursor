#!/usr/bin/env python3
"""
MEP Cabinet Renderer v4 — Pixel-perfect clone của CẤU HÌNH TỦ ĐIỆN WPF app.
Phân tích chính xác màu sắc, tỷ lệ, layout từ reference screenshot.

Usage:
    python3 render_cabinet_v4.py --input TD-01_full.json --output cabinet.png
    python3 render_cabinet_v4.py --demo --output cabinet.png
"""

import argparse, csv, json, math, os, sys
from dataclasses import dataclass, field
from typing import List, Optional, Tuple

try:
    from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
except ImportError:
    print("pip install pillow"); sys.exit(1)

# ═══════════════════════════════════════════════════════════════════════
#  EXACT COLORS from reference screenshot (eye-dropper analysis)
# ═══════════════════════════════════════════════════════════════════════
# Application window
APP_BG          = (30, 31, 37)
APP_TITLE_BG    = (24, 25, 30)
APP_TITLE_LINE  = (45, 47, 54)
TOOLBAR_BG      = (32, 34, 40)
TOOLBAR_BTN     = (42, 44, 52)
TOOLBAR_BDR     = (52, 55, 65)

# Sidebar
SIDEBAR_BG      = (22, 24, 30)
SIDEBAR_HDR     = (18, 20, 26)
SIDEBAR_LINE    = (35, 37, 46)
SIDEBAR_NAV_ACT = (30, 36, 52)
SIDEBAR_NAV_IND = (0, 170, 240)

# Cabinet body — very dark charcoal
CAB_OUTER       = (42, 44, 48)
CAB_FACE        = (48, 51, 56)
CAB_INNER_SHELF = (52, 55, 62)
CAB_SHELF_BG    = (58, 61, 68)
CAB_FRAME       = (36, 38, 42)
CAB_HINGE       = (110, 115, 122)
CAB_SCREW       = (90, 93, 100)

# Bay
BAY_LABEL_BG    = (38, 42, 52)
BAY_NAME_C      = (80, 210, 120)   # Green teal
BAY_LABEL_TXT   = (80, 210, 120)   # Green teal (alias)
BAY_SUB_TXT     = (140, 150, 170)
BAY_SHELF_LINE  = (44, 47, 54)

# DIN rail
DIN_RAIL_TOP    = (195, 200, 208)
DIN_RAIL_MID    = (175, 180, 188)
DIN_RAIL_BOT    = (145, 150, 158)
DIN_HOLE        = (210, 214, 222)

# Slot numbers
SLOT_NUM_C      = (100, 106, 122)
SLOT_BG         = (62, 65, 73)

# Footer / status
FOOTER_BG       = (22, 24, 30)
FOOTER_LINE     = (35, 37, 46)
STATUS_GREEN    = (0, 200, 90)
PROGRESS_BG     = (40, 43, 52)
PROGRESS_GREEN  = (0, 195, 80)

# Text
TXT_WHITE       = (240, 243, 248)
TXT_GREY        = (170, 176, 192)
TXT_DIM         = (110, 116, 132)
TXT_ACCENT      = (0, 175, 255)
TXT_GREEN       = (80, 210, 120)
SCHNEIDER_GREEN = (0, 144, 73)

# ═══════════════════════════════════════════════════════════════════════
#  EXACT LAYOUT DIMENSIONS (matched to reference)
# ═══════════════════════════════════════════════════════════════════════
WIN_W           = 1080    # Reference screenshot width
WIN_H           = 680     # Reference screenshot height

TITLE_H         = 32
MENU_H          = 24
TOOLBAR_H       = 38
LEFT_W          = 160     # Sidebar width

# Cabinet area
CAB_MARGIN_L    = 10
CAB_MARGIN_T    = 6
CAB_MARGIN_R    = 10
CAB_MARGIN_B    = 8

# Per-bay (inside cabinet)
BAY_LBL_W       = 115     # "NGĂN 1 / (Nguồn vào)" label width
BAY_PAD_L       = 8
BAY_PAD_T       = 6
BAY_GAP         = 4       # Between bays

# Module / device slot
MODULE_W_MM     = 18
SCALE_PX_PER_MM = 3.8
MX              = int(MODULE_W_MM * SCALE_PX_PER_MM)  # ~68px per module
MY              = 120     # Device image height
RAIL_H          = 12
SLOT_NUM_H      = 14      # Height of slot number text area

# Footer
FOOTER_H        = 32

# ═══════════════════════════════════════════════════════════════════════
#  DEVICE IMAGE MAP
# ═══════════════════════════════════════════════════════════════════════
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
IMG_DIRS = [
    os.path.join(SCRIPT_DIR, "devices"),
    os.path.join(SCRIPT_DIR, "..", "samples", "cabinet", "devices"),
]

# Priority: v2 images first, then v1
DEV_PNG_MAP = {
    "MCCB":        ["dev_mccb_v2.png", "device_mccb_schneider.png"],
    "MCB 1P":      ["dev_mcb1p_v2.png", "device_mcb1p_schneider.png"],
    "MCB 2P":      ["dev_mcb1p_v2.png", "device_mcb1p_schneider.png"],
    "MCB 3P":      ["dev_mcb3p_v2.png", "device_mcb3p_schneider.png"],
    "MCB 4P":      ["dev_mcb3p_v2.png"],
    "ELCB":        ["dev_mcb3p_v2.png"],
    "CONTACTOR":   ["dev_contactor_v2.png", "device_contactor_ls.png"],
    "RELAY":       ["dev_relay_v2.png", "device_relay_ls.png"],
    "TIMER":       ["dev_timer_v2.png", "device_timer_schneider.png"],
    "METER":       ["dev_meter_v2.png", "device_meter_pm5560.png"],
    "BUSBAR":      ["device_busbar_3phase.png"],
    "BUSBAR_COMB": ["device_busbar_comb.png"],
}

DEV_MODULES = {
    "MCCB": 4, "MCB 1P": 1, "MCB 2P": 2, "MCB 3P": 3, "MCB 4P": 4,
    "ELCB": 2, "CONTACTOR": 4, "RELAY": 3, "TIMER": 4, "METER": 6,
    "BUSBAR": 12, "BUSBAR_COMB": 12, "PILOT": 1, "SURGE": 2,
}


def find_image(dtype: str) -> Optional[Image.Image]:
    dtype_u = dtype.upper().strip()
    fnames = None
    for key, files in DEV_PNG_MAP.items():
        if dtype_u.startswith(key) or key in dtype_u:
            fnames = files
            break
    if not fnames:
        return None
    for d in IMG_DIRS:
        for fn in fnames:
            p = os.path.join(d, fn)
            if os.path.exists(p):
                try:
                    return Image.open(p).convert("RGBA")
                except Exception:
                    pass
    return None


def remove_white_bg(img: Image.Image, threshold=230) -> Image.Image:
    img = img.convert("RGBA")
    pix = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = pix[x, y]
            if r > threshold and g > threshold and b > threshold:
                pix[x, y] = (r, g, b, 0)
    return img


def paste_device(canvas: Image.Image, dev_img: Image.Image,
                 x: int, y: int, w: int, h: int, pad: int = 3):
    tw, th = w - pad * 2, h - pad * 2
    ratio = min(tw / dev_img.width, th / dev_img.height)
    nw, nh = int(dev_img.width * ratio), int(dev_img.height * ratio)
    resized = dev_img.resize((nw, nh), Image.LANCZOS)
    resized = remove_white_bg(resized, 230)
    ox = x + pad + (tw - nw) // 2
    oy = y + pad + (th - nh) // 2
    tmp = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    tmp.paste(resized, (ox, oy), resized)
    alpha = tmp.split()[3]
    canvas.paste(tmp.convert("RGB"), (0, 0), alpha)


def lerp(a: tuple, b: tuple, t: float) -> tuple:
    return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(len(a)))


# ═══════════════════════════════════════════════════════════════════════
#  DATA MODEL
# ═══════════════════════════════════════════════════════════════════════
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
    modules: int = 0
    note: str = ""

    def __post_init__(self):
        if self.modules <= 0:
            self.modules = self._calc()

    def _calc(self) -> int:
        t = self.device_type.upper()
        for k, m in DEV_MODULES.items():
            if t.startswith(k): return m
        if "MCB" in t: return max(1, self.poles)
        return 2


@dataclass
class Bay:
    name: str = "NGĂN"
    label: str = ""
    devices: List[Device] = field(default_factory=list)
    row_modules: int = 12

    @property
    def used_modules(self):
        return sum(d.modules for d in self.devices)

    @property
    def rows_needed(self):
        return max(1, math.ceil(self.used_modules / self.row_modules))


@dataclass
class CabinetSpec:
    name: str = "TD-01"
    project: str = "TD-3P-100A"
    cabinet_type: str = "Tủ điện phân phối và điều khiển"
    voltage: str = "400 VAC"
    frequency: str = "50 Hz"
    in_rated: float = 100
    size: str = "600x800x250"
    floor: str = "FL+1.400"
    created: str = "24/05/2025"
    note: str = "Tủ điện phân phối và điều khiển"
    bays: List[Bay] = field(default_factory=list)


# ═══════════════════════════════════════════════════════════════════════
#  PARSERS
# ═══════════════════════════════════════════════════════════════════════
def parse_json(path: str) -> CabinetSpec:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    if isinstance(data, list):
        data = data[0] if data else {}
    spec = CabinetSpec(
        name=data.get("name", "TD-01"),
        project=data.get("project", ""),
        cabinet_type=data.get("type", "Tủ phân phối"),
        voltage=data.get("voltage", "400 VAC"),
        in_rated=float(data.get("in_rated", 100)),
        size=data.get("size", "600x800x250"),
        floor=data.get("floor", ""),
        created=data.get("created", ""),
        note=data.get("note", ""),
    )
    for bd in data.get("bays", []):
        bay = Bay(name=bd.get("name", "NGĂN"), label=bd.get("label", ""),
                  row_modules=int(bd.get("row_modules", 12)))
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
    cur = None
    with open(path, encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            row = {k.strip(): v.strip() for k, v in row.items() if k}
            bn = row.get("bay", row.get("ngan", ""))
            if cur is None or (bn and bn != cur.name):
                cur = Bay(name=bn or "NGĂN 1",
                          label=row.get("bay_label", ""),
                          row_modules=int(row.get("row_modules", 12)))
                spec.bays.append(cur)
            try: qty = int(row.get("qty", row.get("sl", 1)) or 1)
            except: qty = 1
            try: ia = float(row.get("in_a", row.get("in", 0)) or 0)
            except: ia = 0
            try: poles = int(row.get("poles", 1) or 1)
            except: poles = 1
            try: mods = int(row.get("modules", 0) or 0)
            except: mods = 0
            name = row.get("ten_thiet_bi", row.get("name", ""))
            dtype = row.get("device_type", row.get("loai", name))
            for _ in range(qty):
                dev = Device(name=name or dtype, device_type=dtype,
                             model=row.get("model", ""), in_a=ia, poles=poles,
                             qty=1, manufacturer=row.get("manufacturer", ""),
                             code=row.get("code", ""), modules=mods,
                             note=row.get("ghi_chu", ""))
                if dev.name:
                    cur.devices.append(dev)
    return spec


def make_demo() -> CabinetSpec:
    spec = CabinetSpec("TD-01", "TD-3P-100A", "Tủ điện phân phối và điều khiển",
                       "400 VAC", "50 Hz", 100, "600x800x250", "FL+1.400",
                       "24/05/2025", "Tủ điện phân phối và điều khiển")
    b1 = Bay("NGĂN 1", "Nguồn vào", row_modules=12)
    b1.devices = [
        Device("MCCB tổng", "MCCB", "EasyPact CVS100F", 100, 3, 1, "Schneider", "LV510347", 4),
        Device("MCB dự phòng", "MCB 1P", "Easy9", 32, 1, 1, "Schneider", "EZ9F34132", 1),
        Device("MCB dự phòng", "MCB 1P", "Easy9", 32, 1, 1, "Schneider", "EZ9F34132", 1),
    ]
    b2 = Bay("NGĂN 2", "Phân phối", row_modules=24)
    b2.devices = (
        [Device("MCB đèn", "MCB 1P", "Easy9", 10, 1, 1, "Schneider", "EZ9F34110", 1) for _ in range(5)] +
        [Device("MCB ổ cắm", "MCB 1P", "Easy9", 16, 1, 1, "Schneider", "EZ9F34116", 1) for _ in range(4)] +
        [Device("MCB AC", "MCB 1P", "Easy9", 20, 1, 1, "Schneider", "EZ9F34120", 1) for _ in range(4)] +
        [Device("MCB 3P 16A", "MCB 3P", "iC60N", 16, 3, 1, "Schneider", "A9F74316", 3) for _ in range(2)] +
        [Device("MCB 3P 32A", "MCB 3P", "iC60N", 32, 3, 1, "Schneider", "A9F74332", 3)]
    )
    b3 = Bay("NGĂN 3", "Điều khiển", row_modules=24)
    b3.devices = [
        *[Device("Contactor", "CONTACTOR", "GMC-25", 25, 3, 1, "LS", "MC-025a", 4) for _ in range(2)],
        *[Device("Relay nhiệt", "RELAY", "MT-32", 25, 3, 1, "LS", "MT-32/3H", 3) for _ in range(2)],
        Device("Timer", "TIMER", "RE17RAMU", 0, 0, 1, "Schneider", "RE17RAMU", 4),
        Device("Đồng hồ A", "METER", "PM5560", 100, 3, 1, "Schneider", "METSEPM5560", 6),
    ]
    spec.bays = [b1, b2, b3]
    return spec


# ═══════════════════════════════════════════════════════════════════════
#  RENDERER — Pixel-perfect clone
# ═══════════════════════════════════════════════════════════════════════
class CabinetRenderer:
    def __init__(self, spec: CabinetSpec):
        self.spec = spec
        self._load_fonts()
        self._preload_images()

    def _load_fonts(self):
        paths = {
            "bold": ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                     "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"],
            "regular": ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"],
        }

        def tf(style, size):
            for p in paths[style]:
                if os.path.exists(p):
                    return ImageFont.truetype(p, size)
            return ImageFont.load_default()

        self.f_win_title = tf("bold", 13)
        self.f_toolbar   = tf("regular", 10)
        self.f_sidebar_h = tf("bold", 11)
        self.f_sidebar   = tf("regular", 10)
        self.f_bay_name  = tf("bold", 13)
        self.f_bay_lbl   = tf("regular", 10)
        self.f_dev_label = tf("regular", 9)
        self.f_slot_num  = tf("regular", 8)
        self.f_footer    = tf("regular", 10)
        self.f_status    = tf("regular", 9)

    def _preload_images(self):
        self.img_cache = {}
        types = set(d.device_type for b in self.spec.bays for d in b.devices)
        for dt in types:
            img = find_image(dt)
            if img:
                self.img_cache[dt] = img

    def _layout(self):
        # Calculate exact canvas size matching reference proportions
        max_mods = max((b.row_modules for b in self.spec.bays), default=24)
        bay_h_list = []
        for bay in self.spec.bays:
            rows = bay.rows_needed
            bh = BAY_PAD_T + rows * (MY + RAIL_H + SLOT_NUM_H) + BAY_PAD_T + 8
            bay_h_list.append(bh)

        total_bay_h = sum(bay_h_list) + BAY_GAP * (len(self.spec.bays) - 1)
        cab_content_w = BAY_LBL_W + max_mods * MX + BAY_PAD_L * 2
        cab_h = total_bay_h + CAB_MARGIN_T + CAB_MARGIN_B + 20

        self.img_w = LEFT_W + CAB_MARGIN_L + cab_content_w + CAB_MARGIN_R + 20
        self.img_h = TITLE_H + MENU_H + TOOLBAR_H + cab_h + FOOTER_H

        self.bay_h_list = bay_h_list
        self.cab_x = LEFT_W + CAB_MARGIN_L
        self.cab_y = TITLE_H + MENU_H + TOOLBAR_H + CAB_MARGIN_T
        self.cab_w = cab_content_w + 20
        self.cab_h = cab_h

    def render(self) -> Image.Image:
        self._layout()
        img = Image.new("RGB", (self.img_w, self.img_h), APP_BG)
        draw = ImageDraw.Draw(img)

        self._draw_title_bar(draw)
        self._draw_menu_bar(draw)
        self._draw_toolbar(draw)
        self._draw_sidebar(draw)
        self._draw_cabinet(draw)
        self._draw_bays(img, draw)
        self._draw_footer(draw)

        # Slight sharpness
        return img.filter(ImageFilter.UnsharpMask(radius=0.5, percent=120))

    # ── Title bar ──────────────────────────────────────────────────────
    def _draw_title_bar(self, draw: ImageDraw.ImageDraw):
        for y in range(TITLE_H):
            draw.line([0, y, self.img_w, y], fill=lerp(APP_TITLE_BG, (28, 29, 35), y / TITLE_H))

        # Icon
        draw.rectangle([8, 8, 24, 24], fill=(0, 130, 200), outline=(0, 160, 230))
        draw.text((10, 10), "DB", fill=(255, 255, 255), font=self.f_status)

        draw.text((30, 9), "CẤU HÌNH TỦ ĐIỆN", fill=TXT_WHITE, font=self.f_win_title)

        # Window controls
        for i, (cx, c) in enumerate([(self.img_w - 46, (200, 60, 60)),
                                      (self.img_w - 24, (100, 100, 110))]):
            draw.rectangle([cx - 8, TITLE_H // 2 - 6, cx + 8, TITLE_H // 2 + 6], fill=c)

        # Giao diện dropdown
        rx = self.img_w - 120
        draw.rectangle([rx, 6, rx + 110, TITLE_H - 6], fill=TOOLBAR_BTN, outline=TOOLBAR_BDR)
        draw.text((rx + 6, 11), "Giao diện: Tối", fill=TXT_GREY, font=self.f_toolbar)

    # ── Menu bar ───────────────────────────────────────────────────────
    def _draw_menu_bar(self, draw: ImageDraw.ImageDraw):
        y0 = TITLE_H
        draw.rectangle([0, y0, self.img_w, y0 + MENU_H], fill=(28, 30, 36))
        draw.line([0, y0 + MENU_H - 1, self.img_w, y0 + MENU_H - 1], fill=APP_TITLE_LINE)
        items = ["Tệp", "Dự án", "Cấu hình", "Công cụ", "Trợ giúp"]
        mx = 8
        for item in items:
            draw.text((mx, y0 + 6), item, fill=TXT_GREY, font=self.f_toolbar)
            mx += len(item) * 7 + 16

    # ── Toolbar ────────────────────────────────────────────────────────
    def _draw_toolbar(self, draw: ImageDraw.ImageDraw):
        y0 = TITLE_H + MENU_H
        for y in range(TOOLBAR_H):
            draw.line([0, y0 + y, self.img_w, y0 + y], fill=TOOLBAR_BG)
        draw.line([0, y0 + TOOLBAR_H - 1, self.img_w, y0 + TOOLBAR_H - 1], fill=APP_TITLE_LINE)

        tools = [
            ("Mới", None), ("Mở", None), ("Lưu", None),
            ("Lưu như", None), ("|", None),
            ("Xuất PDF", (200, 60, 60)), ("Xuất Excel", (60, 160, 60)),
            ("In", None), ("|", None),
            ("Hoàn tác", None), ("Làm lại", None), ("|", None),
            ("Kiểm tra", None), ("Báo cáo", None),
        ]
        tx = 8
        ty = y0 + 5
        for label, color in tools:
            if label == "|":
                draw.line([tx + 2, ty, tx + 2, ty + TOOLBAR_H - 12], fill=TOOLBAR_BDR)
                tx += 10
                continue
            tw = len(label) * 7 + 14
            bg = TOOLBAR_BTN
            draw.rectangle([tx, ty, tx + tw, ty + TOOLBAR_H - 12], fill=bg, outline=TOOLBAR_BDR)
            txt_c = color if color else TXT_GREY
            draw.text((tx + 6, ty + 4), label, fill=txt_c, font=self.f_toolbar)
            tx += tw + 4

    # ── Sidebar ────────────────────────────────────────────────────────
    def _draw_sidebar(self, draw: ImageDraw.ImageDraw):
        y0 = TITLE_H + MENU_H + TOOLBAR_H
        for y in range(self.img_h - y0):
            draw.line([0, y0 + y, LEFT_W, y0 + y], fill=SIDEBAR_BG)
        draw.line([LEFT_W - 1, y0, LEFT_W - 1, self.img_h], fill=(38, 40, 50))

        # "DỰ ÁN" header
        draw.rectangle([0, y0, LEFT_W, y0 + 22], fill=SIDEBAR_HDR)
        draw.text((10, y0 + 5), "DỰ ÁN", fill=TXT_ACCENT, font=self.f_sidebar_h)

        fields = [
            ("Tên dự án:", self.spec.project or "TD-3P-100A"),
            ("Mã tủ điện:", self.spec.name),
            ("Điện áp:", self.spec.voltage),
            ("Tần số:", self.spec.frequency if hasattr(self.spec, "frequency") else "50 Hz"),
            ("Dòng định mức:", f"{int(self.spec.in_rated)} A"),
            ("Ngày tạo:", self.spec.created),
            ("Ghi chú:", self.spec.note[:20] if self.spec.note else ""),
        ]
        sy = y0 + 26
        for label, value in fields:
            draw.text((8, sy), label, fill=TXT_DIM, font=self.f_status)
            draw.text((8, sy + 11), value, fill=TXT_WHITE, font=self.f_sidebar)
            sy += 26
            draw.line([6, sy, LEFT_W - 6, sy], fill=SIDEBAR_LINE)
            sy += 3

        # Navigation menu
        nav_items = [
            ("  Tổng quan", False),
            ("  Cấu hình tủ điện", True),
            ("  Sơ đồ một sợi", False),
            ("  Báo cáo vật tư", False),
            ("  Danh mục thiết bị", False),
        ]
        ny = sy + 8
        for label, active in nav_items:
            bg = SIDEBAR_NAV_ACT if active else SIDEBAR_BG
            draw.rectangle([4, ny, LEFT_W - 4, ny + 28], fill=bg)
            if active:
                draw.line([4, ny, 4, ny + 28], fill=SIDEBAR_NAV_IND, width=3)
            icon_c = TXT_ACCENT if active else TXT_DIM
            draw.rectangle([10, ny + 9, 18, ny + 19], fill=icon_c)
            draw.text((24, ny + 8), label.strip(), fill=TXT_WHITE if active else TXT_GREY,
                      font=self.f_sidebar)
            ny += 32

        # Schneider logo at bottom
        logo_y = self.img_h - FOOTER_H - 52
        draw.rectangle([12, logo_y, LEFT_W - 12, logo_y + 40],
                       fill=SCHNEIDER_GREEN, outline=(0, 170, 90))
        draw.text((18, logo_y + 6), "Schneider", fill=(255, 255, 255), font=self.f_sidebar_h)
        draw.text((18, logo_y + 24), "Electric", fill=(180, 230, 200), font=self.f_status)

    # ── Cabinet outer shell ────────────────────────────────────────────
    def _draw_cabinet(self, draw: ImageDraw.ImageDraw):
        cx, cy = self.cab_x, self.cab_y
        cw, ch = self.cab_w, self.cab_h

        # Shadow
        for d in range(6, 0, -1):
            c = tuple(min(255, BG + d * 4) for BG in APP_BG)
            draw.rectangle([cx + d, cy + d, cx + cw + d, cy + ch + d], outline=c)

        # Outer frame gradient
        for y in range(ch):
            t = y / ch
            c = lerp(CAB_OUTER, lerp(CAB_OUTER, (32, 34, 38), 0.4), t * 0.5)
            draw.line([cx, cy + y, cx + cw, cy + y], fill=c)
        draw.rectangle([cx, cy, cx + cw, cy + ch], outline=CAB_FRAME, width=3)

        # Inner door inset
        draw.rectangle([cx + 6, cy + 6, cx + cw - 6, cy + ch - 6],
                       outline=(38, 40, 46), width=2)
        draw.rectangle([cx + 10, cy + 10, cx + cw - 10, cy + ch - 10],
                       fill=CAB_FACE, outline=(40, 42, 48))

        # Left hinges
        for hy in [cy + 35, cy + ch - 35]:
            draw.rectangle([cx - 5, hy - 10, cx + 5, hy + 10],
                           fill=CAB_HINGE, outline=(80, 83, 90))
            draw.ellipse([cx - 3, hy - 3, cx + 3, hy + 3], fill=CAB_SCREW)

        # Right lock
        lx = cx + cw - 10
        ly = cy + ch // 2
        draw.ellipse([lx - 7, ly - 9, lx + 7, ly + 9],
                     fill=CAB_HINGE, outline=(80, 83, 90))
        draw.ellipse([lx - 4, ly - 4, lx + 4, ly + 4], fill=CAB_SCREW)

    # ── Bays + devices ─────────────────────────────────────────────────
    def _draw_bays(self, img: Image.Image, draw: ImageDraw.ImageDraw):
        content_x = self.cab_x + 14 + BAY_PAD_L
        cy = self.cab_y + 14

        for bay_idx, bay in enumerate(self.spec.bays):
            bh = self.bay_h_list[bay_idx]
            rows = bay.rows_needed
            bay_mods = bay.row_modules
            bay_w = BAY_LBL_W + bay_mods * MX

            # Bay shelf background
            for y in range(bh):
                t = y / bh
                c = lerp(CAB_SHELF_BG, lerp(CAB_SHELF_BG, (48, 51, 57), 0.3), t * 0.4)
                draw.line([content_x, cy + y, content_x + bay_w, cy + y], fill=c)
            draw.rectangle([content_x, cy, content_x + bay_w, cy + bh],
                           outline=BAY_SHELF_LINE, width=1)

            # Bay label (left column)
            lbl_x = content_x
            draw.rectangle([lbl_x, cy, lbl_x + BAY_LBL_W - 4, cy + bh],
                           fill=BAY_LABEL_BG, outline=(42, 46, 58))
            draw.text((lbl_x + 10, cy + 12), bay.name, fill=BAY_NAME_C, font=self.f_bay_name)
            if bay.label:
                draw.text((lbl_x + 10, cy + 30), f"({bay.label})", fill=BAY_SUB_TXT, font=self.f_bay_lbl)

            # Usage indicator
            used = bay.used_modules
            total = rows * bay_mods
            pct = min(1.0, used / total) if total > 0 else 0
            bx = lbl_x + 8; bw2 = BAY_LBL_W - 20; by2 = cy + bh - 16
            draw.rectangle([bx, by2, bx + bw2, by2 + 7], fill=(38, 41, 50), outline=(55, 58, 68))
            fc = (0, 180, 70) if pct < 0.7 else ((220, 175, 20) if pct < 0.9 else (210, 50, 50))
            if int(bw2 * pct) > 0:
                draw.rectangle([bx, by2, bx + int(bw2 * pct), by2 + 7], fill=fc)
            draw.text((bx, by2 - 12), f"{used}/{total}", fill=TXT_DIM, font=self.f_slot_num)

            # Device row area
            dev_x = content_x + BAY_LBL_W
            row_y_base = cy + BAY_PAD_T

            for row in range(rows):
                ry = row_y_base + row * (MY + RAIL_H + SLOT_NUM_H)

                # Shelf plate for this row
                for y in range(MY):
                    t = y / MY
                    shade = lerp((62, 65, 73), (55, 58, 66), t)
                    draw.line([dev_x, ry + y, dev_x + bay_mods * MX, ry + y], fill=shade)

                # Empty module slots
                for sl in range(bay_mods):
                    sx = dev_x + sl * MX
                    draw.rectangle([sx + 1, ry + 1, sx + MX - 2, ry + MY - 2],
                                   fill=SLOT_BG, outline=(52, 55, 63), width=1)

                # DIN rail
                rail_y = ry + MY
                for y in range(RAIL_H):
                    t = y / RAIL_H
                    c = lerp(DIN_RAIL_TOP, DIN_RAIL_BOT, t)
                    draw.line([dev_x, rail_y + y, dev_x + bay_mods * MX, rail_y + y], fill=c)
                draw.line([dev_x, rail_y, dev_x + bay_mods * MX, rail_y], fill=(130, 135, 143))
                draw.line([dev_x, rail_y + RAIL_H - 1, dev_x + bay_mods * MX, rail_y + RAIL_H - 1],
                          fill=(130, 135, 143))

                # DIN holes
                for s in range(bay_mods):
                    hx = dev_x + s * MX + MX // 2
                    hy = rail_y + RAIL_H // 2
                    draw.ellipse([hx - 3, hy - 2, hx + 3, hy + 2],
                                 fill=DIN_HOLE, outline=(155, 160, 168))

                # Slot numbers below rail
                num_y = rail_y + RAIL_H + 2
                for sl in range(bay_mods):
                    num = row * bay_mods + sl + 1
                    sx = dev_x + sl * MX
                    draw.text((sx + MX // 2 - 7, num_y), f"{num:02d}",
                              fill=SLOT_NUM_C, font=self.f_slot_num)

            # Place device images
            cur_row = 0; cur_slot = 0
            for dev in bay.devices:
                m = dev.modules
                if cur_slot + m > bay_mods:
                    cur_row += 1; cur_slot = 0
                if cur_row >= rows:
                    break

                dx = dev_x + cur_slot * MX
                dy = row_y_base + cur_row * (MY + RAIL_H + SLOT_NUM_H)
                dw = m * MX

                dev_img = self.img_cache.get(dev.device_type)
                if dev_img:
                    paste_device(img, dev_img, dx, dy, dw, MY, pad=4)

                # Rating label below device (above slot numbers area)
                lbl = f"{int(dev.in_a)}A" if dev.in_a else ""
                if lbl:
                    draw.text((dx + dw // 2 - len(lbl) * 3, dy + MY - 14),
                              lbl, fill=(210, 215, 225), font=self.f_slot_num)

                # Separator
                draw.line([dx + dw - 1, dy, dx + dw - 1, dy + MY],
                          fill=(42, 45, 53), width=1)
                cur_slot += m

            cy += bh + BAY_GAP

    # ── Footer ─────────────────────────────────────────────────────────
    def _draw_footer(self, draw: ImageDraw.ImageDraw):
        fy = self.img_h - FOOTER_H
        draw.rectangle([0, fy, self.img_w, self.img_h], fill=FOOTER_BG)
        draw.line([0, fy, self.img_w, fy], fill=SIDEBAR_LINE)

        # DIN usage bar
        total_used = sum(b.used_modules for b in self.spec.bays)
        total_slots = sum(b.rows_needed * b.row_modules for b in self.spec.bays)
        pct = total_used / total_slots if total_slots > 0 else 0

        draw.text((LEFT_W + 12, fy + 9), "Mức độ sử dụng thanh DIN:", fill=TXT_GREY, font=self.f_footer)
        bar_x = LEFT_W + 200; bar_w = self.img_w - LEFT_W - 320; bar_h = 12; bar_y = fy + 10
        draw.rectangle([bar_x, bar_y, bar_x + bar_w, bar_y + bar_h], fill=PROGRESS_BG, outline=(50, 53, 64))
        filled = int(bar_w * pct)
        if filled > 0:
            for i in range(0, filled, 6):
                sw = min(5, filled - i)
                draw.rectangle([bar_x + i, bar_y + 2, bar_x + i + sw, bar_y + bar_h - 2],
                               fill=PROGRESS_GREEN)
        usage_txt = f"{total_used} / {total_slots} module ({int(pct*100)}%)"
        draw.text((bar_x + bar_w + 8, fy + 9), usage_txt, fill=TXT_GREY, font=self.f_footer)

        # Status left
        draw.text((8, fy + 9), "Trạng thái:", fill=TXT_DIM, font=self.f_status)
        draw.ellipse([78, fy + 12, 86, fy + 20], fill=STATUS_GREEN)
        draw.text((90, fy + 9), "Đã lưu", fill=STATUS_GREEN, font=self.f_status)

        # Version right
        ver = "Phiên bản: 1.0.0.0"
        draw.text((self.img_w - len(ver) * 6 - 8, fy + 9), ver, fill=TXT_DIM, font=self.f_status)


# ═══════════════════════════════════════════════════════════════════════
#  CLI
# ═══════════════════════════════════════════════════════════════════════
def main():
    p = argparse.ArgumentParser(description="MEP Cabinet Renderer v4 — Pixel-perfect")
    p.add_argument("--input", "-i")
    p.add_argument("--output", "-o", default="cabinet_v4.png")
    p.add_argument("--demo", action="store_true")
    args = p.parse_args()

    if args.demo or not args.input:
        spec = make_demo()
    elif args.input.lower().endswith(".json"):
        spec = parse_json(args.input)
    else:
        spec = parse_csv(args.input)

    img = CabinetRenderer(spec).render()
    img.save(args.output, "PNG", dpi=(150, 150))
    print(f"OK {args.output}  ({img.width}x{img.height}px)")


if __name__ == "__main__":
    main()
