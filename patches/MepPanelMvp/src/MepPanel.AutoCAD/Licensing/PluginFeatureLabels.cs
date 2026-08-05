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
                default: return featureCode;
            }
        }
    }
}
