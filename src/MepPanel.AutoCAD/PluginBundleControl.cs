using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace MepPanel.Plugin
{
    internal static class PluginBundleControl
    {
        private static readonly string[] RequiredDlls =
        {
            "MepPanel.Plugin.dll",
            "MepPanel.AutoCAD.Licensing.dll",
            "MepPanel.Blocks.AutoCAD.dll",
            "MepPanel.Core.dll"
        };

        private static bool _initialized;
        private static string _bundlePath = string.Empty;
        private static string _initMessage = string.Empty;

        public static string BundlePath
        {
            get { return _bundlePath; }
        }

        public static string InitMessage
        {
            get { return _initMessage; }
        }

        public static bool IsBundleValid { get; private set; }

        public static IReadOnlyList<string> MissingDlls { get; private set; } =
            Array.Empty<string>();

        public static void InitializeOnLoad()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _bundlePath = Path.GetDirectoryName(typeof(PluginBundleControl).Assembly.Location)
                ?? string.Empty;

            var missing = new List<string>();
            foreach (string dll in RequiredDlls)
            {
                string path = Path.Combine(_bundlePath, dll);
                if (!File.Exists(path))
                {
                    missing.Add(dll);
                }
            }

            MissingDlls = missing;
            IsBundleValid = missing.Count == 0;

            string version = typeof(PluginBundleControl).Assembly.GetName().Version.ToString();
            if (IsBundleValid)
            {
                _initMessage =
                    "MepPanel v" + version + " da load. Bundle OK (4 DLL). " +
                    "Lenh: MEPSTATUS, MEPLOGIN, MEPDB, MEPHVAC. " +
                    "Chua dang nhap - kiem soat license khi chay lenh tool.";
            }
            else
            {
                _initMessage =
                    "MepPanel v" + version + " thieu DLL: " +
                    string.Join(", ", missing) +
                    ". Build lai bang install-plugin-bundle.ps1";
            }

            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\n" + _initMessage);
        }

        public static void WriteStatusToCommandLine()
        {
            var editor = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            if (editor == null)
            {
                return;
            }

            editor.WriteMessage("\n--- MepPanel bundle ---");
            editor.WriteMessage("\nDuong dan: " + (_bundlePath.Length > 0 ? _bundlePath : "(unknown)"));
            editor.WriteMessage("\nBundle hop le: " + (IsBundleValid ? "Co" : "Khong"));

            if (!IsBundleValid && MissingDlls.Count > 0)
            {
                editor.WriteMessage("\nThieu: " + string.Join(", ", MissingDlls));
            }

            foreach (string dll in RequiredDlls)
            {
                string path = Path.Combine(_bundlePath, dll);
                editor.WriteMessage("\n  " + dll + ": " + (File.Exists(path) ? "OK" : "MISSING"));
            }

            LicensingBridge.InvokeHost("WriteControlStatus");
        }
    }
}
