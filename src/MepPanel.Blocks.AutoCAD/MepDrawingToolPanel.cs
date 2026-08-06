using System;
using System.Drawing;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using MepPanel.Blocks.AutoCAD.Drawing;
using MepPanel.Core;

namespace MepPanel.Blocks.AutoCAD;

/// <summary>
/// Panel chính MEP DRAWING TOOL — gom Chọn hệ thống, Chức năng chung, Tủ điện/DB.
/// </summary>
public sealed class MepDrawingToolPanel : UserControl
{
    private Label _statusLabel = null!;

    public MepDrawingToolPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 245, 245);
        Font = new Font("Segoe UI", 9F);

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "MEP DRAWING TOOL",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 153),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(title);

        layout.Controls.Add(BuildGroup("Chọn hệ thống", new (string, string, Action)[]
        {
            ("Hệ điện", PluginFeatures.Draw, () => ShowInfo("Hệ điện", "Dùng nhóm Tủ điện / DB bên dưới.")),
            ("Điều hòa", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, MepHvacDrawingService.DrawDuctRun)),
            ("Hệ nước", PluginFeatures.Water, () => Run(PluginFeatures.Water, MepWaterDrawingService.DrawPipeRun)),
            ("Báo cháy / PCCC", PluginFeatures.Smoke, () => Run(PluginFeatures.Smoke, MepFireDrawingService.DrawPipeRun))
        }));

        layout.Controls.Add(BuildGroup("Ống & phụ kiện (AMC)", new (string, string, Action)[]
        {
            ("Nạp thư viện AMC", PluginFeatures.Water, () => Run(PluginFeatures.Water, MepPipeLibraryService.ImportAmcLibrary)),
            ("Vẽ ống nước + co tự động", PluginFeatures.Water, () => Run(PluginFeatures.Water, MepWaterDrawingService.DrawPipeRun)),
            ("Đặt phụ kiện nước", PluginFeatures.Water, () => Run(PluginFeatures.Water, MepWaterDrawingService.PlaceFitting)),
            ("Vẽ ống PCCC + co tự động", PluginFeatures.Smoke, () => Run(PluginFeatures.Smoke, MepFireDrawingService.DrawPipeRun)),
            ("Đặt phụ kiện PCCC", PluginFeatures.Smoke, () => Run(PluginFeatures.Smoke, MepFireDrawingService.PlaceFitting))
        }));

        layout.Controls.Add(BuildGroup("Chức năng chung", new (string, string, Action)[]
        {
            ("Chọn cùng layer", PluginFeatures.SeLayer, () => Run(PluginFeatures.SeLayer, MepCommonDrawingService.SelectSameLayer)),
            ("Cấu hình tủ", PluginFeatures.Config, () => Run(PluginFeatures.Config, MepCommonDrawingService.ShowConfigHint)),
            ("Xuất CSV", PluginFeatures.Export, () => Run(PluginFeatures.Export, MepCommonDrawingService.ExportCsvHint))
        }));

        layout.Controls.Add(BuildGroup("Tủ điện / DB", new (string, string, Action)[]
        {
            ("Vẽ tủ điện", PluginFeatures.Cabinet2D, () => Run(PluginFeatures.Cabinet2D, MepDbDrawingService.DrawDbBlock)),
            ("Cập nhật tủ", PluginFeatures.Update, () => Run(PluginFeatures.Update, MepCabinetDrawingService.UpdateCabinet)),
            ("Xuất Excel", PluginFeatures.Excel, () => Run(PluginFeatures.Excel, MepCabinetDrawingService.ExportExcelHint)),
            ("Mặt chiếu tủ", PluginFeatures.CabinetViews, () => Run(PluginFeatures.CabinetViews, MepCabinetDrawingService.DrawCabinet2D)),
            ("Bố trí động lực", PluginFeatures.Power, () => Run(PluginFeatures.Power, MepCabinetDrawingService.DrawPowerLayout)),
            ("Sơ đồ 3P-4D+E", PluginFeatures.ThreePhase4W, () => Run(PluginFeatures.ThreePhase4W, MepCabinetDrawingService.Draw3P4DHint))
        }));

        scroll.Controls.Add(layout);

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = Color.FromArgb(230, 230, 230),
            Text = "Sẵn sàng — gõ MEPDB để mở panel."
        };

        Controls.Add(scroll);
        Controls.Add(_statusLabel);
    }

    private GroupBox BuildGroup(string title, (string label, string feature, Action action)[] buttons)
    {
        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(8, 4, 8, 8),
            Width = 280
        };

        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill
        };

        foreach (var (label, _, action) in buttons)
        {
            var btn = new Button
            {
                Text = label,
                Width = 250,
                Height = 32,
                Margin = new Padding(0, 2, 0, 2),
                FlatStyle = FlatStyle.System
            };
            btn.Click += (_, _) => action();
            flow.Controls.Add(btn);
        }

        group.Controls.Add(flow);
        return group;
    }

    private void Run(string feature, Action drawAction)
    {
        if (!PluginFeatureGate.Ensure(feature))
        {
            SetStatus($"Không có quyền: {feature}");
            return;
        }

        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            SetStatus("Không có bản vẽ đang mở.");
            return;
        }

        try
        {
            drawAction();
            SetStatus($"Hoàn thành: {PluginFeatures.DisplayNames.TryGetValue(feature, out var name) ? name : feature}");
        }
        catch (Exception ex)
        {
            SetStatus($"Lỗi: {ex.Message}");
            doc.Editor.WriteMessage($"\n[MEP] Lỗi {feature}: {ex.Message}");
        }
    }

    private void ShowInfo(string title, string message)
    {
        SetStatus(title);
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetStatus(string text) => _statusLabel.Text = text;
}
