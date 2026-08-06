# MEP Cabinet Renderer — ảnh thiết bị AI photorealistic

Renderer ghép **ảnh PNG thiết bị thật** (AI-generated) lên layout tủ DIN trong plugin.

## Công cụ tạo ảnh tham chiếu (như ảnh bạn gửi)

Ảnh tủ điện photorealistic kiểu catalog thường được tạo bằng một trong các cách:

| Công cụ | Vai trò |
|---------|---------|
| **AI image generation** (Midjourney, DALL·E, Cursor Generate) | Vỏ tủ mở, ánh sáng studio, đèn báo pha phát sáng |
| **Blender + V-Ray/Octane** | Render 3D PBR đầy đủ (GI, AO, emissive) |
| **Plugin (pipeline này)** | AI assets + **Python/Pillow composite** — không cần GPU 3D |

Plugin **không chạy Blender/V-Ray** trong AutoCAD. Thay vào đó:

1. **Ảnh AI** — vỏ tủ (`cabinet_shell_template.png`) + từng thiết bị (`device_*.png`)
2. **Layout DIN** — xếp thiết bị theo dữ liệu CSV/JSON
3. **Post-processing** — bóng đổ, glow đèn L1/L2/L3, dây trong máng, AO, vignette

## Cài đặt

```bash
pip install -r requirements.txt
```

## Demo nhanh

```bash
# Photoreal (mặc định) — giống ảnh catalog 3D
python3 render_cabinet.py --demo --quality photoreal --output cabinet.png

# Phẳng, nhanh — không ghép vỏ AI
python3 render_cabinet.py --demo --quality standard --output cabinet_flat.png
```

Chế độ mặc định: `--mode interior` + `--quality photoreal`.

## Ảnh thiết bị AI (`devices/`)

| File | Loại thiết bị |
|------|----------------|
| `cabinet_shell_template.png` | **Vỏ tủ trống** (nền photoreal cho `--quality photoreal`) |
| `device_mccb_schneider.png` | MCCB tổng (Aptomat) |
| `device_mcb1p_schneider.png` | MCB 1P |
| `device_mcb3p_schneider.png` | MCB 3P |
| `device_contactor_ls.png` | Contactor |
| `device_relay_ls.png` | Relay nhiệt |
| `device_timer_schneider.png` | Timer |
| `device_meter_pm5560.png` | Đồng hồ đo |
| `device_pilot_3phase.png` | **Đèn báo pha 3P** (L1/L2/L3 đỏ/vàng/xanh) |
| `device_spd.png` | Chống sét SPD |
| `cabinet-render-reference.png` | Tham chiếu tủ mở (full panel) |

Ảnh được tạo bằng AI theo phong cách **catalog Schneider / tủ điện thực tế**.

## Dùng trong plugin AutoCAD

```powershell
.\scripts\install-renderer-devices.ps1
```

Copy `devices/` + `render_cabinet.py` + `render_cabinet_interior.py` + `render_photoreal.py` vào bundle.

Trong AutoCAD: **Render tủ điện** → script gọi `--mode interior --quality photoreal`.

## Thêm loại thiết bị mới

1. Tạo PNG nền trắng, góc chính diện (AI prompt mẫu):

   > Photorealistic product photo of [thiết bị], front view, pure white background, DIN rail electrical component, studio catalog photography, ultra realistic, isolated cutout, no text

2. Đặt vào `devices/` và thêm mapping trong `render_cabinet.py` → `DEVICE_PNG_MAP`.

## Format CSV

```csv
bay,ten_thiet_bi,device_type,in_a,poles,qty,manufacturer
NGĂN 1,MCCB tổng,MCCB,100,3,1,Schneider
NGĂN 2,MCB đèn,MCB 1P,10,1,4,Schneider
```
