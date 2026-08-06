namespace MepPanel.Core;

/// <summary>
/// Kiểm tra quyền chức năng phụ. Stub dev: luôn cho phép (tạm dừng license server).
/// Bản release đầy đủ thay bằng MepPanel.AutoCAD.Licensing.PluginFeatureGate.
/// </summary>
public static class PluginFeatureGate
{
    public static bool CanUse(string featureCode) => true;

    public static bool Ensure(string featureCode) => true;
}
