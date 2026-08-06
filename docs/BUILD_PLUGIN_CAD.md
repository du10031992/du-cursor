# Build plugin chạy trong AutoCAD 2021

## Yêu cầu

| Thành phần | Ghi chú |
|------------|---------|
| Windows 64-bit | AutoCAD plugin chỉ chạy trên Windows |
| AutoCAD 2021 | Cài tại `C:\Program Files\Autodesk\AutoCAD 2021` |
| .NET SDK | Build `net48` (Visual Studio Build Tools hoặc VS 2022) |

## Build + cài (một lệnh)

PowerShell tại thư mục repo:

```powershell
git pull origin cursor/plugin-features-cc24
.\scripts\build-plugin-from-repo.ps1
```

Script sẽ:

1. Build 4 project: `Core`, `Licensing`, `Blocks.AutoCAD`, `Plugin`
2. Copy DLL vào `bundle\MepPanel.Plugin.bundle\Contents\`
3. Cập nhật `PackageContents.xml` → load `MepPanel.Plugin.dll`
4. Cài vào `%ProgramData%\Autodesk\ApplicationPlugins\`

**Khởi động lại AutoCAD** → plugin tự load.

## Chỉ build, chưa cài

```powershell
.\scripts\build-plugin-from-repo.ps1 -SkipInstall
```

## Chạy không cần License Server (dev)

File `bundle\...\Contents\MepPanel.config.json`:

```json
{
  "licenseServerUrl": "http://localhost:5268/",
  "devMode": true
}
```

`devMode: true` → bỏ qua OTP/server, mở đủ 13 chức năng trên panel.

Khi deploy production: đặt `"devMode": false` và chạy License Server.

## Lệnh trong AutoCAD

| Lệnh | Chức năng |
|------|-----------|
| `MEPDB` | Mở panel **MEP DRAWING TOOL** |
| `MEPHVAC` | Mở cùng panel (tab điều hòa) |
| `MEPSTATUS` | Trạng thái bundle + license |
| `MEPLOGIN` | Đăng nhập OTP (khi devMode=false) |
| `MEPLOGOUT` | Đăng xuất |

## Cấu trúc bundle sau build

```
MepPanel.Plugin.bundle\Contents\
├── MepPanel.Plugin.dll          ← entry AutoCAD load
├── MepPanel.AutoCAD.Licensing.dll
├── MepPanel.Blocks.AutoCAD.dll  ← panel + vẽ
├── MepPanel.Core.dll
└── MepPanel.config.json
```

## Xử lý lỗi thường gặp

| Lỗi | Cách xử lý |
|-----|------------|
| `AcCoreMgd.dll not found` | Cài AutoCAD 2021 hoặc `-AutoCadDir "D:\...\AutoCAD 2021"` |
| Access denied khi cài | **Đóng AutoCAD** rồi chạy lại script |
| Plugin không load | Kiểm tra `%ProgramData%\Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle` |
| Lệnh MEPDB không có | Gõ `MEPSTATUS` — nếu lỗi thì NETLOAD thủ công `MepPanel.Plugin.dll` |

## Build từ source MepPanelMvp (WPF đầy đủ)

Nếu có source gốc bên ngoài:

```powershell
copy plugin.local.json.example plugin.local.json
# Sua pluginSourceRoot tro toi MepPanelMvp
.\scripts\build-plugin-release.ps1
```

Script đó build `MepPanel.AutoCAD.dll` WPF đầy đủ (cấu hình tủ, HVAC window…).
