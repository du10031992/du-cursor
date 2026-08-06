using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Autodesk.Windows;
using Autodesk.AutoCAD.ApplicationServices.Core;
using MepPanel.Blocks.AutoCAD.Drawing;
using MepPanel.Core;
using MepPanel.Core.Standards;

namespace MepPanel.Blocks.AutoCAD
{
    /// <summary>Builds the MEP ribbon tab in AutoCAD 2021.</summary>
    public static class MepRibbonBuilder
    {
        private static RibbonTab _tab;

        public static void BuildOrActivate()
        {
            try
            {
                RibbonControl ribbon = ComponentManager.Ribbon;
                if (ribbon == null) return;

                // Remove old tab if exists
                foreach (RibbonTab t in ribbon.Tabs)
                {
                    if (t.Id == "MEP_DRAWING_TAB") { _tab = t; ribbon.ActiveTab = t; return; }
                }

                _tab = new RibbonTab { Title = "MEP", Id = "MEP_DRAWING_TAB" };

                _tab.Panels.Add(BuildDrawPanel());
                _tab.Panels.Add(BuildDevicePanel());
                _tab.Panels.Add(BuildCabinetPanel());
                _tab.Panels.Add(BuildAnnotatePanel());
                _tab.Panels.Add(BuildCheckPanel());
                _tab.Panels.Add(BuildUtilPanel());
                _tab.Panels.Add(BuildInfoPanel());

                ribbon.Tabs.Add(_tab);
                ribbon.ActiveTab = _tab;
            }
            catch { }
        }

        // ── Panel: Vẽ MEP ──────────────────────────────────────────────
        private static RibbonPanel BuildDrawPanel()
        {
            var src = new RibbonPanelSource { Title = "Vẽ MEP" };
            var row1 = new RibbonRowPanel();
            row1.Items.Add(MakeBtn("Duct", "Ve ong gio HVAC", "MEPHVAC", Colors.Hvac));
            row1.Items.Add(MakeBtn("Pipe", "Ve ong nuoc/PCCC", "MEPWATER", Colors.Water));
            row1.Items.Add(new RibbonRowBreak());
            row1.Items.Add(MakeSmallBtn("Cable Tray", "Ve mang cap", "MEPTRAY", Colors.Elec));
            row1.Items.Add(MakeSmallBtn("Conduit", "Ve trunking", "MEPTRUNK", Colors.Elec));
            src.Items.Add(row1);
            return new RibbonPanel { Source = src };
        }

        // ── Panel: Thiết bị ───────────────────────────────────────────
        private static RibbonPanel BuildDevicePanel()
        {
            var src = new RibbonPanelSource { Title = "Thiết bị" };
            var row = new RibbonRowPanel();
            row.Items.Add(MakeBtn("Thiết bị\nđiện", "He dien - tu dien", "MEPDB", Colors.Elec));
            row.Items.Add(MakeBtn("Thiết bị\nnước", "He nuoc", "MEPWATER", Colors.Water));
            row.Items.Add(MakeBtn("Thiết bị\nHVAC", "Dieu hoa thong gio", "MEPHVAC", Colors.Hvac));
            row.Items.Add(MakeBtn("Thiết bị\nPCCC", "Phong chay chua chay", "MEPFIRE", Colors.Fire));
            row.Items.Add(new RibbonRowBreak());
            row.Items.Add(MakeSmallBtn("Tủ điện", "Mo panel tu dien", "MEPDB", Colors.Elec));
            row.Items.Add(MakeSmallBtn("Insert DB", "Chen block thiet bi", "MEPDB", Colors.Elec));
            src.Items.Add(row);
            return new RibbonPanel { Source = src };
        }

        // ── Panel: Tủ điện ────────────────────────────────────────────
        private static RibbonPanel BuildCabinetPanel()
        {
            var src = new RibbonPanelSource { Title = "Tủ điện" };
            var row = new RibbonRowPanel();
            row.Items.Add(MakeBtn("Vẽ\ntủ điện", "Ve mat chieu tu dien", "MEPDB", Colors.Elec));
            row.Items.Add(MakeBtn("Render\ntủ", "Render bo tri tu → PNG", "MEPDB", Colors.Elec));
            row.Items.Add(new RibbonRowBreak());
            row.Items.Add(MakeSmallBtn("Xuất Excel", "Xuat bang kluong", "MEPDB", Colors.Elec));
            row.Items.Add(MakeSmallBtn("Sơ đồ 3P", "So do 3P-4D+E", "MEPDB", Colors.Elec));
            src.Items.Add(row);
            return new RibbonPanel { Source = src };
        }

