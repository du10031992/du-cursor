# Cài plugin AutoCAD (bundle tự load)

## Yêu cầu

- Windows + AutoCAD 2021
- .NET SDK + Visual Studio (build)
- License Server chạy khi đăng nhập

## Cài bundle (khuyến nghị)

PowerShell tại thư mục repo:

```powershell
git pull origin cursor/license-admin-device-control-cc24
.\scripts\install-plugin-bundle.ps1
```

Script sẽ:

1. Build `MepPanel.Plugin.dll` + dependencies
2. Copy vào `bundle/MepPanel.Plugin.bundle/Contents/`
3. Cài vào `%ProgramData%\Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle`
4. Đổi tên bundle cũ `MepPanelMvp.bundle` → `.OFF` (nếu có)

**Mở lại AutoCAD** → plugin tự load, **không** popup đăng nhập lúc khởi động.

## Lệnh

| Lệnh | Mô tả |
|---|---|
| `MEPSTATUS` | Kiểm tra plugin |
| `MEPLOGIN` | Đăng nhập |
| `MEPDB` | Mở panel MEPDB |
| `MEPHVAC` | Mở panel MEPHVAC |
| `MEPLOGOUT` | Đăng xuất |

## NETLOAD thủ công (dev)

```text
(command "_.NETLOAD" "C:/path/to/MepPanel.Plugin.dll")
```

Cần 3 DLL cùng thư mục: `MepPanel.Plugin.dll`, `MepPanel.AutoCAD.Licensing.dll`, `MepPanel.Blocks.AutoCAD.dll`.

## Cấu hình server production

Copy `MepPanel.config.json.example` → `MepPanel.config.json` trong `Contents/`:

```json
{
  "licenseServerUrl": "https://license.your-domain.com/"
}
```

Xem thêm: `docs/DEPLOY_VPS.md`
