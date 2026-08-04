# Changelog

Mọi thay đổi đáng chú ý của MepPanel được ghi theo [Semantic Versioning](https://semver.org/lang/vi/).

Định dạng phiên bản: `MAJOR.MINOR.PATCH`

- **MAJOR**: thay đổi phá vỡ tương thích
- **MINOR**: thêm tính năng, vẫn tương thích
- **PATCH**: sửa lỗi

---

## [0.2.2] - 2026-08-04 — Sửa NETLOAD (WinForms thay WPF)

### Sửa lỗi
- Đổi LoginWindow từ WPF sang **WinForms** — AutoCAD NETLOAD không còn fail im lặng
- Plugin chính đăng ký lệnh `MEPSTATUS`, `MEPLOGIN`, `MEPDB`, `MEPHVAC` ổn định

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
