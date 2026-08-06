#!/usr/bin/env python3
"""
MEP Cabinet Renderer v2 — Thiết bị photorealistic như thực tế thi công.
Mỗi loại thiết bị (MCB, MCCB, Contactor, Relay, Timer, Meter) được vẽ
chi tiết: màu sắc, form factor, toggle, terminal, logo band.

Usage:
    python3 render_cabinet_v2.py --demo --output cabinet.png
    python3 render_cabinet_v2.py --input devices.json --output cabinet.png
    python3 render_cabinet_v2.py --input devices.csv  --output cabinet.png
"""

import argparse, csv, json, math, os, sys
from dataclasses import dataclass, field
from typing import List, Optional, Tuple

try:
    from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
except ImportError:
    print("pip install pillow"); sys.exit(1)

# ── Scale & layout ──────────────────────────────────────────────────────────
MODULE_W   = 18          # mm per DIN module
SCALE      = 4           # px per mm
MX         = MODULE_W * SCALE   # 72 px per module
MY         = 130         # device height px (tall for realism)
RAIL_H     = 14
MODS_ROW   = 24
HDR_H      = 90
FTR_H      = 65
BAY_LBL_W  = 120
BAY_PAD_X  = 50
BAY_PAD_Y  = 50
BAY_GAP    = 24

# ── Colour palette ──────────────────────────────────────────────────────────
BG          = (22, 25, 32)
CABINET_FACE= (72, 74, 78)
CABINET_EDG = (55, 57, 60)
DIN_RAIL    = (190, 195, 200)
DIN_SLOT    = (200, 202, 205)
SLOT_BG     = (48, 50, 58)
SLOT_NUM    = (100, 105, 115)
BAY_BG      = (52, 56, 66)
BAY_BDR     = (85, 88, 100)
BAY_LBL_BG  = (32, 36, 48)
ACCENT      = (0, 180, 255)
WHITE       = (255, 255, 255)
GREY_LT     = (220, 222, 228)
GREY_MD     = (160, 162, 168)
GREY_DK     = (90, 92, 98)
TEXT_TITLE  = (255, 255, 255)
TEXT_LBL    = (205, 210, 220)
TEXT_SMALL  = (140, 148, 165)
C_GREEN     = (50, 195, 100)
C_YELLOW    = (240, 185, 30)
C_RED       = (220, 55, 55)

# ── Device appearance defs ──────────────────────────────────────────────────
# body_color, accent_color, toggle_color, label_color, band_color
DEV_STYLE = {
    "MCB":        ((68,70,74),   (0,122,55),    (235,238,242), (200,205,215), (0,122,55)),
    "MCCB":       ((50,52,56),   (20,20,22),    (200,202,208), (180,185,195), (30,30,35)),
    "ELCB":       ((60,70,60),   (0,110,50),    (210,230,215), (180,210,185), (0,110,50)),
    "CONTACTOR":  ((40,60,130),  (20,40,100),   (180,195,230), (210,220,240), (10,30,90)),
    "RELAY":      ((140,90,20),  (180,120,30),  (220,180,80),  (245,210,120), (160,100,20)),
    "TIMER":      ((50,60,100),  (30,45,80),    (180,195,225), (200,215,245), (10,25,75)),
    "METER":      ((25,30,45),   (0,160,220),   (10,20,50),    (0,200,255),   (0,90,160)),
    "PILOT":      ((50,55,65),   (200,60,60),   (220,60,60),   (240,200,200), (180,40,40)),
    "SWITCH":     ((60,65,75),   (80,120,80),   (210,225,210), (190,210,190), (50,100,50)),
    "SURGE":      ((80,60,40),   (160,100,30),  (200,155,80),  (230,200,130), (130,80,20)),
    "BUSBAR":     ((60,60,40),   (120,120,30),  (200,200,80),  (230,230,130), (100,100,20)),
    "OTHER":      ((70,72,78),   (90,92,100),   (200,202,208), (180,185,195), (60,62,68)),
}

DEVICE_MODULES = {
    "MCB 1P":1,"MCB 2P":2,"MCB 3P":3,"MCB 4P":4,
    "MCCB":4,"ELCB":2,"CONTACTOR":3,"RELAY":2,
    "TIMER":2,"METER":6,"PILOT":1,"SWITCH":1,
    "SURGE":2,"BUSBAR":6,"TRANSFORMER":8,
}


# ─── Helpers ────────────────────────────────────────────────────────────────
def lerp_color(c1, c2, t):
    return tuple(int(c1[i]*(1-t)+c2[i]*t) for i in range(3))

def clamp(v, lo=0, hi=255):
    return max(lo, min(hi, int(v)))

def lighter(c, amount=40):
    return tuple(clamp(x+amount) for x in c)

def darker(c, amount=40):
    return tuple(clamp(x-amount) for x in c)

def draw_rounded_rect(draw, x0,y0,x1,y1, r, fill, outline=None, width=1):
    draw.rectangle([x0+r,y0,x1-r,y1], fill=fill)
    draw.rectangle([x0,y0+r,x1,y1-r], fill=fill)
    for cx,cy in [(x0+r,y0+r),(x1-r,y0+r),(x0+r,y1-r),(x1-r,y1-r)]:
        draw.ellipse([cx-r,cy-r,cx+r,cy+r], fill=fill)
    if outline:
        draw.arc([x0,y0,x0+2*r,y0+2*r], 180,270, fill=outline, width=width)
        draw.arc([x1-2*r,y0,x1,y0+2*r], 270,360, fill=outline, width=width)
        draw.arc([x0,y1-2*r,x0+2*r,y1], 90,180, fill=outline, width=width)
        draw.arc([x1-2*r,y1-2*r,x1,y1], 0,90, fill=outline, width=width)
        draw.line([x0+r,y0,x1-r,y0], fill=outline, width=width)
        draw.line([x0+r,y1,x1-r,y1], fill=outline, width=width)
        draw.line([x0,y0+r,x0,y1-r], fill=outline, width=width)
        draw.line([x1,y0+r,x1,y1-r], fill=outline, width=width)

