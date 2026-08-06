namespace MepPanel.LicenseServer.Models;

public class License
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public string Plan { get; set; } = "Trial";

    /// <summary>Active | Blocked | Expired</summary>
    public string Status { get; set; } = LicenseStatuses.Active;

    /// <summary>
    /// Mỗi SĐT mặc định chỉ 1 máy. Muốn chuyển máy phải Admin mở (release).
    /// </summary>
    public int MaxDevices { get; set; } = 1;

    /// <summary>
    /// Danh sách chức năng plugin được mở, phân tách bởi dấu phẩy.
    /// Ví dụ: MEPDB,MEPHVAC
    /// </summary>
    public string EnabledFeatures { get; set; } = "MEPDB";

    public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; } =
        DateTime.UtcNow.AddYears(1);

    public DateTime? UpdatedAtUtc { get; set; }
}
