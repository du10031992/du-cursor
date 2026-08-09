# Chạy License Server không cần Visual Studio

Bấm F5 trong Visual Studio chỉ để chạy tạm khi phát triển: đóng Visual Studio là server tắt, và plugin mất kiểm soát. Dưới đây là các cách chạy thay thế.

| Cách | Khi nào dùng | Tự chạy khi bật máy |
|---|---|---|
| Windows Service | Máy chủ tại công ty, nhiều máy AutoCAD | Có |
| Chạy exe trực tiếp | Kiểm tra nhanh, tạm thời | Không |
| Docker/VPS | Nhiều chi nhánh, truy cập qua Internet | Có |
| Visual Studio F5 | Chỉ khi sửa code server | Không |

## Cách 1 — Windows Service (khuyến nghị)

Mở PowerShell **Run as Administrator** tại `C:\MepPanel\du-cursor`:

```powershell
.\scripts\publish-license-server.ps1
.\scripts\install-license-server-service.ps1
```

Kết quả:

- Server chạy nền, **tự bật khi khởi động Windows**.
- Không cần mở Visual Studio hay giữ cửa sổ PowerShell.
- Mặc định mở LAN và mở firewall cổng `5268`, nên máy AutoCAD khác kết nối được.
- Database dùng đúng file cố định `C:\MepPanel\du-cursor\MepPanel.LicenseServer\mep-panel-license.db`.

Script sẽ in URL cho máy khác, ví dụ `http://192.168.1.50:5268/`. Trên từng máy AutoCAD:

```powershell
.\Install-MepPanel-Client.ps1 -LicenseServerUrl "http://192.168.1.50:5268/"
```

Quản lý service:

```powershell
Get-Service MepPanelLicense
Stop-Service MepPanelLicense
Start-Service MepPanelLicense
```

Chỉ muốn dùng trên đúng máy chủ, không mở LAN:

```powershell
.\scripts\install-license-server-service.ps1 -LocalhostOnly
```

Sau khi sửa code server, publish lại rồi service tự khởi động lại:

```powershell
.\scripts\publish-license-server.ps1
```

## Cách 2 — Chạy exe trực tiếp

```powershell
.\scripts\publish-license-server.ps1
& 'C:\MepPanel\LicenseServer\MepPanel.LicenseServer.exe' --urls http://0.0.0.0:5268
```

Server chạy khi còn giữ cửa sổ này. Đóng cửa sổ là tắt.

## Cách 3 — Docker hoặc VPS

Dùng khi các máy AutoCAD ở nhiều nơi và cần truy cập qua Internet: xem [DEPLOY_VPS.md](DEPLOY_VPS.md) và [REMOTE_DEPLOYMENT.md](REMOTE_DEPLOYMENT.md).

## Lưu ý bảo mật

- LAN qua `http://` không mã hóa. Chỉ dùng trong mạng nội bộ tin cậy.
- Server không tự chuyển hướng sang HTTPS. Nếu tự chạy TLS trực tiếp trên server
  (không qua nginx), đặt `Security:RequireHttps=true` để buộc HTTPS.
- Ra Internet phải dùng `https://` với certificate hợp lệ: plugin kiểm tra certificate với server từ xa.
- Trước khi mở ra Internet, đổi `JWT_KEY`, `ADMIN_API_KEY` và giữ `TestMode=false`.
