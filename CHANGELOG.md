# Changelog

Mọi thay đổi đáng chú ý của MepPanel được ghi theo [Semantic Versioning](https://semver.org/lang/vi/).

Định dạng phiên bản: `MAJOR.MINOR.PATCH`

- **MAJOR**: thay đổi phá vỡ tương thích
- **MINOR**: thêm tính năng, vẫn tương thích
- **PATCH**: sửa lỗi

---

## [0.3.0] - 2026-08-05 — Tool panels + bundle tự load + logic vẽ + VPS

### Thêm mới
- Panel AutoCAD **MEPDB** / **MEPHVAC** (`MepPanel.Blocks.AutoCAD`)
- Logic vẽ thật: layer, block thiết bị, vùng MEPDB; duct + elbow MEPHVAC
- Bundle `MepPanel.Plugin.bundle` — AutoCAD tự load (không NETLOAD thủ công)
- Script `scripts/install-plugin-bundle.ps1`
- `MepPanel.config.json` — cấu hình URL server production
- SMS OTP production: `OtpService`, `WebhookSmsGateway`
- Deploy VPS: Docker, nginx, `docs/DEPLOY_VPS.md`

### Sửa lỗi
- Thông báo khóa user/feature rõ ràng hơn

---

## [0.2.4] - 2026-08-05 — Refresh feature từ server

### Sửa lỗi
- Mỗi lệnh `MEPDB`/`MEPHVAC` hỏi server cập nhật quyền feature
- API `/api/Devices/check` cho phép không JWT (DeviceKey)

---

## [0.2.3] - 2026-08-05 — Sửa đơ/lag sau đăng nhập

### Sửa lỗi
- Gọi API license trên thread pool (`AsyncRunner`) — tránh deadlock luồng AutoCAD sau khi bấm Đăng nhập

---

## [0.2.2] - 2026-08-04 — Sửa NETLOAD (MepPanel.Plugin.dll)

### Sửa lỗi
- Đổi tên DLL loader: **`MepPanel.Plugin.dll`** (tránh xung đột bản `MepPanel.AutoCAD.dll` cũ trong APPLOAD/cache)
- Tách `MepPanel.AutoCAD.Licensing.dll` — licensing load sau qua reflection
- LoginWindow dùng WinForms

---

## [0.2.1] - 2026-08-04 — Đăng nhập khi chạy lệnh

### Thay đổi
- Plugin **không** mở cửa sổ đăng nhập khi NETLOAD/khởi động AutoCAD
- Cửa sổ đăng nhập chỉ hiện khi gõ `MEPDB`, `MEPHVAC` hoặc `MEPLOGIN`
- Nếu cache offline còn hạn, lệnh vẫn chạy được mà không cần đăng nhập lại

---

## [0.2.0] - 2026-08-04 — Admin Control UI

### Thêm mới
- Trang quản trị `/admin` để tắt/bật trực tiếp:
  - User (Active/Blocked) theo SĐT bất kỳ
  - Thiết bị (Active/Blocked)
  - Chức năng tool `MEPDB` / `MEPHVAC`
  - Mở chuyển máy (`release-device`)
  - Gia hạn license +30 ngày
- API tương thích Swagger đang dùng:
  - `GET /api/Admin/overview`
  - `PUT /api/Admin/users/{userId}/status`
  - `PUT /api/Admin/devices/{deviceId}/status`
  - `PUT /api/Admin/licenses/{licenseId}/extend`
  - `PUT /api/Admin/users/{userId}/features/{featureCode}`
- F5 mặc định mở `/admin` thay vì chỉ Swagger

---

## [0.1.0] - 2026-08-04 — License Control MVP

### Thêm mới
- `MepPanel.LicenseServer` (ASP.NET Core + SQLite + Swagger)
- Đăng nhập bằng số điện thoại + OTP thử nghiệm (`123456`)
- JWT access token
- Admin API (`X-Admin-ApiKey`):
  - tạo/liệt kê user
  - Active / Block tài khoản
  - mở/khóa chức năng plugin (`MEPDB`, `MEPHVAC`)
  - `release-device` để cho phép chuyển máy
  - khóa/mở từng thiết bị
- Luật **1 SĐT = 1 máy Active**
- Client AutoCAD:
  - `LicenseApiClient`, `LicenseSession`, `LicenseCache`, `LicenseGuard`
  - Login window
  - Lệnh: `MEPLOGIN`, `MEPDB`, `MEPHVAC`, `MEPLOGOUT`
- Integration tests cho block user / seat limit / feature lock
- Tài liệu: `docs/LICENSE_ADMIN_GUIDE.md`

### Gói phát hành
- `MepPanel-LicenseServer-v0.1.0.zip`
- Source plugin AutoCAD kèm theo (build trên máy có AutoCAD 2021)

---

## [Unreleased] — kế hoạch phiên bản sau

### 0.2.0 — Vận hành thử nghiệm ổn định
- Trang Admin web đơn giản (không chỉ Swagger)
- Heartbeat định kỳ từ plugin
- Nhật ký đăng nhập/revoke dễ lọc theo SĐT
- Cấu hình `maxDevices` theo từng gói license trên UI

### 0.3.0 — Production cứng hơn
- SMS OTP thật
- HTTPS + domain/VPS
- PostgreSQL thay SQLite
- Rate-limit + khóa tạm khi nhập OTP sai nhiều lần
