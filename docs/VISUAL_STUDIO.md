# Build trong Visual Studio (không dùng PowerShell)

## Mở solution

```
C:\MepPanel\du-cursor\MepPanelMvp.sln
```

| Project | Vai trò |
|---------|---------|
| **MepPanel.LicenseServer** | License + Admin + OTP |
| **MepPanel.Tests** | Test server (tuỳ chọn) |
| src\MepPanel.* trong repo | Stub dev — **plugin release build từ `C:\MepPanel\MepPanelMvp`** |

---

## 1. License Server (F5)

1. Solution Explorer → chuột phải **MepPanel.LicenseServer** → **Set as Startup Project**
2. Toolbar: **Debug** | **Any CPU**
3. Nhấn **F5** (Start Debugging)

Phải thấy trong Output:

```
Now listening on: http://localhost:5268
Hosting started
```

Admin: http://localhost:5268/admin
OTP test: **123456**

> User được lưu trong SQLite `mep-panel-license.db` tại thư mục chạy
> `MepPanel.LicenseServer`. Nếu F5 source vừa giải nén ở một thư mục khác,
> server sẽ tạo database mới và chỉ hiện user seed. Muốn giữ user cũ: F5
> project cũ, hoặc đóng Visual Studio rồi chép `mep-panel-license.db` cũ sang
> thư mục `MepPanel.LicenseServer` của source mới trước khi F5.

### Lỗi MSB3021 / file bị khóa

**Nguyên nhân:** Server đang F5 mà bấm Build → file `.exe` bị khóa.

**Đã sửa trong project:** mỗi lần Build, VS **tự tắt** `MepPanel.LicenseServer.exe` cũ rồi mới build (xem Output: `[MepPanel] Dung License Server cu...`).

Sau Build xong → **F5** lại để chạy server.

Nếu vẫn lỗi: Task Manager → End **MepPanel.LicenseServer.exe** → Build lại.

---

## 2. Plugin AutoCAD (MepPanelMvp)

Plugin **không** build từ project stub trong `du-cursor\src`. Mở solution trên máy bạn:

```
C:\MepPanel\MepPanelMvp\MepPanelMvp.sln
```

(hoặc solution tương đương có `MepPanel.AutoCAD` + `MepPanel.Blocks.AutoCAD`)

1. Configuration: **Release** | **x64**
2. Build → **Build Solution** (Ctrl+Shift+B)
3. Thứ tự project: **MepPanel.Core** → **MepPanel.Blocks.AutoCAD** → **MepPanel.AutoCAD**

Output DLL:

```
C:\MepPanel\MepPanelMvp\src\MepPanel.AutoCAD\bin\x64\Release\MepPanel.AutoCAD.dll
C:\MepPanel\MepPanelMvp\src\MepPanel.Blocks.AutoCAD\bin\x64\Release\MepPanel.Blocks.AutoCAD.dll
```

Copy vào plugin (một lần, hoặc sau mỗi build):

```
C:\MepPanel\plugin\MepPanel.Plugin.bundle\Contents\
```

Copy cả **MepPanel.Core.dll** và **MepPanel.Blocks.AutoCAD.dll**.

Khởi động lại AutoCAD.

---

## 3. Test OTP + MEPDB

1. VS: **F5** License Server (giữ chạy)
2. AutoCAD → `MEPDB` → SĐT → Gửi OTP → **123456** → Đăng nhập
3. Nút **Hệ nước** trên panel

Plugin config (`Contents\MepPanel.config.json`):

```json
{
  "licenseServerUrl": "http://localhost:5268/"
}
```

---

## 4. Lỗi thường gặp

| Lỗi | Xử lý |
|-----|--------|
| MSB3021 exe locked | Shift+F5 rồi Build |
| CS0006 Tests không tìm thấy DLL | Do LicenseServer build fail — sửa MSB3021 trước |
| OTP connection refused | F5 License Server trước khi mở AutoCAD |
| Build Blocks lỗi | Mở **MepPanelMvp** solution, Release x64 |
