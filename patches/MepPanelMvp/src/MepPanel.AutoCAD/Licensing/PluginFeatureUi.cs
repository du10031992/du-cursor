using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>
    /// Khoa/mo UI theo feature. Dat FrameworkElement.Tag = ma feature tren XAML.
    /// Dispatcher van la lop enforcement bat buoc khi nguoi dung bam nut.
    /// </summary>
    public static class PluginFeatureUi
    {
        private static readonly System.Collections.Generic.Dictionary<string, string> LegacyButtonFeatures =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["Sơ đồ 1 sợi →"] = "MEPDBDRAW",
                ["Vẽ mới"] = "MEPDBDRAW",
                ["Vẽ sơ đồ cấp gió"] = "MEPHVAC",
                ["Sơ đồ 3P-4D+E"] = "MEPDB3P4W",
                ["Xuất CSV"] = "MEPDBEXPORT",
                ["Xuất Excel"] = "MEPDBEXCEL",
                ["Cập nhật"] = "MEPDBUPDATE",
                ["Mặt chiếu tủ"] = "MEPDBCABINETVIEWS",
                ["Bố trí động lực"] = "MEPDBPOWER",
                ["Bố trí 2D"] = "MEPDBCABINET2D",
                ["Render màu"] = "MEPDBCABINET2D",
                ["Trải vỏ + KL"] = "MEPDBCABINET2D",
                ["Sơ đồ đấu dây thực tế"] = "MEPDBCABINET2D",
                ["Render đấu dây PNG"] = "MEPDBCABINET2D",
                ["Kiến thức điện"] = "MEPDBDRAW"
            };

        public static void Apply(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            ApplyRecursive(root);
        }

        private static void ApplyRecursive(DependencyObject element)
        {
            var frameworkElement = element as FrameworkElement;
            if (frameworkElement != null)
            {
                var feature = frameworkElement.Tag as string;
                var button = frameworkElement as Button;
                if (string.IsNullOrWhiteSpace(feature) && button != null)
                {
                    var caption = button.Content as string;
                    if (!string.IsNullOrWhiteSpace(caption))
                    {
                        LegacyButtonFeatures.TryGetValue(caption, out feature);
                    }
                }

                if (!string.IsNullOrWhiteSpace(feature) &&
                    feature.StartsWith("MEP", System.StringComparison.OrdinalIgnoreCase))
                {
                    frameworkElement.IsEnabled = PluginFeatureGate.CanUse(feature);
                    frameworkElement.ToolTip =
                        frameworkElement.IsEnabled
                            ? frameworkElement.ToolTip
                            : "Chuc nang nay chua duoc Admin mo: " + feature;
                }
            }

            var children = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < children; i++)
            {
                ApplyRecursive(VisualTreeHelper.GetChild(element, i));
            }
        }
    }
}
