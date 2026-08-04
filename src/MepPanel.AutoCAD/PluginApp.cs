using Autodesk.AutoCAD.Runtime;
using MepPanel.AutoCAD.Licensing;

[assembly: ExtensionApplication(typeof(MepPanelMvp.PluginApp))]
[assembly: CommandClass(typeof(MepPanel.AutoCAD.Commands.LicenseCommands))]

namespace MepPanelMvp
{
    /// <summary>
    /// Khởi động plugin: không mở UI. Đăng nhập chỉ khi gõ MEPDB/MEPHVAC/MEPLOGIN.
    /// </summary>
    public sealed class PluginApp : IExtensionApplication
    {
        public void Initialize()
        {
            // Cố ý để trống: không hiện popup lúc NETLOAD/khởi động.
            // Lệnh được đăng ký qua [assembly: CommandClass].
        }

        public void Terminate()
        {
            LicenseSession.Clear();
        }
    }
}
