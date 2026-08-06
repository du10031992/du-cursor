#!/usr/bin/env python3
"""
MEP Cabinet AI Renderer — Đọc dữ liệu JSON/CSV → tạo prompt chi tiết → gọi AI image generation.
Chạy độc lập: in ra prompt để dùng với bất kỳ AI image API nào.
"""

import json, csv, sys, os, math
from dataclasses import dataclass, field
from typing import List

# ── Data model (same as v4) ────────────────────────────────────────────
@dataclass
class Device:
    name: str
    device_type: str = "OTHER"
    model: str = ""
    in_a: float = 0
    poles: int = 1
    qty: int = 1
    manufacturer: str = ""
    modules: int = 0
    note: str = ""
    def __post_init__(self):
        if self.modules <= 0:
            self.modules = self._calc()
    def _calc(self):
        MODS = {"MCCB":4,"MCB 1P":1,"MCB 2P":2,"MCB 3P":3,"CONTACTOR":4,
                "RELAY":3,"TIMER":4,"METER":6,"BUSBAR":12}
        t = self.device_type.upper()
        for k,m in MODS.items():
            if t.startswith(k): return m
        if "MCB" in t: return max(1,self.poles)
        return 2
    @property
    def label(self):
        parts = [self.device_type]
        if self.in_a: parts.append(f"{int(self.in_a)}A")
        return " ".join(parts)

@dataclass
class Bay:
    name: str
    label: str = ""
    devices: List[Device] = field(default_factory=list)
    row_modules: int = 12
    @property
    def used_modules(self): return sum(d.modules for d in self.devices)
    @property
    def rows_needed(self): return max(1, math.ceil(self.used_modules / self.row_modules))

@dataclass
class CabinetSpec:
    name: str = "TD-01"
    project: str = ""
    cabinet_type: str = "Tủ phân phối"
    voltage: str = "400 VAC"
    in_rated: float = 100
    created: str = ""
    note: str = ""
    bays: List[Bay] = field(default_factory=list)


def parse_json(path):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    if isinstance(data, list): data = data[0]
    spec = CabinetSpec(name=data.get("name","TD"), project=data.get("project",""),
                       cabinet_type=data.get("type",""), voltage=data.get("voltage","400 VAC"),
                       in_rated=float(data.get("in_rated",100)), created=data.get("created",""),
                       note=data.get("note",""))
    for bd in data.get("bays",[]):
        bay = Bay(name=bd.get("name","NGĂN"), label=bd.get("label",""),
                  row_modules=int(bd.get("row_modules",12)))
        for dd in bd.get("devices",[]):
            qty = int(dd.get("qty",1))
            for _ in range(qty):
                bay.devices.append(Device(
                    name=dd.get("name",""), device_type=dd.get("type","OTHER"),
                    model=dd.get("model",""), in_a=float(dd.get("in_a",0)),
                    poles=int(dd.get("poles",1)), manufacturer=dd.get("manufacturer",""),
                    modules=int(dd.get("modules",0)), note=dd.get("note","")))
        spec.bays.append(bay)
    return spec


