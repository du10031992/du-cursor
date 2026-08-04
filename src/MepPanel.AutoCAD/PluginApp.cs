using System;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using MepPanel.AutoCAD.Licensing;

[assembly: ExtensionApplication(typeof(MepPanelMvp.PluginApp))]
[assembly: CommandClass(typeof(MepPanel.AutoCAD.Commands.LicenseCommands))]

namespace MepPanelMvp
{
    public sealed class PluginApp : IExtensionApplication
    {
        private bool _startupHandled;

        public void Initialize()
        {
            Application.Idle += Application_Idle;

            var document = Application.DocumentManager.MdiActiveDocument;
            document?.Editor.WriteMessage(
                "\nMEP Drawing Tool đang kiểm tra giấy phép...");
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (_startupHandled)
            {
                return;
            }

            _startupHandled = true;
            Application.Idle -= Application_Idle;

            var document = Application.DocumentManager.MdiActiveDocument;

            try
            {
                var apiClient = new LicenseApiClient(LicenseGuard.LicenseServerBaseUrl);

                string autoCadVersion =
                    Convert.ToString(Application.GetSystemVariable("ACADVER"))
                    ?? string.Empty;

                Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                string pluginVersion = assemblyVersion != null
                    ? assemblyVersion.ToString()
                    : "1.0.0.0";

                var loginWindow = new LoginWindow(
                    apiClient,
                    autoCadVersion,
                    pluginVersion);

                bool? loginResult = loginWindow.ShowDialog();

                if (loginResult == true)
                {
                    document?.Editor.WriteMessage("\nĐăng nhập giấy phép thành công.");
                    if (loginWindow.LicenseInfo != null &&
                        !string.IsNullOrWhiteSpace(loginWindow.LicenseInfo.DisplayName))
                    {
                        document?.Editor.WriteMessage(
                            "\nXin chào: " + loginWindow.LicenseInfo.DisplayName);
                    }

                    document?.Editor.WriteMessage(
                        "\nGõ MEPDB hoặc MEPHVAC để mở công cụ (nếu Admin đã mở chức năng).");
                }
                else
                {
                    document?.Editor.WriteMessage("\nChưa đăng nhập giấy phép.");
                }
            }
            catch (Exception ex)
            {
                document?.Editor.WriteMessage(
                    "\nLỗi khởi động hệ thống giấy phép: " + ex.Message);
            }
        }

        public void Terminate()
        {
            Application.Idle -= Application_Idle;
            LicenseSession.Clear();
        }
    }
}
