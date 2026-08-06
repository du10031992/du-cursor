namespace MepPanel.LicenseServer.Models;

public static class UserStatuses
{
    public const string Active = "Active";
    public const string Blocked = "Blocked";
}

public static class LicenseStatuses
{
    public const string Active = "Active";
    public const string Blocked = "Blocked";
    public const string Expired = "Expired";
}

public static class DeviceStatuses
{
    public const string Active = "Active";
    public const string Blocked = "Blocked";
    public const string Revoked = "Revoked";
}

public static class PluginFeatures
{
    /// <summary>Lenh duy nhat vao plugin (MEPDB).</summary>
    public const string Entry = "MEPDB";
    public const string MepDb = Entry;

    /// <summary>Chuc nang phu — khoa/mo trong panel plugin.</summary>
    public static readonly string[] SubFeatures = PluginFeatureCatalog.SubItems
        .Select(x => x.Code)
        .ToArray();

    public static readonly string[] All = [Entry, ..SubFeatures];

    public static bool IsEntry(string? code) =>
        string.Equals(code?.Trim(), Entry, StringComparison.OrdinalIgnoreCase);

    public static bool IsSubFeature(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        SubFeatures.Contains(code.Trim(), StringComparer.OrdinalIgnoreCase);
}