def draw_gradient_rect(img_data, x0,y0,x1,y1, c_top, c_bot):
    """Draw vertical gradient via pixel manipulation."""
    if x1<=x0 or y1<=y0: return
    pixels = img_data.load()
    for y in range(y0, min(y1, img_data.height)):
        t = (y-y0)/(y1-y0) if y1!=y0 else 0
        c = lerp_color(c_top, c_bot, t)
        for x in range(x0, min(x1, img_data.width)):
            pixels[x,y] = c

def screw_terminal(draw, cx, cy, r=5):
    draw.ellipse([cx-r,cy-r,cx+r,cy+r], fill=GREY_MD, outline=GREY_DK)
    draw.line([cx-r+2,cy,cx+r-2,cy], fill=GREY_DK, width=2)
    draw.line([cx,cy-r+2,cx,cy+r-2], fill=GREY_DK, width=2)


# ─── Device face renderers ───────────────────────────────────────────────────
def render_mcb(img, draw, x,y,w,h, poles, in_a, manufacturer=""):
    """Schneider/LS style MCB — toggle + terminals + brand band."""
    style = DEV_STYLE["MCB"]
    body, accent, toggle_c, lbl_c, band_c = style

    # Body gradient
    draw_gradient_rect(img, x,y,x+w,y+h, lighter(body,20), darker(body,10))

    # Side edges (3D effect)
    for i in range(4):
        shade = lighter(body, 30-i*8)
        draw.line([x+i,y,x+i,y+h], fill=shade)
    for i in range(3):
        shade = darker(body, 15+i*8)
        draw.line([x+w-1-i,y,x+w-1-i,y+h], fill=shade)

    # Top terminal area
    term_h = 22
    draw_gradient_rect(img, x+2,y+2,x+w-2,y+term_h, GREY_DK, darker(GREY_DK,10))
    for p in range(poles):
        cx = x + (p+0.5)*MX
        screw_terminal(draw, int(cx), y+12, r=6)

    # Brand colour band
    band_y = y + term_h
    band_h = 12
    draw.rectangle([x+2,band_y,x+w-2,band_y+band_h], fill=band_c)
    if manufacturer.upper().startswith("SCH") or manufacturer == "":
        # Schneider stripe
        draw.rectangle([x+2,band_y+2,x+w-2,band_y+4], fill=lighter(band_c,30))

    # Toggle handle area (main feature)
    tgl_y = band_y + band_h + 4
    tgl_h = 52
    for p in range(poles):
        tx = x + p*MX + 6
        tw = MX - 12

        # Handle body
        draw_gradient_rect(img, tx, tgl_y, tx+tw, tgl_y+tgl_h,
                           lighter(toggle_c,20), darker(toggle_c,10))
        draw.rectangle([tx,tgl_y,tx+tw,tgl_y+tgl_h], outline=GREY_DK, width=1)

        # "ON" bump at top of toggle
        on_h = tgl_h//2 - 2
        draw_gradient_rect(img, tx+2, tgl_y+2, tx+tw-2, tgl_y+on_h,
                           lighter(toggle_c,40), toggle_c)
        draw.rectangle([tx+2,tgl_y+2,tx+tw-2,tgl_y+on_h], outline=lighter(GREY_DK,20), width=1)

        # Red indicator dot
        dot_cx = tx + tw//2
        draw.ellipse([dot_cx-4,tgl_y+on_h+3,dot_cx+4,tgl_y+on_h+11], fill=(220,50,50))

        # "OFF" area
        draw_gradient_rect(img, tx+2,tgl_y+on_h+14,tx+tw-2,tgl_y+tgl_h-2,
                           darker(toggle_c,5), darker(toggle_c,20))

    # Bottom terminal area
    bot_term_y = y + h - term_h
    draw_gradient_rect(img, x+2,bot_term_y,x+w-2,y+h-2, GREY_DK, darker(GREY_DK,10))
    for p in range(poles):
        cx = x + (p+0.5)*MX
        screw_terminal(draw, int(cx), y+h-12, r=6)

    # Current rating label
    try:
        fnt = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 11)
        fnt_sm = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 9)
    except:
        fnt = fnt_sm = ImageFont.load_default()

    lbl = f"{int(in_a)}A" if in_a else "MCB"
    lbl_x = x + w//2
    lbl_y = tgl_y + tgl_h + 4
    draw.text((lbl_x - len(lbl)*3, lbl_y), lbl, fill=GREY_LT, font=fnt)

    # Poles indicator
    poles_lbl = f"{poles}P"
    draw.text((x+3, lbl_y), poles_lbl, fill=TEXT_SMALL, font=fnt_sm)

    # Outer border
    draw.rectangle([x,y,x+w-1,y+h-1], outline=darker(body,25), width=2)


