# Tiêu chuẩn & công thức MEP — Thi công thực tế

> Tham chiếu cho module **Tiêu chuẩn & Tính toán** trong MEP DRAWING TOOL.  
> Không thay thế thẩm tra thiết kế bởi kỹ sư có chứng chỉ.

---

## 1. Hệ điện

### Tiêu chuẩn

| Mã | Nội dung | Ứng dụng thi công |
|----|----------|-------------------|
| TCVN 9207:2012 | Lưới hạ áp | Chọn cáp, MCB, tủ phân phối |
| QCVN 4:2009/BCT | An toàn điện | Kiểm tra nối đất, vỏ tủ |
| TCVN 9393:2012 | Hệ nối đất | Điện trở đất ≤ 10 Ω (tham khảo) |
| IEC 60364 | Thiết kế hạ áp | Bảo vệ quá tải, chọn In |
| TCVN 7806:2008 | Thiết bị đóng cắt | MCB curve B/C/D |

### Công thức chính

| Công thức | Biểu thức | Ghi chú |
|-----------|-----------|---------|
| Dòng 3 pha | `I = P / (√3 × U × cosφ)` | P(W), U=380V |
| Sụt áp | `ΔU% = 2×I×L×ρ / (S×U) × 100` | ρ=0.0175 Ω·mm²/m (đồng), ≤3% |
| Công suất | `P = √3 × U × I × cosφ` | Kiểm tra tải tủ |

---

## 2. Hệ cấp thoát nước

### Tiêu chuẩn

| Mã | Nội dung |
|----|----------|
| TCVN 4513:1988 | Cấp nước trong — thiết kế |
| QCVN 01:2009/BXD | Chất lượng nước cấp |
| TCVN 4474:2012 | Thoát nước trong nhà |
| ASHRAE | Tốc độ ống khuyến nghị |

### Công thức

| Công thức | Biểu thức | Khuyến nghị |
|-----------|-----------|-------------|
| Lưu lượng | `Q = A × v` | Q(L/s), DN(mm) |
| Tốc độ | `v = Q / A` | Cấp: 1.0–2.5 m/s |
| Darcy-Weisbach | `hf = f×(L/D)×v²/(2g)` | Chọn bơm, cột áp |

---

## 3. Hệ PCCC

### Tiêu chuẩn

| Mã | Nội dung |
|----|----------|
| QCVN 06:2022/BXD | PCCC công trình (bắt buộc VN) |
| TCVN 3890:2009 | Phân loại công trình |
| TCVN 6160:1996 | Chữa cháy bằng nước |
| NFPA 13 | Sprinkler |
| NFPA 14 | Trụ & vòi chữa cháy |

### Công thức

| Công thức | Biểu thức |
|-----------|-----------|
| Sprinkler | `Q = K × √P` (L/min, P bar) |
| Mật độ phun | `D = Q / A` (L/min·m²) |
| Vòi CC | `Q = n × q` (L/s) |
| Bồn dự trữ | `V = Q × t` |

---

## 4. Hệ điều hòa thông gió

### Tiêu chuẩn

| Mã | Nội dung |
|----|----------|
| TCVN 5687:2010 | Thông gió & điều hòa |
| TCVN 9391:2012 | Thiết kế điều hòa |
| TCVN 2722:2023 | Môi trường trong phòng |
| ASHRAE Fundamentals | Tải nhiệt |
| SMACNA | Ống gió |

### Công thức

| Công thức | Biểu thức |
|-----------|-----------|
| Tải hiển | `Q = m × cp × ΔT` |
| Lưu lượng gió | `L = Q / (ρ × cp × ΔT)` |
| Tốc độ ống gió | `v = L / A` (2.5–10 m/s) |
| RT | `1 RT = 3.517 kW` |

---

## Sử dụng trong plugin

1. `MEPDB` → **Tiêu chuẩn & Tính toán**
2. **TC + CT** — xem tiêu chuẩn + công thức (hộp thoại + command line)
3. **Tính toán** — nhập số liệu trên command line AutoCAD

Source code: `src/MepPanel.Core/Standards/`, `Calculations/`
