# MepPanel — License control + Plugin AutoCAD

| Thành phần | Version | Build |
|---|---|---|
| License Server + Admin | v0.4.0 | `dotnet run` / F5 |
| Plugin AutoCAD (release) | v0.4.0 | Source MepPanelMvp trên Windows |

Plugin AutoCAD 2021 + License Server để Admin:

- Khóa / mở tài khoản theo số điện thoại (`Active` / `Blocked`)
- Mỗi SĐT chỉ dùng **1 máy**; chuyển máy phải Admin `release-device`
- Khóa / mở từng chức năng plugin (`MEPDB`, `MEPHVAC`, `MEPDBWATER`, `MEPDBSMOKE`)
- Hệ nước / PCCC: vẽ ống, phân tích, render minh họa PNG (Pillow)

## Projects

| Project | Vai trò |
|---|---|
| `MepPanel.LicenseServer` | Máy chủ kiểm soát + `/admin` |
| `src/MepPanel.AutoCAD` | Dev loader stub (`MepPanel.Plugin.dll`) — **không** phải plugin release |
| `bundle/.../MepPanel.AutoCAD.dll` | **Plugin thật v0.13** (Cabinet, HVAC, WPF) |
| `tests/MepPanel.Tests` | Kiểm thử luật license |

## Quick start — License Server

1. Mở **`MepPanelMvp.sln`** trong Visual Studio (`C:\MepPanel\du-cursor\`)
2. Set startup **MepPanel.LicenseServer** → **F5**
3. Admin: `http://localhost:5268/admin`
4. Chi tiết: **[docs/VISUAL_STUDIO.md](docs/VISUAL_STUDIO.md)**

## Quick start — Cài plugin (release)

```powershell
git pull origin cursor/water-pccc-visual-cc24
.\scripts\build-plugin-release.ps1
# hoac chi cai renderer:
.\scripts\install-renderer-devices.ps1
```

Khởi động lại AutoCAD → `MEPDB` → OTP test `123456` → dùng nhóm **Hệ nước** / **PCCC** (vẽ, tính toán, Render → PNG).

## Phát triển & build plugin (cập nhật tính năng)

Xem **[docs/PLUGIN_DEVELOPMENT.md](docs/PLUGIN_DEVELOPMENT.md)**.

Tóm tắt:

```powershell
copy plugin.local.json.example plugin.local.json
# Sua pluginSourceRoot -> thu muc MepPanelMvp source tren may ban

.\scripts\build-plugin-release.ps1
```

## Tài liệu

| File | Nội dung |
|---|---|
| [docs/PLUGIN_DEVELOPMENT.md](docs/PLUGIN_DEVELOPMENT.md) | Build, sửa tính năng, workflow dev |
| [docs/PLUGIN_INSTALL.md](docs/PLUGIN_INSTALL.md) | Cài bundle AutoCAD |
| [docs/LICENSE_ADMIN_GUIDE.md](docs/LICENSE_ADMIN_GUIDE.md) | Admin API & test |
| [docs/DEPLOY_VPS.md](docs/DEPLOY_VPS.md) | Deploy server lên VPS |
| [CHANGELOG.md](CHANGELOG.md) | Lịch sử thay đổi |

## Đóng gói release

```bash
./scripts/pack-release.sh
```

Kết quả trong `dist/`.
