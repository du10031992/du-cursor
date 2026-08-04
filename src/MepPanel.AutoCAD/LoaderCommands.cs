using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(MepPanel.Plugin.PluginApp))]
[assembly: CommandClass(typeof(MepPanel.Plugin.LoaderCommands))]

namespace MepPanel.Plugin
{
    public sealed class PluginApp : IExtensionApplication
    {
        public void Initialize()
        {
            // Không mở UI lúc khởi động — chỉ báo đã load.
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMepPanel.Plugin v0.2.2 đã load. Gõ MEPSTATUS / MEPLOGIN / MEPDB / MEPHVAC.");
        }

        public void Terminate()
        {
        }
    }

    /// <summary>
    /// Lệnh loader — cấu trúc giống MepPanel.LoadTest (đã chứng minh NETLOAD OK).
    /// </summary>
    public class LoaderCommands
    {
        [CommandMethod("MEPSTATUS", CommandFlags.Modal)]
        public void Status()
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMepPanel v0.2.2 loader OK. Gõ MEPLOGIN / MEPDB / MEPHVAC.");
        }

        [CommandMethod("MEPLOGIN", CommandFlags.Modal)]
        public void Login()
        {
            LicensingBridge.InvokeHost("ShowLogin");
        }

        [CommandMethod("MEPDB", CommandFlags.Modal)]
        public void OpenMepDb()
        {
            if (!LicensingBridge.InvokeHostBool("EnsureFeature", "MEPDB"))
            {
                return;
            }

            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPDB đã được mở khóa cho tài khoản này.");
        }

        [CommandMethod("MEPHVAC", CommandFlags.Modal)]
        public void OpenMepHvac()
        {
            if (!LicensingBridge.InvokeHostBool("EnsureFeature", "MEPHVAC"))
            {
                return;
            }

            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPHVAC đã được mở khóa cho tài khoản này.");
        }

        [CommandMethod("MEPLOGOUT", CommandFlags.Modal)]
        public void Logout()
        {
            LicensingBridge.InvokeHost("Logout");
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nĐã đăng xuất giấy phép MepPanel.");
        }
    }
}
