# Quy ước phiên bản & đóng gói

## Số phiên bản hiện tại

Xem file gốc: [`VERSION`](../VERSION)  
Cấu hình MSBuild: [`Directory.Build.props`](../Directory.Build.props)

Khi phát hành bản mới:

1. Sửa `VERSION` (ví dụ `0.2.0`)
2. Sửa `MepPanelVersion` trong `Directory.Build.props` cho khớp
3. Cập nhật [`CHANGELOG.md`](../CHANGELOG.md)
4. Chạy đóng gói:

```bash
./scripts/pack-release.sh
# hoặc chỉ định version:
./scripts/pack-release.sh 0.2.0
```

5. Lấy file trong `dist/`:
   - `MepPanel-LicenseServer-vX.Y.Z.zip` — chỉ máy chủ
   - `MepPanel-vX.Y.Z.zip` — máy chủ + source plugin + docs
   - `SHA256-vX.Y.Z.txt` — kiểm tra toàn vẹn

## Ý nghĩa từng phiên bản (roadmap ngắn)

| Version | Mục tiêu |
|---|---|
| **0.1.0** | MVP kiểm soát license (đang có) |
| **0.2.0** | Admin UI + heartbeat ổn định hơn |
| **0.3.0** | SMS thật + deploy production |

## Gắn với Git (tuỳ chọn)

```bash
git tag -a v0.1.0 -m "MepPanel v0.1.0 License Control MVP"
git push origin v0.1.0
```
