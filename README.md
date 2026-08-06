# MEP DRAWING TOOL v0.16

Plugin AutoCAD 2021 MEP toàn diện: vẽ kỹ thuật, tính toán, tiêu chuẩn, render tủ điện, cấp phép.

| Module | Mô tả |
|--------|-------|
| **Plugin AutoCAD** | Panel MEP DRAWING TOOL — vẽ điện, HVAC, nước, PCCC |
| **Python Renderer** | Render bố trí tủ điện → PNG photorealistic |
| **License Server** | Kiểm soát bản quyền per SĐT/máy/feature |

## Quick Start (Windows + AutoCAD 2021)

```powershell
# 1. Build & cài plugin
.\scripts\build-plugin-from-repo.ps1

# 2. Render tủ điện từ dữ liệu
pip install pillow
python3 scripts\render_cabinet_v3.py --input samples\cabinet\TD-01_full.csv --output cabinet.png
```

Trong AutoCAD: gõ **`MEPDB`** → panel MEP DRAWING TOOL.

## Xem tài liệu đầy đủ

- `docs/00_QUICKSTART.md` — Bắt đầu nhanh
- `docs/PROJECT_SUMMARY.md` — Tổng hợp toàn bộ dự án
- `docs/BUILD_PLUGIN_CAD.md` — Hướng dẫn build chi tiết
- `docs/COMMERCIAL_ROADMAP.md` — Lộ trình thương mại
