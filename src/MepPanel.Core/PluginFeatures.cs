using System.Collections.Generic;

namespace MepPanel.Core;

/// <summary>
/// MEPDB = entry. Còn lại = sub-feature (khớp Admin License Server và panel WPF).
/// </summary>
public static class PluginFeatures
{
    public const string Entry = "MEPDB";
    public const string MepDb = Entry;

    // Chọn hệ thống
    public const string Draw = "MEPDBDRAW";
    public const string MepHvac = "MEPHVAC";
    public const string Water = "MEPDBWATER";
    public const string Smoke = "MEPDBSMOKE";

    // Chức năng chung
    public const string SeLayer = "MEPSELAYER";
    public const string Config = "MEPDBCONFIG";
    public const string Export = "MEPDBEXPORT";

    // Tủ điện / DB
    public const string Cabinet2D = "MEPDBCABINET2D";
    public const string Update = "MEPDBUPDATE";
    public const string Excel = "MEPDBEXCEL";
    public const string CabinetViews = "MEPDBCABINETVIEWS";
    public const string Power = "MEPDBPOWER";
    public const string ThreePhase4W = "MEPDB3P4W";

    public static readonly string[] All =
    {
        Entry, Draw, MepHvac, Water, Smoke,
        SeLayer, Config, Export,
        Cabinet2D, Update, Excel, CabinetViews, Power, ThreePhase4W
    };

    public static readonly string[] SubFeatures =
    {
        Draw, MepHvac, Water, Smoke,
        SeLayer, Config, Export,
        Cabinet2D, Update, Excel, CabinetViews, Power, ThreePhase4W
    };

    public static bool IsEntry(string code) =>
        string.Equals(code, Entry, System.StringComparison.OrdinalIgnoreCase);

    public static bool IsSubFeature(string code) =>
        !IsEntry(code) && System.Array.Exists(SubFeatures, x =>
            string.Equals(x, code, System.StringComparison.OrdinalIgnoreCase));

    public static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            [Entry] = "MEPDB — Lệnh vào plugin",
            [Draw] = "Hệ điện",
            [MepHvac] = "Điều hòa",
            [Water] = "Hệ nước",
            [Smoke] = "Báo cháy",
            [SeLayer] = "Chọn cùng layer",
            [Config] = "Cấu hình tủ",
            [Export] = "Xuất CSV",
            [Cabinet2D] = "Vẽ tủ điện",
            [Update] = "Cập nhật tủ",
            [Excel] = "Xuất Excel",
            [CabinetViews] = "Mặt chiếu tủ",
            [Power] = "Bố trí động lực",
            [ThreePhase4W] = "Sơ đồ 3P-4D+E"
        };
}
