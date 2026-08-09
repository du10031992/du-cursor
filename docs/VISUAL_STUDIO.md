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

> Database dùng một đường dẫn cố định:
> `C:\MepPanel\du-cursor\MepPanel.LicenseServer\mep-panel-license.db`.
> Dù F5 source ở thư mục khác, server vẫn dùng file này. Sao lưu file này
> trước khi đổi máy hoặc cài Windows. Muốn đặt License Server trung tâm tại
> một đường dẫn khác, cấu hình biến môi trường `MEP_PANEL_LICENSE_DB_PATH`
> bằng đường dẫn tuyệt đối tới file `.db`.

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
