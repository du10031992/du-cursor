namespace MepPanel.Core
{
    /// <summary>
    /// Kiem tra quyen chuc nang phu. Stub: luon cho phep.
    /// Ban release day du co the thay bang MepPanel.AutoCAD.Licensing.PluginFeatureGate.
    /// </summary>
    public static class PluginFeatureGate
    {
        public static bool CanUse(string featureCode) => true;

        public static bool Ensure(string featureCode) => true;
    }
}
