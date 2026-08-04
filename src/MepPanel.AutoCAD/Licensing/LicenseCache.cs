using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace MepPanel.AutoCAD.Licensing
{
    [DataContract]
    public class LicenseCacheData
    {
        [DataMember(Name = "phoneNumber")]
        public string PhoneNumber { get; set; }

        [DataMember(Name = "deviceKey")]
        public string DeviceKey { get; set; }

        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; }

        [DataMember(Name = "licensePlan")]
        public string LicensePlan { get; set; }

        [DataMember(Name = "licenseExpiresAtUtc")]
        public DateTime LicenseExpiresAtUtc { get; set; }

        [DataMember(Name = "offlineUntilUtc")]
        public DateTime OfflineUntilUtc { get; set; }

        [DataMember(Name = "features")]
        public List<string> Features { get; set; }

        [DataMember(Name = "savedAtUtc")]
        public DateTime SavedAtUtc { get; set; }
    }

    public static class LicenseCache
    {
        private const string CacheFolderName = "MepPanel";
        private const string CacheFileName = "license-cache.dat";

        private static readonly byte[] AdditionalEntropy =
            Encoding.UTF8.GetBytes("MepPanel.AutoCAD.LicenseCache.v2");

        private static string CacheFilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    CacheFolderName,
                    CacheFileName);
            }
        }

        public static void Save(string phoneNumber, CheckLicenseResponse response)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                throw new ArgumentException("Số điện thoại không hợp lệ.", nameof(phoneNumber));
            }

            if (response == null || !response.Valid)
            {
                throw new InvalidOperationException("Không thể lưu giấy phép không hợp lệ.");
            }

            DateTime nowUtc = DateTime.UtcNow;
            DateTime licenseExpiresAtUtc = ParseUtc(response.LicenseExpiresAtUtc);
            DateTime offlineUntilUtc = ParseUtc(response.OfflineUntilUtc);

            if (offlineUntilUtc <= nowUtc)
            {
                offlineUntilUtc = nowUtc.AddHours(24);
            }

            DateTime maximumOffline = nowUtc.AddHours(24);
            if (offlineUntilUtc > maximumOffline)
            {
                offlineUntilUtc = maximumOffline;
            }

            if (licenseExpiresAtUtc != DateTime.MinValue && offlineUntilUtc > licenseExpiresAtUtc)
            {
                offlineUntilUtc = licenseExpiresAtUtc;
            }

            var cacheData = new LicenseCacheData
            {
                PhoneNumber = phoneNumber.Trim(),
                DeviceKey = DeviceIdentity.GetDeviceKey(),
                DisplayName = response.DisplayName,
                LicensePlan = response.LicensePlan,
                LicenseExpiresAtUtc = licenseExpiresAtUtc,
                OfflineUntilUtc = offlineUntilUtc,
                Features = response.Features != null
                    ? response.Features.Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                    : new List<string>(),
                SavedAtUtc = nowUtc
            };

            byte[] jsonBytes = Serialize(cacheData);
            byte[] encryptedBytes = ProtectedData.Protect(
                jsonBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath));
            File.WriteAllBytes(CacheFilePath, encryptedBytes);
        }

        public static bool TryLoadValid(
            string phoneNumber,
            out LicenseCacheData cacheData,
            out string message)
        {
            cacheData = null;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(phoneNumber) || !File.Exists(CacheFilePath))
            {
                message = "Chưa có giấy phép ngoại tuyến.";
                return false;
            }

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(CacheFilePath);
                byte[] jsonBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);

                cacheData = Deserialize<LicenseCacheData>(jsonBytes);
            }
            catch
            {
                message = "Tệp giấy phép ngoại tuyến bị lỗi.";
                return false;
            }

            if (cacheData == null ||
                !string.Equals(cacheData.PhoneNumber, phoneNumber.Trim(), StringComparison.Ordinal) ||
                !string.Equals(cacheData.DeviceKey, DeviceIdentity.GetDeviceKey(), StringComparison.Ordinal))
            {
                cacheData = null;
                message = "Giấy phép không thuộc số điện thoại/thiết bị này.";
                return false;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (cacheData.OfflineUntilUtc <= nowUtc)
            {
                cacheData = null;
                message = "Quyền sử dụng ngoại tuyến đã hết hạn.";
                return false;
            }

            if (cacheData.LicenseExpiresAtUtc != DateTime.MinValue &&
                cacheData.LicenseExpiresAtUtc <= nowUtc)
            {
                cacheData = null;
                message = "Giấy phép đã hết hạn.";
                return false;
            }

            message = "Giấy phép ngoại tuyến còn hiệu lực.";
            return true;
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    File.Delete(CacheFilePath);
                }
            }
            catch
            {
                // Không chặn plugin nếu xóa cache thất bại.
            }
        }

        private static byte[] Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return stream.ToArray();
            }
        }

        private static T Deserialize<T>(byte[] data)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(data))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        private static DateTime ParseUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DateTime.MinValue;
            }

            DateTime parsed;
            if (!DateTime.TryParse(
                    value.Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out parsed))
            {
                return DateTime.MinValue;
            }

            if (parsed.Kind == DateTimeKind.Utc)
            {
                return parsed;
            }

            if (parsed.Kind == DateTimeKind.Local)
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
    }
}
