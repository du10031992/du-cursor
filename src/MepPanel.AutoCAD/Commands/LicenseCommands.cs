using Autodesk.AutoCAD.Runtime;
using MepPanel.AutoCAD.Licensing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace MepPanel.AutoCAD.Commands
{
    public class LicenseCommands
    {
        [CommandMethod("MEPLOGIN")]
        public void Login()
        {
            var client = new LicenseApiClient(LicenseGuard.LicenseServerBaseUrl);
            string autoCadVersion =
                System.Convert.ToString(AcApp.GetSystemVariable("ACADVER")) ?? string.Empty;
            string pluginVersion = typeof(LicenseCommands).Assembly.GetName().Version.ToString();

            var window = new LoginWindow(client, autoCadVersion, pluginVersion);
            window.ShowDialog();
        }

        [CommandMethod("MEPDB")]
        public void OpenMepDb()
        {
            if (!LicenseGuard.EnsureFeature(PluginFeatureCodes.MepDb))
            {
                return;
            }

            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPDB đã được mở khóa cho tài khoản này.");
            // TODO: gọi UI/tool MEPDB thật của bạn tại đây.
        }

        [CommandMethod("MEPHVAC")]
        public void OpenMepHvac()
        {
            if (!LicenseGuard.EnsureFeature(PluginFeatureCodes.MepHvac))
            {
                return;
            }

            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPHVAC đã được mở khóa cho tài khoản này.");
            // TODO: gọi UI/tool MEPHVAC thật của bạn tại đây.
        }

        [CommandMethod("MEPLOGOUT")]
        public void Logout()
        {
            LicenseSession.Clear();
            LicenseCache.Clear();
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nĐã đăng xuất giấy phép MepPanel.");
        }
    }
}
