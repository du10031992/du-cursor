using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using MepPanel.Core;

[assembly: CommandClass(typeof(MepPanel.Plugin.LoaderCommands))]

namespace MepPanel.Plugin
{
    /// <summary>
    /// Lệnh loader — giống LoadTest (NETLOAD ổn định), gọi licensing/tools qua reflection.
    /// </summary>
    public class LoaderCommands
    {
        static LoaderCommands()
        {
            PluginBundleControl.InitializeOnLoad();
        }

        [CommandMethod("MEPSTATUS", CommandFlags.Modal)]
        public void Status()
        {
            PluginBundleControl.WriteStatusToCommandLine();
        }

        [CommandMethod("MEPLOGIN", CommandFlags.Modal)]
        public void Login()
        {
            LicensingBridge.InvokeHost("ShowLogin");
        }

        [CommandMethod("MEPDB", CommandFlags.Modal)]
        public void OpenMepDb()
        {
            if (!LicensingBridge.InvokeHostBool("EnsureFeature", PluginFeatures.MepDb))
            {
                return;
            }

            ToolBridge.ShowMepDb();
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPDB panel đã mở.");
        }

        [CommandMethod("MEPHVAC", CommandFlags.Modal)]
        public void OpenMepHvac()
        {
            if (!LicensingBridge.InvokeHostBool("EnsureFeature", PluginFeatures.MepHvac))
            {
                return;
            }

            ToolBridge.ShowMepHvac();
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPHVAC panel đã mở.");
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
