using System.Collections.Generic;

namespace MepPanel.Core.Standards
{
    /// <summary>Danh mục tiêu chuẩn VN / quốc tế cho thi công MEP thực tế.</summary>
    public static class MepStandardsCatalog
    {
        public static IReadOnlyList<MepStandardInfo> GetBySystem(MepSystemKind system)
        {
            switch (system)
            {
                case MepSystemKind.Electrical: return Electrical;
                case MepSystemKind.Water: return Water;
                case MepSystemKind.Fire: return Fire;
                case MepSystemKind.Hvac: return Hvac;
                default: return new MepStandardInfo[0];
            }
        }

        public static string GetSystemTitleVi(MepSystemKind system)
        {
            switch (system)
            {
                case MepSystemKind.Electrical: return "Hệ điện";
                case MepSystemKind.Water: return "Hệ cấp thoát nước";
                case MepSystemKind.Fire: return "Hệ PCCC";
                case MepSystemKind.Hvac: return "Hệ điều hòa thông gió";
                default: return system.ToString();
            }
        }

        public static readonly MepStandardInfo[] Electrical =
        {
            new MepStandardInfo("TCVN 9207:2012", "Hệ thống lưới điện hạ áp", "TCVN", "Thiết kế lưới hạ áp đến 1 kV", "Chọn tiết diện dây, bảo vệ, lắp đặt tủ phân phối"),
            new MepStandardInfo("QCVN 4:2009/BCT", "Quy chuẩn kỹ thuật điện hạ áp", "BCT", "An toàn điện công trình dân dụng", "Kiểm tra vỏ kim loại, nối đất, IP phòng điện"),
            new MepStandardInfo("TCVN 9393:2012", "Lắp đặt hệ thống nối đất", "TCVN", "Tiếp địa, chống sét cơ bản", "Điện trở nối đất, cọc, busbar PE"),
            new MepStandardInfo("IEC 60364", "Hệ thống điện hạ áp", "IEC", "Thiết kế, chọn thiết bị, bảo vệ", "Tham chiếu quốc tế khi không có TCVN chi tiết"),
            new MepStandardInfo("TCVN 7806:2008", "Thiết bị đóng cắt hạ áp", "TCVN", "MCB, MCCB, Contactor", "Định mức In, Icu, curve B/C/D")
        };

        public static readonly MepStandardInfo[] Water =
        {
            new MepStandardInfo("TCVN 4513:1988", "Cấp nước bên trong — Thiết kế", "TCVN", "Đường ống cấp nước sinh hoạt", "Tốc độ ống, áp lực, chọn đường kính"),
            new MepStandardInfo("QCVN 01:2009/BXD", "Quy chuẩn nước sạch công trình", "BXD", "Chất lượng nước cấp", "Vật liệu ống PPR, HDPE, đồng thỏa mãn QCVN"),
            new MepStandardInfo("TCVN 4474:2012", "Thoát nước trong nhà", "TCVN", "Ống thoát, thoát sàn, thông hơi", "Độ dốc ống, vent stack, bẫy mùi"),
            new MepStandardInfo("NFPA 13 (tham khảo)", "Sprinkler — không áp dụng trực tiếp nước sinh hoạt", "NFPA", "Phân biệt hệ cấp nước vs PCCC", "Tách van, bồn, đường ống riêng PCCC"),
            new MepStandardInfo("ASHRAE Pipe Sizing", "Khuyến nghị tốc độ ống nước", "ASHRAE", "v = 1.0–2.5 m/s cấp; 0.6–1.2 m/s thoát", "Giảm tiếng ồn, cavitation")
        };

        public static readonly MepStandardInfo[] Fire =
        {
            new MepStandardInfo("QCVN 06:2022/BXD", "PCCC nhà và công trình", "BXD", "Phân loại cháy, thoát nạn, PCCC", "Bắt buộc thi công công trình dân dụng VN"),
            new MepStandardInfo("TCVN 3890:2009", "Phân loại công trình theo yêu cầu PCCC", "TCVN", "Nhóm I–IV, chiều cao, diện tích", "Xác định hệ sprinkler / hydrant"),
            new MepStandardInfo("TCVN 6160:1996", "Hệ thống chữa cháy bằng nước", "TCVN", "Sprinkler, vòi chữa cháy", "Áp lực, lưu lượng tối thiểu"),
            new MepStandardInfo("NFPA 13", "Hệ thống sprinkler", "NFPA", "Mật độ phun, K-factor", "Q = K√P; thiết kế vùng nguy hiểm"),
            new MepStandardInfo("NFPA 14", "Hệ thống đứng & vòi", "NFPA", "Trụ chữa cháy, lưu lượng đồng thời", "2.5 l/s vòi trong nhà; 5 l/s ngoài trời (tham khảo)")
        };

        public static readonly MepStandardInfo[] Hvac =
        {
            new MepStandardInfo("TCVN 5687:2010", "Thông gió và điều hòa không khí", "TCVN", "Thiết kế hệ thống HVAC công trình", "Lưu lượng gió tươi, cân bằng nhiệt"),
            new MepStandardInfo("TCVN 9391:2012", "Điều hòa không khí — Thiết kế", "TCVN", "Tải lạnh, chọn máy, ống gió", "Công trình thương mại, văn phòng"),
            new MepStandardInfo("ASHRAE Fundamentals", "Tải nhiệt, psychrometrics", "ASHRAE", "Q sensible/latent, CFM/L/s", "Chuẩn quốc tế tính tải"),
            new MepStandardInfo("SMACNA", "Ống gió và phụ kiện", "SMACNA", "Tốc độ gió trong ống, tiết diện", "v = 2.5–10 m/s tùy ứng dụng"),
            new MepStandardInfo("TCVN 2722:2023", "Điều kiện môi trường trong phòng", "TCVN", "Nhiệt độ, độ ẩm, tốc độ gió", "Tiêu chuẩn thoải mái người dùng")
        };
    }
}
