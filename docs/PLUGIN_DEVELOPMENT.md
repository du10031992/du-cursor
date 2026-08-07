# Phát triển plugin MepPanel (build & cập nhật tính năng)

Repo này gồm **2 phần**:

| Phần | Build được trên repo? | Mô tả |
|---|---|---|
| **License Server** | Có (mọi OS) | ASP.NET Core — Admin, OTP, khóa user/máy/feature |
| **Plugin AutoCAD v0.4** | Cần source gốc trên Windows | `MepPanel.AutoCAD.dll` + `MepPanel.Core.dll` + renderer Pillow |

Source plugin đầy đủ (Cabinet, HVAC, WPF, …) nằm ở project **MepPanelMvp** trên máy bạn — **chưa** nằm hết trong git. Git chỉ lưu bản **DLL release** trong `bundle/`.

---

## Chuẩn bị (một lần)

### 1. Máy dev

- Windows 10/11
- Visual Studio 2019/2022
- AutoCAD 2021 (x64)
- .NET SDK + .NET Framework 4.8

### 2. Source plugin gốc

Thư mục MepPanelMvp cần có:

```
MepPanelMvp/
  src/
    MepPanel.AutoCAD/     → build ra MepPanel.AutoCAD.dll
    MepPanel.Core/        → build ra MepPanel.Core.dll
```

Đường dẫn PDB trên bản hiện tại (tham khảo):

`C:\Users\DU_COMPUTER\Documents\Codex\2026-07-31\hay\work\MepPanelMvp`

### 3. Cấu hình repo

Trong thư mục repo `du-cursor`:

```powershell
copy plugin.local.json.example plugin.local.json
notepad plugin.local.json
```

Sửa `pluginSourceRoot` trỏ đúng thư mục MepPanelMvp của bạn.

---

## Quy trình hàng ngày (sửa tính năng → test)

### Bước 1 — Chạy License Server

Visual Studio → F5 project `MepPanel.LicenseServer`  
Admin: `https://localhost:7024/admin`

### Bước 2 — Sửa code plugin

Mở solution **MepPanelMvp** (trên máy bạn) hoặc mở trực tiếp:

- `src/MepPanel.AutoCAD/` — lệnh AutoCAD, UI WPF, vẽ CAD
- `src/MepPanel.Core/` — logic domain (Cabinet, HVAC, …)

**Kiểm soát license** (đã có trong plugin v0.13):

- `LicenseGuard` / `AutoCadCommandDispatcher` — chặn lệnh nếu Admin khóa
- Feature: `MEPDB`, `MEPHVAC`, `MEPDBWATER`, `MEPDBSMOKE`
- Hệ nước / PCCC: patch `scripts/apply-water-pccc-patch.ps1` (vẽ + tính toán + `render_pipe_system.py`)

Khi thêm nhóm lệnh mới cần Admin bật/tắt:

1. Thêm constant feature trong Core (ví dụ `PluginFeatures`)
2. Gọi `LicenseGuard.EnsureFeature("TEN_FEATURE")` trước khi chạy lệnh
3. Thêm feature vào License Server (`PluginFeatures.All`, Admin UI)

### Bước 3 — Build + cập nhật bundle + cài

Trong repo `du-cursor`:

```powershell
git pull origin cursor/license-admin-device-control-cc24
.\scripts\build-plugin-release.ps1
```

Script sẽ:

1. Build `MepPanel.AutoCAD` + `MepPanel.Core` từ source gốc
2. Copy DLL vào `bundle/MepPanel.Plugin.bundle/Contents/`
3. Cài bundle vào `%ProgramData%\Autodesk\ApplicationPlugins\`

Chỉ build, chưa cài:

```powershell
.\scripts\build-plugin-release.ps1 -SkipInstall
```

Tăng version bundle (ví dụ 0.14.0):

```powershell
.\scripts\build-plugin-release.ps1 -Version 0.14.0
```

### Bước 4 — Test AutoCAD

1. **Khởi động lại AutoCAD**
2. Gõ lệnh tool (ví dụ `MEPDB`, `MEPDBCABINET2D`)
3. Login OTP `123456` (TestMode)
4. Trên Admin: tắt feature → thử lại lệnh → phải bị chặn

---

## Hai chế độ plugin trong repo

| Chế độ | Lệnh | Khi nào dùng |
|---|---|---|
| **Release v0.13** (plugin thật) | `.\scripts\install-plugin-bundle.ps1` | Cài DLL có sẵn trong bundle, không build |
| **Dev loader stub** | `.\scripts\install-plugin-bundle.ps1 -BuildDevLoader` | Test license với loader đơn giản (5 lệnh) |
| **Build từ source** | `.\scripts\build-plugin-release.ps1` | **Phát triển tính năng plugin thật** |

---

## Đưa source plugin vào git (khuyến nghị lâu dài)

Để build trên mọi máy mà không cần `plugin.local.json`:

1. Copy/copy git submodule thư mục MepPanelMvp vào repo (hoặc merge vào `src/`)
2. Cập nhật `MepPanelMvp.sln` trỏ đúng project
3. Commit source (không commit `bin/`, `obj/`)

Sau đó `build-plugin-release.ps1` có thể mặc định build từ `./src/` trong repo.

---

## Cấu trúc bundle sau khi build

```
MepPanel.Plugin.bundle/
  PackageContents.xml          → Load MepPanel.AutoCAD.dll
  Contents/
    MepPanel.AutoCAD.dll         ← plugin chính
    MepPanel.Core.dll
    MepPanel.config.json         ← URL license server
    plugin.manifest.json
```

---

## Version

| File | Ý nghĩa |
|---|---|
| `VERSION` + `Directory.Build.props` | Version License Server (hiện 0.3.0) |
| `PackageContents.xml` AppVersion | Version plugin AutoCAD (hiện 0.13.0) |

Khi phát hành plugin mới: tăng AppVersion + commit DLL trong bundle.

---

## Lỗi thường gặp

| Lỗi | Cách xử lý |
|---|---|
| Không tìm thấy `pluginSourceRoot` | Tạo `plugin.local.json` |
| `AcDbMgd` / `AcMgd` missing | Cài AutoCAD 2021 hoặc set `autoCadDir` trong config |
| Lệnh không chạy sau cài | Restart AutoCAD; tắt bundle cũ `MepPanelMvp.bundle` |
| OTP lỗi kết nối | License Server phải chạy; URL trong plugin trỏ `https://localhost:7024` |
| Build stub lỗi CS0246 | Dùng `build-plugin-release.ps1`, không dùng `-BuildDevLoader` cho plugin thật |
