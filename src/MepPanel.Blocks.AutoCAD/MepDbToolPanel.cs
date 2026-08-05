using System.Drawing;
using System.Windows.Forms;

namespace MepPanel.Blocks.AutoCAD
{
    internal sealed class MepDbToolPanel : UserControl
    {
        public MepDbToolPanel()
        {
            BackColor = Color.White;
            Padding = new Padding(12);

            var title = new Label
            {
                Text = "MEPDB — Drawing Database",
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var hint = new Label
            {
                Text = "Panel công cụ MEPDB. Gắn lệnh vẽ/block thật của bạn vào đây.",
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(280, 0),
                Padding = new Padding(0, 8, 0, 8)
            };

            var sampleButton = new Button
            {
                Text = "Chèn block mẫu (placeholder)",
                Dock = DockStyle.Top,
                Height = 32
            };
            sampleButton.Click += (s, e) =>
                MessageBox.Show(
                    "Đây là placeholder MEPDB.\nThay bằng logic block/drawing thật.",
                    "MepPanel MEPDB");

            Controls.Add(sampleButton);
            Controls.Add(hint);
            Controls.Add(title);
        }
    }
}
