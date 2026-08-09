namespace MepPanel.LicenseServer.Services;

public interface ISmsGateway
{
    Task SendOtpAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default);
}
