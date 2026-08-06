# MEP Cabinet Renderer — ảnh thiết bị AI photorealistic

Renderer ghép **ảnh PNG thiết bị thật** (AI-generated) lên layout tủ DIN trong plugin.

## Cài đặt

```bash
pip install -r requirements.txt
```

## Demo nhanh

```bash
python3 render_cabinet.py --demo --output cabinet.png
```

## Ảnh thiết bị AI (`devices/`)

| File | Loại thiết bị |
|------|----------------|
| `device_mccb_schneider.png` | MCCB tổng (Aptomat) |
| `device_mcb1p_schneider.png` | MCB 1P |
| `device_mcb3p_schneider.png` | MCB 3P |
| `device_contactor_ls.png` | Contactor |
| `device_relay_ls.png` | Relay nhiệt |
| `device_timer_schneider.png` | Timer |
| `device_meter_pm5560.png` | Đồng hồ đo |
| `cabinet-render-reference.png` | Tham chiếu tủ mở (full panel) |

Ảnh được tạo bằng AI theo phong cách **catalog Schneider / tủ điện thực tế** (nền trắng, dễ tách nền tự động).

## Dùng trong plugin AutoCAD

1. Copy thư mục `devices/` cạnh `render_cabinet.py` trong bundle:
   ```
   MepPanel.Plugin.bundle\Contents\devices\
   ```
2. Trong AutoCAD: **Render tủ** → chọn Demo / Từ bản vẽ / Từ file CSV.

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
