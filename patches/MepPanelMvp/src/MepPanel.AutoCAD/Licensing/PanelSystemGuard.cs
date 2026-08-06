namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>Guard tap trung cho nut chon he thong tren panel WPF.</summary>
    public static class PanelSystemGuard
    {
        public static bool EnsureElectrical() => PluginFeatureGate.Ensure("MEPDBDRAW");
        public static bool EnsureHvac() => PluginFeatureGate.Ensure("MEPHVAC");
        public static bool EnsureWater() => PluginFeatureGate.Ensure("MEPDBWATER");
        public static bool EnsureFireAlarm() => PluginFeatureGate.Ensure("MEPDBSMOKE");
    }
}
