using System;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Lenh noi bo: goi tu panel (AutoCadCommandDispatcher), KHONG dang ky tren command line AutoCAD.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MepInternalCommandAttribute : Attribute
    {
        public string Name { get; }

        public MepInternalCommandAttribute(string name)
        {
            Name = name ?? string.Empty;
        }
    }

    /// <summary>
    /// Khai bao feature bat buoc cho lenh moi.
    /// Dispatcher kiem tra attribute nay truoc khi goi method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class MepRequiresFeatureAttribute : Attribute
    {
        public string FeatureCode { get; }

        public MepRequiresFeatureAttribute(string featureCode)
        {
            FeatureCode = featureCode ?? string.Empty;
        }
    }
}
