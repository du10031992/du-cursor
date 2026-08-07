#!/usr/bin/env python3
"""
Post-processing photoreal cho render tu dien — mo phong phong cach catalog 3D / AI.

Cong cu tham chieu anh nguoi dung:
- AI image generation (Midjourney / DALL-E / Cursor) cho vo tu + thiet bi
- Python Pillow composite (khong can Blender/V-Ray trong plugin)
"""

import os
from typing import List, Optional, Sequence, Tuple

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageChops

from render_cabinet import SCRIPT_DIR, remove_white_background

# Vo tu AI (1536x1024) — vung lap thiet bi ben trong
SHELL_FILES = (
    "cabinet_shell_template.png",
    "electrical-panel-interior-realistic.png",
    "cabinet-render-reference.png",
)
SHELL_SIZE = (1536, 1024)
# left, top, right, bottom — vung DIN ben trong vo tu
INNER_RECT = (300, 155, 1120, 830)
# Den bao pha tren canh cua (trong anh shell)
DOOR_LIGHTS_RECT = (1140, 200, 1490, 380)


def find_shell_template() -> Optional[str]:
    devices = os.path.join(SCRIPT_DIR, "devices")
    for name in SHELL_FILES:
        path = os.path.join(devices, name)
        if os.path.exists(path):
            return path
    return None


def paste_device_with_shadow(
    canvas: Image.Image,
    device_img: Image.Image,
    x: int,
    y: int,
    w: int,
    h: int,
    shadow_offset: Tuple[int, int] = (4, 5),
    shadow_blur: int = 7,
    shadow_alpha: int = 90,
):
    """Dan thiet bi kem bong do — giong render catalog 3D."""
    pad = 4
    target_w = w - pad * 2
    target_h = h - pad * 2
    ratio = min(target_w / device_img.width, target_h / device_img.height)
    nw = max(1, int(device_img.width * ratio))
    nh = max(1, int(device_img.height * ratio))
    resized = device_img.resize((nw, nh), Image.LANCZOS)
    resized = remove_white_background(resized, threshold=235)

    ox = x + pad + (target_w - nw) // 2
    oy = y + pad + (target_h - nh) // 2

    layer = canvas.convert("RGBA")
    alpha = resized.split()[3]

    shadow = Image.new("RGBA", (nw + shadow_blur * 2, nh + shadow_blur * 2), (0, 0, 0, 0))
    smask = alpha.resize((nw, nh), Image.LANCZOS).point(lambda a: min(a, shadow_alpha))
    shadow.paste((20, 22, 28, shadow_alpha), (shadow_blur, shadow_blur), smask)
    shadow = shadow.filter(ImageFilter.GaussianBlur(shadow_blur))

    sx = ox + shadow_offset[0] - shadow_blur
    sy = oy + shadow_offset[1] - shadow_blur
    layer.paste(shadow, (sx, sy), shadow)

    layer.paste(resized, (ox, oy), resized)
    canvas.paste(layer.convert("RGB"), (0, 0))


def add_emissive_glow(
    img: Image.Image,
    centers: Sequence[Tuple[int, int, Tuple[int, int, int]]],
    radius: int = 28,
    intensity: float = 0.55,
) -> Image.Image:
    """Hieu ung phat sang cho den bao pha L1/L2/L3."""
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    for cx, cy, color in centers:
        for r in range(radius, 4, -4):
            alpha = int(255 * intensity * (1 - r / radius) ** 1.6)
            gdraw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(*color, alpha))

    glow = glow.filter(ImageFilter.GaussianBlur(6))
    base = img.convert("RGBA")
    return Image.alpha_composite(base, glow).convert("RGB")


def draw_duct_wires(
    draw: ImageDraw.ImageDraw,
    duct_x: int,
    y0: int,
    y1: int,
    colors: Tuple[Tuple[int, int, int], ...] = ((200, 40, 40), (230, 180, 30), (40, 80, 200)),
    duct_w: int = 32,
):
    """Day dien trong mang — gap 90 do nhu lap tu that."""
    slots = [duct_x + 8 + i * 9 for i in range(len(colors))]
    for i, col in enumerate(colors):
        wx = slots[i % len(slots)]
        mid = (y0 + y1) // 2
        draw.line([(wx, y0), (wx, mid)], fill=col, width=3)
        draw.line([(wx, mid), (wx + (6 if i == 0 else -4), mid + 18)], fill=col, width=3)
        draw.line([(wx + (6 if i == 0 else -4), mid + 18), (wx + (6 if i == 0 else -4), y1 - 8)], fill=col, width=2)


