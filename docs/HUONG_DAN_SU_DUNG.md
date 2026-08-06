# HƯỚNG DẪN CHẠY CHƯƠNG TRÌNH — MEP Drawing Tool v0.16

---

## Tổng quan — 3 thành phần

```
┌──────────────────────────────────────────────────────┐
│  1. Plugin AutoCAD    │  2. Render tủ điện  │  3. Server│
│  (Windows + CAD 2021) │  (Python, mọi OS)   │  (tùy chọn)│
└──────────────────────────────────────────────────────┘
```

---

# PHẦN 1 — PLUGIN AUTOCAD

## Yêu cầu cài đặt

| Thứ | Cần cài | Tải về |
|-----|---------|--------|
| Windows 64-bit | Bắt buộc | — |
| AutoCAD 2021 | Bắt buộc | autodesk.com |
| .NET SDK 8.0 | Bắt buộc | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Git | Bắt buộc | [git-scm.com](https://git-scm.com) |

---

## Bước 1 — Lấy code

```powershell
# Mở PowerShell (Windows), chọn thư mục muốn lưu
cd C:\Users\DU_COMPUTER\Desktop

# Clone repo
git clone https://github.com/du10031992/du-cursor.git
cd du-cursor

# Chuyển sang nhánh plugin
git checkout cursor/plugin-features-cc24
```

---

## Bước 2 — Build và cài plugin

```powershell
# ⚠️ ĐÓNG AUTOCAD TRƯỚC khi chạy

.\scripts\build-plugin-from-repo.ps1
```

Script tự động:
- Build 4 DLL từ `src/`
- Copy vào `bundle\MepPanel.Plugin.bundle\Contents\`
- Cài vào `C:\ProgramData\Autodesk\ApplicationPlugins\`

✅ **Khi thấy:** `Done! Restart AutoCAD - plugin loads automatically.`

---

## Bước 3 — Khởi động AutoCAD

1. Mở **AutoCAD 2021**
2. Tạo hoặc mở bản vẽ bất kỳ
3. Gõ lệnh: **`MEPDB`** → Enter

→ Panel **MEP DRAWING TOOL** mở bên cạnh bản vẽ

---

## Các lệnh AutoCAD

| Lệnh | Tác dụng |
|------|----------|
| `MEPDB` | Mở panel MEP DRAWING TOOL |
| `MEPSTATUS` | Kiểm tra plugin có load không |
| `MEPLOGIN` | Đăng nhập (khi tắt dev mode) |
| `MEPLOGOUT` | Đăng xuất |

---

## Sử dụng panel MEP DRAWING TOOL

### Nhóm 1 — Hệ điện: Máng & Trunking
| Nút | Làm gì | Thao tác |
|-----|--------|---------|
| Vẽ máng cáp | Vẽ đường máng 2 biên | Click điểm 1 → 2 → ... → Enter |
| Vẽ trunking | Vẽ ống luồn dây | Như trên |
| Co 90° máng | Đặt phụ kiện tại góc | Click vị trí → click hướng |

### Nhóm 2 — HVAC
| Nút | Làm gì |
|-----|--------|
| Ống gió thông minh | Click nhiều điểm → Enter; tự đặt Co 45°/90°, Reducer |
| Ống gió mềm (flex) | Click điểm đầu → cuối |
| Reducer thủ công | Nhập W lớn/nhỏ → click vị trí |

### Nhóm 3 — Ống & phụ kiện (AMC)
| Nút | Làm gì |
|-----|--------|
| **Nạp thư viện AMC** | Import block từ `AMC_TEMPLATE_RV29.dwg` ← **Chạy trước** |
| Vẽ ống nước tự động | Chọn DN → click điểm → Enter; tự đặt Co, Tee, Nút bít |
| Vẽ ống PCCC tự động | Như ống nước, thêm Sprinkler/Chữa cháy |
| Đặt phụ kiện (thủ công) | Chọn loại → click vị trí → click hướng |

### Nhóm 4 — Tiêu chuẩn & Tính toán
| Nút | Làm gì |
|-----|--------|
| TC + CT — Hệ điện | Xem TCVN 9207, IEC 60364 + công thức |
| Tính toán — Hệ điện | Nhập P, U, cosφ → tính dòng, sụt áp trên command line |
| TC + CT / Tính toán (nước/PCCC/HVAC) | Tương tự cho từng hệ |

### Nhóm 5 — Tủ điện / DB
| Nút | Làm gì |
|-----|--------|
| Vẽ tủ điện | Click vị trí đặt block |
| Mặt chiếu tủ | Click 2 góc → vẽ hình chữ nhật 3 ngăn |
| **Render bố trí tủ → PNG** | Chọn file CSV/JSON → xuất ảnh tủ điện |

---

## Lưu ý quan trọng

```
✅ devMode = true  → KHÔNG cần License Server
   File: bundle\MepPanel.Plugin.bundle\Contents\MepPanel.config.json
   {
     "devMode": true,
     "pipeLibraryDwg": "samples/templates/AMC_TEMPLATE_RV29.dwg"
   }
```

---

# PHẦN 2 — RENDER TỦ ĐIỆN (Python)

## Yêu cầu

```powershell
# Cài Python 3.8+ nếu chưa có: python.org
# Kiểm tra
python --version

# Cài thư viện
pip install pillow
```

---

## Cách 1 — Render từ CSV (đơn giản nhất)

**Tạo file CSV** (ví dụ `C:\tu_dien.csv`):
```csv
bay,ten_thiet_bi,device_type,in_a,poles,qty,manufacturer
NGĂN 1,MCCB tổng,MCCB,100,3,1,Schneider
NGĂN 1,MCB dự phòng,MCB 1P,32,1,2,Schneider
NGĂN 2,MCB đèn,MCB 1P,10,1,4,Schneider
NGĂN 2,MCB ổ cắm,MCB 1P,16,1,4,Schneider
NGĂN 2,MCB điều hòa,MCB 1P,20,1,3,Schneider
NGĂN 2,MCB 3P 16A,MCB 3P,16,3,2,Schneider
NGĂN 3,Contactor bơm,CONTACTOR,25,3,2,LS
NGĂN 3,Relay nhiệt,RELAY,25,3,2,LS
NGĂN 3,Timer,TIMER,0,0,1,Schneider
NGĂN 3,Đồng hồ ampe,METER,100,3,1,Schneider
```

**Chạy render:**
```powershell
cd C:\Users\DU_COMPUTER\Desktop\du-cursor

python scripts\render_cabinet_v4.py --input C:\tu_dien.csv --output C:\tu_dien.png

# Xem kết quả
start C:\tu_dien.png
```

---

## Cách 2 — Render kết hợp PIL + AI (đẹp nhất)

```powershell
# Bước 1: Render PIL layout
python scripts\render_cabinet_v4.py --input samples\cabinet\TD-01_full.json --output base.png

# Bước 2: Tạo AI prompt
python scripts\generate_cabinet_prompt.py --input samples\cabinet\TD-01_full.json --output prompt.txt

# Bước 3: Dùng prompt.txt với AI image generator yêu thích
# → Kết quả photorealistic như ảnh demo
```

---

## Cách 3 — Demo ngay không cần file

```powershell
python scripts\render_cabinet_v4.py --demo --output demo_cabinet.png
start demo_cabinet.png
```

---

## Các loại thiết bị hỗ trợ

| `device_type` | Thiết bị | Ảnh |
|--------------|----------|-----|
| `MCCB` | Schneider EasyPact CVS100F | ✅ |
| `MCB 1P` | Schneider Easy9 1 cực | ✅ |
| `MCB 3P` | Schneider Easy9 3 cực | ✅ |
| `CONTACTOR` | LS GMC-25 | ✅ |
| `RELAY` | LS MT-32 | ✅ |
| `TIMER` | Schneider RE17RAMU | ✅ |
| `METER` | Schneider PM5560 | ✅ |
| `BUSBAR` | Thanh cái đồng 3P+N | ✅ |
| `BUSBAR_COMB` | Comb busbar | ✅ |

---

# PHẦN 3 — LICENSE SERVER (tùy chọn)

> Bỏ qua phần này nếu `devMode: true` trong config.

## Chạy từ source (Visual Studio)

```
1. Mở MepPanelMvp.sln
2. Set startup project: MepPanel.LicenseServer
3. Nhấn F5
4. Trình duyệt mở: https://localhost:7024/admin/
```

## Chạy từ file build

```powershell
cd C:\Users\DU_COMPUTER\Desktop\du-cursor\dist
# Giải nén MEP-Drawing-Tool-v0.16.0.zip
cd MEP-Drawing-Tool-v0.16.0\3_license_server
.\START_SERVER.ps1

# Hoặc Linux/VPS:
bash START_SERVER.sh
```

## Đăng nhập Admin

```
URL: http://localhost:5268/admin/
     hoặc https://localhost:7024/admin/

Admin Key: MEP-PANEL-ADMIN-TEST-2026
SĐT test:  0900000001
OTP test:  123456
```

---

# XỬ LÝ LỖI PHỔ BIẾN

| Lỗi | Nguyên nhân | Giải pháp |
|-----|-------------|-----------|
| `AcCoreMgd.dll not found` | AutoCAD chưa cài | Cài AutoCAD 2021 |
| `Access denied` khi build | AutoCAD đang mở | **Đóng AutoCAD** trước |
| Lệnh `MEPDB` không nhận | Plugin chưa load | Gõ `MEPSTATUS`; nếu lỗi: NETLOAD thủ công |
| `Pillow not found` | Chưa cài Python lib | `pip install pillow` |
| Panel không mở | DLL thiếu | Chạy lại `build-plugin-from-repo.ps1` |
| Lệnh build lỗi | .NET SDK thiếu | Cài [.NET 8 SDK](https://dotnet.microsoft.com/download) |

### NETLOAD thủ công (khi plugin không tự load)

```autocad
; Trong AutoCAD command line:
NETLOAD
; Chọn file: C:\ProgramData\Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle\Contents\MepPanel.Plugin.dll
```

---

# NÂNG CẤP PHIÊN BẢN

```powershell
cd C:\Users\DU_COMPUTER\Desktop\du-cursor

# Kéo code mới
git pull origin cursor/plugin-features-cc24

# Đóng AutoCAD → build lại
.\scripts\build-plugin-from-repo.ps1

# Khởi động lại AutoCAD
```

---

# LIÊN KẾT

| Tài liệu | Đường dẫn |
|----------|-----------|
| Tổng hợp dự án | `docs/PROJECT_SUMMARY.md` |
| Build chi tiết | `docs/BUILD_PLUGIN_CAD.md` |
| Tiêu chuẩn kỹ thuật | `docs/MEP_STANDARDS_REFERENCE.md` |
| Thư viện ống AMC | `docs/PIPE_LIBRARY_AMC.md` |
| Lộ trình thương mại | `docs/COMMERCIAL_ROADMAP.md` |
| Repo GitHub | [cursor/plugin-features-cc24](https://github.com/du10031992/du-cursor/pull/2) |
