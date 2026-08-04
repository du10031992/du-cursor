using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace MepPanel.AutoCAD.Licensing
{
    public static class LicensingHost
    {
        public static void ShowLogin()
        {
            var client = new LicenseApiClient(LicenseGuard.LicenseServerBaseUrl);
            string autoCadVersion =
                System.Convert.ToString(Application.GetSystemVariable("ACADVER")) ?? string.Empty;
            string pluginVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            using (var window = new LoginWindow(client, autoCadVersion, pluginVersion))
            {
                window.ShowDialog();
            }
        }

        public static bool EnsureFeature(string featureCode)
        {
            return LicenseGuard.EnsureFeature(featureCode);
        }

        public static void Logout()
        {
            LicenseSession.Clear();
            LicenseCache.Clear();
        }
    }
}
