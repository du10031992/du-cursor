# Lộ trình hoàn thiện Plugin MEP DRAWING TOOL

> Tạm dừng mở rộng License Server — tập trung chức năng vẽ trên AutoCAD.

## Trạng thái hiện tại (stub trong `src/`)

| Nhóm | Chức năng | Mã | Trạng thái |
|------|-----------|-----|------------|
| Chọn hệ thống | Hệ điện | MEPDBDRAW | ✅ Thông tin + nhóm DB |
| | Điều hòa | MEPHVAC | ✅ Ống thẳng / chữ L + elbow |
| | Hệ nước | MEPDBWATER | ✅ Ống nước polyline |
| | Báo cháy | MEPDBSMOKE | ✅ Đặt block detector |
| Chức năng chung | Chọn cùng layer | MEPSELAYER | ✅ |
| | Cấu hình tủ | MEPDBCONFIG | 🔶 Hướng dẫn (chưa WPF) |
| | Xuất CSV | MEPDBEXPORT | 🔶 Hướng dẫn |
| Tủ điện / DB | Vẽ tủ điện | MEPDBCABINET2D | ✅ Block thiết bị |
| | Cập nhật tủ | MEPDBUPDATE | 🔶 Chọn block + ghi chú |
| | Xuất Excel | MEPDBEXCEL | 🔶 Hướng dẫn |
| | Mặt chiếu tủ | MEPDBCABINETVIEWS | ✅ Hình chữ nhật + nhãn |
| | Bố trí động lực | MEPDBPOWER | ✅ Dây mẫu |
| | Sơ đồ 3P-4D+E | MEPDB3P4W | 🔶 Hướng dẫn |

**Chú thích:** ✅ = có lệnh vẽ/hành động trên bản vẽ · 🔶 = stub / cần import source WPF đầy đủ

## Panel thống nhất

- Lệnh **`MEPDB`** mở palette **MEP DRAWING TOOL** (3 nhóm như trên).
- Các lệnh cũ `MEPHVAC` vẫn hoạt động (backward compatible).

## Bước tiếp theo (ưu tiên)

1. **Import source MepPanelMvp** vào repo (`src/MepPanelMvp/` hoặc submodule) — cần cho WPF tủ/HVAC đầy đủ.
2. Hoàn thiện **HVAC**: ống L, reducer, tag kích thước.
3. **Xuất Excel/CSV** thật từ thuộc tính block tủ.
4. **Sơ đồ 3P-4D+E** — vẽ busbar + nhánh MCB.
5. Gom test tự động cho drawing services (mock Document nếu cần).

## Build & cài

```powershell
cd C:\Users\DU_COMPUTER\Desktop\AI
git pull
.\scripts\build-plugin-release.ps1
# Đóng AutoCAD trước khi install
.\scripts\install-plugin-bundle.ps1
```

Trong AutoCAD: `MEPDB` → panel 3 nhóm.
