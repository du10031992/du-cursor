using MepPanel.LicenseServer.Data;
using MepPanel.LicenseServer.Models;

namespace MepPanel.LicenseServer.Services;

public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        string action,
        string detail,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Detail = detail,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
