using Autodesk.AutoCAD.Windows;
using MepPanel.Core;

namespace MepPanel.Blocks.AutoCAD
{
    public static class ToolHost
    {
        private static PaletteSet _mepDbPalette;
        private static PaletteSet _mepHvacPalette;

        public static void ShowMepDb()
        {
            EnsureMepDbPalette();
            _mepDbPalette.Visible = true;
        }

        public static void ShowMepHvac()
        {
            EnsureMepHvacPalette();
            _mepHvacPalette.Visible = true;
        }

        private static void EnsureMepDbPalette()
        {
            if (_mepDbPalette != null)
            {
                return;
            }

            _mepDbPalette = CreatePalette(
                PluginFeatures.MepDb,
                "MEPDB",
                new MepDbToolPanel());
        }

        private static void EnsureMepHvacPalette()
        {
            if (_mepHvacPalette != null)
            {
                return;
            }

            _mepHvacPalette = CreatePalette(
                PluginFeatures.MepHvac,
                "MEPHVAC",
                new MepHvacToolPanel());
        }

        private static PaletteSet CreatePalette(string id, string title, System.Windows.Forms.Control panel)
        {
            var palette = new PaletteSet(id, title, new System.Guid(
                id == PluginFeatures.MepDb
                    ? "a1b2c3d4-e5f6-4789-a012-000000000001"
                    : "b2c3d4e5-f6a7-4890-b123-000000000002"))
            {
                Style = PaletteSetStyles.ShowPropertiesMenu
                    | PaletteSetStyles.ShowAutoHideButton
                    | PaletteSetStyles.ShowCloseButton,
                MinimumSize = new System.Drawing.Size(300, 220)
            };

            palette.Add(title, panel);
            return palette;
        }
    }
}
