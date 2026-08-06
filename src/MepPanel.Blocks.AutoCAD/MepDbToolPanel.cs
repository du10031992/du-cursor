using System;
using System.Drawing;
using System.Windows.Forms;
using MepPanel.Blocks.AutoCAD.Drawing;

namespace MepPanel.Blocks.AutoCAD
{
    internal sealed class MepDbToolPanel : UserControl
    {
        public MepDbToolPanel()
        {
            BackColor = Color.White;
            Padding = new Padding(12);
            AutoScroll = true;

            AddTitle("MEPDB - Drawing Database");
            AddHint("Tao layer, chen block thiet bi, ve vung MEPDB.");
            AddButton("Tao layer MEPDB", (s, e) => RunSafe(MepDbDrawingService.EnsureLayer));
            AddButton("Chen block thiet bi (MEPDB_EQUIP)", (s, e) => RunSafe(MepDbDrawingService.InsertEquipmentMarker));
            AddButton("Ve vung thiet bi (hinh chu nhat)", (s, e) => RunSafe(MepDbDrawingService.DrawEquipmentZone));
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
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MepPanel MEPDB");
            }
        }
    }
}
