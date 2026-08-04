# Hướng dẫn Admin — Khóa/mở tài khoản & chức năng plugin

## Chạy máy chủ

1. Mở `MepPanelMvp.sln` trong Visual Studio  
2. Set startup project: `MepPanel.LicenseServer`  
3. F5 → Swagger: `https://localhost:7024/swagger`

Admin API key mặc định (đổi trong `appsettings.json`):

```text
X-Admin-ApiKey: MEP-PANEL-ADMIN-TEST-2026
```

OTP thử nghiệm: `123456`

User seed sẵn:

- `0900000001` — features: MEPDB, MEPHVAC  
- `0900000002` — features: MEPDB  

## Luật kiểm soát

- Mỗi SĐT chỉ dùng **1 máy Active**
- Muốn chuyển máy: Admin gọi `release-device`
- Admin có thể `Active` / `Blocked` user
- Admin có thể mở/khóa từng chức năng (`MEPDB`, `MEPHVAC`)
- Plugin lệnh `MEPDB` / `MEPHVAC` bị chặn nếu feature không được cấp

## API Admin chính

| Mục đích | Method | Path |
|---|---|---|
| Danh sách user | GET | `/api/admin/users` |
| Tạo user | POST | `/api/admin/users` |
| Khóa/mở user | PATCH | `/api/admin/users/{id}/status` |
| Mở/khóa chức năng | PATCH | `/api/admin/users/{id}/features` |
| Mở chuyển máy | POST | `/api/admin/users/{id}/release-device` |
| Khóa/mở 1 thiết bị | PATCH | `/api/admin/devices/{id}/status` |

### Ví dụ tạo user

```json
{
  "phoneNumber": "0912345678",
  "displayName": "Ky su A",
  "maxDevices": 1,
  "features": ["MEPDB", "MEPHVAC"]
}
```

### Ví dụ khóa user

```json
{ "status": "Blocked" }
```

### Ví dụ chỉ mở MEPDB

```json
{ "features": ["MEPDB"] }
```

## Test trên máy bạn

1. F5 LicenseServer  
2. AutoCAD 2021 → `NETLOAD` `MepPanel.AutoCAD.dll`  
3. Login SĐT `0900000001` / OTP `123456`  
4. Gõ `MEPDB`, `MEPHVAC`  
5. Trên Swagger: Block user hoặc bỏ feature → lệnh plugin bị chặn  
6. Activate máy thứ 2 cùng SĐT → bị từ chối  
7. `release-device` → máy mới login được  
