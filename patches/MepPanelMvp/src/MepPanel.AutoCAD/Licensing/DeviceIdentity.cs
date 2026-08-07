using System;
using System.Security.Cryptography;
using System.Text;

namespace MepPanel.AutoCAD.Licensing
{
    public static class DeviceIdentity
    {
        private static string _cachedKey;

        public static string GetDeviceKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedKey))
            {
                return _cachedKey;
            }

            string raw = (Environment.MachineName ?? "pc") + "|" +
                         (Environment.UserName ?? "user") + "|" +
                         (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:");

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                _cachedKey = BitConverter.ToString(hash).Replace("-", string.Empty);
            }

            return _cachedKey;
        }

        public static string GetDeviceName()
        {
            return Environment.MachineName;
        }
    }
}