def apply_ambient_occlusion(img: Image.Image, rect: Tuple[int, int, int, int], strength: float = 0.22) -> Image.Image:
    """Toi goc / canh — mo phong AO nhe."""
    x0, y0, x1, y1 = rect
    ao = Image.new("L", img.size, 0)
    draw = ImageDraw.Draw(ao)
    pad = 48
    for edge, box in (
        ("top", (x0, y0, x1, y0 + pad)),
        ("bottom", (x0, y1 - pad, x1, y1)),
        ("left", (x0, y0, x0 + pad, y1)),
        ("right", (x1 - pad, y0, x1, y1)),
    ):
        draw.rectangle(box, fill=int(255 * strength))
    ao = ao.filter(ImageFilter.GaussianBlur(18))
    base = img.convert("RGB")
    dark = Image.new("RGB", img.size, (12, 14, 18))
    return Image.composite(base, Image.composite(dark, base, ao), ao.point(lambda v: int(v * 0.85)))


def apply_studio_post(img: Image.Image) -> Image.Image:
    """Color grade + vignette nhe — giong anh studio catalog."""
    img = ImageEnhance.Contrast(img).enhance(1.06)
    img = ImageEnhance.Color(img).enhance(1.04)
    img = ImageEnhance.Sharpness(img).enhance(1.12)

    w, h = img.size
    vignette = Image.new("L", (w, h), 0)
    vd = ImageDraw.Draw(vignette)
    vd.ellipse([-w * 0.08, -h * 0.08, w * 1.08, h * 1.08], fill=220)
    vignette = vignette.filter(ImageFilter.GaussianBlur(max(w, h) // 8))
    dark = Image.new("RGB", (w, h), (18, 20, 24))
    return Image.composite(img, dark, vignette)


def composite_on_shell(layout: Image.Image, shell_path: Optional[str] = None) -> Image.Image:
    """
    Ghep layout DIN len vo tu AI.
    layout: anh noi that (RGB) da ve thiet bi
    """
    path = shell_path or find_shell_template()
    if not path:
        return apply_studio_post(layout)

    shell = Image.open(path).convert("RGB")
    if shell.size != SHELL_SIZE:
        shell = shell.resize(SHELL_SIZE, Image.LANCZOS)

    x0, y0, x1, y1 = INNER_RECT
    inner_w, inner_h = x1 - x0, y1 - y0
    fitted = layout.resize((inner_w, inner_h), Image.LANCZOS)

    out = shell.copy()
    # Loai nen toi — chi giu thiet bi / rail
    fitted_rgba = fitted.convert("RGBA")
    data = fitted_rgba.getdata()
    new_data = []
    for r, g, b, a in data:
        if r < 55 and g < 55 and b < 60:
            new_data.append((r, g, b, 0))
        else:
            new_data.append((r, g, b, 255))
    fitted_rgba.putdata(new_data)
    mask = fitted_rgba.split()[3].filter(ImageFilter.GaussianBlur(1))
    out.paste(fitted, (x0, y0), mask)
    return apply_studio_post(out)


def door_light_centers_from_layout(
    layout: Image.Image,
    door_x: int,
    door_y: int,
    door_w: int,
) -> List[Tuple[int, int, Tuple[int, int, int]]]:
    """Map vi tri den tu layout phang sang toa do tren anh shell."""
    path = find_shell_template()
    if not path:
        return []

    lx0, ly0, lx1, ly1 = INNER_RECT
    scale_x = (lx1 - lx0) / layout.width
    scale_y = (ly1 - ly0) / layout.height

    # Vi tri den tren cua trong layout phang
    cx_layout = door_x + door_w // 2
    cy_layout = door_y + 56
    spacing = (layout.width * 0.08)

    shell_cx = int(lx0 + cx_layout * scale_x)
    shell_cy = int(ly0 + cy_layout * scale_y)
    dx = int(spacing * scale_x * 0.35)

    colors = ((220, 45, 45), (245, 195, 25), (35, 95, 210))
    return [(shell_cx - dx + i * dx, shell_cy, colors[i]) for i in range(3)]
