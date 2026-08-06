# Lộ trình thương mại — MEP DRAWING TOOL

> Hướng phát triển sản phẩm phần mềm MEP trên **internet**, **máy chủ**, và mô hình kinh doanh bền vững.

---

## 1. Định vị sản phẩm

| Hạng mục | Nội dung |
|----------|----------|
| **Tên** | MEP DRAWING TOOL |
| **Đối tượng** | Kỹ sư MEP, nhà thầu điện/nước/PCCC/HVAC, văn phòng thiết kế |
| **Giá trị** | Vẽ nhanh trên AutoCAD + tiêu chuẩn VN + tính toán + thư viện ống AMC |
| **Khác biệt** | Tiếng Việt, TCVN/QCVN tích hợp, license theo máy/SĐT, Admin cloud |

---

## 2. Kiến trúc thương mại (Cloud + Client)

```
                    ┌─────────────────────────────────────┐
                    │     Internet / Cloud (Azure/VPS)     │
                    ├─────────────────────────────────────┤
                    │  • License Server (đã có)             │
                    │  • Admin Portal /admin                │
                    │  • API tính toán MEP (mới)            │
                    │  • Thư viện block / template CDN      │
                    │  • Cập nhật plugin OTA                │
                    │  • Dashboard doanh nghiệp (B2B)       │
                    └──────────────┬──────────────────────┘
                                   │ HTTPS
         ┌─────────────────────────┼─────────────────────────┐
         │                         │                         │
   ┌─────▼─────┐           ┌───────▼───────┐         ┌───────▼───────┐
   │ AutoCAD   │           │ Web App       │         │ Mobile Admin  │
   │ Plugin    │           │ (tính toán,   │         │ (OTP, bật/tắt │
   │ Windows   │           │  báo giá)     │         │  feature)     │
   └───────────┘           └───────────────┘         └───────────────┘
```

---

## 3. Gói sản phẩm (Pricing tiers — đề xuất)

| Gói | Đối tượng | Tính năng | Giá tham khảo |
|-----|-----------|-----------|---------------|
| **Starter** | Cá nhân, 1 máy | Vẽ cơ bản, devMode off, 5 feature | 990k–1.5M VNĐ/năm |
| **Pro** | Văn phòng 3–10 máy | Full 13 feature + AMC library + tính toán | 3–8M VNĐ/năm |
| **Enterprise** | Nhà thầu lớn | Admin B2B, SSO, API, template riêng | Báo giá theo seat |
| **Cloud Calc** | Không cần AutoCAD | Web tính toán MEP + xuất PDF | Freemium / 200k/tháng |

---

## 4. Hạ tầng máy chủ (đã có + mở rộng)

### Đã triển khai trong repo

| Thành phần | Công nghệ | URL mẫu |
|------------|-----------|---------|
| License Server | ASP.NET Core 8 | `https://your-domain.com` |
| Admin Control | Static + API | `/admin` |
| OTP / JWT | TestMode + production | API `/api/auth/*` |
| Feature flags | SQLite / SQL Server | Per user, per device |

### Mở rộng thương mại (Phase 2–4)

| Phase | Hạng mục | Mô tả |
|-------|----------|--------|
| **2** | **API Tính toán** | REST `/api/calc/electrical`, `/water`, `/fire`, `/hvac` — gọi từ plugin & web |
| **2** | **CDN thư viện** | Host `AMC_TEMPLATE_RV29.dwg`, block packs theo hãng — tải qua API |
| **3** | **OTA Update** | Plugin check version → tải bundle mới từ server (đã có manifest) |
| **3** | **Web Portal** | Khách hàng: đăng ký, thanh toán (VNPay/Momo), tải plugin |
| **4** | **Multi-tenant Admin** | Mỗi công ty 1 tenant: user, máy, template riêng |
| **4** | **Telemetry** | Feature usage (ẩn danh) — upsell Pro |

---

## 5. Kênh phân phối Internet

| Kênh | Hành động |
|------|-----------|
| **Website sản phẩm** | Landing page, video demo AutoCAD, bảng giá |
| **YouTube / TikTok** | Hướng dẫn MEPDB, vẽ ống AMC, tính PCCC |
| **Facebook group MEP VN** | Hỗ trợ, chia sẻ template |
| **Marketplace** | Autodesk App Store (dài hạn), website riêng (ngắn hạn) |
| **Đại lý** | Nhà thầu MEP bán kèm license cho công trình |

---

## 6. Mô hình doanh thu

```
Doanh thu = License subscription
          + Template / block packs (AMC mở rộng)
          + Cloud calculation API (pay-per-call)
          + Enterprise support & training
          + Custom template theo công trình (dịch vụ)
```

---

## 7. Roadmap kỹ thuật 12 tháng

| Quý | Mục tiêu |
|-----|----------|
| **Q1** | v1.0 plugin repo build + tiêu chuẩn/tính toán ✅ (đang làm) |
| **Q2** | API calc trên License Server; web demo tính toán |
| **Q3** | Import MepPanelMvp WPF; thanh toán VNPay; OTA update |
| **Q4** | App Store Autodesk; multi-tenant; mobile admin app |

---

## 8. Triển khai server production

### VPS tối thiểu (100 user)

| Thông số | Giá trị |
|----------|--------|
| CPU/RAM | 2 vCPU, 4 GB |
| OS | Ubuntu 22.04 / Windows Server |
| Domain + SSL | Let's Encrypt |
| DB | SQLite → PostgreSQL khi >500 user |

### Biến môi trường

```bash
ASPNETCORE_URLS=https://0.0.0.0:443
MEP_ADMIN_KEY=...
MEP_JWT_SECRET=...
ConnectionStrings__Default=...
```

### Docker (đề xuất)

```yaml
services:
  mep-license:
    image: meppanel/license-server:latest
    ports: ["443:443"]
  mep-cdn:
    image: nginx:alpine
    volumes: ["./templates:/usr/share/nginx/html/templates"]
```

---

## 9. Pháp lý & tuân thủ

- Giấy phép phần mềm: EULA tiếng Việt — cấm crack, 1 SĐT = N máy theo gói
- Tiêu chuẩn trong app mang tính **tham khảo** — disclaimer không thay thế thẩm tra
- Bảo mật: HTTPS, hash OTP, JWT expiry, Admin key riêng

---

## 10. KPI theo dõi

| KPI | Mục tiêu năm 1 |
|-----|----------------|
| User trả phí | 50–200 |
| MRR | 50–200 triệu VNĐ |
| Retention | >70% gia hạn |
| NPS | >40 |

---

## Liên hệ triển khai

- Repo: `du-cursor` branch `cursor/plugin-features-cc24`
- Build plugin: `docs/BUILD_PLUGIN_CAD.md`
- License: `docs/LICENSE_ADMIN_GUIDE.md`
- Kiến trúc app: `docs/MEP_APPLICATION_ARCHITECTURE.md`
