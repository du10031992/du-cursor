# MepPanel MVP — License control

Plugin AutoCAD 2021 + License Server để Admin:

- Khóa / mở tài khoản theo số điện thoại (`Active` / `Blocked`)
- Mỗi SĐT chỉ dùng **1 máy**; chuyển máy phải Admin `release-device`
- Khóa / mở từng chức năng plugin (`MEPDB`, `MEPHVAC`)

## Projects

| Project | Vai trò |
|---|---|
| `MepPanel.LicenseServer` | Máy chủ kiểm soát (Swagger) |
| `src/MepPanel.AutoCAD` | Plugin + license gate |
| `src/MepPanel.Core` | Shared constants |
| `tests/MepPanel.Tests` | Kiểm thử luật license |

## Quick start (máy bạn)

1. Mở `MepPanelMvp.sln` bằng Visual Studio  
2. F5 project `MepPanel.LicenseServer` → `https://localhost:7024/swagger`  
3. Build `MepPanel.AutoCAD` (cần AutoCAD 2021)  
4. AutoCAD: `NETLOAD` → login SĐT test `0900000001` / OTP `123456`  
5. Gõ `MEPDB` / `MEPHVAC`

Chi tiết Admin API: [docs/LICENSE_ADMIN_GUIDE.md](docs/LICENSE_ADMIN_GUIDE.md)
