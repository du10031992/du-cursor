namespace MepPanel.LicenseServer.Models;

public class Device
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public string DeviceKeyHash { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string AutoCadVersion { get; set; } = string.Empty;

    public string PluginVersion { get; set; } = string.Empty;

    /// <summary>Active | Blocked | Revoked</summary>
    public string Status { get; set; } = DeviceStatuses.Active;

    public DateTime ActivatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAtUtc { get; set; }
}
