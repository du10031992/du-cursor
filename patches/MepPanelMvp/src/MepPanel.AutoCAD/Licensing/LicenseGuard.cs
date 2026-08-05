using System;
using System.Reflection;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace MepPanel.AutoCAD.Licensing
{
    public static class LicenseGuard
    {
        private const string LicenseServerBaseUrl =
            "http://192.168.1.7:5268/";

        public static bool EnsureAuthorized()
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

                    Application.ShowAlertDialog(
                        response != null && !string.IsNullOrWhiteSpace(response.Message)
                            ? response.Message
                            : "Thiet bi da bi khoa hoac giay phep khong hop le.");

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LicenseSession.Clear();

                Application.ShowAlertDialog(
                    "Khong kiem tra duoc giay phep voi may chu.\n" +
                    ex.Message);

                return false;
            }
        }

        private static bool TryShowLoginDialog()
        {
            try
            {
                var client = new LicenseApiClient(LicenseServerBaseUrl);

                string autoCadVersion =
                    Convert.ToString(Application.GetSystemVariable("ACADVER"))
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

                window.ShowDialog();

                return LicenseSession.IsAuthorized;
            }
            catch (Exception ex)
            {
                Application.ShowAlertDialog(
                    "Loi mo cua so dang nhap: " + ex.Message);
                return false;
            }
        }
    }
}
