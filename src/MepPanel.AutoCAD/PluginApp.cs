using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using MepPanel.AutoCAD.Licensing;

[assembly: ExtensionApplication(typeof(MepPanelMvp.PluginApp))]
[assembly: CommandClass(typeof(MepPanel.AutoCAD.Commands.LicenseCommands))]

namespace MepPanelMvp
{
    public sealed class PluginApp : IExtensionApplication
    {
        private bool _welcomeShown;

        public void Initialize()
        {
            // Lúc NETLOAD thường chưa có document → chờ Idle mới in được thông báo.
            Application.Idle += Application_Idle;
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (_welcomeShown)
            {
                return;
            }

            _welcomeShown = true;
            Application.Idle -= Application_Idle;

            var document = Application.DocumentManager.MdiActiveDocument;
            document?.Editor.WriteMessage(
                "\nMEP Drawing Tool v0.2.1 đã tải. Gõ MEPDB, MEPHVAC hoặc MEPLOGIN để đăng nhập.");
        }

        public void Terminate()
        {
            Application.Idle -= Application_Idle;
            LicenseSession.Clear();
        }
    }
}
