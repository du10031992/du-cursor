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

        public static void WriteControlStatus()
        {
            var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (editor == null)
            {
                return;
            }

            string pluginVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            editor.WriteMessage("\n--- MepPanel license control ---");
            editor.WriteMessage("\nPlugin: v" + pluginVersion);
            editor.WriteMessage("\nLicense server: " + LicenseGuard.LicenseServerBaseUrl);

            if (!LicenseSession.IsAuthorized)
            {
                editor.WriteMessage("\nTrang thai: Chua dang nhap");
                editor.WriteMessage("\nChay MEPLOGIN hoac MEPDB/MEPHVAC de dang nhap.");
                editor.WriteMessage("\nAdmin kiem soat: user, thiet bi, MEPDB, MEPHVAC tren /admin");
                return;
            }

            editor.WriteMessage("\nTrang thai: Da dang nhap");
            editor.WriteMessage("\nSDT: " + LicenseSession.PhoneNumber);
            if (!string.IsNullOrWhiteSpace(LicenseSession.DisplayName))
            {
                editor.WriteMessage("\nTen: " + LicenseSession.DisplayName);
            }

            if (!string.IsNullOrWhiteSpace(LicenseSession.LicensePlan))
            {
                editor.WriteMessage("\nGoi: " + LicenseSession.LicensePlan);
            }

            editor.WriteMessage(
                "\nFeature: " + string.Join(", ", LicenseSession.Features));
            editor.WriteMessage("\nAdmin co the khoa user/thiet bi/tung feature bat ky luc.");
        }
    }
}
