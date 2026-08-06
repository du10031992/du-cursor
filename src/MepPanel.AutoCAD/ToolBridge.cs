using System;
using System.IO;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace MepPanel.Plugin
{
    internal static class ToolBridge
    {
        private const string HostTypeName = "MepPanel.Blocks.AutoCAD.ToolHost";
        private const string ToolsAssemblyFileName = "MepPanel.Blocks.AutoCAD.dll";

        private static Assembly _toolsAssembly;
        private static Type _hostType;

        public static void ShowMepDb()
        {
            InvokeHost("ShowMepDb");
        }

        public static void ShowMepHvac()
        {
            InvokeHost("ShowMepHvac");
        }

        public static void ShowDrawingToolPanel()
        {
            InvokeHost("ShowDrawingToolPanel");
        }

        private static void InvokeHost(string methodName)
        {
            try
            {
                Type hostType = GetHostType();
                MethodInfo method = hostType.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Static);

                if (method == null)
                {
                    throw new MissingMethodException(hostType.FullName, methodName);
                }

                method.Invoke(null, null);
            }
            catch (TargetInvocationException ex)
            {
                ReportError(ex.InnerException ?? ex);
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        private static Type GetHostType()
        {
            if (_hostType != null)
            {
                return _hostType;
            }

            string pluginDir = Path.GetDirectoryName(typeof(ToolBridge).Assembly.Location);
            string toolsPath = Path.Combine(pluginDir, ToolsAssemblyFileName);

            if (!File.Exists(toolsPath))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy " + ToolsAssemblyFileName +
                    " cạnh MepPanel.Plugin.dll. Hãy build lại solution.",
                    toolsPath);
            }

            _toolsAssembly = Assembly.LoadFrom(toolsPath);
            _hostType = _toolsAssembly.GetType(HostTypeName, true);
            return _hostType;
        }

        private static void ReportError(Exception ex)
        {
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMepPanel tool lỗi: " + ex.Message);
            AcApp.ShowAlertDialog("MepPanel tool lỗi:\n" + ex.Message);
        }
    }
}
