using System.Windows;
using System.Windows.Threading;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 多功能界面类型
    /// </summary>
    public enum ActiveViewType
    {
        TreeView,
        FileView
    }

    /// <summary>
    /// 主窗口VM，管理导航状态和子VM
    /// </summary>
    public class MainWindowVM : BaseViewModel
    {
        private readonly FileService _fileService;
        private DispatcherTimer? _autoSaveTimer;

        /// <summary>
        /// 自动保存间隔（后续可改为配置项）
        /// </summary>
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(10);

        // === 导航状态 ===
        private ActiveViewType _activeView = ActiveViewType.FileView;
        private bool _isSidebarExpanded = true;

        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set
            {
                if (SetProperty(ref _isSidebarExpanded, value))
                {
                    OnPropertyChanged(nameof(NavigationColumnWidth));
                    OnPropertyChanged(nameof(WorkspaceColumnWidth));
                }
            }
        }

        public GridLength NavigationColumnWidth =>
            IsSidebarExpanded ? new GridLength(48) : new GridLength(0);

        public GridLength WorkspaceColumnWidth =>
            IsSidebarExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        public ActiveViewType ActiveView
        {
            get => _activeView;
            set
            {
                if (SetProperty(ref _activeView, value))
                {
                    OnPropertyChanged(nameof(IsTreeViewVisible));
                    OnPropertyChanged(nameof(IsFileViewVisible));
                }
            }
        }
        public bool IsTreeViewVisible => _activeView == ActiveViewType.TreeView;
        public bool IsFileViewVisible => _activeView == ActiveViewType.FileView;

        // === 子VM ===
        public FileViewVM FileViewVM { get; }
        public TreeVisualizationVM TreeVisualizationVM { get; }
        public ChatInformationVM ChatInformationVM { get; }

        /// <summary>
        /// 当前打开的聊天树（单文件模式，无列表）
        /// </summary>
        public ChatTree? CurrentChatTree { get; private set; }

        // === 导航命令 ===
        public Commands.RelayCommand ShowTreeViewCommand { get; }
        public Commands.RelayCommand ShowFileViewCommand { get; }
        public Commands.RelayCommand ShowSettingsCommand { get; }
        public Commands.RelayCommand ToggleSidebarCommand { get; }

        public MainWindowVM(FileService fileService)
        {
            _fileService = fileService;

            FileViewVM = new FileViewVM(fileService);
            TreeVisualizationVM = new TreeVisualizationVM();
            ChatInformationVM = new ChatInformationVM();

            // 导航命令
            ShowTreeViewCommand = new Commands.RelayCommand(_ => ActiveView = ActiveViewType.TreeView);
            ShowFileViewCommand = new Commands.RelayCommand(_ => ActiveView = ActiveViewType.FileView);
            ToggleSidebarCommand = new Commands.RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
            ShowSettingsCommand = new Commands.RelayCommand(_ =>
            {
                var settingsWindow = new Views.SettingsWindow();
                settingsWindow.ShowDialog();
            });

            // 文件操作 → 加载树
            FileViewVM.FileCreatedOrOpened += OnFileCreatedOrOpened;

            // 节点选中 → 右侧信息面板
            TreeVisualizationVM.SelectedNodeChanged += (nodeVM) => { ChatInformationVM.SelectedNode = nodeVM; };

            // 树变更（消息发送等）→ 更新树可视化
            ChatInformationVM.ChatTreeChanged += TreeVisualizationVM.UpdateTree;
        }

        /// <summary>
        /// 当文件被创建或打开时，构建对应的节点 VM 树并启动自动保存
        /// </summary>
        private void OnFileCreatedOrOpened(ChatTree tree)
        {
            CurrentChatTree = tree;

            // 直接创建根节点 VM，TreeNodeVM 构造函数会自动递归创建所有子节点
            TreeNodeVM rootNodeVM = new TreeNodeVM(tree.RootNode, null);
            TreeVisualizationVM.SetTree(rootNodeVM);
            TreeVisualizationVM.CurrentChatTree = tree;
            ChatInformationVM.CurrentChatTree = tree;

            // 自动切换到树视图
            ActiveView = ActiveViewType.TreeView;

            // 启动自动保存定时器
            StartAutoSave();
        }

        /// <summary>
        /// 手动保存（Ctrl+S 调用）
        /// </summary>
        public async Task SaveAsync()
        {
            if (CurrentChatTree != null)
            {
                await _fileService.SaveChatTreeAsync(CurrentChatTree);
            }
        }

        /// <summary>
        /// 启动自动保存定时器（10 分钟间隔）
        /// </summary>
        private void StartAutoSave()
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
            _autoSaveTimer.Tick += async (s, e) => await TryAutoSaveAsync();
            _autoSaveTimer.Start();
        }

        /// <summary>
        /// 尝试自动保存（仅在有未保存修改且有文件路径时执行）
        /// </summary>
        private async Task TryAutoSaveAsync()
        {
            if (CurrentChatTree?.IsModified == true && CurrentChatTree?.FilePath != null)
            {
                await _fileService.SaveChatTreeAsync(CurrentChatTree);
            }
        }

        /// <summary>
        /// 停止自动保存定时器（窗口关闭时调用）
        /// </summary>
        public void StopAutoSave()
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer = null;
        }
    }
}
