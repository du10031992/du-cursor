namespace MepPanel.LicenseServer.Models;

public class User
{
    public int Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = "User";

    /// <summary>Active | Blocked</summary>
    public string Status { get; set; } = UserStatuses.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public License? License { get; set; }

    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
