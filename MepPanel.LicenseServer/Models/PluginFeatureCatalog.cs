namespace MepPanel.LicenseServer.Models;

public sealed record PluginFeatureInfo(string Code, string LabelVi, string Group);

/// <summary>Tên tiếng Việt khớp panel MEP DRAWING TOOL.</summary>
public static class PluginFeatureCatalog
{
    public const string EntryCode = PluginFeatures.Entry;

    public static readonly PluginFeatureInfo Entry = new(
        EntryCode,
        "MEPDB — Lệnh vào plugin",
        "Vào plugin");

    public static readonly PluginFeatureInfo[] SubItems =
    [
        // Chọn hệ thống
        new("MEPDBDRAW", "Hệ điện", "Chọn hệ thống"),
        new("MEPHVAC", "Điều hòa", "Chọn hệ thống"),
        new("MEPDBWATER", "Hệ nước", "Chọn hệ thống"),
        new("MEPDBSMOKE", "Báo cháy", "Chọn hệ thống"),

        // Chức năng chung
        new("MEPSELAYER", "Chọn cùng layer", "Chức năng chung"),
        new("MEPDBCONFIG", "Cấu hình tủ", "Chức năng chung"),
        new("MEPDBEXPORT", "Xuất CSV", "Chức năng chung"),

        // Tủ điện / DB
        new("MEPDBCABINET2D", "Vẽ tủ điện", "Tủ điện / DB"),
        new("MEPDBUPDATE", "Cập nhật tủ", "Tủ điện / DB"),
        new("MEPDBEXCEL", "Xuất Excel", "Tủ điện / DB"),
        new("MEPDBCABINETVIEWS", "Mặt chiếu tủ", "Tủ điện / DB"),
        new("MEPDBPOWER", "Bố trí động lực", "Tủ điện / DB"),
        new("MEPDB3P4W", "Sơ đồ 3P-4D+E", "Tủ điện / DB"),

        // HVAC (trong panel Điều hòa)
        new("MEPHVACDRAW", "Vẽ HVAC", "Điều hòa"),
        new("MEPHVACCONFIG", "Cấu hình HVAC", "Điều hòa"),
        new("MEPHVACSMOKE", "Khói HVAC", "Điều hòa"),

        // Khác (tool nâng cao)
        new("MEPDBCABINET3D", "Tủ 3D", "Khác"),
        new("MEPDBDUPLICATE", "Nhân bản tủ", "Khác"),
        new("MEPDBEDIT", "Sửa tủ", "Khác"),
        new("MEPDBHELP", "Trợ giúp", "Khác"),
        new("MEPDBKNOWLEDGE", "Thư viện điện", "Khác"),
        new("MEPDBREALRENDER", "Render wiring thật", "Khác"),
        new("MEPDBREALWIRING", "Wiring thật", "Khác"),
        new("MEPDBRENDER", "Render tủ", "Khác"),
        new("MEPDBUNFOLD", "Triển khai tủ", "Khác"),
        new("MEPDEVICEBLOCKS", "Block thiết bị", "Khác"),
    ];

    public static string? GetLabelVi(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (string.Equals(code, EntryCode, StringComparison.OrdinalIgnoreCase))
        {
            return Entry.LabelVi;
        }

        return SubItems.FirstOrDefault(x =>
            string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))?.LabelVi;
    }

    public static IReadOnlyList<object> ToApiList() =>
        SubItems
            .GroupBy(x => x.Group)
            .Select(g => new
            {
                group = g.Key,
                items = g.Select(x => new { code = x.Code, labelVi = x.LabelVi }).ToArray()
            })
            .ToArray<object>();

    public static Dictionary<string, string> AllLabels()
    {
        var dict = SubItems.ToDictionary(
            x => x.Code,
            x => x.LabelVi,
            StringComparer.OrdinalIgnoreCase);
        dict[EntryCode] = Entry.LabelVi;
        return dict;
    }
}
