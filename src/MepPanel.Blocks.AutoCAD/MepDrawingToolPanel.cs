using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MepPanel.Blocks.AutoCAD.Drawing;
using MepPanel.Core;
using MepPanel.Core.Standards;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MepPanel.Blocks.AutoCAD
{
    /// <summary>
    /// Panel MEP DRAWING TOOL — dark theme, icon buttons, giống bản WPF gốc.
    /// </summary>
    public sealed class MepDrawingToolPanel : UserControl
    {
        // ── Dark theme colors (giống screenshot) ──────────────────────
        private static readonly Color C_BG        = Color.FromArgb(26, 30, 42);
        private static readonly Color C_TITLE_BG  = Color.FromArgb(18, 22, 34);
        private static readonly Color C_GROUP_HDR = Color.FromArgb(32, 38, 54);
        private static readonly Color C_GROUP_TXT = Color.FromArgb(79, 195, 247);
        private static readonly Color C_BTN_BG    = Color.FromArgb(38, 44, 62);
        private static readonly Color C_BTN_HVR   = Color.FromArgb(52, 62, 88);
        private static readonly Color C_BTN_TXT   = Color.FromArgb(220, 225, 240);
        private static readonly Color C_ACCENT    = Color.FromArgb(0, 180, 255);
        private static readonly Color C_STATUS_BG = Color.FromArgb(18, 22, 32);
        private static readonly Color C_STATUS_TXT= Color.FromArgb(120, 140, 170);

        private static readonly Color IC_ELEC  = Color.FromArgb(255, 195, 30);
        private static readonly Color IC_HVAC  = Color.FromArgb(30, 180, 210);
        private static readonly Color IC_WATER = Color.FromArgb(30, 130, 220);
        private static readonly Color IC_FIRE  = Color.FromArgb(230, 65, 40);
        private static readonly Color IC_LAYER = Color.FromArgb(100, 200, 120);
        private static readonly Color IC_CAB   = Color.FromArgb(130, 100, 220);
        private static readonly Color IC_UTIL  = Color.FromArgb(160, 170, 190);
        private static readonly Color IC_INFO  = Color.FromArgb(80, 180, 160);

        private Label _statusLabel;

        public MepDrawingToolPanel()
        {
            BackColor = C_BG;
            Dock      = DockStyle.Fill;
            Font      = new Font("Segoe UI", 9F);
            AutoScroll = false;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = C_BG,
                Padding = new Padding(0)
            };

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0),
                BackColor = C_BG
            };

            // ── Title bar ──────────────────────────────────────────────
            layout.Controls.Add(BuildTitleBar());

            // ── Chọn hệ thống ──────────────────────────────────────────
            layout.Controls.Add(BuildGroupHeader("Chọn hệ thống"));
            layout.Controls.Add(BuildIconGrid(new[]
            {
                ("Hệ điện",   IC_ELEC,  "⚡", PluginFeatures.Draw,    (Action)(() => Run(PluginFeatures.Draw,    () => ShowSystemInfo("HE DIEN")))),
                ("Điều hòa",  IC_HVAC,  "❄", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, MepHvacAdvancedService.DrawSmartDuctRun)),
                ("Hệ nước",   IC_WATER, "💧", PluginFeatures.Water,   () => Run(PluginFeatures.Water,   () => MepPipeAutoLayoutService.DrawSmartPipeRun(MepPipeSystem.Water))),
                ("Báo cháy",  IC_FIRE,  "🔥", PluginFeatures.Smoke,   () => Run(PluginFeatures.Smoke,   () => MepPipeAutoLayoutService.DrawSmartPipeRun(MepPipeSystem.Fire))),
            }));

            // ── Chức năng chung ────────────────────────────────────────
            layout.Controls.Add(BuildGroupHeader("Chức năng chung"));
            layout.Controls.Add(BuildIconRow(new[]
            {
                ("Chọn\ncùng layer", IC_LAYER, "≡", PluginFeatures.SeLayer, (Action)(() => Run(PluginFeatures.SeLayer, MepCommonDrawingService.SelectSameLayer))),
                ("Cấu hình\ntủ",     IC_CAB,   "⚙", PluginFeatures.Config,  () => Run(PluginFeatures.Config,  MepCommonDrawingService.ShowConfigHint)),
                ("Xuất\nCSV",        IC_UTIL,  "📄", PluginFeatures.Export,  () => Run(PluginFeatures.Export,  MepCommonDrawingService.ExportCsvHint)),
                ("Help",             IC_INFO,  "?",  PluginFeatures.Draw,    () => Run(PluginFeatures.Draw,    () => MepKnowledgeService.ShowStandards(MepSystemKind.Electrical))),
                ("Quick\nLoad",      IC_LAYER, "▶",  PluginFeatures.Draw,    () => Run(PluginFeatures.Draw,    MepPipeLibraryService.ImportAmcLibrary)),
            }));

            // ── Tủ điện / DB ───────────────────────────────────────────
            layout.Controls.Add(BuildGroupHeader("Tủ điện / DB"));
            layout.Controls.Add(BuildIconGrid(new[]
            {
                ("Vẽ\ntủ điện",     IC_ELEC,  "⬜", PluginFeatures.Cabinet2D,  (Action)(() => Run(PluginFeatures.Cabinet2D,   MepDbDrawingService.DrawDbBlock))),
                ("Cập nhật\ntủ",    IC_ELEC,  "↻",  PluginFeatures.Update,      () => Run(PluginFeatures.Update,      MepCabinetDrawingService.UpdateCabinet)),
                ("Xuất\nExcel",     IC_LAYER, "📊", PluginFeatures.Excel,       () => Run(PluginFeatures.Excel,       MepCabinetDrawingService.ExportExcelHint)),
                ("Mặt chiếu\ntủ",   IC_CAB,   "▦",  PluginFeatures.CabinetViews,() => Run(PluginFeatures.CabinetViews,MepCabinetDrawingService.DrawCabinet2D)),
                ("Bố trí\nđộng lực",IC_ELEC,  "⚡", PluginFeatures.Power,       () => Run(PluginFeatures.Power,       MepCabinetDrawingService.DrawPowerLayout)),
                ("Sơ đồ\n3P-4D+E",  IC_UTIL,  "≋",  PluginFeatures.ThreePhase4W,() => Run(PluginFeatures.ThreePhase4W,MepCabinetDrawingService.Draw3P4DHint)),
                ("Render\ntủ",      IC_INFO,  "🖼", PluginFeatures.Cabinet2D,  () => Run(PluginFeatures.Cabinet2D,   MepCabinetRenderService.RenderFromDrawing)),
                ("Dây\nthực tế",    IC_UTIL,  "〰", PluginFeatures.Cabinet2D,  () => Run(PluginFeatures.Cabinet2D,   MepCabinetDrawingService.Draw3P4DHint)),
            }));

            // ── Ống MEP ────────────────────────────────────────────────
            layout.Controls.Add(BuildGroupHeader("Ống MEP (AMC)"));
            layout.Controls.Add(BuildIconRow(new[]
            {
                ("Ống\ngió",    IC_HVAC,  "⊟", PluginFeatures.MepHvac, (Action)(() => Run(PluginFeatures.MepHvac, MepHvacAdvancedService.DrawSmartDuctRun))),
                ("Ống\nmềm",   IC_HVAC,  "∿",  PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, MepHvacAdvancedService.DrawFlexibleDuct)),
                ("Ống\nnước",  IC_WATER, "—",  PluginFeatures.Water,   () => Run(PluginFeatures.Water,   () => MepPipeAutoLayoutService.DrawSmartPipeRun(MepPipeSystem.Water))),
                ("Ống\nPCCC",  IC_FIRE,  "—",  PluginFeatures.Smoke,   () => Run(PluginFeatures.Smoke,   () => MepPipeAutoLayoutService.DrawSmartPipeRun(MepPipeSystem.Fire))),
                ("Máng\ncáp",  IC_ELEC,  "▬",  PluginFeatures.Draw,    () => Run(PluginFeatures.Draw,    MepElectricalRoutingService.DrawCableTrayRun)),
            }));

            // ── Tiêu chuẩn & Tính toán ────────────────────────────────
            layout.Controls.Add(BuildGroupHeader("Tiêu chuẩn & Tính toán"));
            layout.Controls.Add(BuildIconRow(new[]
            {
                ("TC\nĐiện",   IC_ELEC,  "📋", PluginFeatures.Draw,    (Action)(() => Run(PluginFeatures.Draw,    () => MepKnowledgeService.ShowStandards(MepSystemKind.Electrical)))),
                ("Tính\nĐiện", IC_ELEC,  "∑",  PluginFeatures.Draw,    () => Run(PluginFeatures.Draw,    () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Electrical))),
                ("TC\nNước",   IC_WATER, "📋", PluginFeatures.Water,   () => Run(PluginFeatures.Water,   () => MepKnowledgeService.ShowStandards(MepSystemKind.Water))),
                ("Tính\nNước", IC_WATER, "∑",  PluginFeatures.Water,   () => Run(PluginFeatures.Water,   () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Water))),
                ("TC\nPCCC",   IC_FIRE,  "📋", PluginFeatures.Smoke,   () => Run(PluginFeatures.Smoke,   () => MepKnowledgeService.ShowStandards(MepSystemKind.Fire))),
            }));

            scroll.Controls.Add(layout);

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = C_STATUS_BG,
                ForeColor = C_STATUS_TXT,
                Text = "Sẵn sàng"
            };

            Controls.Add(scroll);
            Controls.Add(_statusLabel);
        }

        // ── Title bar ──────────────────────────────────────────────────
        private Control BuildTitleBar()
        {
            var pnl = new Panel
            {
                Width = 280,
                Height = 36,
                BackColor = C_TITLE_BG
            };
            var lbl = new Label
            {
                Text = "MEP DRAWING TOOL",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = C_ACCENT,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            pnl.Controls.Add(lbl);
            return pnl;
        }

        // ── Group header ───────────────────────────────────────────────
        private Control BuildGroupHeader(string title)
        {
            var pnl = new Panel
            {
                Width = 280,
                Height = 28,
                BackColor = C_GROUP_HDR
            };
            var lbl = new Label
            {
                Text = "  " + title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = C_GROUP_TXT,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            // Chevron right side
            var chevron = new Label
            {
                Text = "∨",
                Dock = DockStyle.Right,
                Width = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = C_GROUP_TXT,
                BackColor = Color.Transparent
            };
            pnl.Controls.Add(lbl);
            pnl.Controls.Add(chevron);
            return pnl;
        }

        // ── 2×N icon grid ─────────────────────────────────────────────
        private Control BuildIconGrid((string label, Color color, string symbol, string feat, Action action)[] items)
        {
            const int BTN_W = 68, BTN_H = 62, COLS = 4, PAD = 4;
            int rows = (int)Math.Ceiling(items.Length / (double)COLS);

            var pnl = new Panel
            {
                Width  = 280,
                Height = rows * (BTN_H + PAD) + PAD * 2,
                BackColor = C_BG,
                Padding = new Padding(PAD)
            };

            for (int i = 0; i < items.Length; i++)
            {
                int col = i % COLS, row = i / COLS;
                var (label, color, symbol, feat, action) = items[i];
                var btn = MakeIconButton(label, color, symbol, BTN_W, BTN_H);
                btn.Location = new Point(PAD + col * (BTN_W + PAD), PAD + row * (BTN_H + PAD));
                var fi = feat; var ai = action;
                btn.Click += (s, e) => Run(fi, ai);
                pnl.Controls.Add(btn);
            }
            return pnl;
        }

        // ── Horizontal icon row ────────────────────────────────────────
        private Control BuildIconRow((string label, Color color, string symbol, string feat, Action action)[] items)
        {
            const int BTN_W = 52, BTN_H = 58, PAD = 4;
            var pnl = new Panel
            {
                Width  = 280,
                Height = BTN_H + PAD * 2,
                BackColor = C_BG,
                Padding = new Padding(PAD)
            };

            for (int i = 0; i < items.Length; i++)
            {
                var (label, color, symbol, feat, action) = items[i];
                var btn = MakeIconButton(label, color, symbol, BTN_W, BTN_H);
                btn.Location = new Point(PAD + i * (BTN_W + PAD), PAD);
                var fi = feat; var ai = action;
                btn.Click += (s, e) => Run(fi, ai);
                pnl.Controls.Add(btn);
            }
            return pnl;
        }

        // ── Single icon button ─────────────────────────────────────────
        private Button MakeIconButton(string label, Color iconColor, string symbol, int w, int h)
        {
            var btn = new Button
            {
                Width     = w,
                Height    = h,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_BTN_BG,
                ForeColor = C_BTN_TXT,
                Font      = new Font("Segoe UI", 7.5F),
                Text      = "",
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(55, 62, 85);
            btn.FlatAppearance.BorderSize  = 1;
            btn.FlatAppearance.MouseOverBackColor  = C_BTN_HVR;
            btn.FlatAppearance.MouseDownBackColor  = Color.FromArgb(60, 80, 120);

            // Draw icon + label via Paint
            btn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(btn.BackColor);

                // Icon circle
                int iconSize = Math.Min(w, h - 18) - 10;
                int ix = (w - iconSize) / 2;
                int iy = 6;
                using (var brush = new SolidBrush(Color.FromArgb(40, iconColor)))
                    g.FillEllipse(brush, ix, iy, iconSize, iconSize);
                using (var pen = new Pen(iconColor, 2))
                    g.DrawEllipse(pen, ix + 1, iy + 1, iconSize - 2, iconSize - 2);

                // Symbol text inside circle
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var brush = new SolidBrush(iconColor))
                {
                    var f = new Font("Segoe UI", iconSize * 0.35F, FontStyle.Bold);
                    g.DrawString(symbol, f, brush, new RectangleF(ix, iy, iconSize, iconSize), sf);
                    f.Dispose();
                }

                // Label text
                string[] lines = label.Split('\n');
                using (var sf2 = new StringFormat { Alignment = StringAlignment.Center })
                using (var brush2 = new SolidBrush(C_BTN_TXT))
                {
                    var fSmall = new Font("Segoe UI", 7.5F);
                    float ty = iy + iconSize + 4;
                    foreach (string line in lines)
                    {
                        g.DrawString(line, fSmall, brush2,
                            new RectangleF(0, ty, w, 14), sf2);
                        ty += 12;
                    }
                    fSmall.Dispose();
                }

                // Border
                using (var pen = new Pen(Color.FromArgb(55, 62, 85)))
                    g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
            };

            return btn;
        }

        // ── Run action ────────────────────────────────────────────────
        private void Run(string feature, Action action)
        {
            if (!PluginFeatureGate.Ensure(feature))
            {
                SetStatus("Không có quyền: " + feature);
                return;
            }
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { SetStatus("Không có bản vẽ đang mở."); return; }
            try
            {
                action();
                string name;
                SetStatus("✓ " + (PluginFeatures.DisplayNames.TryGetValue(feature, out name) ? name : feature));
            }
            catch (Exception ex)
            {
                SetStatus("Lỗi: " + ex.Message);
                doc.Editor.WriteMessage("\n[MEP] " + ex.Message);
            }
        }

        private void ShowSystemInfo(string name)
        {
            MessageBox.Show("Dùng các nhóm bên dưới cho hệ " + name,
                "MEP - " + name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.Text = text;
        }
    }
}
