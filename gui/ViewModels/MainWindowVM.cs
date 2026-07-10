using System.Windows;
using TreeChat.Models;

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
        // === 导航状态 ===
        private ActiveViewType _activeView = ActiveViewType.FileView;
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

        public MainWindowVM()
        {
            FileViewVM = new FileViewVM();
            TreeVisualizationVM = new TreeVisualizationVM();
            ChatInformationVM = new ChatInformationVM();

            // 导航命令
            ShowTreeViewCommand = new Commands.RelayCommand(_ => ActiveView = ActiveViewType.TreeView);
            ShowFileViewCommand = new Commands.RelayCommand(_ => ActiveView = ActiveViewType.FileView);
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
        /// 当文件被创建或打开时，构建对应的节点 VM 树
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
        }
    }
}
