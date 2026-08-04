# Changelog

Mọi thay đổi đáng chú ý của MepPanel được ghi theo [Semantic Versioning](https://semver.org/lang/vi/).

Định dạng phiên bản: `MAJOR.MINOR.PATCH`

- **MAJOR**: thay đổi phá vỡ tương thích
- **MINOR**: thêm tính năng, vẫn tương thích
- **PATCH**: sửa lỗi

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
