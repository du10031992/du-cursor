using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using MepPanel.Core.Calculations;
using MepPanel.Core.Standards;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>Tieu chuan + tinh toan he nuoc / PCCC (va xem TC dien/HVAC).</summary>
    public static class MepKnowledgeService
    {
        public static void ShowStandards(MepSystemKind system)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            var sb = new StringBuilder();
            sb.AppendLine("=== " + MepStandardsCatalog.GetSystemTitleVi(system) + " — TIEU CHUAN ===");

            foreach (MepStandardInfo s in MepStandardsCatalog.GetBySystem(system))
            {
                sb.AppendLine();
                sb.AppendLine(s.Code + " — " + s.TitleVi);
                sb.AppendLine("  Co quan: " + s.Issuer);
                sb.AppendLine("  Pham vi: " + s.Scope);
                sb.AppendLine("  Ap dung: " + s.ApplyNote);
            }

            sb.AppendLine();
            sb.AppendLine("=== CONG THUC ===");
            foreach (MepFormulaInfo f in MepFormulaCatalog.GetBySystem(system))
            {
                sb.AppendLine();
                sb.AppendLine(f.NameVi + " [" + f.Id + "]");
                sb.AppendLine("  " + f.Expression);
                sb.AppendLine("  Bien: " + f.VariablesVi);
                sb.AppendLine("  Don vi: " + f.UnitNote);
                sb.AppendLine("  TC: " + f.StandardRef);
            }

            string text = sb.ToString();
            doc.Editor.WriteMessage("\n" + text.Replace("\r", string.Empty));
            MessageBox.Show(
                text,
                MepStandardsCatalog.GetSystemTitleVi(system) + " — Tieu chuan & Cong thuc",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        public static void RunQuickCalculation(MepSystemKind system)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            try
            {
                MepCalculationResult result;
                switch (system)
                {
                    case MepSystemKind.Water:
                        result = RunWaterCalc(ed);
                        break;
                    case MepSystemKind.Fire:
                        result = RunFireCalc(ed);
                        break;
                    default:
                        ed.WriteMessage("\n[MEP] Tinh toan nhanh hien ho tro He nuoc va PCCC. Dung TC + CT de xem cong thuc.");
                        MessageBox.Show(
                            "Tinh toan nhanh hien ho tro He nuoc va PCCC.\nMo TC + CT de xem tieu chuan / cong thuc.",
                            "Tinh toan",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                }

                if (result == null)
                {
                    return;
                }

                ed.WriteMessage("\n[MEP TINH TOAN] " + result);
                MessageBox.Show(result.ToString(), "Ket qua tinh toan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[MEP] Loi tinh toan: " + ex.Message);
                MessageBox.Show(ex.Message, "Loi tinh toan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static MepCalculationResult RunWaterCalc(Editor ed)
        {
            var kw = new PromptKeywordOptions("\nTinh toan nuoc: [VanToc] LuuLuong TonThat")
            {
                AllowNone = true
            };
            kw.Keywords.Add("VanToc");
            kw.Keywords.Add("LuuLuong");
            kw.Keywords.Add("TonThat");
            kw.Keywords.Default = "VanToc";
            PromptResult mode = ed.GetKeywords(kw);
            if (mode.Status != PromptStatus.OK && mode.Status != PromptStatus.None)
            {
                return null;
            }

            string m = mode.Status == PromptStatus.None ? "VanToc" : mode.StringResult;
            if (m == "LuuLuong")
            {
                double v = ReadDouble(ed, "Van toc v (m/s): ", 1.5);
                double dn = ReadDouble(ed, "Duong kinh DN (mm): ", 50);
                return WaterCalculations.FlowFromVelocity(v, dn);
            }

            if (m == "TonThat")
            {
                double f = ReadDouble(ed, "He so ma sat f: ", 0.02);
                double len = ReadDouble(ed, "Chieu dai L (m): ", 30);
                double dn = ReadDouble(ed, "DN (mm): ", 50);
                double v = ReadDouble(ed, "Van toc v (m/s): ", 1.5);
                return WaterCalculations.DarcyHeadLoss(f, len, dn, v);
            }

            double q = ReadDouble(ed, "Luu luong Q (L/s): ", 2.5);
            double d = ReadDouble(ed, "DN (mm): ", 50);
            return WaterCalculations.PipeVelocityFromFlowLs(q, d);
        }

        private static MepCalculationResult RunFireCalc(Editor ed)
        {
            var kw = new PromptKeywordOptions("\nTinh toan PCCC: [Sprinkler] MatDo Voi Bon")
            {
                AllowNone = true
            };
            kw.Keywords.Add("Sprinkler");
            kw.Keywords.Add("MatDo");
            kw.Keywords.Add("Voi");
            kw.Keywords.Add("Bon");
            kw.Keywords.Default = "Sprinkler";
            PromptResult mode = ed.GetKeywords(kw);
            if (mode.Status != PromptStatus.OK && mode.Status != PromptStatus.None)
            {
                return null;
            }

            string m = mode.Status == PromptStatus.None ? "Sprinkler" : mode.StringResult;
            switch (m)
            {
                case "MatDo":
                    double q = ReadDouble(ed, "Tong luu luong Q (L/min): ", 600);
                    double area = ReadDouble(ed, "Dien tich thiet ke A (m2): ", 120);
                    return FireCalculations.SprayDensity(q, area);
                case "Voi":
                    int n = (int)ReadDouble(ed, "So voi dong thoi: ", 2);
                    double qls = ReadDouble(ed, "Luu luong/voi (L/s): ", 2.5);
                    return FireCalculations.HydrantTotalFlow(n, qls);
                case "Bon":
                    double qbon = ReadDouble(ed, "Luu luong (L/min): ", 1500);
                    double t = ReadDouble(ed, "Thoi gian cung cap (phut): ", 60);
                    return FireCalculations.ReserveTankVolume(qbon, t);
                default:
                    double k = ReadDouble(ed, "He so K (L/min/sqrt bar): ", 80);
                    double p = ReadDouble(ed, "Ap luc P (bar): ", 1.0);
                    return FireCalculations.SprinklerFlow(k, p);
            }
        }

        private static double ReadDouble(Editor ed, string prompt, double defaultValue)
        {
            var opts = new PromptDoubleOptions(prompt)
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = defaultValue,
                UseDefaultValue = true
            };
            PromptDoubleResult res = ed.GetDouble(opts);
            if (res.Status != PromptStatus.OK)
            {
                throw new System.InvalidOperationException("Da huy nhap lieu.");
            }

            return res.Value;
        }
    }
}
