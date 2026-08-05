namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>
    /// Kiem tra chuc nang phu trong panel WPF (sau khi da vao plugin bang lenh MEPDB).
    /// </summary>
    public static class PluginFeatureGate
    {
        public const string Entry = "MEPDB";

        public static bool CanUse(string subFeatureCode)
        {
            return LicenseSession.IsAuthorized
                && LicenseSession.HasFeature(Entry)
                && LicenseSession.HasFeature(subFeatureCode);
        }

        public static bool Ensure(string subFeatureCode)
        {
            return LicenseGuard.EnsureSubFeature(subFeatureCode);
        }
    }
}
