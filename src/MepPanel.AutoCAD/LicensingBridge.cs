using System;
using System.IO;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace MepPanel.Plugin
{
    internal static class LicensingBridge
    {
        private const string HostTypeName = "MepPanel.AutoCAD.Licensing.LicensingHost";
        private const string LicensingAssemblyFileName = "MepPanel.AutoCAD.Licensing.dll";

        private static Assembly _licensingAssembly;
        private static Type _hostType;

        public static void InvokeHost(string methodName, params object[] args)
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

                method.Invoke(null, args);
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

        public static bool InvokeHostBool(string methodName, params object[] args)
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

                object result = method.Invoke(null, args);
                return result is bool && (bool)result;
            }
            catch (TargetInvocationException ex)
            {
                ReportError(ex.InnerException ?? ex);
                return false;
            }
            catch (Exception ex)
            {
                ReportError(ex);
                return false;
            }
        }

        private static Type GetHostType()
        {
            if (_hostType != null)
            {
                return _hostType;
            }

            string pluginDir = Path.GetDirectoryName(typeof(LicensingBridge).Assembly.Location);
            string licensingPath = Path.Combine(pluginDir, LicensingAssemblyFileName);

            if (!File.Exists(licensingPath))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy " + LicensingAssemblyFileName +
                    " cạnh MepPanel.Plugin.dll. Hãy build lại solution.",
                    licensingPath);
            }

            _licensingAssembly = Assembly.LoadFrom(licensingPath);
            _hostType = _licensingAssembly.GetType(HostTypeName, true);
            return _hostType;
        }

        private static void ReportError(Exception ex)
        {
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMepPanel lỗi: " + ex.Message);
            AcApp.ShowAlertDialog("MepPanel lỗi:\n" + ex.Message);
        }
    }
}
