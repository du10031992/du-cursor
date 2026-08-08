using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;
using MepPanelMvp.UI;

namespace MepPanelMvp.Commands
{
    /// <summary>
    /// He nuoc / PCCC — goi tu nut panel WPF (khong dang ky lenh CLI).
    /// Ve CAD luon qua MepDocumentContext (tranh eLockViolation).
    /// </summary>
    public class WaterFireCommands
    {
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

            // Toan bo prompt + ve CAD chay trong command context
            MepDocumentContext.Run(() =>
            {
                Editor ed = doc.Editor;
                WarnIfFeatureMissing(ed, featureCode, title);

                var kw = new PromptKeywordOptions(
                    "\n" + title + ": chon chuc nang [VeOng] PhuKien ThuVien TieuChuan TinhToan Render")
                {
                    AllowNone = true
                };
                kw.Keywords.Add("VeOng");
                kw.Keywords.Add("PhuKien");
                kw.Keywords.Add("ThuVien");
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
                        case "ThuVien":
                            InvokeStatic(
                                "MepPanel.Blocks.AutoCAD.Drawing.MepPipeLibraryService",
                                "ImportAmcLibrary");
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
                    string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    ed.WriteMessage("\n[MEP] Loi " + title + ": " + msg);
                    Write(title + ": " + msg);
                }
                catch (Exception ex)
                {
                    ed.WriteMessage("\n[MEP] Loi " + title + ": " + ex.Message);
                    Write(title + ": " + ex.Message);
                }
            });
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
            }
        }

        private static void InvokeKnowledge(string method, string systemKindName)
        {
            Type knowledge = FindType("MepPanel.Blocks.AutoCAD.Drawing.MepKnowledgeService");
            Type kindType = FindType("MepPanel.Core.Standards.MepSystemKind");
            if (knowledge == null || kindType == null)
            {
                throw new InvalidOperationException(
                    "Thieu service tinh toan. Dong AutoCAD, build lai plugin.");
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
                    "Thieu " + typeName + ". Dong AutoCAD, build lai plugin.");
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
                }
            }

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
