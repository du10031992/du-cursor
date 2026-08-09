# Tập hợp file cần dùng

Hệ thống gồm **2 phần chạy độc lập**: một máy chủ kiểm soát, và nhiều máy AutoCAD dùng plugin.

```text
MÁY CHỦ (1 máy)                       MÁY AUTOCAD (nhiều máy)
License Server + Admin + database  ←  Plugin bundle (MepPanel.config.json)
```

---

## A. Máy chủ kiểm soát

### A1. File chạy server

Thư mục cài: `C:\MepPanel\LicenseServer\`

| File | Vai trò |
|---|---|
| `MepPanel.LicenseServer.exe` | Chương trình máy chủ |
| `MepPanel.LicenseServer.dll` | Code chính |
| `MepPanel.LicenseServer.runtimeconfig.json` | Cấu hình .NET runtime |
| `MepPanel.LicenseServer.deps.json` | Danh sách thư viện |
| `appsettings.json` | Cấu hình gốc (OTP test, admin key mặc định) |
| `appsettings.Production.json` | Cấu hình production |
| `wwwroot\admin\index.html` | Trang Admin `/admin` |
| các `.dll` thư viện kèm theo | EF Core, SQLite, JWT, Swagger |

Sinh ra bằng:

```powershell
.\scripts\publish-license-server.ps1
```

> Gói `MepPanel-LicenseServer-v*.zip` được build trên Linux nên **không có** `.exe` cho Windows. Trên máy chủ Windows hãy chạy `publish-license-server.ps1`.

### A2. File dữ liệu (quan trọng nhất — phải sao lưu)

```text
C:\MepPanel\du-cursor\MepPanel.LicenseServer\mep-panel-license.db
C:\MepPanel\du-cursor\MepPanel.LicenseServer\mep-panel-license.db-wal
C:\MepPanel\du-cursor\MepPanel.LicenseServer\mep-panel-license.db-shm
```

Chứa toàn bộ user, quyền, thiết bị, hạn dùng. Sao lưu cả 3 file khi server đã tắt.

### A3. Script cài đặt và vận hành

| File | Dùng để |
|---|---|
| `scripts\publish-license-server.ps1` | Build server thành exe |
| `scripts\install-license-server-service.ps1` | Cài chạy nền, tự bật theo Windows, mở LAN + firewall |
| `scripts\run-license-server.ps1` | Chạy tạm bằng dotnet (tuỳ chọn) |

### A4. Nếu deploy VPS/Internet

| File | Dùng để |
|---|---|
| `deploy\docker-compose.yml` | Chạy server bằng Docker |
| `deploy\nginx.conf` | HTTPS, giới hạn tần số, chặn IP cho `/admin` |
| `deploy\.env.example` | Mẫu khai `JWT_KEY`, `ADMIN_API_KEY`, SMS |
| `MepPanel.LicenseServer\Dockerfile` | Build image |

---

## B. Máy AutoCAD (client)

Thư mục cài: `C:\ProgramData\Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle\`
(hoặc `C:\MepPanel\plugin\MepPanel.Plugin.bundle\`)

### B1. File bắt buộc

| File | Vai trò |
|---|---|
| `PackageContents.xml` | AutoCAD tự nạp plugin |
| `Contents\MepPanel.AutoCAD.dll` | Plugin chính: `MEPDB`, UI, kiểm soát quyền |
| `Contents\MepPanel.Core.dll` | Tính toán, dữ liệu |
| `Contents\MepPanel.Blocks.AutoCAD.dll` | Vẽ CAD, hệ nước/PCCC, render |
| `Contents\MepPanel.config.json` | **URL máy chủ** — quyết định máy nào kiểm soát |
| `Contents\plugin.manifest.json` | Khai báo tính năng |

### B2. File cho tính năng thư viện và render

| File | Vai trò |
|---|---|
| `Contents\samples\templates\AMC_TEMPLATE_RV29.dwg` | Thư viện ống AMC |
| `Contents\render_pipe_system.py` | Render sơ đồ nước/PCCC |
| `Contents\render_cabinet.py` | Render tủ điện |
| `Contents\render_cabinet_interior.py` | Render bên trong tủ |
| `Contents\render_photoreal.py` | Render ảnh thực |
| `Contents\devices\*.png` | Ảnh thiết bị dùng khi render |

Cần Python 3 + Pillow trên máy client nếu dùng render:

```powershell
py -m pip install pillow
```

### B3. Script cài client

| File | Dùng để |
|---|---|
| `Install-MepPanel-Client.ps1` | Cài bundle + ghi URL máy chủ |
| `verify-client-server.ps1` | Kiểm tra client có nối đúng máy chủ |

---

## C. Gói phát hành

Sinh bằng `./scripts/pack-release.sh`:

| Gói | Gửi cho ai |
|---|---|
| `MepPanel-Client-v0.4.0.zip` | Từng máy AutoCAD |
| `MepPanel-LicenseServer-v0.4.0.zip` | Người quản trị máy chủ |
| `MepPanel-v0.4.0.zip` | Bản đầy đủ (server + client + source) |
| `SHA256-v0.4.0.txt` | Kiểm tra toàn vẹn file |

---

## D. Thứ tự triển khai

**Máy chủ**

```powershell
cd C:\MepPanel\du-cursor
.\scripts\publish-license-server.ps1
.\scripts\install-license-server-service.ps1
```

Ghi lại URL script in ra, ví dụ `http://192.168.1.10:5268/`.

**Từng máy AutoCAD**

```powershell
.\Install-MepPanel-Client.ps1 -LicenseServerUrl "http://192.168.1.10:5268/"
.\verify-client-server.ps1
```

Mở AutoCAD → `MEPDB` → đăng nhập bằng số điện thoại đã tạo trên `/admin`.

---

## E. File KHÔNG được đưa cho máy client

| File | Lý do |
|---|---|
| `mep-panel-license.db*` | Dữ liệu cấp phép, chỉ nằm ở máy chủ |
| `MepPanel.LicenseServer\*` | Client không được tự chạy máy chủ |
| `deploy\.env` | Chứa khoá bí mật |
| `plugin.local.json` | Đường dẫn build nội bộ |

Xem thêm: [SERVER_ALWAYS_ON.md](SERVER_ALWAYS_ON.md), [REMOTE_DEPLOYMENT.md](REMOTE_DEPLOYMENT.md), [LICENSE_ADMIN_GUIDE.md](LICENSE_ADMIN_GUIDE.md).
