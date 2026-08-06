# MEP DRAWING TOOL — Tổng hợp dự án

> Bộ công cụ MEP (Mechanical Electrical Plumbing) tích hợp AutoCAD 2021, gồm: plugin vẽ kỹ thuật, tính toán kỹ thuật, thư viện ống AMC, render tủ điện, và hệ thống cấp phép theo máy/SĐT.

---

## 1. Tổng quan dự án

| Hạng mục | Chi tiết |
|----------|----------|
| **Tên sản phẩm** | MEP DRAWING TOOL |
| **Phiên bản hiện tại** | v0.16 (dev) |
| **Nền tảng** | AutoCAD 2021 · Windows 64-bit · .NET Framework 4.8 |
| **Ngôn ngữ** | C# (plugin), Python (render), ASP.NET Core (server) |
| **Repo** | `cursor/plugin-features-cc24` |
| **Tests** | 24/24 pass |

---

## 2. Kiến trúc hệ thống

```
┌──────────────────────────────────────────────────────────────────┐
│                    MEP DRAWING TOOL                               │
│                                                                   │
│  ┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐ │
│  │  AutoCAD Plugin │  │  Python Renderer │  │  License Server │ │
│  │  (C# .NET 4.8)  │  │  (PIL/Pillow)    │  │  (ASP.NET Core) │ │
│  └────────┬────────┘  └────────┬─────────┘  └────────┬────────┘ │
│           │                    │                       │          │
│  ┌────────▼──────────────────────────────────────────▼────────┐ │
│  │              MepPanel.Core (.NET Standard 2.0)              │ │
│  │    Standards · Calculations · PluginFeatures · Models       │ │
│  └─────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

---

## 3. Chức năng đã xây dựng

### 3.1 Panel AutoCAD — MEP DRAWING TOOL (lệnh `MEPDB`)

#### Nhóm A: Hệ điện — Máng & Trunking
| Nút | Chức năng | Layer |
|-----|-----------|-------|
| Vẽ máng cáp | Đa điểm, co 90° tự động | `MEP_CABLE_TRAY` |
| Vẽ trunking | Ống luồn dây nhỏ | `MEP_TRUNKING` |
| Co 90° máng/trunking | Đặt phụ kiện thủ công | — |

#### Nhóm B: HVAC — Ống gió nâng cao
| Nút | Chức năng |
|-----|-----------|
| Ống gió thông minh | Đa điểm · co 45°/90° tự động · đổi size → reducer |
| Ống gió mềm (flex) | Đường cong sóng · đầu nối 2 đầu |
| Reducer thủ công | Chọn size lớn → nhỏ tại 1 điểm |
| Ống gió cơ bản (L) | Chế độ cũ: 2 điểm + elbow |

#### Nhóm C: Chọn hệ thống
| Nút | Layer vẽ |
|-----|---------|
| Hệ điện | `MEPDB` / `MEPDB_CABINET` |
| Điều hòa | `MEP_HVAC` |
| Hệ nước | `MEP_WATER` |
| Báo cháy / PCCC | `MEP_FIRE` |

#### Nhóm D: Ống & phụ kiện (Thư viện AMC)
| Nút | Chức năng |
|-----|-----------|
| Nạp thư viện AMC | Import block từ `AMC_TEMPLATE_RV29.dwg` |
| Vẽ ống nước + co | Đa điểm · co 90° tại góc |
| Đặt phụ kiện nước | Co90 · Tee · Giảm · Van · Nút bít |
| Vẽ ống PCCC + co | Giống nước |
| Đặt phụ kiện PCCC | Co90 · Tee · Van · Sprinkler · Chữa cháy · Đầu báo |

#### Nhóm E: Tiêu chuẩn & Tính toán
| Hệ | Tiêu chuẩn | Tính toán nhanh |
|----|-----------|-----------------|
| Điện | TCVN 9207, IEC 60364, QCVN 4 | I 3 pha · sụt áp ΔU% |
| Nước | TCVN 4513, QCVN 01, TCVN 4474 | Tốc độ ống · lưu lượng · Darcy |
| PCCC | QCVN 06, NFPA 13/14, TCVN 6160 | Q=K√P · mật độ · bồn |
| HVAC | TCVN 5687/9391, ASHRAE, SMACNA | Tải · lưu lượng gió · RT |

#### Nhóm F: Tủ điện / DB
| Nút | Chức năng |
|-----|-----------|
| Vẽ tủ điện | Block thiết bị + vùng |
| Cập nhật tủ | Chọn block hiện có |
| Mặt chiếu tủ | Hình chữ nhật 3 ngăn |
| Bố trí động lực | Lưới dây |
| **Render bố trí tủ → PNG** | Composite ảnh thực → xuất file ảnh |

### 3.2 Render tủ điện (Python)

```
File CSV/JSON (khối lượng)
     ↓
render_cabinet_v3.py
     ↓
┌──────────────────────────────┐
│ Layout engine                │
│ + Device PNG compositor      │
│ + DIN rail, bay, cabinet     │
│ + Sidebar thông tin dự án    │
│ + Footer usage bar           │
└──────────────────────────────┘
     ↓