def parse_csv(path):
    spec = CabinetSpec(); cur = None
    with open(path, encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            row = {k.strip():v.strip() for k,v in row.items() if k}
            bn = row.get("bay","")
            if cur is None or (bn and bn != cur.name):
                cur = Bay(name=bn or "NGĂN 1", label=row.get("bay_label",""),
                          row_modules=int(row.get("row_modules",12)))
                spec.bays.append(cur)
            try: qty=int(row.get("qty",row.get("sl",1)) or 1)
            except: qty=1
            try: ia=float(row.get("in_a",0) or 0)
            except: ia=0
            name = row.get("ten_thiet_bi",row.get("name",""))
            dtype = row.get("device_type",row.get("loai",name))
            for _ in range(qty):
                d = Device(name=name or dtype, device_type=dtype,
                           model=row.get("model",""), in_a=ia,
                           poles=int(row.get("poles",1) or 1),
                           manufacturer=row.get("manufacturer",""),
                           modules=int(row.get("modules",0) or 0))
                if d.name: cur.devices.append(d)
    return spec


# ── Device description builders ────────────────────────────────────────
def describe_device(dev: Device) -> str:
    t = dev.device_type.upper()
    mfr = dev.manufacturer or "Schneider"
    ia = f"{int(dev.in_a)}A" if dev.in_a else ""
    model = dev.model or ""

    if "MCCB" in t:
        return (f"1 large black Schneider EasyPact {model or 'CVS100F'} MCCB 3P {ia} "
                f"(rectangular moulded case ~4 modules wide, large grey rotary handle, "
                f"silver screw terminals top/bottom, Schneider green logo, "
                f"white rating label '{model} {ia}')")

    elif "MCB" in t:
        p = f"{dev.poles}P" if dev.poles > 1 else "1P"
        pole_desc = (f"{dev.poles} connected white toggle handles side by side"
                     if dev.poles > 1 else "single white toggle handle")
        mods = f"{dev.modules} module wide"
        return (f"1 Schneider Easy9 MCB {p} {ia} "
                f"({mods}, white/off-white housing, narrow green Schneider brand strip at top "
                f"~12% height, {pole_desc} in center in ON position, small red indicator dot, "
                f"'{ia}' black text at bottom)")

    elif "CONTACTOR" in t:
        ls_mfr = dev.manufacturer.upper() if dev.manufacturer else "LS"
        return (f"1 {ls_mfr} {model or 'GMC-25'} contactor {ia} "
                f"(bright royal blue rectangular body ~4 modules wide, 3 silver screw terminals "
                f"labeled L1 L2 L3 on top, coil connections A1/A2 on sides, "
                f"3 output terminals T1/T2/T3 at bottom, '{ls_mfr}' white logo on body, "
                f"'{ia}' rating label)")

    elif "RELAY" in t:
        return (f"1 {dev.manufacturer or 'LS'} {model or 'MT-32'} thermal overload relay "
                f"(orange-brown compact body, circular rotary adjustment dial on front, "
                f"TEST and RESET buttons, contact terminals '95 NC 96' '97 NO 98', "
                f"'{dev.manufacturer or 'LS'}' logo, '{model or 'MT-32'}' text)")

    elif "TIMER" in t:
        return (f"1 Schneider {model or 'RE17RAMU'} timer relay "
                f"(white plastic DIN mount body 2 modules wide, large round analog dial "
                f"with time scale markings, black rotary knob, Schneider logo, LED indicator)")

    elif "METER" in t:
        return (f"1 Schneider {model or 'PM5560'} digital power meter "
                f"(black square panel meter, large LCD/LED display showing '{ia}' in "
                f"bright blue 7-segment digits with glow, 4 navigation buttons below, "
                f"Schneider Electric branding)")

    elif "BUSBAR" in t:
        return (f"copper busbar system spanning full bay width "
                f"(3 horizontal copper bars L1/L2/L3 in red/yellow/blue, "
                f"neutral N bar grey, mounted on white insulators, tap holes visible)")

    else:
        return f"1 {dev.device_type} {ia} ({mfr} DIN rail device)"


def generate_prompt(spec: CabinetSpec, style: str = "full_app") -> str:
    """Tạo prompt cực chi tiết cho AI image generation."""

    total_used = sum(b.used_modules for b in spec.bays)
    total_slots = sum(b.rows_needed * b.row_modules for b in spec.bays)
    pct = int(total_used / total_slots * 100) if total_slots else 0

    # ── Bay descriptions ────────────────────────────────────────────────
    bay_descs = []
    for bay in spec.bays:
        dev_list = []
        cur_slot = 0
        for dev in bay.devices:
            desc = describe_device(dev)
            slot_range = f"slots {cur_slot+1:02d}–{cur_slot+dev.modules:02d}"
            dev_list.append(f"    - {desc} [{slot_range}]")
            cur_slot += dev.modules

        # empty slots
        if cur_slot < bay.row_modules:
            remaining = bay.row_modules - cur_slot
            dev_list.append(f"    - {remaining} empty grey DIN module slots "
                            f"[slots {cur_slot+1:02d}–{bay.row_modules:02d}]")

        devices_text = "\n".join(dev_list)
        nums = " ".join(f"{i:02d}" for i in range(1, bay.row_modules + 1))
        bay_descs.append(
            f"  BAY '{bay.name}' label='{bay.label}' (green text on dark label column left):\n"
            f"{devices_text}\n"
            f"    - Silver DIN rail strip at bottom of row\n"
            f"    - Small slot numbers below rail: {nums}"
        )

    bays_text = "\n\n".join(bay_descs)

    prompt = f"""Photorealistic screenshot of a professional Windows desktop application "CẤU HÌNH TỦ ĐIỆN" (Electrical Cabinet Configuration), dark theme, 16:9 aspect ratio, ultra-high quality, 4K resolution.

== APPLICATION WINDOW ==
- Title bar (very dark charcoal #1A1B21): icon 'DB' in blue square, title "CẤU HÌNH TỦ ĐIỆN" white bold text. Window control buttons top-right (minimize, maximize, close). Top-right: "Giao diện: Tối" dropdown.
- Menu bar below title (dark #1C1E24): items "Tệp  Dự án  Cấu hình  Công cụ  Trợ giúp" grey text.
- Toolbar (dark #202228): buttons "Mới  Mở  Lưu  Lưu như | [Xuất PDF red] [Xuất Excel green] In | Hoàn tác  Làm lại | Kiểm tra  Báo cáo" — all as dark-background small buttons with text.

== LEFT SIDEBAR (160px wide, very dark #16181E) ==
- Header "DỰ ÁN" cyan text.
- Project info fields (label grey, value white):
  Tên dự án: {spec.project or 'TD-3P-100A'}
  Mã tủ điện: {spec.name}
  Điện áp: {spec.voltage}
  Tần số: 50 Hz
  Dòng định mức: {int(spec.in_rated)} A
  Ngày tạo: {spec.created}
  Ghi chú: {(spec.note or '')[:30]}
- Navigation menu items (dark rounded rows):
  □ Tổng quan
  ■ Cấu hình tủ điện ← ACTIVE (left blue indicator bar, slightly lighter bg)
  □ Sơ đồ một sợi
  □ Báo cáo vật tư
  □ Danh mục thiết bị
- Bottom: Schneider Electric logo (green rectangle, white text "Schneider Electric").

== MAIN CABINET VIEW (fills right portion) ==
The cabinet is a large dark charcoal grey metal enclosure (#2A2C30 outer, #343638 face):
- Left edge: two metal hinges visible.
- Right edge: circular door lock.
- Contains {len(spec.bays)} horizontal bays/shelves stacked vertically.
- Each bay has a dark left label column showing bay name in bright green text.

{bays_text}

The devices inside look like REAL PHYSICAL PRODUCTS mounted on real DIN rails:
- MCBs are white/off-white Schneider Easy9 style with green brand strip
- MCCB is large black moulded case Schneider EasyPact CVS100F
- Contactors are bright blue LS GMC-25
- Relays are orange-brown LS MT-32
- Timer is white with round dial
- Meter has glowing blue digital display "100.0 A"
All devices appear as realistic photographs, not illustrations.

== FOOTER (very dark strip at bottom) ==
"Mức độ sử dụng thanh DIN:" label, then a segmented green progress bar filled to {pct}%, then "{total_used} / {total_slots} module ({pct}%)" text.
Left: "Trạng thái: ● Đã lưu" (green dot).
Right: "Phiên bản: 1.0.0.0" dim text.

Style: professional Windows dark theme engineering software, photorealistic, ultra-sharp, looks exactly like a real application screenshot not a mockup."""

    return prompt


def main():
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument("--input", "-i", default=None)
    p.add_argument("--demo", action="store_true")
    p.add_argument("--output", "-o", default="prompt.txt")
    args = p.parse_args()

    if args.demo or not args.input:
        # Use built-in demo
        spec = CabinetSpec("TD-01","TD-3P-100A","Tủ điện phân phối và điều khiển",
                           "400 VAC", 100, "24/05/2025", "Tủ điện phân phối và điều khiển")
        b1 = Bay("NGĂN 1","Nguồn vào",row_modules=12)
        b1.devices=[
            Device("MCCB tổng","MCCB","EasyPact CVS100F",100,3,1,"Schneider",4),
            Device("MCB 1P","MCB 1P","Easy9",32,1,1,"Schneider",1),
            Device("MCB 1P","MCB 1P","Easy9",32,1,1,"Schneider",1),
        ]
        b2 = Bay("NGĂN 2","Phân phối",row_modules=24)
        b2.devices=(
            [Device("MCB 10A","MCB 1P","Easy9",10,1,1,"Schneider",1) for _ in range(5)]+
            [Device("MCB 16A","MCB 1P","Easy9",16,1,1,"Schneider",1) for _ in range(4)]+
            [Device("MCB 20A","MCB 1P","Easy9",20,1,1,"Schneider",1) for _ in range(4)]+
            [Device("MCB 3P 16A","MCB 3P","iC60N",16,3,1,"Schneider",3) for _ in range(2)]+
            [Device("MCB 3P 32A","MCB 3P","iC60N",32,3,1,"Schneider",3)]
        )
        b3 = Bay("NGĂN 3","Điều khiển",row_modules=24)
        b3.devices=[
            *[Device("Contactor","CONTACTOR","GMC-25",25,3,1,"LS",4) for _ in range(2)],
            *[Device("Relay","RELAY","MT-32",25,3,1,"LS",3) for _ in range(2)],
            Device("Timer","TIMER","RE17RAMU",0,0,1,"Schneider",4),
            Device("Meter","METER","PM5560",100,3,1,"Schneider",6),
        ]
        spec.bays=[b1,b2,b3]
    elif args.input.endswith(".json"):
        spec = parse_json(args.input)
    else:
        spec = parse_csv(args.input)

    prompt = generate_prompt(spec)

    if args.output == "-":
        print(prompt)
    else:
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(prompt)
        print(f"Prompt saved: {args.output} ({len(prompt)} chars)")
        print("\n--- PREVIEW (first 300 chars) ---")
        print(prompt[:300] + "...")


if __name__ == "__main__":
    main()
