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

Production khuyến nghị dùng VPS/domain HTTPS theo [DEPLOY_VPS.md](DEPLOY_VPS.md).

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

## 4. Mở firewall

- VPS: mở cổng `443` (HTTPS).
- Server trong LAN: dùng URL `https://<ip-hoặc-tên-máy>/`, mở firewall cổng HTTPS và cài certificate tin cậy trên các máy client.
- Không công khai Admin API key và không dùng OTP test `123456` trong production.
