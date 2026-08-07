using System.Security.Cryptography;
using System.Text;

namespace MepPanel.LicenseServer.Services;

public static class DeviceKeyHasher
{
    public static string Hash(string deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            throw new ArgumentException("DeviceKey không hợp lệ.", nameof(deviceKey));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(deviceKey.Trim()));
        return Convert.ToHexString(bytes);
    }
}
