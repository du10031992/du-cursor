using System;
using System.Drawing;
using System.Windows.Forms;
using MepPanel.Blocks.AutoCAD.Drawing;

namespace MepPanel.Blocks.AutoCAD
{
    internal sealed class MepHvacToolPanel : UserControl
    {
        public MepHvacToolPanel()
        {
            BackColor = Color.White;
            Padding = new Padding(12);
            AutoScroll = true;

            AddTitle("MEPHVAC - HVAC Tools");
            AddHint("Tao layer, ve ong duct va elbow mau.");
            AddButton("Tao layer MEP_HVAC", (s, e) => RunSafe(MepHvacDrawingService.EnsureLayer));
            AddButton("Ve doan ong (2 diem)", (s, e) => RunSafe(MepHvacDrawingService.DrawDuctRun));
            AddButton("Ve elbow mau (arc)", (s, e) => RunSafe(MepHvacDrawingService.DrawDuctElbowPlaceholder));
        }

        private void AddTitle(string text)
        {
            Controls.Add(new Label
            {
                Text = text,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 6)
            });
        }

        private void AddHint(string text)
        {
            Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(280, 0),
                Padding = new Padding(0, 0, 0, 8)
            });
        }

        private void AddButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 32,
                Margin = new Padding(0, 0, 0, 6)
            };
            button.Click += click;
            Controls.Add(button);
        }

        private static void RunSafe(Action action)
        {
            try
            {
                MepBlocksDocumentContext.Run(action);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MepPanel MEPHVAC");
            }
        }
    }
}
