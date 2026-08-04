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
    public const string MepDb = "MEPDB";
    public const string MepHvac = "MEPHVAC";

    public static readonly string[] All =
    [
        MepDb,
        MepHvac
    ];
}
