using System;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices.Core;
using MepPanel.Core;

namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>
    /// Chặn lệnh plugin nếu tài khoản/thiết bị bị khóa hoặc chưa được mở chức năng.
    /// </summary>
    public static class LicenseGuard
    {
        // Mac dinh dev; production dat trong MepPanel.config.json cạnh plugin.
        public static string LicenseServerBaseUrl => LicenseConfig.LicenseServerBaseUrl;

        public static bool EnsureAuthorized(bool requireFreshServerFeatures = false)
        {
            if (LicenseConfig.DevMode)
            {
                EnsureDevSession();
                return true;
            }

            if (!LicenseSession.IsAuthorized)
            {
                // Thử dùng cache offline ngắn hạn trước khi mở cửa sổ đăng nhập.
                if (!TryAuthorizeFromOfflineCache() && !TryShowLoginDialog())
                {
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

                CheckLicenseResponse response = AsyncRunner.Run(
                    () => client.CheckLicenseAsync(LicenseSession.PhoneNumber, pluginVersion));

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
            catch (System.Exception ex)
            {
                if (requireFreshServerFeatures)
                {
                    LicenseSession.Clear();
                    Application.ShowAlertDialog(ex.Message);
                    return false;
                }

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
            if (LicenseConfig.DevMode)
            {
                EnsureDevSession();
                return true;
            }

            if (!EnsureAuthorized(requireFreshServerFeatures: true))
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

        private static void EnsureDevSession()
        {
            if (LicenseSession.IsAuthorized)
            {
                return;
            }

            LicenseSession.Authorize(
                "dev-mode",
                "Che do dev (devMode=true)",
                string.Empty,
                PluginFeatures.All,
                "dev");
        }

        private static bool TryAuthorizeFromOfflineCache()
        {
            LicenseCacheData cache;
            string message;
            if (!LicenseCache.TryLoadAnyValid(out cache, out message))
            {
                return false;
            }

            LicenseSession.Authorize(
                cache.PhoneNumber,
                cache.DisplayName,
                string.Empty,
                cache.Features,
                cache.LicensePlan);

            return true;
        }

        private static bool TryShowLoginDialog()
        {
            var client = new LicenseApiClient(LicenseServerBaseUrl);

            string autoCadVersion =
                Convert.ToString(Application.GetSystemVariable("ACADVER"))
                ?? string.Empty;

            string pluginVersion = Assembly.GetExecutingAssembly()
                .GetName()
                .Version
                .ToString();

            using (var loginWindow = new LoginWindow(client, autoCadVersion, pluginVersion))
            {
                if (loginWindow.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                        "\nChưa đăng nhập giấy phép.");
                    return false;
                }

                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\nĐăng nhập giấy phép thành công.");

                if (loginWindow.LicenseInfo != null &&
                    !string.IsNullOrWhiteSpace(loginWindow.LicenseInfo.DisplayName))
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                        "\nXin chào: " + loginWindow.LicenseInfo.DisplayName);
                }

                return true;
            }
        }
    }
}
