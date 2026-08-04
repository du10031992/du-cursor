using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using MepPanel.AutoCAD.Licensing;

[assembly: ExtensionApplication(typeof(MepPanelMvp.PluginApp))]
[assembly: CommandClass(typeof(MepPanel.AutoCAD.Commands.LicenseCommands))]

namespace MepPanelMvp
{
    public sealed class PluginApp : IExtensionApplication
    {
        public void Initialize()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            document?.Editor.WriteMessage(
                "\nMEP Drawing Tool đã tải. Gõ MEPDB, MEPHVAC hoặc MEPLOGIN để đăng nhập.");
        }

        public void Terminate()
        {
            LicenseSession.Clear();
        }
    }
}
