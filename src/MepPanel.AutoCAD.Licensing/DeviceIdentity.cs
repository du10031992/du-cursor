using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>
    /// Tạo DeviceKey ổn định theo máy (không dùng MAC).
    /// </summary>
    public static class DeviceIdentity
    {
        private static string _cachedKey;

        public static string GetDeviceKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedKey))
            {
                return _cachedKey;
            }

            string machineGuid = ReadMachineGuid();
            string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            string raw = machineGuid + "|" + systemDrive.ToUpperInvariant();

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

        private static string ReadMachineGuid()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT UUID FROM Win32_ComputerSystemProduct"))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        object value = item["UUID"];
                        if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                        {
                            return value.ToString().Trim();
                        }
                    }
                }
            }
            catch
            {
                // Fallback bên dưới.
            }

            return Environment.MachineName + "|" + Environment.UserName;
        }
    }
}
