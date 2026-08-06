using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using MepPanel.Core.Calculations;
using MepPanel.Core.Standards;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>Tiêu chuẩn, tài liệu, tính toán khoa học — tích hợp MEP DRAWING TOOL.</summary>
    public static class MepKnowledgeService
    {
        public static void ShowStandards(MepSystemKind system)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            var sb = new StringBuilder();
            sb.AppendLine("=== " + MepStandardsCatalog.GetSystemTitleVi(system) + " — TIÊU CHUẨN ===");

            foreach (MepStandardInfo s in MepStandardsCatalog.GetBySystem(system))
            {
                sb.AppendLine();
                sb.AppendLine(s.Code + " — " + s.TitleVi);
                sb.AppendLine("  Cơ quan: " + s.Issuer);
                sb.AppendLine("  Phạm vi: " + s.Scope);
                sb.AppendLine("  Áp dụng: " + s.ApplyNote);
            }

            sb.AppendLine();
            sb.AppendLine("=== CÔNG THỨC ===");
            foreach (MepFormulaInfo f in MepFormulaCatalog.GetBySystem(system))
            {
                sb.AppendLine();
                sb.AppendLine(f.NameVi + " [" + f.Id + "]");
                sb.AppendLine("  " + f.Expression);
                sb.AppendLine("  Biến: " + f.VariablesVi);
                sb.AppendLine("  Đơn vị: " + f.UnitNote);
                sb.AppendLine("  TC: " + f.StandardRef);
            }

            string text = sb.ToString();
            doc.Editor.WriteMessage("\n" + text.Replace("\r", string.Empty));
            MessageBox.Show(text, MepStandardsCatalog.GetSystemTitleVi(system) + " — Tiêu chuẩn & Công thức",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void RunQuickCalculation(MepSystemKind system)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            try
            {
                MepCalculationResult result = system switch
                {
                    MepSystemKind.Electrical => RunElectricalCalc(ed),
                    MepSystemKind.Water => RunWaterCalc(ed),
                    MepSystemKind.Fire => RunFireCalc(ed),
                    MepSystemKind.Hvac => RunHvacCalc(ed),
                    _ => null
                };

                if (result == null)
                {
                    return;
                }

                ed.WriteMessage("\n[MEP TINH TOAN] " + result);
                MessageBox.Show(result.ToString(), "Kết quả tính toán", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[MEP] Loi tinh toan: " + ex.Message);
                MessageBox.Show(ex.Message, "Lỗi tính toán", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static MepCalculationResult RunElectricalCalc(Editor ed)
        {
            var kw = new PromptKeywordOptions("\nTinh toan dien: [3Pha] 1Pha SutAp")
            {
                AllowNone = true
            };
            kw.Keywords.Add("3Pha");
            kw.Keywords.Add("1Pha");
            kw.Keywords.Add("SutAp");
            kw.Keywords.Default = "3Pha";
            PromptResult mode = ed.GetKeywords(kw);
            if (mode.Status != PromptStatus.OK && mode.Status != PromptStatus.None)
            {
                return null;
            }

            string m = mode.Status == PromptStatus.None ? "3Pha" : mode.StringResult;
            if (m == "SutAp")
            {
                double i = ReadDouble(ed, "Dong I (A): ", 32);
                double l = ReadDouble(ed, "Chieu dai L (m): ", 25);
                double s = ReadDouble(ed, "Tiet dien S (mm2): ", 6);
                double u = ReadDouble(ed, "Dien ap U (V): ", 220);
                return ElectricalCalculations.VoltageDropPercent(i, l, s, u);
            }

            double p = ReadDouble(ed, "Cong suat P (W): ", 15000);
            double u2 = ReadDouble(ed, "Dien ap U (V): ", 380);
            double cos = ReadDouble(ed, "He so cos phi: ", 0.85);
            return m == "1Pha"
                ? ElectricalCalculations.SinglePhaseCurrent(p, u2, cos)
                : ElectricalCalculations.ThreePhaseCurrent(p, u2, cos);
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

        private static MepCalculationResult RunHvacCalc(Editor ed)
        {
            var kw = new PromptKeywordOptions("\nTinh toan HVAC: [Gio] VanToc RT")
            {
                AllowNone = true
            };
            kw.Keywords.Add("Gio");
            kw.Keywords.Add("VanToc");
            kw.Keywords.Add("RT");
            kw.Keywords.Default = "Gio";
            PromptResult mode = ed.GetKeywords(kw);
            if (mode.Status != PromptStatus.OK && mode.Status != PromptStatus.None)
            {
                return null;
            }

            string m = mode.Status == PromptStatus.None ? "Gio" : mode.StringResult;
            if (m == "VanToc")
            {
                double l = ReadDouble(ed, "Luu luong gio (m3/s): ", 1.2);
                double w = ReadDouble(ed, "Rong ong (mm): ", 400);
                double h = ReadDouble(ed, "Cao ong (mm): ", 250);
                return HvacCalculations.DuctVelocity(l, w, h);
            }

            if (m == "RT")
            {
                double kwLoad = ReadDouble(ed, "Tai lanh (kW): ", 35);
                return HvacCalculations.CoolingTons(kwLoad);
            }

            double q = ReadDouble(ed, "Tai nhiet (W): ", 12000);
            double dt = ReadDouble(ed, "Delta T (K): ", 8);
            return HvacCalculations.AirFlowFromCoolingLoad(q, dt);
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
