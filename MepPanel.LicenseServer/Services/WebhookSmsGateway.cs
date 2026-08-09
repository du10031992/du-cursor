using System.Net.Http.Json;

namespace MepPanel.LicenseServer.Services;

/// <summary>
/// Gui OTP qua webhook (ket noi nha cung cap SMS cua ban).
/// Cau hinh Sms:WebhookUrl + Sms:ApiKey trong appsettings.Production.json
/// </summary>
public sealed class WebhookSmsGateway : ISmsGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookSmsGateway> _logger;

    public WebhookSmsGateway(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WebhookSmsGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendOtpAsync(
        string phoneNumber,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        string webhookUrl = _configuration["Sms:WebhookUrl"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new InvalidOperationException("Sms:WebhookUrl chua cau hinh.");
        }

        var client = _httpClientFactory.CreateClient("SmsWebhook");
        string apiKey = _configuration["Sms:ApiKey"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

        var payload = new
        {
            phoneNumber,
            message = $"Ma OTP MepPanel cua ban la {otpCode}. Hieu luc 5 phut.",
            otp = otpCode
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
