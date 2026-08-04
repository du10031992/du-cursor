using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(MepPanel.AutoCAD.Commands.LicenseCommands))]

namespace MepPanel.AutoCAD.Commands
{
    /// <summary>
    /// Loader mỏng — giống LoadTest, gọi licensing qua reflection để NETLOAD ổn định.
    /// </summary>
    public class LicenseCommands
    {
        private const string HostTypeName = "MepPanel.AutoCAD.Licensing.LicensingHost";
        private const string LicensingAssemblyFileName = "MepPanel.AutoCAD.Licensing.dll";

        private static Assembly _licensingAssembly;
        private static Type _hostType;

        [CommandMethod("MEPSTATUS", CommandFlags.Modal)]
        public void Status()
        {
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMepPanel v0.2.2 loader OK. Gõ MEPLOGIN / MEPDB / MEPHVAC.");
        }

        [CommandMethod("MEPLOGIN", CommandFlags.Modal)]
        public void Login()
        {
            InvokeHost("ShowLogin");
        }

        [CommandMethod("MEPDB", CommandFlags.Modal)]
        public void OpenMepDb()
        {
            if (!InvokeHostBool("EnsureFeature", "MEPDB"))
            {
                return;
            }

            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPDB đã được mở khóa cho tài khoản này.");
        }

        [CommandMethod("MEPHVAC", CommandFlags.Modal)]
        public void OpenMepHvac()
        {
            if (!InvokeHostBool("EnsureFeature", "MEPHVAC"))
            {
                return;
            }

            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nMEPHVAC đã được mở khóa cho tài khoản này.");
        }

        [CommandMethod("MEPLOGOUT", CommandFlags.Modal)]
        public void Logout()
        {
            InvokeHost("Logout");
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nĐã đăng xuất giấy phép MepPanel.");
        }

        private static void InvokeHost(string methodName, params object[] args)
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

        private static bool InvokeHostBool(string methodName, params object[] args)
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

            string pluginDir = Path.GetDirectoryName(typeof(LicenseCommands).Assembly.Location);
            string licensingPath = Path.Combine(pluginDir, LicensingAssemblyFileName);

            if (!File.Exists(licensingPath))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy " + LicensingAssemblyFileName +
                    " cạnh MepPanel.AutoCAD.dll. Hãy build lại solution.",
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
