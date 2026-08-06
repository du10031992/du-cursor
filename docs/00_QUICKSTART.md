# QUICKSTART — MEP Drawing Tool v0.16

## Yêu cầu

| Thành phần | Phiên bản | Ghi chú |
|-----------|-----------|---------|
| Windows | 64-bit | Plugin chạy trên Windows |
| AutoCAD | 2021 | `C:\Program Files\Autodesk\AutoCAD 2021` |
| .NET SDK | 4.8 + 8.0 | Visual Studio Build Tools |
| Python | 3.8+ | Cho renderer tủ điện |
| Pillow | latest | `pip install pillow` |

---

## 1. Cài plugin vào AutoCAD

```powershell
# Đóng AutoCAD trước
cd C:\Users\...\MEP-Drawing-Tool
.\scripts\build-plugin-from-repo.ps1

# Khởi động lại AutoCAD
# Gõ MEPDB → panel MEP DRAWING TOOL mở
```

**Config (không cần License Server):**
`bundle\MepPanel.Plugin.bundle\Contents\MepPanel.config.json`
```json
{ "devMode": true }
```

---

## 2. Render bố trí tủ điện → PNG

```powershell
pip install pillow

# Render từ file CSV khối lượng
python3 scripts\render_cabinet_v3.py --input samples\cabinet\TD-01_full.csv --output cabinet.png

# Hoặc JSON đầy đủ
python3 scripts\render_cabinet_v3.py --input samples\cabinet\TD-01_full.json --output cabinet.png

# Demo ngay
python3 scripts\render_cabinet_v3.py --demo --output demo.png
```

**Format CSV tối thiểu:**
```csv
bay,ten_thiet_bi,device_type,in_a,poles,qty,manufacturer
NGĂN 1,MCCB tổng,MCCB,100,3,1,Schneider
NGĂN 2,MCB đèn,MCB 1P,10,1,4,Schneider
NGĂN 2,MCB 3P,MCB 3P,16,3,2,LS
NGĂN 3,Contactor,CONTACTOR,25,3,2,LS
NGĂN 3,Đồng hồ,METER,100,3,1,Schneider
```

---

## 3. Khởi chạy License Server (tùy chọn)

```powershell
cd LicenseServer
dotnet MepPanel.LicenseServer.dll --urls "https://localhost:7024"

# Hoặc từ source trong Visual Studio: F5 MepPanel.LicenseServer
```

Admin: `https://localhost:7024/admin/`
- Admin key: `MEP-PANEL-ADMIN-TEST-2026`
- OTP test: `123456`
- SĐT test: `0900000001`

---

## 4. Lệnh AutoCAD

| Lệnh | Chức năng |
|------|-----------|
| `MEPDB` | Mở panel MEP DRAWING TOOL |
| `MEPSTATUS` | Kiểm tra bundle + license |
| `MEPLOGIN` | Đăng nhập OTP (khi devMode=false) |
| `MEPLOGOUT` | Đăng xuất |

---

## 5. Nâng cấp lên phiên bản mới

```powershell
# Kéo code mới
git pull origin cursor/plugin-features-cc24

# Build lại
.\scripts\build-plugin-from-repo.ps1

# Khởi động lại AutoCAD
```

---

Tài liệu chi tiết: `docs/PROJECT_SUMMARY.md`
