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

        /// <summary>
        /// Hoi server moi lan va chan neu Admin da tat chuc nang (MEPDB/MEPHVAC).
        /// </summary>
        public static bool EnsureFeature(string featureCode)
        {
            if (!EnsureAuthorized(requireFreshServerFeatures: true))
            {
                return false;
            }

            if (LicenseSession.HasFeature(featureCode))
            {
                return true;
            }

            AcApp.ShowAlertDialog(
                "Tai khoan cua ban chua duoc mo chuc nang: " + featureCode + "\n" +
                "Lien he Admin de Active chuc nang nay.");

            return false;
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