PNG photorealistic (2600x963px)
```

**7 loại thiết bị có ảnh thực:**
- Schneider EasyPact CVS100F (MCCB)
- Schneider Easy9 MCB 1P / 3P
- LS GMC-25 Contactor
- LS MT-32 Relay nhiệt
- Schneider RE17RAMU Timer
- Schneider PM5560 Meter

### 3.3 License Server (ASP.NET Core)

- OTP qua SĐT · JWT · offline cache
- Admin `/admin`: bật/tắt từng chức năng per user/máy
- 13 sub-features tiếng Việt
- devMode (không cần server khi phát triển)

---

## 4. Cấu trúc source code

```
MEP DRAWING TOOL/
├── src/
│   ├── MepPanel.AutoCAD/           # Loader DLL, lệnh MEPDB/MEPHVAC…
│   ├── MepPanel.AutoCAD.Licensing/ # Login OTP, LicenseGuard, session
│   ├── MepPanel.Blocks.AutoCAD/    # Panel UI + tất cả drawing services
│   │   └── Drawing/
│   │       ├── MepDbDrawingService.cs         # Tủ điện
│   │       ├── MepHvacDrawingService.cs       # Ống gió cơ bản
│   │       ├── MepHvacAdvancedService.cs      # Ống mềm, co 45, reducer
│   │       ├── MepElectricalRoutingService.cs # Máng cáp, trunking
│   │       ├── MepWaterDrawingService.cs      # Hệ nước
│   │       ├── MepFireDrawingService.cs       # PCCC
│   │       ├── MepPipeLibraryService.cs       # Thư viện ống AMC
│   │       ├── MepCabinetRenderService.cs     # Gọi Python renderer
│   │       └── MepKnowledgeService.cs         # Tiêu chuẩn + tính toán
│   └── MepPanel.Core/
│       ├── Standards/               # TCVN/QCVN/NFPA/ASHRAE catalog
│       ├── Calculations/            # Engine tính toán 4 hệ (unit tests)
│       └── PluginFeatures.cs        # Mã feature
│
├── MepPanel.LicenseServer/          # ASP.NET Core license + admin
├── tests/MepPanel.Tests/            # 24 unit tests
│
├── scripts/
│   ├── build-plugin-from-repo.ps1   # Build chính từ repo
│   ├── install-plugin-bundle.ps1    # Cài vào AutoCAD
│   ├── render_cabinet_v3.py         # Renderer tủ điện
│   └── (10 PowerShell patch scripts)
│
├── bundle/MepPanel.Plugin.bundle/   # Bundle cài AutoCAD
│   └── Contents/
│       ├── MepPanel.Plugin.dll      # Entry DLL
│       ├── render_cabinet.py        # Renderer (copy từ v3)
│       ├── MepPanel.config.json     # Config + devMode
│       └── samples/templates/       # AMC_TEMPLATE_RV29.dwg
│
├── samples/
│   ├── templates/AMC_TEMPLATE_RV29.dwg
│   └── cabinet/
│       ├── TD-01_full.json / .csv   # Dữ liệu mẫu tủ TD-01
│       └── devices/                 # 7 PNG thiết bị
│
└── docs/                            # 13 tài liệu kỹ thuật
```

---

## 5. Cách sử dụng

### 5.1 Build & cài plugin AutoCAD (Windows)

```powershell
# 1. Clone repo
git clone https://github.com/du10031992/du-cursor.git
cd du-cursor
git checkout cursor/plugin-features-cc24

# 2. Build và cài (cần AutoCAD 2021 + .NET SDK)
# Đóng AutoCAD trước
.\scripts\build-plugin-from-repo.ps1

# 3. Khởi động AutoCAD → gõ MEPDB
```

### 5.2 Các lệnh AutoCAD

| Lệnh | Chức năng |
|------|-----------|
| `MEPDB` | Mở panel MEP DRAWING TOOL |
| `MEPHVAC` | Mở panel (tab điều hòa) |
| `MEPSTATUS` | Trạng thái bundle + license |
| `MEPLOGIN` | Đăng nhập OTP |
| `MEPLOGOUT` | Đăng xuất |

### 5.3 Render tủ điện từ bảng khối lượng

```powershell
# Python 3 + Pillow
pip install pillow

# Từ CSV
python3 scripts/render_cabinet_v3.py --input samples/cabinet/TD-01_full.csv --output tu_dien.png

# Từ JSON
python3 scripts/render_cabinet_v3.py --input samples/cabinet/TD-01_full.json --output tu_dien.png

