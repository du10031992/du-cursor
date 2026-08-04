using System;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>
    /// Chặn lệnh plugin nếu tài khoản/thiết bị bị khóa hoặc chưa được mở chức năng.
    /// </summary>
    public static class LicenseGuard
    {
        // Khớp với LicenseServer đang chạy local.
        public const string LicenseServerBaseUrl = "http://localhost:7024/";

        public static bool EnsureAuthorized()
        {
            if (!LicenseSession.IsAuthorized)
            {
                // Thử dùng cache offline ngắn hạn.
                if (!TryAuthorizeFromOfflineCache())
                {
                    Application.ShowAlertDialog(
                        "Bạn chưa đăng nhập hoặc thiết bị chưa được cấp quyền.\n" +
                        "Hãy đăng nhập bằng số điện thoại được Admin cấp.");
                    return false;
                }
            }

            try
            {
                var client = new LicenseApiClient(LicenseServerBaseUrl);
                client.SetAccessToken(LicenseSession.AccessToken);

                string pluginVersion = Assembly.GetExecutingAssembly()
                    .GetName()
                    .Version
                    .ToString();

                CheckLicenseResponse response = client
                    .CheckLicenseAsync(LicenseSession.PhoneNumber, pluginVersion)
                    .GetAwaiter()
                    .GetResult();

                if (response == null || !response.Valid)
                {
                    LicenseSession.Clear();
                    LicenseCache.Clear();

                    Application.ShowAlertDialog(
                        response != null && !string.IsNullOrWhiteSpace(response.Message)
                            ? response.Message
                            : "Thiết bị đã bị khóa hoặc giấy phép không hợp lệ.");

                    return false;
                }

                LicenseCache.Save(LicenseSession.PhoneNumber, response);
                LicenseSession.Authorize(
                    LicenseSession.PhoneNumber,
                    response.DisplayName,
                    LicenseSession.AccessToken,
                    response.Features,
                    response.LicensePlan);

                return true;
            }
            catch (System.Exception)
            {
                // Mất mạng: cho phép trong cửa sổ offline nếu cache còn hạn.
                LicenseCacheData cache;
                string message;
                if (LicenseCache.TryLoadValid(LicenseSession.PhoneNumber, out cache, out message))
                {
                    LicenseSession.Authorize(
                        cache.PhoneNumber,
                        cache.DisplayName,
                        LicenseSession.AccessToken,
                        cache.Features,
                        cache.LicensePlan);
                    return true;
                }

                LicenseSession.Clear();
                Application.ShowAlertDialog(
                    "Không kiểm tra được giấy phép với máy chủ và cache ngoại tuyến đã hết.\n" +
                    message);
                return false;
            }
        }

        public static bool EnsureFeature(string featureCode)
        {
            if (!EnsureAuthorized())
            {
                return false;
            }

            if (LicenseSession.HasFeature(featureCode))
            {
                return true;
            }

            Application.ShowAlertDialog(
                "Tài khoản của bạn chưa được mở chức năng: " + featureCode + "\n" +
                "Liên hệ Admin để Active chức năng này.");

            return false;
        }

        private static bool TryAuthorizeFromOfflineCache()
        {
            // Không có phone trong session thì không dùng cache.
            return false;
        }
    }
}
