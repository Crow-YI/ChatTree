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

            // 监听 ActiveView 变化，通过代码切换视图可见性（比 XAML 绑定更可靠）
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowVM.ActiveView))
                    UpdateViewVisibility();
            };

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            treeView.FileDropped += TreeView_FileDropped;

            // 设置初始视图可见性
            UpdateViewVisibility();

            if (!string.IsNullOrWhiteSpace(_initialFilePath) &&
                _initialFilePath.EndsWith(".chat", StringComparison.OrdinalIgnoreCase))
            {
                _vm.FileViewVM.LoadFromPath(_initialFilePath);
            }
        }

        /// <summary>
        /// 根据当前 ActiveView 切换多功能界面的可见性
        /// </summary>
        private void UpdateViewVisibility()
        {
            treeView.Visibility = _vm.ActiveView == ActiveViewType.TreeView
                ? Visibility.Visible : Visibility.Collapsed;
            fileView.Visibility = _vm.ActiveView == ActiveViewType.FileView
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TreeView_FileDropped(string filePath)
        {
            if (filePath.EndsWith(".chat", StringComparison.OrdinalIgnoreCase))
            {
                _vm.FileViewVM.LoadFromPath(filePath);
            }
        }
    }
}
