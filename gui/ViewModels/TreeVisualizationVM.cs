using System.Windows;
using TreeChat.Commands;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    public class TreeVisualizationVM : BaseViewModel
    {
        // 根节点VM
        public TreeNodeVM? RootNode { get; private set; }

        // 选中的节点（与View的依赖属性双向绑定）
        private TreeNodeVM? _selectedNode;
        public TreeNodeVM? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if(value != null && _selectedNode != value)
                    SelectedNodeChanged?.Invoke(value);
                SetProperty(ref _selectedNode, value);
                RenameNodeCommand?.OnCanExecuteChanged();
            }
        }

        private ChatTree? _currentChatTree;
        public ChatTree? CurrentChatTree
        {
            get => _currentChatTree;
            set => SetProperty(ref _currentChatTree, value);
        }

        public RelayCommand ShowConfigCommand { get; }
        public RelayCommand RenameNodeCommand { get; }

        public event Action? CanvasPropertyChanged;

        public event Action<TreeNodeVM>? SelectedNodeChanged;

        public TreeVisualizationVM()
        {
            RootNode = null;
            // 先初始化命令
            ShowConfigCommand = new RelayCommand(ExecuteShowConfig);
            RenameNodeCommand = new RelayCommand(ExecuteRenameNode, CanExecuteRenameNode);
            // 再设置SelectedNode
            SelectedNode = null;
        }

        private bool CanExecuteRenameNode(object? parameter)
        {
            return SelectedNode != null;
        }

        private void ExecuteRenameNode(object? parameter)
        {
            if (SelectedNode == null)
                return;

            string currentName = SelectedNode.Node.Name ?? SelectedNode.Node.NodeID.ToString();
            var dialog = new Views.RenameDialog(currentName);
            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.NewName;
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    SelectedNode.Node.Name = newName;

                    // 同步到 Python 后端
                    if (CurrentChatTree?.TreeId != null)
                    {
                        _ = App.Backend.RenameNodeAsync(
                            CurrentChatTree.TreeId, SelectedNode.Node.NodeID, newName);
                    }

                    // 重新计算整个树的布局
                    if (RootNode != null)
                    {
                        TreeLayoutService.LayoutTree(RootNode);
                    }
                    // 通知UI更新
                    OnPropertyChanged(nameof(SelectedNode));
                    CanvasPropertyChanged?.Invoke();
                }
            }
        }

        private async void ExecuteShowConfig(object? parameter)
        {
            // 直接打开配置对话框，构造时已自动读取 ApiConfig 中的当前值
            var dialog = new Views.ConfigDialog();

            if (dialog.ShowDialog() == true)
            {
                // 将修改后的配置保存回全局 ApiConfig
                ApiConfig.ApiKey = dialog.ApiKey;
                ApiConfig.ApiEndpoint = dialog.ApiEndpoint;
                ApiConfig.ModelName = dialog.ModelName;
                ApiConfig.Temperature = dialog.Temperature;
                ApiConfig.TopP = dialog.TopP;
                ApiConfig.TopK = dialog.TopK;

                // 持久化到文件
                ApiConfig.SaveToFile();

                // 同步到 Python 后端
                try
                {
                    await App.Backend.PushConfigAsync(
                        new Models.ChatConfigData
                        {
                            Model = ApiConfig.ModelName,
                            Temperature = ApiConfig.Temperature,
                            TopP = ApiConfig.TopP,
                            TopK = ApiConfig.TopK,
                            MaxTokens = ApiConfig.MaxTokens,
                        });
                }
                catch { /* 配置同步失败不影响使用 */ }
            }
        }

        public void SetTree(TreeNodeVM rootNode)
        {
            RootNode = rootNode;
            TreeLayoutService.LayoutTree(RootNode);
            CanvasPropertyChanged?.Invoke();
            SelectedNode = rootNode;
        }

        public void UpdateTree(TreeNodeVM updateNode, TreeNodeVM selectedNode)
        {
            if(RootNode == null)
                return;
            TreeLayoutService.UpdateLayoutTree(updateNode);
            CanvasPropertyChanged?.Invoke();
        }
    }
}