using MepPanel.LicenseServer.Data;
using MepPanel.LicenseServer.Models;
using MepPanel.LicenseServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MepPanel.LicenseServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuditService _auditService;

    public AuthController(
        AppDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        JwtTokenService jwtTokenService,
        AuditService auditService)
    {
        _db = db;
        _configuration = configuration;
        _environment = environment;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
    }

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp(RequestOtpRequest request)
    {
        var phoneNumber = NormalizePhone(request.PhoneNumber);
        if (phoneNumber.Length == 0)
        {
            return BadRequest(new { message = "Số điện thoại không hợp lệ." });
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber);

        if (user == null)
        {
            return NotFound(new { message = "Số điện thoại chưa được Admin đăng ký." });
        }

        if (user.Status != UserStatuses.Active)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đang bị khóa." });
        }

        var testMode = _configuration.GetValue("LicenseSettings:TestMode", true);
        var testOtp = _configuration["LicenseSettings:TestOtp"] ?? "123456";

        if (testMode || _environment.IsDevelopment())
        {
            await _auditService.WriteAsync(
                "request-otp",
                $"OTP thử nghiệm cho {phoneNumber}",
                user.Id);

            return Ok(new
            {
                message = "OTP thử nghiệm đã được tạo.",
                testOtp
            });
        }

        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new { message = "Dịch vụ SMS thật chưa được cấu hình." });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request)
    {
        var phoneNumber = NormalizePhone(request.PhoneNumber);
        var otp = (request.Otp ?? string.Empty).Trim();
        var testOtp = _configuration["LicenseSettings:TestOtp"] ?? "123456";

        if (phoneNumber.Length == 0)
        {
            return BadRequest(new
            {
                authenticated = false,
                message = "Số điện thoại không hợp lệ."
            });
        }

        if (!string.Equals(otp, testOtp, StringComparison.Ordinal))
        {
            return Unauthorized(new
            {
                authenticated = false,
                message = "Mã OTP không chính xác."
            });
        }

        var user = await _db.Users
            .Include(x => x.License)
            .FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber);

        if (user == null)
        {
            return NotFound(new
            {
                authenticated = false,
                message = "Số điện thoại chưa được Admin đăng ký."
            });
        }

        if (user.Status != UserStatuses.Active)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    authenticated = false,
                    message = "Tài khoản đang bị khóa."
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
                    authenticated = false,
                    message = "Không có giấy phép hợp lệ hoặc giấy phép đã hết hạn."
                });
        }

        var (token, expiresAt) = _jwtTokenService.CreateAccessToken(user);

        await _auditService.WriteAsync(
            "verify-otp",
            $"Đăng nhập thành công: {phoneNumber}",
            user.Id);

        return Ok(new
        {
            authenticated = true,
            accessToken = token,
            accessTokenExpiresAtUtc = expiresAt,
            userId = user.Id,
            phoneNumber = user.PhoneNumber,
            displayName = user.DisplayName,
            role = user.Role,
            message = "Xác thực thành công.",
            license = new
            {
                license.Id,
                license.Plan,
                license.Status,
                license.StartsAtUtc,
                license.ExpiresAtUtc,
                license.MaxDevices,
                features = FeatureParser.Parse(license.EnabledFeatures)
            }
        });
    }

    private static string NormalizePhone(string? phone)
        => (phone ?? string.Empty).Trim();
}

public class RequestOtpRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}

public class VerifyOtpRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Otp { get; set; } = string.Empty;
}
