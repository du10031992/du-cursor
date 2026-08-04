using MepPanel.LicenseServer.Data;
using MepPanel.LicenseServer.Models;
using MepPanel.LicenseServer.Security;
using MepPanel.LicenseServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Controllers;

/// <summary>
/// API quản trị: khóa/mở user, thiết bị, chức năng plugin theo SĐT.
/// Header bắt buộc: X-Admin-ApiKey
/// </summary>
[ApiController]
[Route("api/[controller]")]
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

    /// <summary>Tổng quan toàn hệ thống — dùng cho trang Admin UI.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(x => x.License)
            .Include(x => x.Devices)
            .OrderBy(x => x.Id)
            .ToListAsync();

        var mapped = users.Select(MapUser).ToList();

        return Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            totals = new
            {
                users = users.Count,
                activeUsers = users.Count(x => x.Status == UserStatuses.Active),
                blockedUsers = users.Count(x => x.Status == UserStatuses.Blocked),
                devices = users.Sum(x => x.Devices.Count),
                activeDevices = users.Sum(x => x.Devices.Count(d => d.Status == DeviceStatuses.Active)),
                availableFeatures = PluginFeatures.All
            },
            users = mapped
        });
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

    [HttpGet("users/by-phone/{phoneNumber}")]
    public async Task<IActionResult> GetByPhone(string phoneNumber)
    {
        var phone = (phoneNumber ?? string.Empty).Trim();
        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.License)
            .Include(x => x.Devices)
            .FirstOrDefaultAsync(x => x.PhoneNumber == phone);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy SĐT này." });
        }

        return Ok(MapUser(user));
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

    [HttpPut("users/{userId:int}/status")]
    [HttpPatch("users/{userId:int}/status")]
    public async Task<IActionResult> SetUserStatus(int userId, SetStatusRequest request)
    {
        var status = NormalizeStatus(request.Status, UserStatuses.Active, UserStatuses.Blocked);
        if (status == null)
        {
            return BadRequest(new { message = "Status phải là Active hoặc Blocked." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
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

    [HttpPut("users/{userId:int}/features")]
    [HttpPatch("users/{userId:int}/features")]
    public async Task<IActionResult> SetFeatures(int userId, SetFeaturesRequest request)
    {
        var user = await _db.Users
            .Include(x => x.License)
            .FirstOrDefaultAsync(x => x.Id == userId);

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

    /// <summary>Bật/tắt 1 chức năng cụ thể (MEPDB hoặc MEPHVAC) theo user.</summary>
    [HttpPut("users/{userId:int}/features/{featureCode}")]
    public async Task<IActionResult> SetSingleFeature(
        int userId,
        string featureCode,
        SetFeatureEnabledRequest request)
    {
        var code = (featureCode ?? string.Empty).Trim().ToUpperInvariant();
        if (!PluginFeatures.All.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = $"Feature không hỗ trợ. Chỉ nhận: {string.Join(", ", PluginFeatures.All)}"
            });
        }

        var user = await _db.Users
            .Include(x => x.License)
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user?.License == null)
        {
            return NotFound(new { message = "Không tìm thấy user/license." });
        }

        var current = FeatureParser.Parse(user.License.EnabledFeatures).ToList();
        if (request.Enabled)
        {
            if (!current.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(code);
            }
        }
        else
        {
            current = current
                .Where(x => !string.Equals(x, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        user.License.EnabledFeatures = FeatureParser.Join(current);
        user.License.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "admin-toggle-feature",
            $"User {user.PhoneNumber} {code}={(request.Enabled ? "ON" : "OFF")}",
            user.Id);

        return Ok(new
        {
            user.Id,
            user.PhoneNumber,
            feature = code,
            enabled = request.Enabled,
            features = FeatureParser.Parse(user.License.EnabledFeatures),
            message = request.Enabled
                ? $"Đã bật {code}."
                : $"Đã tắt {code}."
        });
    }

    [HttpPost("users/{userId:int}/release-device")]
    public async Task<IActionResult> ReleaseDevice(int userId)
    {
        var user = await _db.Users
            .Include(x => x.Devices)
            .FirstOrDefaultAsync(x => x.Id == userId);

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

    [HttpPut("devices/{deviceId:int}/status")]
    [HttpPatch("devices/{deviceId:int}/status")]
    public async Task<IActionResult> SetDeviceStatus(int deviceId, SetStatusRequest request)
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
            .FirstOrDefaultAsync(x => x.Id == deviceId);

        if (device == null)
        {
            return NotFound(new { message = "Không tìm thấy thiết bị." });
        }

        if (status == DeviceStatuses.Active)
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
            phoneNumber = device.User?.PhoneNumber,
            device.DeviceName,
            device.Status,
            message = "Đã cập nhật trạng thái thiết bị."
        });
    }

    [HttpPut("licenses/{licenseId:int}/extend")]
    public async Task<IActionResult> ExtendLicense(int licenseId, ExtendLicenseRequest request)
    {
        var license = await _db.Licenses
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == licenseId);

        if (license == null)
        {
            return NotFound(new { message = "Không tìm thấy giấy phép." });
        }

        if (request.ExpiresAtUtc.HasValue)
        {
            license.ExpiresAtUtc = request.ExpiresAtUtc.Value.ToUniversalTime();
        }
        else
        {
            var days = request.ExtraDays > 0 ? request.ExtraDays : 30;
            var baseline = license.ExpiresAtUtc > DateTime.UtcNow
                ? license.ExpiresAtUtc
                : DateTime.UtcNow;
            license.ExpiresAtUtc = baseline.AddDays(days);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = NormalizeStatus(
                request.Status,
                LicenseStatuses.Active,
                LicenseStatuses.Blocked,
                LicenseStatuses.Expired);
            if (status != null)
            {
                license.Status = status;
            }
        }
        else if (license.ExpiresAtUtc > DateTime.UtcNow &&
                 license.Status == LicenseStatuses.Expired)
        {
            license.Status = LicenseStatuses.Active;
        }

        license.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "admin-extend-license",
            $"License {license.Id} expires={license.ExpiresAtUtc:o}",
            license.UserId);

        return Ok(new
        {
            license.Id,
            license.UserId,
            phoneNumber = license.User?.PhoneNumber,
            license.Status,
            license.ExpiresAtUtc,
            message = "Đã cập nhật hạn giấy phép."
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
        var featureList = FeatureParser.Parse(license?.EnabledFeatures);
        return new
        {
            user.Id,
            userId = user.Id,
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
                    licenseId = license.Id,
                    license.Plan,
                    license.Status,
                    license.MaxDevices,
                    license.StartsAtUtc,
                    license.ExpiresAtUtc,
                    features = featureList,
                    featureFlags = PluginFeatures.All.ToDictionary(
                        f => f,
                        f => featureList.Contains(f, StringComparer.OrdinalIgnoreCase))
                },
            devices = user.Devices
                .OrderByDescending(x => x.LastSeenAtUtc)
                .Select(d => new
                {
                    d.Id,
                    deviceId = d.Id,
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

public class SetFeatureEnabledRequest
{
    public bool Enabled { get; set; }
}

public class ExtendLicenseRequest
{
    public int ExtraDays { get; set; } = 30;

    public DateTime? ExpiresAtUtc { get; set; }

    public string? Status { get; set; }
}
