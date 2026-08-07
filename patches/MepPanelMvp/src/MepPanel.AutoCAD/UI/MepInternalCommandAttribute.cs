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
}