        // ── Panel: Chú thích ──────────────────────────────────────────
        private static RibbonPanel BuildAnnotatePanel()
        {
            var src = new RibbonPanelSource { Title = "Chú thích" };
            var row = new RibbonRowPanel();
            row.Items.Add(MakeSmallBtn("Text", "Them chu thich", "MEPDB", Colors.Neutral));
            row.Items.Add(MakeSmallBtn("Tag", "Gan nhan thiet bi", "MEPDB", Colors.Neutral));
            row.Items.Add(MakeSmallBtn("Ký hiệu", "Chen ky hieu MEP", "MEPDB", Colors.Neutral));
            src.Items.Add(row);
            return new RibbonPanel { Source = src };
        }

        // ── Panel: Kiểm tra ───────────────────────────────────────────
        private static RibbonPanel BuildCheckPanel()
        {
            var src = new RibbonPanelSource { Title = "Kiểm tra" };
            var row = new RibbonRowPanel();
            row.Items.Add(MakeBtn("Check hệ\nthống", "Kiem tra he thong MEP", "MEPSTATUS", Colors.Neutral));
            row.Items.Add(MakeSmallBtn("Layer\nTools", "Chon cung layer", "MEPDB", Colors.Neutral));
            src.Items.Add(row);
            return new RibbonPanel { Source = src };
        }

        // ── Panel: Tiện ích ───────────────────────────────────────────
        private static RibbonPanel BuildUtilPanel()
        {
            var src = new RibbonPanelSource { Title = "Tiện ích" };
            var row = new RibbonRowPanel();
            row.Items.Add(MakeSmallBtn("Cấu hình", "Cau hinh plugin", "MEPDB", Colors.Neutral));
            row.Items.Add(MakeSmallBtn("MEP Info", "Thong tin he thong", "MEPSTATUS", Colors.Neutral));
            src.Items.Add(row);
            return new RibbonPanel { Source = src };
        }

        // ── Panel: Thông tin ──────────────────────────────────────────
        private static RibbonPanel BuildInfoPanel()
        {
            var src = new RibbonPanelSource { Title = "Thông tin" };
            var row = new RibbonRowPanel();
            row.Items.Add(MakeBtn("MEP\nInfo", "Tieu chuan + tinh toan", "MEPDB", Colors.Neutral));
            row.Items.Add(MakeSmallBtn("Trợ giúp", "Huong dan su dung", "MEPDB", Colors.Neutral));
            src.Items.Add(row);
            return new RibbonPanel { Source = src };
        }

        // ── Button factories ──────────────────────────────────────────
        private static RibbonButton MakeBtn(string label, string tooltip, string cmd, Color iconColor)
        {
            return new RibbonButton
            {
                Text           = label,
                ToolTip        = tooltip,
                ShowText       = true,
                Size           = RibbonItemSize.Large,
                Orientation    = System.Windows.Controls.Orientation.Vertical,
                LargeImage     = MakeIcon(iconColor, 32),
                Image          = MakeIcon(iconColor, 16),
                CommandHandler = new RibbonCmdHandler(cmd)
            };
        }

        private static RibbonButton MakeSmallBtn(string label, string tooltip, string cmd, Color iconColor)
        {
            return new RibbonButton
            {
                Text           = label,
                ToolTip        = tooltip,
                ShowText       = true,
                Size           = RibbonItemSize.Standard,
                Image          = MakeIcon(iconColor, 16),
                CommandHandler = new RibbonCmdHandler(cmd)
            };
        }

        private static System.Windows.Media.Imaging.BitmapImage MakeIcon(Color c, int size)
        {
            using (var bmp = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(c))
                {
                    g.FillEllipse(brush, 1, 1, size - 2, size - 2);
                }
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
        }

        private static class Colors
        {
            public static readonly Color Elec    = Color.FromArgb(255, 200, 40);
            public static readonly Color Water   = Color.FromArgb(30, 130, 220);
            public static readonly Color Hvac    = Color.FromArgb(0, 190, 200);
            public static readonly Color Fire    = Color.FromArgb(230, 60, 40);
            public static readonly Color Neutral = Color.FromArgb(100, 120, 160);
        }
    }

    /// <summary>Ribbon command handler — gọi AutoCAD command.</summary>
    internal class RibbonCmdHandler : System.Windows.Input.ICommand
    {
        private readonly string _cmd;
        public RibbonCmdHandler(string cmd) { _cmd = cmd; }
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                    doc.SendStringToExecute(_cmd + " ", true, false, true);
            }
            catch { }
        }
    }
}