# Demo
python3 scripts/render_cabinet_v3.py --demo --output demo.png
```

**Format CSV tối thiểu:**
```csv
bay,ten_thiet_bi,device_type,in_a,poles,qty,manufacturer
NGĂN 1,MCCB tổng,MCCB,100,3,1,Schneider
NGĂN 2,MCB chiếu sáng,MCB 1P,10,1,4,Schneider
NGĂN 3,Contactor bơm,CONTACTOR,25,3,2,LS
```

### 5.4 Chạy không cần License Server (dev mode)

```json
// bundle/Contents/MepPanel.config.json
{
  "licenseServerUrl": "http://localhost:5268/",
  "devMode": true,
  "pipeLibraryDwg": "samples/templates/AMC_TEMPLATE_RV29.dwg"
}
```

### 5.5 Khởi chạy License Server

```powershell
# Trong Visual Studio: F5 project MepPanel.LicenseServer
# Hoặc:
cd MepPanel.LicenseServer
dotnet run

# Admin: https://localhost:7024/admin/
# OTP test: 123456
# Admin key: MEP-PANEL-ADMIN-TEST-2026
```

---

## 6. Hướng phát triển

### Giai đoạn 1 — Hoàn thiện plugin (Ưu tiên cao)

| Hạng mục | Việc cần làm |
|----------|-------------|
| **Import MepPanelMvp** | Đưa source WPF đầy đủ vào repo (`src/MepPanelMvp/`) để có cấu hình tủ WPF, HVAC window, sơ đồ 3P-4D+E thật |
| **Ảnh thiết bị thật** | Bổ sung PNG catalog từ Schneider/LS/ABB/Chint vào `devices/` |
| **Xuất Excel/CSV thật** | Đọc attribute block AutoCAD → xuất BOM thực tế |
| **Sơ đồ 3P-4D+E** | Vẽ busbar + nhánh MCB trên bản vẽ |
| **Tag kích thước HVAC** | Ghi kích thước ống W×H sau khi vẽ |

### Giai đoạn 2 — Cloud & API (3-6 tháng)

```
Internet
  ├── API tính toán MEP (/api/calc/electrical, /water, /fire, /hvac)
  ├── CDN thư viện block/template (host AMC + thêm library)
  ├── OTA update plugin (check version → tải DLL mới)
  └── Web portal: đăng ký, thanh toán VNPay/Momo, tải plugin
```

### Giai đoạn 3 — Sản phẩm thương mại (6-12 tháng)

| Gói | Tính năng | Giá tham khảo |
|-----|-----------|---------------|
| **Starter** | Plugin cơ bản, 1 máy | 990k–1.5M/năm |
| **Pro** | Full feature + AMC + render tủ | 3–8M/năm |
| **Enterprise** | Admin B2B, API, template riêng | Theo dự án |
| **Cloud Calc** | Web tính toán (không cần AutoCAD) | Freemium |

### Kênh phân phối

- Website sản phẩm + video demo YouTube
- Autodesk App Store (dài hạn)
- Cộng đồng MEP Vietnam Facebook/Zalo
- Đại lý nhà thầu MEP

---

## 7. Tài liệu tham khảo

| File | Nội dung |
|------|----------|
| `docs/BUILD_PLUGIN_CAD.md` | Build & cài trên Windows |
| `docs/MEP_STANDARDS_REFERENCE.md` | Bảng tiêu chuẩn TCVN/QCVN/NFPA |
| `docs/MEP_APPLICATION_ARCHITECTURE.md` | Kiến trúc 5 module |
| `docs/PIPE_LIBRARY_AMC.md` | Thư viện ống AMC |
| `docs/COMMERCIAL_ROADMAP.md` | Lộ trình thương mại chi tiết |
| `docs/LICENSE_ADMIN_GUIDE.md` | Hướng dẫn Admin license |
| `docs/PLUGIN_FEATURE_ROADMAP.md` | Trạng thái từng chức năng |

---

## 8. Trạng thái chức năng

| Chức năng | Trạng thái |
|-----------|-----------|
| Lệnh MEPDB / panel | ✅ Hoàn chỉnh |
| Hệ điện: máng cáp, trunking | ✅ Hoàn chỉnh |
| HVAC: ống thẳng, L, mềm, co 45/90, reducer | ✅ Hoàn chỉnh |
| Hệ nước: ống + phụ kiện AMC | ✅ Hoàn chỉnh |
| PCCC: ống + sprinkler/chữa cháy | ✅ Hoàn chỉnh |
| Tiêu chuẩn TCVN/QCVN/NFPA 4 hệ | ✅ Hoàn chỉnh |
| Tính toán kỹ thuật (24 unit tests) | ✅ Hoàn chỉnh |
| Render tủ điện → PNG (ảnh thiết bị thực) | ✅ Hoàn chỉnh |
| License server + Admin web | ✅ Hoàn chỉnh |
| Cấu hình tủ WPF (cửa sổ đầy đủ) | 🔶 Cần MepPanelMvp source |
| Sơ đồ 3P-4D+E | 🔶 Stub |
| OTA update, Cloud API | 📅 Phase 2 |
| Web portal, thanh toán | 📅 Phase 3 |

**Chú thích:** ✅ Có thể dùng ngay · 🔶 Stub/hướng dẫn · 📅 Kế hoạch

---

*MEP Drawing Tool — Build từ repo: `git checkout cursor/plugin-features-cc24` → `.\scripts\build-plugin-from-repo.ps1`*
