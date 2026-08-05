# MEPDB — lệnh duy nhất vào plugin

## Kiến trúc mới

| Lớp | Mô tả |
|-----|--------|
| **Lệnh AutoCAD** | Chỉ `MEPDB` — mở đăng nhập + panel điều khiển |
| **Admin `/admin`** | Bật `MEPDB` (vào plugin) + từng **chức năng phụ** (Cabinet, HVAC, …) |
| **Trong panel** | Nút/tool gọi `PluginFeatureGate.Ensure("MEPDBCABINET2D")` |

Các lệnh `MEPDBCABINET2D`, `MEPHVAC`, … **không còn** trên command line (patch build tự ẩn `[CommandMethod]`).

## Build

```powershell
git pull
.\scripts\build-plugin-release.ps1
```

Script tự:

1. Copy `LicenseGuard`, `PluginFeatureGate`, `PanelSystemGuard`, `LicenseSession`, …
2. `apply-single-entry-patch.ps1` — comment `[CommandMethod]` các lệnh MEP* trừ `MEPDB`
3. `apply-subfeature-guard-patch.ps1` — inject guard vào handler panel (map + regex tên method)
4. `audit-subfeature-guards.ps1` — báo handler nào chưa có guard (chạy tự động khi build)
5. `apply-feature-guard-patch.ps1` — `MEPDB` → `EnsureEntry()`

Sau build, nếu thấy cảnh báo `[!!] CHUA CO GUARD`, gửi tên method cho dev để bổ sung vào `apply-subfeature-guard-patch.ps1`.

## Trong source MepPanelMvp (panel WPF)

Trước khi chạy tool phụ:

```csharp
if (!PluginFeatureGate.Ensure(PluginFeatures.Cabinet2D))
    return;
```

Hoặc chỉ ẩn nút:

```csharp
button.IsEnabled = PluginFeatureGate.CanUse(PluginFeatures.Cabinet2D);
```

## Admin

- **MEPDB** = user được vào plugin (bắt buộc)
- **MEPDBCABINET2D**, **MEPHVAC**, … = khóa/mở từng tool trong panel
