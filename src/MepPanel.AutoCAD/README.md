# Dev loader (stub) — không phải plugin release

Project này build ra **`MepPanel.Plugin.dll`** (loader 5 lệnh) dùng cho test license nhanh.

**Plugin thật v0.13** (`MepPanel.AutoCAD.dll` — Cabinet, HVAC, WPF) build từ source **MepPanelMvp** riêng.

Xem: [docs/PLUGIN_DEVELOPMENT.md](../../docs/PLUGIN_DEVELOPMENT.md)

```powershell
# Plugin release (khuyến nghị)
.\scripts\build-plugin-release.ps1

# Loader stub (chỉ test license)
.\scripts\install-plugin-bundle.ps1 -BuildDevLoader
```
