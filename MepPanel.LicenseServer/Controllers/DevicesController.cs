using System.Security.Claims;
using MepPanel.LicenseServer.Data;
using MepPanel.LicenseServer.Models;
using MepPanel.LicenseServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly AuditService _auditService;

    public DevicesController(
        AppDbContext db,
        IConfiguration configuration,
        AuditService auditService)
    {
        _db = db;
        _configuration = configuration;
        _auditService = auditService;
    }

    /// <summary>
    /// Kích hoạt máy hiện tại. Mỗi SĐT chỉ 1 máy Active.
    /// Muốn chuyển máy: Admin phải gọi release-device trước.
    /// </summary>
    [HttpPost("activate")]
    [Authorize]
    public async Task<IActionResult> Activate(ActivateDeviceRequest request)
    {
        var phoneNumber = ResolvePhoneNumber(request.PhoneNumber);
        if (phoneNumber.Length == 0)
        {
            return BadRequest(new
            {
                activated = false,
                message = "Số điện thoại không hợp lệ."
            });
        }

        if (string.IsNullOrWhiteSpace(request.DeviceKey))
        {
            return BadRequest(new
            {
                activated = false,
                message = "DeviceKey không hợp lệ."
            });
        }

        var user = await _db.Users
            .Include(x => x.License)
            .FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber);

        if (user == null)
        {
            return NotFound(new
            {
                activated = false,
                message = "Số điện thoại chưa được đăng ký."
            });
        }

        if (user.Status != UserStatuses.Active)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    activated = false,
                    message = "Tài khoản đã bị khóa."
                });
        }

        var license = user.License;
        if (license == null ||
            license.Status != LicenseStatuses.Active ||
            license.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    activated = false,
                    message = "Giấy phép không còn hiệu lực."
                });
        }

        var maxDevices = license.MaxDevices <= 0 ? 1 : license.MaxDevices;
        var deviceKeyHash = DeviceKeyHasher.Hash(request.DeviceKey);

        var existingForKey = await _db.Devices
            .FirstOrDefaultAsync(x => x.DeviceKeyHash == deviceKeyHash);

        if (existingForKey != null && existingForKey.UserId != user.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    activated = false,
                    message = "Máy này đã được đăng ký cho tài khoản khác."
                });
        }

        if (existingForKey != null)
        {
            if (existingForKey.Status == DeviceStatuses.Blocked)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        activated = false,
                        message = "Thiết bị đã bị Admin khóa."
                    });
            }

            if (existingForKey.Status == DeviceStatuses.Revoked)
            {
                // Cho phép bind lại cùng máy sau khi admin đã release/revoke.
                var activeCount = await _db.Devices.CountAsync(x =>
                    x.UserId == user.Id &&
                    x.Status == DeviceStatuses.Active &&
                    x.Id != existingForKey.Id);

                if (activeCount >= maxDevices)
                {
                    return StatusCode(
                        StatusCodes.Status403Forbidden,
                        new
                        {
                            activated = false,
                            message =
                                "Tài khoản đã gắn máy khác. Liên hệ Admin để mở chuyển máy."
                        });
                }

                existingForKey.Status = DeviceStatuses.Active;
                existingForKey.RevokedAtUtc = null;
            }

            existingForKey.DeviceName = request.DeviceName ?? existingForKey.DeviceName;
            existingForKey.AutoCadVersion = request.AutoCadVersion ?? string.Empty;
            existingForKey.PluginVersion = request.PluginVersion ?? string.Empty;
            existingForKey.LastSeenAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditService.WriteAsync(
                "device-reactivate",
                $"Xác nhận lại thiết bị {existingForKey.Id}",
                user.Id);

            return Ok(new
            {
                activated = true,
                message = "Thiết bị đã được xác nhận lại.",
                deviceId = existingForKey.Id,
                licenseExpiresAtUtc = license.ExpiresAtUtc,
                features = FeatureParser.Parse(license.EnabledFeatures)
            });
        }

        var activeDevices = await _db.Devices
            .Where(x => x.UserId == user.Id && x.Status == DeviceStatuses.Active)
            .ToListAsync();

        if (activeDevices.Count >= maxDevices)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    activated = false,
                    message =
                        $"Tài khoản chỉ được dùng trên {maxDevices} máy. " +
                        "Muốn chuyển máy, Admin phải mở (release) máy hiện tại trước.",
                    activeDevices = activeDevices.Select(x => new
                    {
                        x.Id,
                        x.DeviceName,
                        x.LastSeenAtUtc
                    })
                });
        }

        var device = new Device
        {
            UserId = user.Id,
            DeviceKeyHash = deviceKeyHash,
            DeviceName = request.DeviceName ?? Environment.MachineName,
            AutoCadVersion = request.AutoCadVersion ?? string.Empty,
            PluginVersion = request.PluginVersion ?? string.Empty,
            Status = DeviceStatuses.Active,
            ActivatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        await _auditService.WriteAsync(
            "device-activate",
            $"Kích hoạt thiết bị mới {device.Id} ({device.DeviceName})",
            user.Id);

        return Ok(new
        {
            activated = true,
            message = "Kích hoạt thiết bị thành công.",
            deviceId = device.Id,
            licenseExpiresAtUtc = license.ExpiresAtUtc,
            features = FeatureParser.Parse(license.EnabledFeatures)
        });
    }

    /// <summary>
    /// Kiểm tra quyền chạy plugin + trả danh sách chức năng được mở.
    /// </summary>
    [HttpPost("check")]
    [Authorize]
    public async Task<IActionResult> Check(CheckDeviceRequest request)
    {
        var now = DateTime.UtcNow;
        var phoneNumber = ResolvePhoneNumber(request.PhoneNumber);

        if (phoneNumber.Length == 0 || string.IsNullOrWhiteSpace(request.DeviceKey))
        {
            return BadRequest(new
            {
                valid = false,
                message = "Thiếu số điện thoại hoặc DeviceKey."
            });
        }

        var user = await _db.Users
            .Include(x => x.License)
            .FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber);

        if (user == null)
        {
            return NotFound(new
            {
                valid = false,
                message = "Số điện thoại chưa được đăng ký."
            });
        }

        if (user.Status != UserStatuses.Active)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    valid = false,
                    message = "Tài khoản đã bị khóa."
                });
        }

        var deviceKeyHash = DeviceKeyHasher.Hash(request.DeviceKey);
        var device = await _db.Devices.FirstOrDefaultAsync(x =>
            x.UserId == user.Id &&
            x.DeviceKeyHash == deviceKeyHash);

        if (device == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    valid = false,
                    message = "Thiết bị chưa được kích hoạt."
                });
        }

        if (device.Status != DeviceStatuses.Active)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    valid = false,
                    message = "Thiết bị đã bị khóa hoặc đã bị thu hồi. Liên hệ Admin."
                });
        }

        var license = user.License;
        if (license == null ||
            license.Status != LicenseStatuses.Active ||
            license.ExpiresAtUtc <= now)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    valid = false,
                    message = "Giấy phép không còn hiệu lực."
                });
        }

        var graceHours = _configuration.GetValue("LicenseSettings:OfflineGraceHours", 24);
        if (graceHours < 1)
        {
            graceHours = 1;
        }

        device.LastSeenAtUtc = now;
        if (!string.IsNullOrWhiteSpace(request.PluginVersion))
        {
            device.PluginVersion = request.PluginVersion;
        }

        await _db.SaveChangesAsync();

        var features = FeatureParser.Parse(license.EnabledFeatures);
        var offlineUntil = now.AddHours(graceHours);
        if (offlineUntil > license.ExpiresAtUtc)
        {
            offlineUntil = license.ExpiresAtUtc;
        }

        return Ok(new
        {
            valid = true,
            message = "Giấy phép hợp lệ.",
            userId = user.Id,
            displayName = user.DisplayName,
            phoneNumber = user.PhoneNumber,
            deviceId = device.Id,
            licensePlan = license.Plan,
            licenseExpiresAtUtc = license.ExpiresAtUtc,
            offlineUntilUtc = offlineUntil,
            maxDevices = license.MaxDevices,
            features
        });
    }

    private string ResolvePhoneNumber(string? requestPhone)
    {
        // Ưu tiên số điện thoại trong JWT để client không tự giả mạo.
        var tokenPhone =
            User.FindFirstValue("phoneNumber")
            ?? User.FindFirstValue("unique_name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(tokenPhone))
        {
            return tokenPhone.Trim();
        }

        return (requestPhone ?? string.Empty).Trim();
    }
}

public class ActivateDeviceRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string DeviceKey { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string AutoCadVersion { get; set; } = string.Empty;

    public string PluginVersion { get; set; } = string.Empty;
}

public class CheckDeviceRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string DeviceKey { get; set; } = string.Empty;

    public string PluginVersion { get; set; } = string.Empty;
}
