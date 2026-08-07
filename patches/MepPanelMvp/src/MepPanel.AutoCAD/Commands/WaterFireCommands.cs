using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

namespace MepPanelMvp.Commands
{
    /// <summary>
    /// He nuoc / PCCC — goi tu nut panel WPF hoac lenh MEPWATER / MEPFIRE.
    /// Khong dung WinForms MessageBox (tranh loi reference WPF project).
    /// </summary>
    public class WaterFireCommands
    {
        [CommandMethod("MEPWATER", CommandFlags.Modal)]
        public void WaterMenuCmd() => ShowWaterMenu();

        [CommandMethod("MEPFIRE", CommandFlags.Modal)]
        public void FireMenuCmd() => ShowFireMenu();

        /// <summary>Goi truc tiep tu HeNuoc_Click (khong can SendStringToExecute).</summary>
        public static void ShowWaterMenu()
        {
            RunSystemMenu(
                title: "He nuoc",
                featureCode: "MEPDBWATER",
                drawType: "MepPanel.Blocks.AutoCAD.Drawing.MepWaterDrawingService",
                drawMethod: "DrawPipeRun",
                fittingMethod: "PlaceFitting",
                systemKind: "Water",
                renderMethod: "RenderWater");
        }

        /// <summary>Goi truc tiep tu BaoChay_Click.</summary>
        public static void ShowFireMenu()
        {
            RunSystemMenu(
                title: "PCCC / Bao chay",
                featureCode: "MEPDBSMOKE",
                drawType: "MepPanel.Blocks.AutoCAD.Drawing.MepFireDrawingService",
                drawMethod: "DrawPipeRun",
                fittingMethod: "PlaceFitting",
                systemKind: "Fire",
                renderMethod: "RenderFire");
        }

        private static void RunSystemMenu(
            string title,
            string featureCode,
            string drawType,
            string drawMethod,
            string fittingMethod,
            string systemKind,
            string renderMethod)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                Write("Khong co ban ve dang mo.");
                return;
            }

            Editor ed = doc.Editor;

            // Chi canh bao neu thieu sub-feature — van cho dung neu da login MEPDB.
            WarnIfFeatureMissing(ed, featureCode, title);

            var kw = new PromptKeywordOptions(
                "\n" + title + ": chon chuc nang [VeOng] PhuKien TieuChuan TinhToan Render")
            {
                AllowNone = true
            };
            kw.Keywords.Add("VeOng");
            kw.Keywords.Add("PhuKien");
            kw.Keywords.Add("TieuChuan");
            kw.Keywords.Add("TinhToan");
            kw.Keywords.Add("Render");
            kw.Keywords.Default = "VeOng";

            PromptResult res = ed.GetKeywords(kw);
            if (res.Status != PromptStatus.OK && res.Status != PromptStatus.None)
            {
                return;
            }

            string choice = res.Status == PromptStatus.None ? "VeOng" : res.StringResult;
            try
            {
                switch (choice)
                {
                    case "PhuKien":
                        InvokeStatic(drawType, fittingMethod);
                        break;
                    case "TieuChuan":
                        InvokeKnowledge("ShowStandards", systemKind);
                        break;
                    case "TinhToan":
                        InvokeKnowledge("RunQuickCalculation", systemKind);
                        break;
                    case "Render":
                        InvokeStatic(
                            "MepPanel.Blocks.AutoCAD.Drawing.MepPipeRenderService",
                            renderMethod);
                        break;
                    default:
                        InvokeStatic(drawType, drawMethod);
                        break;
                }
            }
            catch (TargetInvocationException ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                ed.WriteMessage("\n[MEP] Loi " + title + ": " + msg);
                Write(title + ": " + msg);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[MEP] Loi " + title + ": " + ex.Message);
                Write(title + ": " + ex.Message);
            }
        }

        private static void WarnIfFeatureMissing(Editor ed, string featureCode, string title)
        {
            try
            {
                Type gate = FindType("MepPanel.AutoCAD.Licensing.PluginFeatureGate")
                    ?? FindType("MepPanel.Core.PluginFeatureGate");
                if (gate == null)
                {
                    return;
                }

                MethodInfo canUse = gate.GetMethod("CanUse", BindingFlags.Public | BindingFlags.Static);
                if (canUse == null)
                {
                    return;
                }

                object ok = canUse.Invoke(null, new object[] { featureCode });
                if (ok is bool b && !b)
                {
                    ed.WriteMessage(
                        "\n[MEP] Canh bao: chua bat feature " + featureCode +
                        " tren Admin. Van thu chay " + title + ".");
                }
            }
            catch
            {
                /* ignore */
            }
        }

        private static void InvokeKnowledge(string method, string systemKindName)
        {
            Type knowledge = FindType("MepPanel.Blocks.AutoCAD.Drawing.MepKnowledgeService");
            Type kindType = FindType("MepPanel.Core.Standards.MepSystemKind");
            if (knowledge == null || kindType == null)
            {
                throw new InvalidOperationException(
                    "Thieu service tinh toan. Dong AutoCAD, chay: .\\scripts\\build-plugin-release.ps1");
            }

            object kind = Enum.Parse(kindType, systemKindName);
            MethodInfo mi = knowledge.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (mi == null)
            {
                throw new InvalidOperationException("Khong tim thay " + method);
            }

            mi.Invoke(null, new[] { kind });
        }

        private static void InvokeStatic(string typeName, string methodName)
        {
            Type t = FindType(typeName);
            if (t == null)
            {
                throw new InvalidOperationException(
                    "Thieu " + typeName + ". Dong AutoCAD, chay: .\\scripts\\build-plugin-release.ps1");
            }

            MethodInfo mi = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (mi == null)
            {
                throw new InvalidOperationException("Khong tim thay " + typeName + "." + methodName);
            }

            mi.Invoke(null, null);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(fullName, throwOnError: false);
                    if (t != null)
                    {
                        return t;
                    }
                }
                catch
                {
                    /* skip */
                }
            }

            // Thu load Blocks DLL canh plugin
            try
            {
                string dir = Path.GetDirectoryName(typeof(WaterFireCommands).Assembly.Location) ?? "";
                string blocks = Path.Combine(dir, "MepPanel.Blocks.AutoCAD.dll");
                if (File.Exists(blocks))
                {
                    Assembly asm = Assembly.LoadFrom(blocks);
                    Type t = asm.GetType(fullName, throwOnError: false);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }
            catch
            {
                /* skip */
            }

            return null;
        }

        private static void Write(string message)
        {
            try
            {
                System.Windows.MessageBox.Show(
                    message,
                    "MepPanel",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage("\n[MEP] " + message);
            }
        }
    }
}
