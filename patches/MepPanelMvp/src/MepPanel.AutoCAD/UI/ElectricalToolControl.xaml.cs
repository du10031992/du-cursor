using MepPanel.AutoCAD.Licensing;
using System.Windows;
using System.Windows.Controls;

namespace MepPanelMvp.UI
{
    public partial class ElectricalToolControl : UserControl
    {
        public ElectricalToolControl()
        {
            InitializeComponent();
            DataContext = PaletteHost.ViewModel;
            ApplyTheme(PluginThemeService.LoadAndApply(this), false);
            PluginFeatureUi.Apply(this);
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu { PlacementTarget = ThemeButton };
            foreach (var theme in PluginThemeService.AvailableThemes)
            {
                var selectedTheme = theme;
                var item = new MenuItem { Header = theme.DisplayName, Tag = theme.Key };
                item.Click += (unusedSender, unusedArgs) => ApplyTheme(selectedTheme, true);
                menu.Items.Add(item);
            }
            ThemeButton.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void ApplyTheme(PluginTheme theme, bool save)
        {
            if (save) PluginThemeService.ApplyAndSave(this, theme);
            ThemeButton.Content = "Giao diện: " + theme.DisplayName;
        }

        private void OpenElectricalConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (PluginFeatureGate.Ensure("MEPDBDRAW")) ConfigurationHost.Show();
        }

        private void OpenPanelConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (PluginFeatureGate.Ensure("MEPDBCONFIG")) ConfigurationHost.Show();
        }

        private void Hvac_Click(object sender, RoutedEventArgs e)
        {
            if (PluginFeatureGate.Ensure("MEPHVAC")) HvacConfigurationHost.Show();
        }

        private void NewPanel_Click(object sender, RoutedEventArgs e)
        {
            if (!PluginFeatureGate.Ensure("MEPDBCONFIG")) return;
            PaletteHost.ViewModel.NewPanel();
            ConfigurationHost.Show();
        }

        private void EditPanel_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBEDIT");
        private void Export_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBEXPORT");
        private void Excel_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBEXCEL");
        private void SelectLayer_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPSELAYER");
        private void DuplicatePanel_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBDUPLICATE");
        private void Help_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBHELP");
        private void CabinetViews_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBCABINETVIEWS");
        private void PowerLayout_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBPOWER");
        private void ThreePhaseFourWire_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDB3P4W");
        private void RealisticWiring_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBREALWIRING");
        private void RealisticWiringRender_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBREALRENDER");
        private void DeviceBlocks_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDEVICEBLOCKS");
        private void Cabinet2d_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBCABINET2D");
        private void CabinetRender_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBRENDER");
        private void CabinetUnfold_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBUNFOLD");
        private void ElectricalKnowledge_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBKNOWLEDGE");

        private void BranchPanel_Click(object sender, RoutedEventArgs e)
        {
            if (!PluginFeatureGate.Ensure("MEPDBCONFIG")) return;
            PaletteHost.ViewModel.Panel.PanelType = "Tủ nhánh";
            ConfigurationHost.Show();
        }

        private void MainPanel_Click(object sender, RoutedEventArgs e)
        {
            if (!PluginFeatureGate.Ensure("MEPDBCONFIG")) return;
            PaletteHost.ViewModel.Panel.PanelType = "Tủ tổng";
            PaletteHost.ViewModel.Panel.SupplyPhase = 3;
            ConfigurationHost.Show();
        }

        private void Renumber_Click(object sender, RoutedEventArgs e)
        {
            if (!PluginFeatureGate.Ensure("MEPDBCONFIG")) return;
            PaletteHost.ViewModel.RenumberCircuits();
            ConfigurationHost.Show();
        }

        private void HeNuoc_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPWATER");
        private void BaoChay_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPFIRE");
    }
}
