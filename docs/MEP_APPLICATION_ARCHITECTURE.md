# MEP DRAWING TOOL — Kiến trúc ứng dụng

Plugin được tổ chức như **ứng dụng MEP nhỏ** trong AutoCAD, gồm 5 module:

```
┌─────────────────────────────────────────────────────────┐
│              MEP DRAWING TOOL (Palette)                  │
├─────────────┬─────────────┬──────────────┬──────────────┤
│ Chọn hệ     │ Ống AMC     │ Tiêu chuẩn   │ Tủ điện/DB   │
│ thống       │ phụ kiện    │ & Tính toán  │              │
├─────────────┴─────────────┴──────────────┴──────────────┤
│ MepPanel.Plugin.dll          → Loader, lệnh MEPDB       │
│ MepPanel.Blocks.AutoCAD.dll  → UI, vẽ, thư viện, tính   │
│ MepPanel.Core.dll            → Tiêu chuẩn, công thức    │
│ MepPanel.AutoCAD.Licensing   → License (tùy chọn)       │
└─────────────────────────────────────────────────────────┘
```

## Module source

| Module | Thư mục | Mô tả |
|--------|---------|--------|
| Tiêu chuẩn | `src/MepPanel.Core/Standards/` | TCVN, QCVN, NFPA, ASHRAE |
| Tính toán | `src/MepPanel.Core/Calculations/` | Công thức có unit test |
| Thư viện ống | `Drawing/MepPipeLibraryService.cs` | AMC template |
| Kiến thức UI | `Drawing/MepKnowledgeService.cs` | Hiển thị TC + calculator |
| Manifest | `bundle/.../mep-application.json` | Mô tả bộ tool |

## Lệnh AutoCAD

| Lệnh | Module |
|------|--------|
| `MEPDB` | Mở toàn bộ ứng dụng |
| `MEPSTATUS` | Trạng thái bundle |

## Tài liệu liên quan

- `docs/MEP_STANDARDS_REFERENCE.md` — bảng tiêu chuẩn đầy đủ
- `docs/PIPE_LIBRARY_AMC.md` — thư viện ống
- `docs/COMMERCIAL_ROADMAP.md` — hướng thương mại
- `docs/BUILD_PLUGIN_CAD.md` — build & cài

## Versioning

- **0.14** — Build từ repo, dev mode
- **0.15** — Tiêu chuẩn + tính toán 4 hệ
- **1.0** (mục tiêu) — WPF đầy đủ + cloud sync
