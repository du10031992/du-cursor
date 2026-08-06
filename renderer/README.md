# MEP Cabinet Renderer — Blender 3D + Pillow fallback

Renderer tủ điện cho plugin AutoCAD. **Mặc định dùng Blender Cycles** (3D photorealistic).

## Chế độ render

| `--quality` | Công cụ | Thời gian | Chất lượng |
|-------------|---------|-----------|------------|
| **`blender`** (mặc định) | Blender Cycles / V-Ray | 1–5 phút | Cao nhất — 3D thật |
| `photoreal` | Pillow + AI shell | vài giây | Tốt — composite 2D |
| `standard` | Pillow phẳng | vài giây | Nhanh — layout kỹ thuật |

## Cài Blender (Windows)

1. Tải Blender: https://www.blender.org/download/
2. Kiểm tra:
   ```powershell
   .\scripts\install-blender-render.ps1
   ```
3. Cài assets vào plugin:
   ```powershell
   .\scripts\install-renderer-devices.ps1
   ```

### V-Ray (tùy chọn)

V-Ray for Blender cần **license riêng**: https://www.chaos.com/vray/blender

```powershell
py renderer/render_cabinet.py --demo --quality blender --engine vray --samples 512 --output cabinet_vray.png
```

Nếu chưa cài V-Ray addon → tự fallback **Cycles**.

## Demo

```bash
# Blender Cycles 3D (256 samples, ~2-4 phut)
python3 render_cabinet.py --demo --quality blender --samples 256 --output cabinet.png

# Nhanh hon (128 samples)
python3 render_cabinet.py --demo --quality blender --samples 128 --output cabinet.png

# Fallback Pillow (khong can Blender)
python3 render_cabinet.py --demo --quality photoreal --output cabinet_flat.png
```

## Pipeline Blender

```
CSV/JSON → export_blender_layout() → layout.json
         → blender --background --python blender/build_scene.py
         → Cycles/V-Ray render → PNG 1920×1280
```

Scene 3D gồm:
- Vỏ tủ + cửa mở 48°
- Thanh DIN + máng dây
- Thiết bị texture từ `devices/*.png`
- Đèn báo pha L1/L2/L3 phát sáng (emission)
- Studio lighting + Filmic color grade

## Ảnh thiết bị AI (`devices/`)

| File | Loại |
|------|------|
| `device_mccb_schneider.png` | MCCB tổng |
| `device_mcb1p_schneider.png` | MCB 1P |
| `device_mcb3p_schneider.png` | MCB 3P |
| `device_contactor_ls.png` | Contactor |
| `device_pilot_3phase.png` | Đèn báo pha |
| `cabinet_shell_template.png` | Vỏ tủ AI (Pillow photoreal) |

## Plugin AutoCAD

Sau `install-renderer-devices.ps1`, bundle chứa:
```
Contents/
  render_cabinet.py
  render_blender.py
  render_cabinet_interior.py
  render_photoreal.py
  blender/build_scene.py
  devices/*.png
```

Trong AutoCAD: **Render tủ điện** → gọi `--quality blender` (cần Blender trong PATH).

## Format CSV

```csv
bay,ten_thiet_bi,device_type,in_a,poles,qty,manufacturer
NGĂN 1,MCCB tổng,MCCB,100,3,1,Schneider
NGĂN 2,MCB đèn,MCB 1P,10,1,4,Schneider
```
