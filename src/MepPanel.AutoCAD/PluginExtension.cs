using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(MepPanel.Plugin.PluginExtension))]

namespace MepPanel.Plugin
{
    public sealed class PluginExtension : IExtensionApplication
    {
        public void Initialize()
        {
            PluginBundleControl.InitializeOnLoad();
        }

        public void Terminate()
        {
        }
    }
}
