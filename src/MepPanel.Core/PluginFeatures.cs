using System.Collections.Generic;

namespace MepPanel.Core;

/// <summary>
/// MEPDB = entry. Con lai = sub-feature (khop Admin License Server va panel).
/// </summary>
public static class PluginFeatures
{
    public const string Entry = "MEPDB";
    public const string MepDb = Entry;

    public const string Draw = "MEPDBDRAW";
    public const string MepHvac = "MEPHVAC";
    public const string Water = "MEPDBWATER";
    public const string Smoke = "MEPDBSMOKE";

    public const string SeLayer = "MEPSELAYER";
    public const string Config = "MEPDBCONFIG";
    public const string Export = "MEPDBEXPORT";

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
            [Entry] = "MEPDB — Lenh vao plugin",
            [Draw] = "He dien",
            [MepHvac] = "Dieu hoa",
            [Water] = "He nuoc",
            [Smoke] = "Bao chay / PCCC",
            [SeLayer] = "Chon cung layer",
            [Config] = "Cau hinh tu",
            [Export] = "Xuat CSV",
            [Cabinet2D] = "Ve tu dien",
            [Update] = "Cap nhat tu",
            [Excel] = "Xuat Excel",
            [CabinetViews] = "Mat chieu tu",
            [Power] = "Bo tri dong luc",
            [ThreePhase4W] = "So do 3P-4D+E"
        };
}
