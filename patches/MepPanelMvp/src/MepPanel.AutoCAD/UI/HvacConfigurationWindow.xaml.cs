using MepPanel.AutoCAD.Licensing;
using System.Windows;
using System.Windows.Controls;

namespace MepPanelMvp.UI
{
    public partial class HvacConfigurationWindow : Window
    {
        public HvacConfigurationWindow(HvacViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        public HvacViewModel ViewModel { get; }

        public void CommitEdits()
        {
            OutletsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            OutletsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        public void RefreshFromViewModel()
        {
            DataContext = null;
            DataContext = ViewModel;
            OutletsGrid.Items.Refresh();
        }

        private void Calculate_Click(object sender, RoutedEventArgs e) { CommitEdits(); ViewModel.Recalculate(); OutletsGrid.Items.Refresh(); }
        private void Add_Click(object sender, RoutedEventArgs e) { CommitEdits(); ViewModel.AddOutlet(); OutletsGrid.Items.Refresh(); }
        private void Duplicate_Click(object sender, RoutedEventArgs e) { CommitEdits(); ViewModel.DuplicateOutlet(); OutletsGrid.Items.Refresh(); }
        private void Remove_Click(object sender, RoutedEventArgs e) { ViewModel.RemoveOutlet(); OutletsGrid.Items.Refresh(); }
        private void Up_Click(object sender, RoutedEventArgs e) { ViewModel.MoveSelected(-1); OutletsGrid.Items.Refresh(); }
        private void Down_Click(object sender, RoutedEventArgs e) { ViewModel.MoveSelected(1); OutletsGrid.Items.Refresh(); }
        private void New_Click(object sender, RoutedEventArgs e) { ViewModel.NewSystem(); RefreshFromViewModel(); }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            CommitEdits();
            ViewModel.Recalculate();
            OutletsGrid.Items.Refresh();
            AutoCadCommandDispatcher.Queue("MEPHVACDRAW");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
