using System;
using System.Drawing;
using System.Windows.Forms;
using MepPanel.Blocks.AutoCAD.Drawing;
using MepPanel.Core.Standards;

namespace MepPanel.Blocks.AutoCAD
{
    /// <summary>Panel WinForms — chi He nuoc / PCCC (MEPDB chinh dung panel WPF).</summary>
    internal sealed class MepDbToolPanel : UserControl
    {
        public MepDbToolPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            AutoScroll = true;

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12),
                Width = 300
            };

            AddTitle(layout, "He nuoc");
            AddHint(layout, "Ve ong, phu kien AMC, phan tich, minh hoa PNG — layer MEP_WATER.");
            AddButton(layout, "Nap thu vien AMC", MepPipeLibraryService.ImportAmcLibrary);
            AddButton(layout, "Ve ong nuoc + co", MepWaterDrawingService.DrawPipeRun);
            AddButton(layout, "Dat phu kien nuoc", MepWaterDrawingService.PlaceFitting);
            AddButton(layout, "TC + CT he nuoc", () => MepKnowledgeService.ShowStandards(MepSystemKind.Water));
            AddButton(layout, "Tinh toan he nuoc", () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Water));
            AddButton(layout, "Render he nuoc -> PNG", MepPipeRenderService.RenderWater);

            AddTitle(layout, "He PCCC / Bao chay");
            AddHint(layout, "Sprinkler, hydrant, dau bao — layer MEP_FIRE.");
            AddButton(layout, "Ve ong PCCC + co", MepFireDrawingService.DrawPipeRun);
            AddButton(layout, "Dat phu kien PCCC", MepFireDrawingService.PlaceFitting);
            AddButton(layout, "TC + CT PCCC", () => MepKnowledgeService.ShowStandards(MepSystemKind.Fire));
            AddButton(layout, "Tinh toan PCCC", () => MepKnowledgeService.RunQuickCalculation(MepSystemKind.Fire));
            AddButton(layout, "Render PCCC -> PNG", MepPipeRenderService.RenderFire);

            Controls.Add(layout);
        }

        private static void AddTitle(Control parent, string text)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 4),
                MaximumSize = new Size(270, 0)
            });
        }

        private static void AddHint(Control parent, string text)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(270, 0),
                Margin = new Padding(0, 0, 0, 6)
            });
        }

        private static void AddButton(Control parent, string text, Action action)
        {
            var button = new Button
            {
                Text = text,
                Width = 260,
                Height = 32,
                Margin = new Padding(0, 0, 0, 6)
            };
            button.Click += (s, e) => RunSafe(action);
            parent.Controls.Add(button);
        }

        private static void RunSafe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MepPanel");
            }
        }
    }
}
