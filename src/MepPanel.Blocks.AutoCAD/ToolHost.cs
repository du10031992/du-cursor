using Autodesk.AutoCAD.Windows;
using MepPanel.Core;

namespace MepPanel.Blocks.AutoCAD;

public static class ToolHost
{
    private static PaletteSet _drawingToolPalette;

    public static void ShowMepDb() => ShowDrawingToolPanel();

    public static void ShowMepHvac() => ShowDrawingToolPanel();

    public static void ShowDrawingToolPanel()
    {
        EnsureDrawingToolPalette();
        _drawingToolPalette.Visible = true;
    }

    private static void EnsureDrawingToolPalette()
    {
        if (_drawingToolPalette != null)
        {
            return;
        }

        _drawingToolPalette = new PaletteSet(
            PluginFeatures.MepDb,
            "MEP DRAWING TOOL",
            new Guid("a1b2c3d4-e5f6-4789-a012-000000000099"))
        {
            Style = PaletteSetStyles.ShowPropertiesMenu
                | PaletteSetStyles.ShowAutoHideButton
                | PaletteSetStyles.ShowCloseButton,
            MinimumSize = new System.Drawing.Size(300, 420)
        };

        _drawingToolPalette.Add("MEP DRAWING TOOL", new MepDrawingToolPanel());
    }
}
