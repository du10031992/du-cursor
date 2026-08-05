using System.Drawing;
using System.Windows.Forms;

namespace MepPanel.Blocks.AutoCAD
{
    internal sealed class MepHvacToolPanel : UserControl
    {
        public MepHvacToolPanel()
        {
            BackColor = Color.White;
            Padding = new Padding(12);

            var title = new Label
            {
                Text = "MEPHVAC — HVAC Tools",
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var hint = new Label
            {
                Text = "Panel công cụ MEPHVAC. Gắn lệnh duct/pipe thật của bạn vào đây.",
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(280, 0),
                Padding = new Padding(0, 8, 0, 8)
            };

            var sampleButton = new Button
            {
                Text = "Vẽ ống mẫu (placeholder)",
                Dock = DockStyle.Top,
                Height = 32
            };
            sampleButton.Click += (s, e) =>
                MessageBox.Show(
                    "Đây là placeholder MEPHVAC.\nThay bằng logic HVAC thật.",
                    "MepPanel MEPHVAC");

            Controls.Add(sampleButton);
            Controls.Add(hint);
            Controls.Add(title);
        }
    }
}
