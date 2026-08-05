using System;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace MepPanel.AutoCAD.Licensing
{
    public static class LicenseGuard
    {
        private const string LicenseServerBaseUrl =
            "http://192.168.1.7:5268/";

        private const string EntryFeature = "MEPDB";

        /// <summary>
        /// Lenh MEPDB duy nhat: dang nhap + kiem tra quyen vao plugin.
        /// </summary>
        public static bool EnsureEntry()
        {
            if (!LicenseSession.IsAuthorized)
            {
                if (!TryShowLoginDialog())
                {
                    return false;
                }
            }

            if (!EnsureAuthorized(requireFreshServerFeatures: true))
            {
                return false;
            }

            if (!LicenseSession.HasFeature(EntryFeature))
            {
                AcApp.ShowAlertDialog(
                    "Tai khoan chua duoc mo quyen vao plugin (MEPDB).\n" +
                    "Lien he Admin de Active.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Chuc nang phu trong panel — khong goi server lai (da refresh o EnsureEntry).
        /// </summary>
        public static bool EnsureSubFeature(string subFeatureCode)
        {
            if (!LicenseSession.IsAuthorized || !LicenseSession.HasFeature(EntryFeature))
            {
                AcApp.ShowAlertDialog(
                    "Chua dang nhap hoac chua co quyen vao plugin.\n" +
                    "Hay goi lenh MEPDB de dang nhap.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(subFeatureCode))
            {
                return false;
            }

            if (LicenseSession.HasFeature(subFeatureCode))
            {
                return true;
            }

            AcApp.ShowAlertDialog(
                "Chuc nang phu chua duoc mo: " + subFeatureCode + "\n" +
                "Lien he Admin de bat tren trang /admin.");

            return false;
        }

        public static bool EnsureAuthorized(bool requireFreshServerFeatures = false)
        {
            if (!LicenseSession.IsAuthorized)
            {
                if (!TryShowLoginDialog())
                {
                    return false;
                }
            }

            try
            {
                var client = new LicenseApiClient(LicenseServerBaseUrl);
                client.SetAccessToken(LicenseSession.AccessToken);

                string pluginVersion = typeof(LicenseGuard)
                    .Assembly
                    .GetName()
                    .Version
                    .ToString();

                CheckLicenseResponse response = AsyncRunner.Run(
                    () => client.CheckLicenseAsync(
                        LicenseSession.PhoneNumber,
                        pluginVersion));

                if (response == null || !response.Valid)
                {
                    LicenseSession.Clear();

                    AcApp.ShowAlertDialog(
                        response != null && !string.IsNullOrWhiteSpace(response.Message)
                            ? response.Message
                            : "Thiet bi da bi khoa hoac giay phep khong hop le.");

                    return false;
                }

                LicenseSession.Authorize(
                    LicenseSession.PhoneNumber,
                    response.DisplayName,
                    LicenseSession.AccessToken,
                    response.Features,
                    response.LicensePlan);

                return true;
            }
            catch (Exception ex)
            {
                if (requireFreshServerFeatures)
                {
                    LicenseSession.Clear();
                    AcApp.ShowAlertDialog(ex.Message);
                    return false;
                }

                LicenseSession.Clear();

                AcApp.ShowAlertDialog(
                    "Khong kiem tra duoc giay phep voi may chu.\n" +
                    ex.Message);

                return false;
            }
        }

        /// <summary>Giữ tương thích build cũ — map về EnsureEntry hoặc EnsureSubFeature.</summary>
        public static bool EnsureFeature(string featureCode)
        {
            if (string.IsNullOrWhiteSpace(featureCode) ||
                string.Equals(featureCode, EntryFeature, StringComparison.OrdinalIgnoreCase))
            {
                return EnsureEntry();
            }

            return EnsureSubFeature(featureCode);
        }

        private static bool TryShowLoginDialog()
        {
            try
            {
                var client = new LicenseApiClient(LicenseServerBaseUrl);

                string autoCadVersion =
                    Convert.ToString(AcApp.GetSystemVariable("ACADVER"))
                    ?? string.Empty;

                string pluginVersion = Assembly.GetExecutingAssembly()
                    .GetName()
                    .Version
                    .ToString();

                var loginControl = new LoginWindow(
                    client,
                    autoCadVersion,
                    pluginVersion);

                var window = new Window
                {
                    Title = "Dang nhap giay phep",
                    Content = loginControl,
                    Width = 400,
                    Height = 520,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                DispatcherTimer closeTimer = null;
                DispatcherTimer delayTimer = null;

                closeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                closeTimer.Tick += (sender, args) =>
                {
                    if (!LicenseSession.IsAuthorized)
                    {
                        return;
                    }

                    closeTimer.Stop();

                    delayTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(600)
                    };
                    delayTimer.Tick += (sender2, args2) =>
                    {
                        delayTimer.Stop();
                        window.Close();
                    };
                    delayTimer.Start();
                };
                closeTimer.Start();

                try
                {
                    window.ShowDialog();
                }
                finally
                {
                    closeTimer.Stop();
                    if (delayTimer != null)
                    {
                        delayTimer.Stop();
                    }
                }

                return LicenseSession.IsAuthorized;
            }
            catch (Exception ex)
            {
                AcApp.ShowAlertDialog(
                    "Loi mo cua so dang nhap: " + ex.Message);
                return false;
            }
        }
    }
}
