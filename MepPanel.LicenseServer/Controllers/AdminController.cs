using MepPanel.LicenseServer.Data;
using MepPanel.LicenseServer.Models;
using MepPanel.LicenseServer.Security;
using MepPanel.LicenseServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Controllers;

/// <summary>
/// API quản trị: khóa/mở user, khóa/mở chức năng plugin, mở chuyển máy.
/// Gửi header: X-Admin-ApiKey
/// </summary>
[ApiController]
[Route("api/admin")]
[AdminApiKey]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly AuditService _auditService;

    public AdminController(
        AppDbContext db,
        IConfiguration configuration,
        AuditService auditService)
    {
        _db = db;
        _configuration = configuration;
        _auditService = auditService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers()
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(x => x.License)
            .Include(x => x.Devices)
            .OrderBy(x => x.Id)
            .ToListAsync();

        return Ok(users.Select(MapUser));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var phone = (request.PhoneNumber ?? string.Empty).Trim();
        if (phone.Length == 0)
        {
            return BadRequest(new { message = "Số điện thoại không hợp lệ." });
        }

        var exists = await _db.Users.AnyAsync(x => x.PhoneNumber == phone);
        if (exists)
        {
            return Conflict(new { message = "Số điện thoại đã tồn tại." });
        }

        var maxDevices = request.MaxDevices > 0
            ? request.MaxDevices
            : _configuration.GetValue("LicenseSettings:DefaultMaxDevices", 1);

        // Theo yêu cầu hiện tại: mặc định 1 máy / 1 SĐT.
        if (maxDevices < 1)
        {
            maxDevices = 1;
        }

        var defaultFeatures = _configuration
            .GetSection("LicenseSettings:DefaultFeatures")
            .Get<string[]>()
            ?? [PluginFeatures.MepDb, PluginFeatures.MepHvac];

        var features = request.Features is { Length: > 0 }
            ? FeatureParser.NormalizeKnown(request.Features)
            : FeatureParser.NormalizeKnown(defaultFeatures);

        var user = new User
        {
            PhoneNumber = phone,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? phone
                : request.DisplayName.Trim(),
            Role = "User",
            Status = UserStatuses.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var license = new License
        {
            UserId = user.Id,
            Plan = string.IsNullOrWhiteSpace(request.Plan) ? "Trial" : request.Plan!.Trim(),
            Status = LicenseStatuses.Active,
            MaxDevices = maxDevices,
            EnabledFeatures = FeatureParser.Join(features),
            StartsAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = request.ExpiresAtUtc ?? DateTime.UtcNow.AddYears(1)
        };

        _db.Licenses.Add(license);
        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "admin-create-user",
            $"Tạo user {phone}, features={license.EnabledFeatures}, maxDevices={maxDevices}",
            user.Id);

        var created = await _db.Users
            .AsNoTracking()
            .Include(x => x.License)
            .Include(x => x.Devices)
            .FirstAsync(x => x.Id == user.Id);

        return Ok(MapUser(created));
    }

    /// <summary>Active / Blocked toàn bộ tài khoản.</summary>
    [HttpPatch("users/{id:int}/status")]
    public async Task<IActionResult> SetUserStatus(int id, SetStatusRequest request)
    {
        var status = NormalizeStatus(request.Status, UserStatuses.Active, UserStatuses.Blocked);
        if (status == null)
        {
            return BadRequest(new { message = "Status phải là Active hoặc Blocked." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy user." });
        }

        user.Status = status;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "admin-user-status",
            $"User {user.PhoneNumber} -> {status}",
            user.Id);

        return Ok(new
        {
            user.Id,
            user.PhoneNumber,
            user.Status,
            message = status == UserStatuses.Active
                ? "Đã mở tài khoản."
                : "Đã khóa tài khoản."
        });
    }

    /// <summary>Mở/khóa từng chức năng plugin (MEPDB, MEPHVAC, ...).</summary>
    [HttpPatch("users/{id:int}/features")]
    public async Task<IActionResult> SetFeatures(int id, SetFeaturesRequest request)
    {
        var user = await _db.Users
            .Include(x => x.License)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user?.License == null)
        {
            return NotFound(new { message = "Không tìm thấy user/license." });
        }

        var features = FeatureParser.NormalizeKnown(request.Features ?? []);
        user.License.EnabledFeatures = FeatureParser.Join(features);
        user.License.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "admin-set-features",
            $"User {user.PhoneNumber} features={user.License.EnabledFeatures}",
            user.Id);

        return Ok(new
        {
            user.Id,
            user.PhoneNumber,
            features = FeatureParser.Parse(user.License.EnabledFeatures),
            message = "Đã cập nhật chức năng plugin."
        });
    }

    /// <summary>
    /// Mở chuyển máy: revoke toàn bộ device Active hiện tại của user.
    /// Sau đó user có thể kích hoạt máy mới.
    /// </summary>
    [HttpPost("users/{id:int}/release-device")]
    public async Task<IActionResult> ReleaseDevice(int id)
    {
        var user = await _db.Users
            .Include(x => x.Devices)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy user." });
        }

        var active = user.Devices
            .Where(x => x.Status == DeviceStatuses.Active)
            .ToList();

        foreach (var device in active)
        {
            device.Status = DeviceStatuses.Revoked;
            device.RevokedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "admin-release-device",
            $"Release {active.Count} device(s) của {user.PhoneNumber}",
            user.Id);

        return Ok(new
        {
            user.Id,
            user.PhoneNumber,
            releasedCount = active.Count,
            message = active.Count == 0
                ? "Không có máy Active để mở."
                : "Đã mở chuyển máy. User có thể kích hoạt máy mới."
        });
    }

    /// <summary>Khóa hoặc mở một thiết bị cụ thể.</summary>
    [HttpPatch("devices/{id:int}/status")]
    public async Task<IActionResult> SetDeviceStatus(int id, SetStatusRequest request)
    {
        var status = NormalizeStatus(
            request.Status,
            DeviceStatuses.Active,
            DeviceStatuses.Blocked,
            DeviceStatuses.Revoked);

        if (status == null)
        {
            return BadRequest(new
            {
                message = "Status phải là Active, Blocked hoặc Revoked."
            });
        }

        var device = await _db.Devices
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (device == null)
        {
            return NotFound(new { message = "Không tìm thấy thiết bị." });
        }

        // Không cho Active nếu user đã có máy Active khác (max 1 theo mặc định).
        if (status == DeviceStatuses.Active && device.User != null)
        {
            var license = await _db.Licenses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == device.UserId);

            var maxDevices = license?.MaxDevices > 0 ? license.MaxDevices : 1;
            var activeCount = await _db.Devices.CountAsync(x =>
                x.UserId == device.UserId &&
                x.Status == DeviceStatuses.Active &&
                x.Id != device.Id);

            if (activeCount >= maxDevices)
            {
                return Conflict(new
                {
                    message =
                        "User đã có máy Active khác. Hãy release-device trước khi mở máy này."
                });
            }
        }

        device.Status = status;
        device.RevokedAtUtc = status is DeviceStatuses.Revoked or DeviceStatuses.Blocked
            ? DateTime.UtcNow
            : null;

        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "admin-device-status",
            $"Device {device.Id} -> {status}",
            device.UserId);

        return Ok(new
        {
            device.Id,
            device.UserId,
            device.DeviceName,
            device.Status,
            message = "Đã cập nhật trạng thái thiết bị."
        });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 500);

        var logs = await _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(take)
            .ToListAsync();

        return Ok(logs);
    }

    private static string? NormalizeStatus(string? value, params string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return allowed.FirstOrDefault(x =>
            string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static object MapUser(User user)
    {
        var license = user.License;
        return new
        {
            user.Id,
            user.PhoneNumber,
            user.DisplayName,
            user.Role,
            user.Status,
            user.CreatedAtUtc,
            license = license == null
                ? null
                : new
                {
                    license.Id,
                    license.Plan,
                    license.Status,
                    license.MaxDevices,
                    license.StartsAtUtc,
                    license.ExpiresAtUtc,
                    features = FeatureParser.Parse(license.EnabledFeatures)
                },
            devices = user.Devices
                .OrderByDescending(x => x.LastSeenAtUtc)
                .Select(d => new
                {
                    d.Id,
                    d.DeviceName,
                    d.Status,
                    d.AutoCadVersion,
                    d.PluginVersion,
                    d.ActivatedAtUtc,
                    d.LastSeenAtUtc,
                    d.RevokedAtUtc
                })
        };
    }
}

public class CreateUserRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Plan { get; set; }

    public int MaxDevices { get; set; } = 1;

    public DateTime? ExpiresAtUtc { get; set; }

    public string[]? Features { get; set; }
}

public class SetStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class SetFeaturesRequest
{
    public string[]? Features { get; set; }
}
