using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TreeChat.Services;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowVM _vm;
        private string? _initialFilePath;

        public MainWindow()
        {
            var fileService = new FileService();
            _vm = new MainWindowVM(fileService);

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

        // ==================== 保存命令 ====================

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _vm.CurrentChatTree != null;
        }

        private async void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            await _vm.SaveAsync();
        }

        // ==================== 窗口关闭 ====================

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_vm.CurrentChatTree?.IsModified == true)
            {
                var result = MessageBox.Show(
                    "当前对话有未保存的更改，是否保存？",
                    "提示",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 关闭时同步等待保存完成（写入量极小，毫秒级）
                    _vm.SaveAsync().GetAwaiter().GetResult();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // 停止自动保存定时器
            _vm.StopAutoSave();

            base.OnClosing(e);
        }
    }
}
