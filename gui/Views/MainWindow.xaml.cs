using System.Windows;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowVM _vm = new();
        private string? _initialFilePath;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            _initialFilePath = App.StartupFilePath;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            treeView.FileDropped += TreeView_FileDropped;

            if (!string.IsNullOrWhiteSpace(_initialFilePath) && 
                _initialFilePath.EndsWith(".chat", StringComparison.OrdinalIgnoreCase))
            {
                _vm.ChatManagementPanelVM.LoadChatFromPath(_initialFilePath);
            }
        }

        private void TreeView_FileDropped(string filePath)
        {
            if (filePath.EndsWith(".chat", StringComparison.OrdinalIgnoreCase))
            {
                _vm.ChatManagementPanelVM.LoadChatFromPath(filePath);
            }
        }
    }
}