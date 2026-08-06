# Thư viện ống AMC — Hệ nước & PCCC

Template `AMC_TEMPLATE_RV29.dwg` chứa block phụ kiện ống các hệ. Plugin hỗ trợ:

## Quy trình khuyên dùng

1. Mở bản vẽ làm việc (hoặc template AMC)
2. **`MEPDB`** → nhóm **Ống & phụ kiện (AMC)**
3. Bấm **Nạp thư viện AMC** — import block từ file thư viện vào bản vẽ hiện tại
4. **Vẽ ống nước / PCCC + co tự động** — chọn nhiều điểm, Enter kết thúc → co 90 tại góc
5. **Đặt phụ kiện** — chọn loại (Co90, Tee, Van, Sprinkler, ChuaChay…), chọn vị trí + hướng

## Phụ kiện hệ nước

| Loại | Từ khóa | Block AMC (nếu có) | Tự tạo |
|------|---------|-------------------|--------|
| Co 90 | Co90 | *CO*90*, *ELBOW*, *ONG*CO* | MEP_WATER_FIT_ELBOW90 |
| Chữ T | Tee | *TEE*, *CHU*T* | MEP_WATER_FIT_TEE |
| Co giảm | Giam | *REDUC*, *GIAM* | MEP_WATER_FIT_REDUCER |
| Van | Van | *VALVE*, *VAN* | MEP_WATER_FIT_VALVE |
| Nút bít | Nut | *CAP*, *NUOT* | MEP_WATER_FIT_CAP |

## Phụ kiện PCCC

| Loại | Từ khóa | Block AMC | Tự tạo |
|------|---------|-----------|--------|
| Co 90 | Co90 | *PCCC*CO*, *FIRE*ELBOW* | MEP_FIRE_FIT_ELBOW90 |
| Chữ T | Tee | *FIRE*TEE* | MEP_FIRE_FIT_TEE |
| Van | Van | *PCCC*VAN* | MEP_FIRE_FIT_VALVE |
| Đầu phun | Sprinkler | *SPRINK*, *SPK*, *PHUN* | MEP_FIRE_FIT_SPRINKLER |
| Chữa cháy | ChuaChay | *HYDR*, *CHUA*CHAY* | MEP_FIRE_FIT_HYDRANT |
| Đầu báo | DauBao | *DETEC*, *BAO*CHAY* | MEP_FIRE_FIT_DETECTOR |

**Ưu tiên:** block có sẵn trong bản vẽ (sau khi nạp AMC) → nếu không có → plugin **tự tạo** block geometry.

## Cấu hình đường dẫn thư viện

`MepPanel.config.json`:

```json
{
  "pipeLibraryDwg": "samples/templates/AMC_TEMPLATE_RV29.dwg"
}
```

Đường dẫn tương đối so với thư mục `Contents` của bundle.

## Layer

| Hệ | Layer |
|----|-------|
| Nước | `MEP_WATER` |
| PCCC | `MEP_FIRE` |