def render_mccb(img, draw, x,y,w,h, poles, in_a, manufacturer=""):
    """MCCB — big lever, moulded case."""
    style = DEV_STYLE["MCCB"]
    body, accent, toggle_c, lbl_c, band_c = style

    draw_gradient_rect(img, x,y,x+w,y+h, lighter(body,15), darker(body,15))

    # Side bevel
    for i in range(5):
        draw.line([x+i,y,x+i,y+h], fill=lighter(body,25-i*5))
    for i in range(4):
        draw.line([x+w-1-i,y,x+w-1-i,y+h], fill=darker(body,10+i*8))

    # Terminals top
    term_h = 25
    draw_gradient_rect(img, x+3,y+3,x+w-3,y+term_h, GREY_DK, (55,57,62))
    for p in range(poles):
        cx = x + int((p+0.5)*w/poles)
        screw_terminal(draw, cx, y+14, r=8)
        # cable entry slot
        draw.rectangle([cx-4,y+3,cx+4,y+8], fill=(30,30,32))

    # Big rotary lever handle
    lever_y = y + term_h + 6
    lever_h = 55
    lever_w = w - 16
    lx = x + 8

    # Lever surround
    draw_gradient_rect(img, lx,lever_y,lx+lever_w,lever_y+lever_h,
                       (45,47,50),(30,32,35))
    draw.rectangle([lx,lever_y,lx+lever_w,lever_y+lever_h], outline=(20,22,25), width=2)

    # Lever body
    llx = lx+8; lly = lever_y+8
    llw = lever_w-16; llh = lever_h-16
    draw_gradient_rect(img, llx,lly,llx+llw,lly+llh,
                       lighter(toggle_c,15), darker(toggle_c,5))
    draw.rectangle([llx,lly,llx+llw,lly+llh], outline=lighter(toggle_c,30), width=2)

    # Arrow symbol on lever
    ax = llx + llw//2
    draw.polygon([(ax,lly+5),(ax-8,lly+18),(ax+8,lly+18)], fill=(100,105,115))
    draw.polygon([(ax,lly+llh-5),(ax-8,lly+llh-18),(ax+8,lly+llh-18)],
                 fill=(80,85,95))

    # Status window
    win_y = lever_y + lever_h + 4
    draw.rectangle([x+w//2-14,win_y,x+w//2+14,win_y+14],
                   fill=(10,20,10), outline=(0,120,40), width=1)
    draw.text((x+w//2-10,win_y+1), "ON", fill=(0,220,80),
              font=ImageFont.load_default())

    # Rating plate
    bot_y = y+h-28
    draw_gradient_rect(img, x+4,bot_y,x+w-4,y+h-4, (60,62,66),(50,52,56))
    try:
        fnt = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 11)
    except: fnt = ImageFont.load_default()
    lbl = f"MCCB {int(in_a)}A/{poles}P"
    draw.text((x+6, bot_y+4), lbl, fill=GREY_LT, font=fnt)

    # Bottom terminals
    for p in range(poles):
        cx = x + int((p+0.5)*w/poles)
        screw_terminal(draw, cx, y+h-14, r=8)

    draw.rectangle([x,y,x+w-1,y+h-1], outline=darker(body,30), width=3)


def render_contactor(img, draw, x,y,w,h, poles, in_a, manufacturer=""):
    """Contactor — LS/Schneider TeSys style, blue body."""
    style = DEV_STYLE["CONTACTOR"]
    body, accent, toggle_c, lbl_c, band_c = style

    draw_gradient_rect(img, x,y,x+w,y+h, lighter(body,25), darker(body,10))

    # Side bevel
    for i in range(4):
        draw.line([x+i,y,x+i,y+h], fill=lighter(body,30-i*7))
    for i in range(3):
        draw.line([x+w-1-i,y,x+w-1-i,y+h], fill=darker(body,15+i*8))

    # Top power terminals (3 main contacts)
    term_h = 22
    draw_gradient_rect(img, x+2,y+2,x+w-2,y+term_h, (35,38,45),(28,30,38))
    for p in range(3):
        cx = x + int((p+0.5)*w/3)
        # Power terminal
        draw.rectangle([cx-7,y+3,cx+7,y+16], fill=(55,60,70), outline=(40,45,55), width=1)
        screw_terminal(draw, cx, y+11, r=5)
        # Terminal label L1/L2/L3
        try: fnt6 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",7)
        except: fnt6 = ImageFont.load_default()
        draw.text((cx-5, y+16), f"L{p+1}", fill=(100,180,255), font=fnt6)

    # Coil/body
    body_y = y + term_h + 3
    body_h = h - term_h - 22 - 3
    draw_gradient_rect(img, x+4,body_y,x+w-4,body_y+body_h,
                       lighter(body,10), body)
    draw.rectangle([x+4,body_y,x+w-4,body_y+body_h], outline=darker(body,25), width=1)

    # Spring / electromagnet visual
    spring_y = body_y + 6
    spring_h = body_h - 12
    cx2 = x + w//2
    # Core
    draw.rectangle([cx2-12,spring_y+5,cx2+12,spring_y+spring_h-5],
                   fill=lighter(accent,20), outline=lighter(accent,40), width=1)
    # Coil lines
    for i in range(0, spring_h-10, 6):
        draw.line([cx2-10,spring_y+8+i,cx2+10,spring_y+8+i],
                  fill=lighter(body,40), width=1)

    # Aux contact indicator
    aux_x = x + w - 22
    aux_y = body_y + 8
    draw.rectangle([aux_x,aux_y,aux_x+14,aux_y+20], fill=(45,50,60), outline=(70,80,100))
    draw.text((aux_x+2,aux_y+4), "AUX", fill=TEXT_SMALL,
              font=ImageFont.load_default())

    # Status LED
    draw.ellipse([x+8,body_y+8,x+16,body_y+16], fill=(0,220,80))

    # Bottom load terminals T1/T2/T3
    term2_y = y + h - 20
    for p in range(3):
        cx = x + int((p+0.5)*w/3)
        draw.rectangle([cx-7,term2_y,cx+7,y+h-3], fill=(55,60,70), outline=(40,45,55))
        screw_terminal(draw, cx, y+h-12, r=5)
        try: fnt6 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",7)
        except: fnt6 = ImageFont.load_default()
        draw.text((cx-5, term2_y), f"T{p+1}", fill=(100,180,255), font=fnt6)

    # Rating label
    try: fnt10 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",10)
    except: fnt10 = ImageFont.load_default()
    lbl = f"{'LS' if manufacturer.upper().startswith('LS') else 'K'} {int(in_a)}A"
    draw.text((x+5, body_y+body_h//2+6), lbl, fill=lbl_c, font=fnt10)

    draw.rectangle([x,y,x+w-1,y+h-1], outline=darker(body,30), width=2)


def render_relay(img, draw, x,y,w,h, poles, in_a, manufacturer=""):
    """Thermal relay / overload relay."""
    style = DEV_STYLE["RELAY"]
    body, accent, toggle_c, lbl_c, band_c = style

    draw_gradient_rect(img, x,y,x+w,y+h, lighter(body,20), body)

    # Top terminals
    for p in range(2):
        cx = x + int((p+0.5)*w/2)
        screw_terminal(draw, cx, y+12, r=5)

    # Body with adjustment dial area
    mid_y = y + 25
    mid_h = h - 50
    draw_gradient_rect(img, x+4,mid_y,x+w-4,mid_y+mid_h, lighter(body,15),body)
    draw.rectangle([x+4,mid_y,x+w-4,mid_y+mid_h], outline=darker(body,20), width=1)

    # Adjustment dial
    dial_cx = x + w//2
    dial_cy = mid_y + mid_h//2 - 5
    dr = 14
    draw.ellipse([dial_cx-dr,dial_cy-dr,dial_cx+dr,dial_cy+dr],
                 fill=lighter(accent,15), outline=lighter(accent,40), width=2)
    draw.ellipse([dial_cx-4,dial_cy-4,dial_cx+4,dial_cy+4], fill=GREY_DK)
    # Tick marks
    for ang in range(0,360,45):
        rad = math.radians(ang)
        x1r = dial_cx + int((dr-3)*math.cos(rad))
        y1r = dial_cy + int((dr-3)*math.sin(rad))
        x2r = dial_cx + int((dr+1)*math.cos(rad))
        y2r = dial_cy + int((dr+1)*math.sin(rad))
        draw.line([x1r,y1r,x2r,y2r], fill=lighter(accent,50), width=1)
    # Pointer
    draw.line([dial_cx,dial_cy,dial_cx+10,dial_cy-8],
              fill=GREY_LT, width=2)

    # Reset button
    btn_y = mid_y + mid_h - 20
    draw_rounded_rect(draw, x+w//2-8,btn_y,x+w//2+8,btn_y+14, 4,
                      fill=(0,160,60), outline=(0,100,40))

    # Current range label
    try: fnt9 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",9)
    except: fnt9 = ImageFont.load_default()
    lbl = f"{int(in_a)}A" if in_a else "OL"
    draw.text((x+4, mid_y+2), lbl, fill=lbl_c, font=fnt9)

    for p in range(2):
        cx = x + int((p+0.5)*w/2)
        screw_terminal(draw, cx, y+h-12, r=5)

    draw.rectangle([x,y,x+w-1,y+h-1], outline=darker(body,25), width=2)


def render_timer(img, draw, x,y,w,h, in_a, manufacturer=""):
    """Timer relay — round dial face, DIN rail mount."""
    style = DEV_STYLE["TIMER"]
    body, accent, toggle_c, lbl_c, band_c = style

    draw_gradient_rect(img, x,y,x+w,y+h, lighter(body,20), body)

    mid_y = y + 15
    # Face plate
    draw_gradient_rect(img, x+4,mid_y,x+w-4,y+h-20, (30,35,55),(20,25,45))
    draw.rectangle([x+4,mid_y,x+w-4,y+h-20], outline=(60,75,120), width=1)

    # Circular dial
    dial_cx = x + w//2
    dial_cy = mid_y + (h-35)//2
    dr = min(w//2-8, (h-35)//2-6)
    # Dial face
    draw.ellipse([dial_cx-dr,dial_cy-dr,dial_cx+dr,dial_cy+dr],
                 fill=(240,245,255), outline=(160,170,200), width=2)
    # Scale marks
    for i in range(12):
        ang = math.radians(i*30 - 90)
        x1r = dial_cx + int((dr-8)*math.cos(ang))
        y1r = dial_cy + int((dr-8)*math.sin(ang))
        x2r = dial_cx + int((dr-2)*math.cos(ang))
        y2r = dial_cy + int((dr-2)*math.sin(ang))
        draw.line([x1r,y1r,x2r,y2r], fill=(80,90,110), width=2 if i%3==0 else 1)
    # Pointer at ~2/3
    ptr_ang = math.radians(120 - 90)
    draw.line([dial_cx,dial_cy,
               dial_cx+int((dr-10)*math.cos(ptr_ang)),
               dial_cy+int((dr-10)*math.sin(ptr_ang))],
              fill=(200,30,30), width=2)
    draw.ellipse([dial_cx-3,dial_cy-3,dial_cx+3,dial_cy+3], fill=(80,85,95))

    # Time range label
    try: fnt8 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",8)
    except: fnt8 = ImageFont.load_default()
    draw.text((x+6,mid_y+2), "0-60s", fill=lbl_c, font=fnt8)

    # Terminals top/bottom
    for p in range(2):
        cx = x + int((p+0.5)*w/2)
        screw_terminal(draw, cx, y+10, r=5)
        screw_terminal(draw, cx, y+h-10, r=5)

    draw.rectangle([x,y,x+w-1,y+h-1], outline=darker(body,25), width=2)


def render_meter(img, draw, x,y,w,h, poles, in_a, manufacturer=""):
    """Digital panel meter / ammeter."""
    style = DEV_STYLE["METER"]
    body, accent, toggle_c, lbl_c, band_c = style

    draw_gradient_rect(img, x,y,x+w,y+h, lighter(body,10), body)

    # Front panel
    fp_pad = 5
    draw.rectangle([x+fp_pad,y+fp_pad,x+w-fp_pad,y+h-fp_pad],
                   fill=(18,22,38), outline=accent, width=2)

    # Display screen
    scr_pad = 10
    scr_y = y + fp_pad + 8
    scr_h = (h - fp_pad*2 - 50)
    draw_gradient_rect(img, x+scr_pad,scr_y,x+w-scr_pad,scr_y+scr_h,
                       (5,15,30),(10,25,50))
    draw.rectangle([x+scr_pad,scr_y,x+w-scr_pad,scr_y+scr_h],
                   outline=(0,150,200), width=2)

    # 7-segment style reading
    try:
        fnt_big = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf",22)
        fnt_sm  = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",9)
    except:
        fnt_big = fnt_sm = ImageFont.load_default()

    reading = f"{int(in_a):3d}.{0}" if in_a > 0 else "0.0"
    draw.text((x+scr_pad+4, scr_y+4), reading, fill=(0,220,255), font=fnt_big)
    draw.text((x+w-scr_pad-18, scr_y+scr_h-14), "A", fill=(0,180,220), font=fnt_sm)

    # Backlight glow (simple)
    for glow in range(3,0,-1):
        draw.rectangle([x+scr_pad+glow,scr_y+glow,
                        x+w-scr_pad-glow,scr_y+scr_h-glow],
                       outline=(0,80+glow*20,120+glow*20), width=1)

    # CT ratio label
    ct_y = scr_y + scr_h + 4
    draw.text((x+fp_pad+2, ct_y), f"CT {int(in_a)}/5A" if in_a else "METER",
              fill=TEXT_SMALL, font=fnt_sm)

    # Selector / mode button
    btn_y = y + h - fp_pad - 16
    draw_rounded_rect(draw, x+fp_pad+4,btn_y,x+fp_pad+20,btn_y+10, 3,
                      fill=accent, outline=lighter(accent,20))

    # Terminals
    for p in range(min(poles,3)):
        cx = x + int((p+0.5)*w/min(poles,3))
        screw_terminal(draw, cx, y+h-8, r=5)

    draw.rectangle([x,y,x+w-1,y+h-1], outline=darker(body,20), width=2)


def render_generic(img, draw, x,y,w,h, dev_type, name, in_a):
    """Fallback generic device block with gradient + label."""
    skey = next((k for k in DEV_STYLE if k in dev_type.upper()), "OTHER")
    body, accent, toggle_c, lbl_c, band_c = DEV_STYLE[skey]

    draw_gradient_rect(img, x,y,x+w,y+h, lighter(body,20), darker(body,10))
    draw.rectangle([x+2,y+2,x+w-2,y+20], fill=band_c, outline=darker(band_c,15))

    try:
        fnt = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",11)
        fnt_sm = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",9)
    except:
        fnt = fnt_sm = ImageFont.load_default()

    label_short = (name[:8] + ".") if len(name) > 8 else name
    draw.text((x+4,y+h-28), label_short, fill=GREY_LT, font=fnt)
    if in_a:
        draw.text((x+4,y+h-14), f"{int(in_a)}A", fill=TEXT_SMALL, font=fnt_sm)

    screw_terminal(draw, x+w//2, y+10, r=5)
    screw_terminal(draw, x+w//2, y+h-10, r=5)
    draw.rectangle([x,y,x+w-1,y+h-1], outline=darker(body,25), width=2)


def dispatch_device(img, draw, device, dx, dy, w_px):
    """Route to correct renderer based on device type."""
    t = device.device_type.upper()
    h = MY
    p = device.poles or 1
    ia = device.in_a
    mfr = device.manufacturer

    if "MCCB" in t:
        render_mccb(img, draw, dx,dy,w_px,h, p,ia,mfr)
    elif "MCB" in t or "ELCB" in t:
        render_mcb(img, draw, dx,dy,w_px,h, p,ia,mfr)
    elif "CONTACTOR" in t or "CONT" in t:
        render_contactor(img, draw, dx,dy,w_px,h, p,ia,mfr)
    elif "RELAY" in t:
        render_relay(img, draw, dx,dy,w_px,h, p,ia,mfr)
    elif "TIMER" in t:
        render_timer(img, draw, dx,dy,w_px,h, ia,mfr)
    elif "METER" in t or "DONG HO" in t or "AMMETER" in t:
        render_meter(img, draw, dx,dy,w_px,h, p,ia,mfr)
    else:
        render_generic(img, draw, dx,dy,w_px,h, t, device.name, ia)


# ─── Data model (same as v1) ─────────────────────────────────────────────────
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

    def __post_init__(self):
        self.modules = self._calc_modules()

    def _calc_modules(self):
        t = self.device_type.upper()
        for k,m in DEVICE_MODULES.items():
            if t.startswith(k.upper()): return m
        if "MCB" in t: return max(1, self.poles)
        if "MCCB" in t: return 4
        if "CONTACTOR" in t or "CONT" in t: return 3
        return 2

@dataclass
class Bay:
    name: str = "NGĂN"
    label: str = ""
    devices: List[Device] = field(default_factory=list)
    max_modules: int = MODS_ROW

    @property
    def rows_needed(self):
        total = sum(d.modules * d.qty for d in self.devices)
        return max(1, math.ceil(total / self.max_modules))

    @property
    def used_modules(self):
        return sum(d.modules * d.qty for d in self.devices)

@dataclass
class CabinetSpec:
    name: str = "DB"
    cabinet_type: str = "Tủ phân phối"
    size: str = "600x800x200"
    floor_level: str = ""
    bays: List[Bay] = field(default_factory=list)


# ─── Demo spec ───────────────────────────────────────────────────────────────
def make_demo():
    spec = CabinetSpec("DB-T1A","Tủ phân phối","600x800x200","FL+1.400")
    b1 = Bay("NGĂN 1","Nguồn vào")
    b1.devices=[
        Device("MCCB tổng","MCCB",100,3,3,1,manufacturer="Schneider"),
        Device("MCB 1P 32A","MCB 1P",32,poles=1,qty=2),
    ]
    b2 = Bay("NGĂN 2","Phân phối")
    b2.devices=[
        Device("MCB đèn","MCB 1P",10,poles=1,qty=4),
        Device("MCB ổ cắm","MCB 1P",16,poles=1,qty=4),
        Device("MCB AC","MCB 1P",20,poles=1,qty=3),
        Device("MCB 3P","MCB 3P",16,poles=3,qty=2),
        Device("MCB 3P 32A","MCB 3P",32,poles=3,qty=1,manufacturer="LS"),
    ]
    b3 = Bay("NGĂN 3","Điều khiển")
    b3.devices=[
        Device("Contactor K1","CONTACTOR",25,poles=3,qty=2,manufacturer="LS"),
        Device("Relay nhiệt","RELAY",10,poles=2,qty=2),
        Device("Timer T1","TIMER",0,poles=0,qty=1),
        Device("Đồng hồ A","METER",100,poles=3,qty=1),
    ]
    spec.bays=[b1,b2,b3]
    return spec


# ─── Parsers ─────────────────────────────────────────────────────────────────
def parse_json(path):
    with open(path,encoding="utf-8") as f: data=json.load(f)
    if isinstance(data,list): data=data[0] if data else {}
    spec=CabinetSpec(data.get("name","DB"),data.get("type","Tủ phân phối"),
                     data.get("size","600x800x200"),data.get("floor",""))
    for bd in data.get("bays",[]):
        bay=Bay(bd.get("name","NGĂN"),bd.get("label",""))
        for dd in bd.get("devices",[]):
            dev=Device(dd.get("name",dd.get("type","")),dd.get("type","OTHER"),
                       float(dd.get("in_a",0)),float(dd.get("voltage",230)),
                       int(dd.get("poles",1)),dd.get("curve","C"),
                       int(dd.get("qty",1)),dd.get("manufacturer",""),
                       dd.get("code",""),dd.get("note",""))
            bay.devices.append(dev)
        spec.bays.append(bay)
    return spec

def parse_csv(path):
    spec=CabinetSpec()
    cur=Bay("NGĂN 1","Phân phối"); spec.bays.append(cur)
    with open(path,encoding="utf-8-sig") as f:
        reader=csv.DictReader(f)
        for row in reader:
            row={k.strip():v.strip() for k,v in row.items() if k}
            bn=row.get("bay",row.get("ngan",""))
            if bn and bn!=cur.name:
                cur=Bay(bn,row.get("bay_label",bn)); spec.bays.append(cur)
            name=row.get("ten_thiet_bi",row.get("name",row.get("ten","")))
            dtype=row.get("device_type",row.get("loai",row.get("type",name)))
            try: ia=float(row.get("in_a",row.get("in",row.get("dong",0)))or 0)
            except: ia=0
            try: qty=int(row.get("qty",row.get("sl",row.get("so_luong",1)))or 1)
            except: qty=1
            try: poles=int(row.get("poles",row.get("pha",1))or 1)
            except: poles=1
            dev=Device(name or dtype,dtype,ia,0,poles,"C",qty,
                       row.get("manufacturer",row.get("nha_san_xuat","")),
                       row.get("code",row.get("ma","")),
                       row.get("ghi_chu",row.get("note","")))
            if dev.name: cur.devices.append(dev)
    return spec


# ─── Main renderer ───────────────────────────────────────────────────────────
class CabinetRenderer:
    def __init__(self, spec, mods_row=MODS_ROW):
        self.spec = spec
        self.mods_row = mods_row
        for b in spec.bays: b.max_modules = mods_row
        try:
            self.f_title = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",26)
            self.f_sub   = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",18)
            self.f_bay   = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",15)
            self.f_dev   = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",12)
            self.f_small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",10)
        except:
            self.f_title=self.f_sub=self.f_bay=self.f_dev=self.f_small=ImageFont.load_default()

    def _layout(self):
        row_counts = [b.rows_needed for b in self.spec.bays]
        total_rows = sum(row_counts)
        n_bays = len(self.spec.bays)
        cw = BAY_LBL_W + self.mods_row*MX + BAY_PAD_X*2
        ch = sum(b.rows_needed*(MY+RAIL_H+2)+BAY_GAP+36 for b in self.spec.bays) + BAY_PAD_Y*2
        self.img_w = max(1200, cw+220)
        self.img_h = HDR_H + ch + FTR_H + 20
        self.cab_x = 16; self.cab_y = HDR_H+8
        self.cab_w = cw; self.cab_h = ch

    def render(self):
        self._layout()
        img = Image.new("RGB",(self.img_w,self.img_h),BG)
        draw = ImageDraw.Draw(img)

        self._header(draw, img)
        self._cabinet_body(draw)
        self._bays(img,draw)
        self._footer(draw)
        self._sidebar(draw)

        img = img.filter(ImageFilter.SMOOTH_MORE)
        return img

    def _header(self,draw,img):
        # Background gradient
        for y in range(HDR_H):
            t=y/HDR_H
            c=lerp_color((18,22,32),(28,32,44),t)
            draw.line([0,y,self.img_w,y],fill=c)
        draw.line([0,HDR_H-2,self.img_w,HDR_H-2],fill=ACCENT,width=2)

        # Icon placeholder
        draw_rounded_rect(draw,12,12,50,50,6,fill=(0,130,200),outline=(0,170,240))
        draw.text((20,22),"DB",fill=WHITE,font=self.f_bay)

        draw.text((60,8),f"CẤU HÌNH TỦ ĐIỆN  —  {self.spec.name}",fill=TEXT_TITLE,font=self.f_title)
        draw.text((60,42),f"{self.spec.cabinet_type}  |  {self.spec.size} mm  |  {self.spec.floor_level}",
                  fill=TEXT_LBL,font=self.f_sub)

        # Stats right
        total_dev = sum(d.qty for b in self.spec.bays for d in b.devices)
        total_mod = sum(b.used_modules for b in self.spec.bays)
        rx = self.img_w - 310
        draw.text((rx,10),f"Tổng module: {total_mod}",fill=TEXT_LBL,font=self.f_sub)
        draw.text((rx,36),f"Thiết bị: {total_dev}  |  Ngăn: {len(self.spec.bays)}",
                  fill=TEXT_SMALL,font=self.f_dev)

    def _cabinet_body(self,draw):
        x0=self.cab_x; y0=self.cab_y
        x1=x0+self.cab_w; y1=y0+self.cab_h
        # Shadow
        for d in range(8,0,-1):
            alpha = 40-d*4
            c=(BG[0]+alpha,BG[1]+alpha,BG[2]+alpha)
            draw.rectangle([x0+d,y0+d,x1+d,y1+d],outline=c)
        # Body
        draw.rectangle([x0,y0,x1,y1],fill=CABINET_FACE,outline=CABINET_EDG,width=3)
        # Door frame inner
        draw.rectangle([x0+6,y0+6,x1-6,y1-6],outline=darker(CABINET_EDG,20),width=2)
        # Hinges
        for hy in [y0+40,y1-40]:
            draw.rectangle([x0-4,hy-8,x0+4,hy+8],fill=GREY_MD,outline=GREY_DK)
        # Lock
        lx=x1-12; ly=(y0+y1)//2
        draw.ellipse([lx-6,ly-8,lx+6,ly+8],fill=GREY_LT,outline=GREY_DK)
        draw.ellipse([lx-3,ly-3,lx+3,ly+3],fill=GREY_DK)

    def _bays(self,img,draw):
        cx=self.cab_x+BAY_PAD_X
        cy=self.cab_y+BAY_PAD_Y

        for bay in self.spec.bays:
            rows=bay.rows_needed
            bay_h=rows*(MY+RAIL_H+2)+36
            bay_w=BAY_LBL_W+self.mods_row*MX

            # Bay shell
            draw.rectangle([cx,cy,cx+bay_w,cy+bay_h],fill=BAY_BG,outline=BAY_BDR,width=2)

            # Bay label column
            draw.rectangle([cx,cy,cx+BAY_LBL_W-4,cy+bay_h],fill=BAY_LBL_BG,outline=BAY_BDR)
            draw.text((cx+8,cy+10),bay.name,fill=ACCENT,font=self.f_bay)
            if bay.label:
                draw.text((cx+8,cy+30),bay.label,fill=TEXT_SMALL,font=self.f_small)

            # Progress bar
            used=bay.used_modules
            total_slots=rows*self.mods_row
            pct=used/total_slots if total_slots else 0
            bx=cx+6; by=cy+bay_h-18; bw=BAY_LBL_W-16
            draw.rectangle([bx,by,bx+bw,by+8],fill=SLOT_BG,outline=BAY_BDR)
            fc=C_GREEN if pct<0.7 else (C_YELLOW if pct<0.9 else C_RED)
            if int(bw*pct)>0:
                draw.rectangle([bx,by,bx+int(bw*pct),by+8],fill=fc)
            draw.text((bx,by-13),f"{used}/{total_slots}",fill=TEXT_SMALL,font=self.f_small)

            # Rows of devices
            dev_x=cx+BAY_LBL_W
            row_y=cy+4
            cur_row=0; cur_slot=0

            # Draw DIN rails + empty slots first
            for r in range(rows):
                ry=row_y+r*(MY+RAIL_H+2)
                # Empty slot backgrounds
                for sl in range(self.mods_row):
                    sx=dev_x+sl*MX
                    draw.rectangle([sx+1,ry+1,sx+MX-1,ry+MY-1],
                                   fill=SLOT_BG,outline=(60,62,70),width=1)
                    num=r*self.mods_row+sl+1
                    draw.text((sx+MX//2-8,ry+MY//2-6),f"{num:02d}",
                              fill=SLOT_NUM,font=self.f_small)
                # DIN rail
                rail_y=ry+MY
                draw.rectangle([dev_x,rail_y,dev_x+self.mods_row*MX,rail_y+RAIL_H],
                                fill=DIN_RAIL,outline=(150,155,160),width=1)
                # DIN slot holes
                for s in range(0,self.mods_row*MX,MX//2):
                    draw.ellipse([dev_x+s+2,rail_y+3,dev_x+s+8,rail_y+RAIL_H-3],
                                 fill=DIN_SLOT,outline=GREY_MD)

            # Place devices
            for dev in bay.devices:
                for _ in range(dev.qty):
                    m=dev.modules
                    if cur_slot+m>self.mods_row:
                        cur_row+=1; cur_slot=0
                    if cur_row>=rows: break
                    dx2=dev_x+cur_slot*MX
                    dy2=row_y+cur_row*(MY+RAIL_H+2)
                    dispatch_device(img,draw,dev,dx2,dy2,m*MX)
                    cur_slot+=m

            cy+=bay_h+BAY_GAP

    def _footer(self,draw):
        fy=self.img_h-FTR_H
        for y in range(FTR_H):
            c=lerp_color((18,22,32),(22,26,36),y/FTR_H)
            draw.line([0,fy+y,self.img_w,fy+y],fill=c)
        draw.line([0,fy,self.img_w,fy],fill=ACCENT,width=2)

        dev_count=sum(d.qty for b in self.spec.bays for d in b.devices)
        mod_count=sum(b.used_modules for b in self.spec.bays)
        slots=sum(b.rows_needed*self.mods_row for b in self.spec.bays)
        txt=(f"  {self.spec.name}  |  {dev_count} thiết bị  |  "
             f"{mod_count}/{slots} module  |  MEP Drawing Tool v2.0")
        draw.text((18,fy+12),txt,fill=TEXT_LBL,font=self.f_dev)
        draw.text((18,fy+34),"  Bản vẽ tham khảo — xác nhận với kỹ sư trước khi thi công.",
                  fill=TEXT_SMALL,font=self.f_small)

    def _sidebar(self,draw):
        sx=self.cab_x+self.cab_w+28
        sy=self.cab_y+10
        draw.text((sx,sy),"THIẾT BỊ",fill=ACCENT,font=self.f_bay); sy+=28
        legend=[("MCB 1P",DEV_STYLE["MCB"][0]),("MCB 3P",DEV_STYLE["MCB"][0]),
                ("MCCB",DEV_STYLE["MCCB"][0]),("Contactor",DEV_STYLE["CONTACTOR"][0]),
                ("Relay",DEV_STYLE["RELAY"][0]),("Timer",DEV_STYLE["TIMER"][0]),
                ("Meter",DEV_STYLE["METER"][0])]
        for lbl,c in legend:
            draw_rounded_rect(draw,sx,sy,sx+26,sy+18,3,fill=c,outline=lighter(c,30))
            draw.text((sx+32,sy+2),lbl,fill=TEXT_LBL,font=self.f_dev); sy+=24
        sy+=16
        draw.text((sx,sy),"TRẠNG THÁI",fill=ACCENT,font=self.f_bay); sy+=24
        for lbl,c in [("Tốt < 70%",C_GREEN),("Đầy 70-90%",C_YELLOW),("Quá >90%",C_RED)]:
            draw.rectangle([sx,sy,sx+24,sy+12],fill=c)
            draw.text((sx+30,sy),lbl,fill=TEXT_SMALL,font=self.f_small); sy+=18
        sy+=16
        draw.text((sx,sy),"KÝ HIỆU",fill=ACCENT,font=self.f_bay); sy+=24
        # Terminal screw icon
        draw.ellipse([sx,sy,sx+12,sy+12],fill=GREY_MD,outline=GREY_DK)
        draw.line([sx+2,sy+6,sx+10,sy+6],fill=GREY_DK,width=2)
        draw.line([sx+6,sy+2,sx+6,sy+10],fill=GREY_DK,width=2)
        draw.text((sx+18,sy+1),"Đầu cốt",fill=TEXT_SMALL,font=self.f_small); sy+=18
        # DIN rail icon
        draw.rectangle([sx,sy,sx+24,sy+8],fill=DIN_RAIL,outline=GREY_MD)
        draw.text((sx+30,sy),"DIN rail",fill=TEXT_SMALL,font=self.f_small)


# ─── CLI ─────────────────────────────────────────────────────────────────────
def main():
    parser=argparse.ArgumentParser(description="MEP Cabinet Renderer v2")
    parser.add_argument("--input","-i"); parser.add_argument("--output","-o",default="cabinet.png")
    parser.add_argument("--demo",action="store_true"); parser.add_argument("--modules",type=int,default=24)
    args=parser.parse_args()

    if args.demo or not args.input: spec=make_demo()
    elif args.input.lower().endswith(".json"): spec=parse_json(args.input)
    else: spec=parse_csv(args.input)

    img=CabinetRenderer(spec,args.modules).render()
    img.save(args.output,"PNG",dpi=(150,150))
    print(f"OK {args.output}  ({img.width}x{img.height}px)")

if __name__=="__main__": main()
