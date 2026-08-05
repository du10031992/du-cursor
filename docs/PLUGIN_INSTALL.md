# Cài plugin AutoCAD (bundle tự load)

## Yêu cầu

- Windows + AutoCAD 2021
- License Server chạy khi đăng nhập / dùng lệnh tool

## Cài bundle (plugin release v0.13)

PowerShell tại thư mục repo:

```powershell
git pull origin cursor/license-admin-device-control-cc24
.\scripts\install-plugin-bundle.ps1
```

Script cài **plugin thật** từ bundle (không build lại):

| File | Vai trò |
|---|---|
| `MepPanel.AutoCAD.dll` | Plugin chính — MEPDB, MEPHVAC, UI, licensing |
| `MepPanel.Core.dll` | Core services |

Đường dẫn cài:  
`%ProgramData%\Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle\Contents\`

**Mở lại AutoCAD** → plugin tự load.

## Kiểm soát license (Admin)

Plugin **v0.13** đã tích hợp `LicenseGuard` + `AutoCadCommandDispatcher`:

- Mỗi lệnh MEPDB/MEPHVAC → check server (user, máy, feature)
- Admin tại **`https://localhost:7024/admin`**:
  - Khóa/mở **user** theo SĐT
  - Khóa/mở **máy** (1 SĐT = 1 máy)
  - Bật/tắt **MEPDB** / **MEPHVAC**

Test user (seed): `0912345678` hoặc tạo mới trên Admin. OTP test: **`123456`**.

## Lệnh plugin (v0.13)

| Nhóm | Lệnh ví dụ |
|---|---|
| MEPDB | `MEPDB`, `MEPDBDRAW`, `MEPDBCABINET2D`, `MEPDBREALWIRING`, ... |
| MEPHVAC | `MEPHVAC`, `MEPHVACDRAW`, `MEPHVACCONFIG`, ... |

Khi chưa đăng nhập, chạy lệnh tool → cửa sổ login OTP.

## Dev loader (tùy chọn)

Build loader đơn giản từ source (v0.3 stub):

```powershell
.\scripts\install-plugin-bundle.ps1 -BuildDevLoader
```

## Cấu hình server

File `MepPanel.config.json` trong `Contents/` (nếu plugin hỗ trợ):

```json
{
  "licenseServerUrl": "https://localhost:7024/"
}
```

## NETLOAD thủ công

```text
(command "_.NETLOAD" "C:/ProgramData/Autodesk/ApplicationPlugins/MepPanel.Plugin.bundle/Contents/MepPanel.AutoCAD.dll")
```

Cần `MepPanel.Core.dll` cùng thư mục.
