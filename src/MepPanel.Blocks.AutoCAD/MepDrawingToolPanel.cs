using System;
using System.Drawing;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using MepPanel.Blocks.AutoCAD.Drawing;
using MepPanel.Core;
using MepPanel.Core.Standards;

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

        layout.Controls.Add(BuildGroup("Hệ điện — Máng & Trunking", new (string, string, Action)[]
        {
            ("Vẽ máng cáp", PluginFeatures.Draw, () => Run(PluginFeatures.Draw, MepElectricalRoutingService.DrawCableTrayRun)),
            ("Vẽ trunking", PluginFeatures.Draw, () => Run(PluginFeatures.Draw, MepElectricalRoutingService.DrawTrunkingRun)),
            ("Co 90° máng cáp", PluginFeatures.Draw, () => Run(PluginFeatures.Draw, MepElectricalRoutingService.PlaceTrayElbow90)),
            ("Co 90° trunking", PluginFeatures.Draw, () => Run(PluginFeatures.Draw, MepElectricalRoutingService.PlaceTrunkingElbow90))
        }));

        layout.Controls.Add(BuildGroup("HVAC — Ống gió nâng cao", new (string, string, Action)[]
        {
            ("Ống gió thông minh (co 45/90, reducer)", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, MepHvacAdvancedService.DrawSmartDuctRun)),
            ("Ống gió mềm (flex)", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, MepHvacAdvancedService.DrawFlexibleDuct)),
            ("Reducer thủ công (lớn → nhỏ)", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, MepHvacAdvancedService.DrawDuctReducerManual)),
            ("Ống gió cơ bản (L)", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, MepHvacDrawingService.DrawDuctRun))
        }));

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
            ("Vẽ ống nước tự động (DN+phụ kiện)", PluginFeatures.Water, () => Run(PluginFeatures.Water, () => MepPipeAutoLayoutService.DrawSmartPipeRun(MepPipeSystem.Water))),
            ("Vẽ ống PCCC tự động (DN+phụ kiện)", PluginFeatures.Smoke, () => Run(PluginFeatures.Smoke, () => MepPipeAutoLayoutService.DrawSmartPipeRun(MepPipeSystem.Fire))),
            ("Đặt phụ kiện nước (thủ công)", PluginFeatures.Water, () => Run(PluginFeatures.Water, MepWaterDrawingService.PlaceFitting)),
            ("Đặt phụ kiện PCCC (thủ công)", PluginFeatures.Smoke, () => Run(PluginFeatures.Smoke, MepFireDrawingService.PlaceFitting))
        }));

        layout.Controls.Add(BuildGroup("Chức năng chung", new (string, string, Action)[]
        {
            ("Chọn cùng layer", PluginFeatures.SeLayer, () => Run(PluginFeatures.SeLayer, MepCommonDrawingService.SelectSameLayer)),
            ("Cấu hình tủ", PluginFeatures.Config, () => Run(PluginFeatures.Config, MepCommonDrawingService.ShowConfigHint)),
            ("Xuất CSV", PluginFeatures.Export, () => Run(PluginFeatures.Export, MepCommonDrawingService.ExportCsvHint))
        }));

        layout.Controls.Add(BuildGroup("Tiêu chuẩn & Tính toán", new (string, string, Action)[]
        {
            ("TC + CT — Hệ điện", PluginFeatures.Draw, () => Run(PluginFeatures.Draw, () => MepKnowledgeService.ShowStandards(MepSystemKind.Electrical))),
            ("Tính toán — Hệ điện", PluginFeatures.Draw, () => Run(PluginFeatures.Draw, () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Electrical))),
            ("TC + CT — Hệ nước", PluginFeatures.Water, () => Run(PluginFeatures.Water, () => MepKnowledgeService.ShowStandards(MepSystemKind.Water))),
            ("Tính toán — Hệ nước", PluginFeatures.Water, () => Run(PluginFeatures.Water, () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Water))),
            ("TC + CT — PCCC", PluginFeatures.Smoke, () => Run(PluginFeatures.Smoke, () => MepKnowledgeService.ShowStandards(MepSystemKind.Fire))),
            ("Tính toán — PCCC", PluginFeatures.Smoke, () => Run(PluginFeatures.Smoke, () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Fire))),
            ("TC + CT — Điều hòa", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, () => MepKnowledgeService.ShowStandards(MepSystemKind.Hvac))),
            ("Tính toán — Điều hòa", PluginFeatures.MepHvac, () => Run(PluginFeatures.MepHvac, () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Hvac)))
        }));

        layout.Controls.Add(BuildGroup("Tủ điện / DB", new (string, string, Action)[]
        {
            ("Vẽ tủ điện", PluginFeatures.Cabinet2D, () => Run(PluginFeatures.Cabinet2D, MepDbDrawingService.DrawDbBlock)),
            ("Cập nhật tủ", PluginFeatures.Update, () => Run(PluginFeatures.Update, MepCabinetDrawingService.UpdateCabinet)),
            ("Xuất Excel", PluginFeatures.Excel, () => Run(PluginFeatures.Excel, MepCabinetDrawingService.ExportExcelHint)),
            ("Mặt chiếu tủ", PluginFeatures.CabinetViews, () => Run(PluginFeatures.CabinetViews, MepCabinetDrawingService.DrawCabinet2D)),
            ("Bố trí động lực", PluginFeatures.Power, () => Run(PluginFeatures.Power, MepCabinetDrawingService.DrawPowerLayout)),
            ("Sơ đồ 3P-4D+E", PluginFeatures.ThreePhase4W, () => Run(PluginFeatures.ThreePhase4W, MepCabinetDrawingService.Draw3P4DHint)),
            ("🖼 Render bố trí tủ → PNG", PluginFeatures.Cabinet2D, () => Run(PluginFeatures.Cabinet2D, MepCabinetRenderService.RenderFromDrawing))
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
