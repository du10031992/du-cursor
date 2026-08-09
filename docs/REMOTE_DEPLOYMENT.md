# Triển khai MepPanel nhiều máy — Server trung tâm

## Kiến trúc

```text
Máy Admin/VPS (1 máy duy nhất)
  License Server + mep-panel-license.db + /admin
              ↑ HTTPS
Máy AutoCAD A, B, C...
  MepPanel Plugin bundle
```

- Chỉ Server trung tâm giữ user, quyền, thiết bị và `mep-panel-license.db`.
- Máy AutoCAD **không** chạy License Server, không chép file database.
- Admin đổi quyền ở `/admin`; lần gọi lệnh plugin tiếp theo sẽ hỏi Server và nhận quyền mới.

## 1. Chạy Server trung tâm

Server phải chạy nền, không phụ thuộc Visual Studio. Cách cài chạy nền tự khởi động
trên máy chủ Windows: [SERVER_ALWAYS_ON.md](SERVER_ALWAYS_ON.md).

Production qua Internet khuyến nghị dùng VPS/domain HTTPS theo [DEPLOY_VPS.md](DEPLOY_VPS.md).

URL ví dụ:

```text
https://license.congty.vn/
```

Không dùng `http://localhost:5268/` trên máy client: `localhost` của mỗi client là chính máy đó, không phải Server trung tâm.

Sao lưu database tại Server:

```text
mep-panel-license.db
mep-panel-license.db-wal
mep-panel-license.db-shm
```

Khi Server đang tắt, sao lưu cả ba file cùng lúc. Khi dùng Docker, database nằm trong volume `license-data`.

## 2. Cài plugin tại mỗi máy AutoCAD

Giải nén thư mục/gói `ClientPlugin`, mở PowerShell **Run as Administrator**, rồi chạy:

```powershell
.\Install-MepPanel-Client.ps1 -LicenseServerUrl "https://license.congty.vn/"
```

Script cài bundle vào:

```text
C:\ProgramData\Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle
```

và tạo cấu hình client:

```text
...\Contents\MepPanel.config.json
```

Chỉ file này trên client chứa URL Server. Không đặt database user trên client.

Gói `ClientPlugin` đã kèm DLL, thư viện AMC, renderer và ảnh thiết bị.
Nếu dùng Render PNG, cài thêm Python 3 + Pillow trên máy client:

```powershell
py -m pip install pillow
```

## 3. Kiểm soát từ xa

1. Admin vào `https://license.congty.vn/admin`.
2. Tạo user hoặc bật/tắt feature.
3. User mở AutoCAD, gõ `MEPDB`.
4. Plugin refresh quyền từ Server:
   - MEPDB tắt: không vào được plugin.
   - Feature phụ tắt: nút mờ/disabled và command cũng bị chặn.
   - Khóa user/máy: lần kiểm tra Server tiếp theo bị từ chối.

## 4. Kiểm tra kết nối trên máy client

```powershell
.\verify-client-server.ps1
```

Script đọc `MepPanel.config.json` đã cài, gọi `/health` của Server và cảnh báo nếu client còn trỏ `localhost`.

## 5. Bắt buộc trước khi mở ra Internet

| Việc | Lý do |
|---|---|
| Dùng HTTPS với certificate hợp lệ | Plugin kiểm tra certificate với Server từ xa; cert sai sẽ bị từ chối |
| Đổi `JWT_KEY` và `ADMIN_API_KEY` | Key mặc định trong repo là key thử nghiệm |
| Giữ `LicenseSettings__TestMode=false` | TestMode dùng OTP cố định `123456` và trả mã đó trong API |
| Cấu hình SMS thật | Không có SMS thì không gửi được OTP production |
| Giới hạn IP cho `/admin` trong `nginx.conf` | Trang Admin chỉ được bảo vệ bằng API key |

Server đã tự chặn: tối đa 5 lần xin OTP mỗi 15 phút cho một số điện thoại, và tạm khóa 15 phút sau 5 lần nhập OTP sai. `nginx.conf` giới hạn thêm tần số gọi `/api/Auth/`.

## 6. Mở firewall

- VPS: mở cổng `443` (HTTPS).
- Server trong LAN: dùng URL `https://<ip-hoặc-tên-máy>/`, mở firewall cổng HTTPS và cài certificate tin cậy trên các máy client.
- Không công khai Admin API key và không dùng OTP test `123456` trong production.
