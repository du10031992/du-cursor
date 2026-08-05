namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>Tên tiếng Việt — khớp panel MEP DRAWING TOOL.</summary>
    public static class PluginFeatureLabels
    {
        public static string GetDisplayName(string featureCode)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
            {
                return featureCode;
            }

            switch (featureCode.Trim().ToUpperInvariant())
            {
                case "MEPDB": return "MEPDB — Lệnh vào plugin";
                case "MEPDBDRAW": return "Hệ điện";
                case "MEPHVAC": return "Điều hòa";
                case "MEPDBWATER": return "Hệ nước";
                case "MEPDBSMOKE": return "Báo cháy";
                case "MEPSELAYER": return "Chọn cùng layer";
                case "MEPDBCONFIG": return "Cấu hình tủ";
                case "MEPDBEXPORT": return "Xuất CSV";
                case "MEPDBCABINET2D": return "Vẽ tủ điện";
                case "MEPDBUPDATE": return "Cập nhật tủ";
                case "MEPDBEXCEL": return "Xuất Excel";
                case "MEPDBCABINETVIEWS": return "Mặt chiếu tủ";
                case "MEPDBPOWER": return "Bố trí động lực";
                case "MEPDB3P4W": return "Sơ đồ 3P-4D+E";
                case "MEPHVACDRAW": return "Vẽ HVAC";
                case "MEPHVACCONFIG": return "Cấu hình HVAC";
                case "MEPHVACSMOKE": return "Khói HVAC";
                case "MEPDBCABINET3D": return "Tủ 3D";
                case "MEPDBDUPLICATE": return "Nhân bản tủ";
                case "MEPDBEDIT": return "Sửa tủ";
                case "MEPDBHELP": return "Trợ giúp";
                case "MEPDBKNOWLEDGE": return "Thư viện điện";
                case "MEPDBREALRENDER": return "Render wiring thật";
                case "MEPDBREALWIRING": return "Wiring thật";
                case "MEPDBRENDER": return "Render tủ";
                case "MEPDBUNFOLD": return "Triển khai tủ";
                case "MEPDEVICEBLOCKS": return "Block thiết bị";
                default: return featureCode;
            }
        }
    }
}
