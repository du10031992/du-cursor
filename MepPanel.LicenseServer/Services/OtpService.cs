using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MepPanel.LicenseServer.Services;

public sealed class OtpService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ISmsGateway _smsGateway;
    private readonly AuditService _auditService;
    private readonly ILogger<OtpService> _logger;

    private static readonly ConcurrentDictionary<string, OtpEntry> Store =
        new(StringComparer.Ordinal);

    public OtpService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ISmsGateway smsGateway,
        AuditService auditService,
        ILogger<OtpService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _smsGateway = smsGateway;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<OtpRequestResult> RequestOtpAsync(string phoneNumber, int? userId)
    {
        bool testMode = _configuration.GetValue("LicenseSettings:TestMode", true);
        string testOtp = _configuration["LicenseSettings:TestOtp"] ?? "123456";

        if (testMode || _environment.IsDevelopment())
        {
            await _auditService.WriteAsync(
                "request-otp",
                $"OTP thu nghiem cho {phoneNumber}",
                userId);

            return new OtpRequestResult
            {
                Message = "OTP thu nghiem da duoc tao.",
                TestOtp = testOtp
            };
        }

        string otp = Random.Shared.Next(100000, 999999).ToString();
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(5);
        Store[phoneNumber] = new OtpEntry(Hash(otp), expiresAt);

        try
        {
            await _smsGateway.SendOtpAsync(phoneNumber, otp);
        }
        catch (Exception ex)
        {
            Store.TryRemove(phoneNumber, out _);
            _logger.LogError(ex, "Gui SMS OTP that bai cho {Phone}", phoneNumber);
            throw new InvalidOperationException("Khong gui duoc SMS OTP. Kiem tra cau hinh Sms:WebhookUrl.");
        }

        await _auditService.WriteAsync(
            "request-otp",
            $"OTP production da gui cho {phoneNumber}",
            userId);

        return new OtpRequestResult
        {
            Message = "Ma OTP da duoc gui qua SMS."
        };
    }

    public bool VerifyOtp(string phoneNumber, string otp)
    {
        bool testMode = _configuration.GetValue("LicenseSettings:TestMode", true);
        string testOtp = _configuration["LicenseSettings:TestOtp"] ?? "123456";

        if (testMode || _environment.IsDevelopment())
        {
            return string.Equals((otp ?? string.Empty).Trim(), testOtp, StringComparison.Ordinal);
        }

        if (!Store.TryGetValue(phoneNumber, out OtpEntry? entry) || entry == null)
        {
            return false;
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            Store.TryRemove(phoneNumber, out _);
            return false;
        }

        bool ok = string.Equals(entry.CodeHash, Hash(otp ?? string.Empty), StringComparison.Ordinal);
        if (ok)
        {
            Store.TryRemove(phoneNumber, out _);
        }

        return ok;
    }

    private static string Hash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes);
    }

    private sealed record OtpEntry(string CodeHash, DateTime ExpiresAtUtc);
}

public sealed class OtpRequestResult
{
    public string Message { get; set; } = string.Empty;

    public string? TestOtp { get; set; }
}
