# Deploy License Server lên VPS (HTTPS + SMS OTP)

Hướng dẫn triển khai `MepPanel.LicenseServer` trên VPS Linux với Docker, nginx và Let's Encrypt.

## Yêu cầu

- VPS Ubuntu 22.04+ (1 vCPU, 1 GB RAM trở lên)
- Domain trỏ A record về IP VPS (ví dụ `license.your-domain.com`)
- Nhà cung cấp SMS có webhook HTTP (POST JSON)

## 1. Chuẩn bị VPS

```bash
sudo apt update && sudo apt install -y docker.io docker-compose-plugin git
sudo usermod -aG docker $USER
# đăng xuất/đăng nhập lại để dùng docker không cần sudo
```

Clone repo:

```bash
git clone https://github.com/du10031992/du-cursor.git
cd du-cursor/deploy
cp .env.example .env
nano .env
```

Điền trong `.env`:

| Biến | Mô tả |
|---|---|
| `JWT_KEY` | Chuỗi bí mật ≥ 32 ký tự |
| `ADMIN_API_KEY` | Key Admin (header `X-Admin-ApiKey`) |
| `SMS_WEBHOOK_URL` | URL webhook SMS của bạn |
| `SMS_API_KEY` | API key gửi kèm header `X-Api-Key` (nếu có) |

## 2. SSL (Let's Encrypt)

Sửa `nginx.conf`: thay `license.your-domain.com` bằng domain thật.

Lần đầu (chỉ HTTP, lấy cert):

```bash
mkdir -p certs
# Tạm comment block HTTPS trong nginx.conf, chỉ giữ port 80
docker compose up -d license-server
# Dùng certbot trên host hoặc copy cert vào deploy/certs/
```

Sau khi có `fullchain.pem` và `privkey.pem` trong `deploy/certs/`:

```bash
docker compose up -d
```

Admin UI: `https://license.your-domain.com/admin`

## 3. SMS webhook

Server gửi POST JSON tới `Sms:WebhookUrl`:

```json
{
  "phoneNumber": "0912345678",
  "message": "Ma OTP MepPanel cua ban la 123456. Hieu luc 5 phut.",
  "otp": "123456"
}
```

Header tùy chọn: `X-Api-Key: {Sms:ApiKey}`

**Test mode:** đặt `LicenseSettings__TestMode=true` → OTP cố định `123456`, không gửi SMS.

## 4. Cấu hình plugin AutoCAD (production)

Trong thư mục bundle plugin (`Contents/`), tạo `MepPanel.config.json`:

```json
{
  "licenseServerUrl": "https://license.your-domain.com/"
}
```

Mẫu có sẵn: `bundle/MepPanel.Plugin.bundle/Contents/MepPanel.config.json.example`

Sau khi sửa, chạy lại `scripts/install-plugin-bundle.ps1` trên máy Windows.

## 5. Kiểm tra

```bash
curl -s https://license.your-domain.com/api/Auth/request-otp \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"0912345678"}'
```

- User phải được Admin tạo trước trong `/admin`
- Production (`TestMode=false`): OTP gửi qua SMS
- Dev/test: `TestMode=true` → response có `testOtp`

## 6. Backup database

SQLite nằm trong volume Docker `/data/mep-panel-license.db`:

```bash
docker compose exec license-server ls -la /data
docker cp $(docker compose ps -q license-server):/data/mep-panel-license.db ./backup-$(date +%F).db
```

## Chạy không Docker

```bash
cd MepPanel.LicenseServer
export ASPNETCORE_ENVIRONMENT=Production
export Jwt__Key="your-key"
export Admin__ApiKey="your-admin-key"
export Sms__WebhookUrl="https://..."
dotnet publish -c Release -o ./publish
dotnet ./publish/MepPanel.LicenseServer.dll --urls "http://0.0.0.0:8080"
```

Đặt nginx reverse proxy phía trước như `deploy/nginx.conf`.
