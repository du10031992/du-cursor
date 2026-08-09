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

    private static readonly ConcurrentDictionary<string, ThrottleEntry> Throttles =
        new(StringComparer.Ordinal);

    private const int MaxRequestsPerWindow = 5;
    private const int MaxVerifyAttempts = 5;
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(15);

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

        // Chong spam SMS va dò OTP tu Internet.
        if (!TryReserveRequestSlot(phoneNumber, out TimeSpan retryAfter))
        {
            await _auditService.WriteAsync(
                "request-otp-throttled",
                $"Chan yeu cau OTP qua nhieu cho {phoneNumber}",
                userId);

            throw new OtpThrottledException(
                "Ban da yeu cau OTP qua nhieu lan. Thu lai sau " +
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes)) + " phut.");
        }

        string otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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

        if (IsVerifyLocked(phoneNumber))
        {
            return false;
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
            ResetVerifyAttempts(phoneNumber);
            return true;
        }

        // Sau nhieu lan sai, huy OTP hien tai va tam khoa de chan dò 6 chu so.
        if (RegisterFailedVerify(phoneNumber))
        {
            Store.TryRemove(phoneNumber, out _);
        }

        return false;
    }

    private static bool TryReserveRequestSlot(string phoneNumber, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        DateTime now = DateTime.UtcNow;
        ThrottleEntry entry = Throttles.GetOrAdd(phoneNumber, _ => new ThrottleEntry(now));

        lock (entry.Sync)
        {
            if (now - entry.WindowStartUtc >= ThrottleWindow)
            {
                entry.WindowStartUtc = now;
                entry.RequestCount = 0;
            }

            if (entry.RequestCount >= MaxRequestsPerWindow)
            {
                retryAfter = entry.WindowStartUtc + ThrottleWindow - now;
                return false;
            }

            entry.RequestCount++;
            return true;
        }
    }

    private static bool IsVerifyLocked(string phoneNumber)
    {
        if (!Throttles.TryGetValue(phoneNumber, out ThrottleEntry? entry) || entry == null)
        {
            return false;
        }

        lock (entry.Sync)
        {
            if (entry.LockedUntilUtc == null)
            {
                return false;
            }

            if (entry.LockedUntilUtc <= DateTime.UtcNow)
            {
                entry.LockedUntilUtc = null;
                entry.FailedVerifyCount = 0;
                return false;
            }

            return true;
        }
    }

    /// <summary>Tra ve true khi vua bi khoa (OTP hien tai phai bi huy).</summary>
    private static bool RegisterFailedVerify(string phoneNumber)
    {
        DateTime now = DateTime.UtcNow;
        ThrottleEntry entry = Throttles.GetOrAdd(phoneNumber, _ => new ThrottleEntry(now));

        lock (entry.Sync)
        {
            entry.FailedVerifyCount++;
            if (entry.FailedVerifyCount < MaxVerifyAttempts)
            {
                return false;
            }

            entry.LockedUntilUtc = now + ThrottleWindow;
            return true;
        }
    }

    private static void ResetVerifyAttempts(string phoneNumber)
    {
        if (Throttles.TryGetValue(phoneNumber, out ThrottleEntry? entry) && entry != null)
        {
            lock (entry.Sync)
            {
                entry.FailedVerifyCount = 0;
                entry.LockedUntilUtc = null;
            }
        }
    }

    private static string Hash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes);
    }

    private sealed record OtpEntry(string CodeHash, DateTime ExpiresAtUtc);

    private sealed class ThrottleEntry
    {
        public ThrottleEntry(DateTime windowStartUtc)
        {
            WindowStartUtc = windowStartUtc;
        }

        public object Sync { get; } = new object();

        public DateTime WindowStartUtc { get; set; }

        public int RequestCount { get; set; }

        public int FailedVerifyCount { get; set; }

        public DateTime? LockedUntilUtc { get; set; }
    }
}

/// <summary>Yeu cau OTP bi chan vi qua nhieu lan trong thoi gian ngan.</summary>
public sealed class OtpThrottledException : Exception
{
    public OtpThrottledException(string message)
        : base(message)
    {
    }
}

public sealed class OtpRequestResult
{
    public string Message { get; set; } = string.Empty;

    public string? TestOtp { get; set; }
}
