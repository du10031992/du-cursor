using System;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MepPanelMvp.Commands
{
    /// <summary>
    /// Lenh he nuoc / PCCC goi tu nut panel WPF (HỆ NƯỚC / BÁO CHÁY).
    /// </summary>
    public class WaterFireCommands
    {
        [CommandMethod("MEPWATER", CommandFlags.Modal)]
        public void WaterMenu()
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

        [CommandMethod("MEPFIRE", CommandFlags.Modal)]
        public void FireMenu()
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
                MessageBox.Show("Khong co ban ve dang mo.", title);
                return;
            }

            Editor ed = doc.Editor;

            if (!EnsureFeature(featureCode, title))
            {
                return;
            }

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
            catch (Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                ed.WriteMessage("\n[MEP] Loi " + title + ": " + msg);
                MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool EnsureFeature(string featureCode, string title)
        {
            try
            {
                // Prefer Licensing gate when available.
                Type gate = FindType("MepPanel.AutoCAD.Licensing.PluginFeatureGate")
                    ?? FindType("MepPanel.Core.PluginFeatureGate");
                if (gate == null)
                {
                    return true;
                }

                MethodInfo ensure = gate.GetMethod("Ensure", BindingFlags.Public | BindingFlags.Static);
                if (ensure == null)
                {
                    return true;
                }

                object ok = ensure.Invoke(null, new object[] { featureCode });
                if (ok is bool b && !b)
                {
                    MessageBox.Show(
                        "Chua mo tinh nang " + title + " (" + featureCode + ").\n\n" +
                        "Vao Admin /admin → bat feature cho SĐT của bạn, roi dang nhap lai (MEPDB).",
                        title,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return false;
                }
            }
            catch
            {
                // neu gate loi van cho chay (dev)
            }

            return true;
        }

        private static void InvokeKnowledge(string method, string systemKindName)
        {
            Type knowledge = FindType("MepPanel.Blocks.AutoCAD.Drawing.MepKnowledgeService");
            Type kindType = FindType("MepPanel.Core.Standards.MepSystemKind");
            if (knowledge == null || kindType == null)
            {
                throw new InvalidOperationException(
                    "Thieu MepKnowledgeService. Chay lai .\\scripts\\build-plugin-release.ps1 (apply-water-pccc-patch).");
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
                    "Thieu " + typeName + ". Chay lai .\\scripts\\build-plugin-release.ps1 de apply patch he nuoc/PCCC.");
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

            return null;
        }
    }
}
