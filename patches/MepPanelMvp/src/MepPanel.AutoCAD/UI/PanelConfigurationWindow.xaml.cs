using MepPanel.AutoCAD.Licensing;
using System.Windows;
using System.Windows.Controls;

namespace MepPanelMvp.UI
{
    public partial class PanelConfigurationWindow : Window
    {
        public PanelConfigurationWindow(PanelViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            RefreshOrientation();
        }

        public PanelViewModel ViewModel { get; }

        public void CommitEdits()
        {
            CircuitsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        public void RefreshFromViewModel()
        {
            DataContext = null;
            DataContext = ViewModel;
            RefreshOrientation();
        }

        private void QuickLoad_Click(object sender, RoutedEventArgs e)
        {
            CommitEdits();
            var tag = (sender as Button)?.Tag as string;
            if (tag == "Khác") tag = "Chiếu sáng";
            ViewModel.SelectedPreset = tag ?? "Chiếu sáng";
            ViewModel.AddPreset();
            if ((sender as Button)?.Tag as string == "Khác" && ViewModel.SelectedCircuit != null)
            {
                ViewModel.SelectedCircuit.Name = "PHỤ TẢI MỚI";
                ViewModel.SelectedCircuit.LoadType = "Khác";
            }
            CircuitsGrid.Items.Refresh();
        }

        private void RemoveCircuit_Click(object sender, RoutedEventArgs e) { ViewModel.RemoveSelected(); CircuitsGrid.Items.Refresh(); }
        private void DuplicateCircuit_Click(object sender, RoutedEventArgs e) { ViewModel.DuplicateSelected(); CircuitsGrid.Items.Refresh(); }
        private void MoveUp_Click(object sender, RoutedEventArgs e) { ViewModel.MoveSelected(-1); CircuitsGrid.Items.Refresh(); }
        private void MoveDown_Click(object sender, RoutedEventArgs e) { ViewModel.MoveSelected(1); CircuitsGrid.Items.Refresh(); }
        private void Balance_Click(object sender, RoutedEventArgs e) { CommitEdits(); ViewModel.Recalculate(true); CircuitsGrid.Items.Refresh(); }
        private void Recalculate_Click(object sender, RoutedEventArgs e) { CommitEdits(); ViewModel.Recalculate(false); CircuitsGrid.Items.Refresh(); }
        private void IncreaseCable_Click(object sender, RoutedEventArgs e) { CommitEdits(); ViewModel.IncreaseSelectedCable(); CircuitsGrid.Items.Refresh(); }
        private void Draw_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBDRAW");
        private void Update_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBUPDATE");
        private void Export_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBEXPORT");
        private void CabinetViews_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBCABINETVIEWS");
        private void PowerLayout_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBPOWER");
        private void ThreePhaseFourWire_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDB3P4W");
        private void RealisticWiring_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBREALWIRING");
        private void RealisticWiringRender_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBREALRENDER");
        private void Cabinet2d_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBCABINET2D");
        private void CabinetRender_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBRENDER");
        private void CabinetUnfold_Click(object sender, RoutedEventArgs e) => QueuePanelCommand("MEPDBUNFOLD");
        private void ElectricalKnowledge_Click(object sender, RoutedEventArgs e) => AutoCadCommandDispatcher.Queue("MEPDBKNOWLEDGE");
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void QueuePanelCommand(string command)
        {
            CommitEdits();
            ViewModel.Recalculate(false);
            AutoCadCommandDispatcher.Queue(command);
        }

        private void Orientation_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || ViewModel == null) return;
            ViewModel.Panel.Orientation = (sender as FrameworkElement)?.Tag as string ?? "Vertical";
        }

        private void RefreshOrientation()
        {
            if (VerticalRadio == null || HorizontalRadio == null || ViewModel == null) return;
            VerticalRadio.IsChecked = ViewModel.Panel.Orientation != "Horizontal";
            HorizontalRadio.IsChecked = ViewModel.Panel.Orientation == "Horizontal";
        }
    }
}
