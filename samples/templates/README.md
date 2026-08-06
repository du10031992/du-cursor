# AMC_TEMPLATE_RV29.dwg

Template bản vẽ mẫu (AMC / RV29) dùng để test plugin MepPanel trên AutoCAD.

## Thông tin file

| Thuộc tính | Giá trị |
|------------|---------|
| Tên | `AMC_TEMPLATE_RV29.dwg` |
| Định dạng | DWG (AC1021 — AutoCAD 2007–2009) |
| Kích thước | ~6.1 MB |
| Mở bằng | AutoCAD 2021 (tương thích ngược) |

## Cách dùng với plugin

1. Build & cài plugin (xem `docs/BUILD_PLUGIN_CAD.md`):

   ```powershell
   .\scripts\build-plugin-from-repo.ps1
   ```

2. Mở AutoCAD 2021 → **File → Open** → chọn `samples\templates\AMC_TEMPLATE_RV29.dwg`

3. Gõ lệnh **`MEPDB`** → panel **MEP DRAWING TOOL** mở bên phải

4. Thử các chức năng vẽ — plugin tạo layer tự động:

   | Layer | Chức năng |
   |-------|-----------|
   | `MEP_HVAC` | Điều hòa / ống gió |
   | `MEP_WATER` | Hệ nước |
   | `MEP_FIRE` | Báo cháy |
   | `MEPDB` / `MEPDB_CABINET` | Tủ điện / DB |

5. Kiểm tra trạng thái: **`MEPSTATUS`**

## Dev mode (không cần license server)

Trong `MepPanel.config.json`:

```json
{ "devMode": true }
```

## Ghi chú

- File template giữ nguyên layer/block gốc của dự án AMC — plugin vẽ thêm trên layer `MEP_*` riêng, không ghi đè template.
- Nếu cần chuẩn hóa layer template cho MEP, mở bản vẽ và gửi danh sách layer (lệnh AutoCAD `LAYER`) để map vào plugin.
