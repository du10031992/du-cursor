using Autodesk.AutoCAD.Runtime;
using MepPanel.AutoCAD.Licensing;

// Không dùng IExtensionApplication để tránh lỗi khởi động chặn đăng ký lệnh.
[assembly: CommandClass(typeof(MepPanel.AutoCAD.Commands.LicenseCommands))]

namespace MepPanelMvp
{
    // Giữ class để tương thích; lệnh đăng ký qua CommandClass ở trên.
    internal static class PluginMarker
    {
        // Session được xóa khi AutoCAD unload assembly (nếu có).
        static PluginMarker()
        {
            // no-op
        }
    }
}
