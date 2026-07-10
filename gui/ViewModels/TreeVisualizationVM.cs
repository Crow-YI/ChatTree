using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    public class TreeVisualizationVM : BaseViewModel
    {
        /// <summary>
        /// 当前是否已加载文件（控制空状态提示的显示）
        /// </summary>
        private bool _isFileLoaded;
        public bool IsFileLoaded
        {
            get => _isFileLoaded;
            private set => SetProperty(ref _isFileLoaded, value);
        }

        // 根节点VM
        public TreeNodeVM? RootNode { get; private set; }

        // 选中的节点
        private TreeNodeVM? _selectedNode;
        public TreeNodeVM? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != null)
                    _selectedNode.IsSelected = false;

                if (SetProperty(ref _selectedNode, value))
                {
                    if (value != null)
                    {
                        value.IsSelected = true;
                        SelectedNodeChanged?.Invoke(value);
                    }
                    RenameNodeCommand?.OnCanExecuteChanged();
                }
            }
        }

        private ChatTree? _currentChatTree;
        public ChatTree? CurrentChatTree
        {
            get => _currentChatTree;
            set => SetProperty(ref _currentChatTree, value);
        }

        /// <summary>
        /// 展平的节点列表，供 NodifyEditor.Decorators 绑定
        /// </summary>
        public ObservableCollection<TreeNodeVM> Decorators { get; } = new();

        /// <summary>
        /// 连线列表，供 NodifyEditor.Connections 绑定
        /// </summary>
        public ObservableCollection<ConnectionVM> Connections { get; } = new();

        /// <summary>
        /// NodifyEditor 选中项集合（双向绑定用）
        /// </summary>
        public ObservableCollection<object> SelectedItems { get; } = new();

        public RelayCommand ShowConfigCommand { get; }
        public RelayCommand RenameNodeCommand { get; }
        public RelayCommand SelectNodeCommand { get; }

        public event Action<TreeNodeVM>? SelectedNodeChanged;

        /// <summary>
        /// 树渲染完成后触发（供 View 执行 FitToScreen 等操作）
        /// </summary>
        public event Action? TreeRendered;

        public TreeVisualizationVM()
        {
            RootNode = null;
            ShowConfigCommand = new RelayCommand(ExecuteShowConfig);
            RenameNodeCommand = new RelayCommand(ExecuteRenameNode, CanExecuteRenameNode);
            SelectNodeCommand = new RelayCommand(ExecuteSelectNode);
            SelectedNode = null;

            SelectedItems.CollectionChanged += OnSelectedItemsChanged;
        }

        /// <summary>
        /// NodifyEditor 选中变化时同步到 SelectedNode
        /// </summary>
        private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var selected = SelectedItems.OfType<TreeNodeVM>().FirstOrDefault();
            SelectedNode = selected;
        }

        /// <summary>
        /// 用户鼠标点击节点时选中
        /// </summary>
        private void ExecuteSelectNode(object? parameter)
        {
            if (parameter is TreeNodeVM node)
            {
                SelectedNode = node;
                // 用户点击时 DecoratorContainer 已存在，直接同步选中
                SelectedItems.Clear();
                SelectedItems.Add(node);
            }
        }

        /// <summary>
        /// 将当前选中状态同步到 SelectedItems 集合（供 NodifyEditor 识别）
        /// 在 Decorators 渲染完成后调用，避免 Nodify 找不到对应容器
        /// </summary>
        public void SyncSelectedToNodify()
        {
            SelectedItems.Clear();
            if (_selectedNode != null)
                SelectedItems.Add(_selectedNode);
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
                    RebuildAll(RootNode!);
                    Application.Current.Dispatcher.InvokeAsync(
                        SyncSelectedToNodify,
                        System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private async void ExecuteShowConfig(object? parameter)
        {
            var dialog = new Views.ConfigDialog();

            if (dialog.ShowDialog() == true)
            {
                ApiConfig.ApiKey = dialog.ApiKey;
                ApiConfig.ApiEndpoint = dialog.ApiEndpoint;
                ApiConfig.ModelName = dialog.ModelName;
                ApiConfig.Temperature = dialog.Temperature;
                ApiConfig.TopP = dialog.TopP;
                ApiConfig.MaxTokens = dialog.MaxTokens;

                ApiConfig.SaveToFile();

                try
                {
                    await App.Backend.PushConfigAsync(
                        new ChatConfigData
                        {
                            Model = ApiConfig.ModelName,
                            Temperature = ApiConfig.Temperature,
                            TopP = ApiConfig.TopP,
                            MaxTokens = ApiConfig.MaxTokens,
                        });
                }
                catch { /* 配置同步失败不影响使用 */ }
            }
        }

        /// <summary>
        /// 设置新的对话树，触发布局计算并重建展平列表和连线
        /// </summary>
        public void SetTree(TreeNodeVM rootNode)
        {
            try
            {
                RootNode = rootNode;
                IsFileLoaded = true;
                TreeLayoutService.LayoutTree(RootNode);
                RebuildAll(RootNode);
                SelectedNode = rootNode;

                // NodifyEditor 需要等布局通道完成才能创建 DecoratorContainer，
                // 延后同步选中状态，避免 Nodify 找不到容器而崩溃
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SyncSelectedToNodify();
                    TreeRendered?.Invoke();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SetTree 异常：{ex.GetType().Name}\n{ex.Message}\n\n堆栈：{ex.StackTrace}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 树变更后增量更新布局并重建展平列表和连线
        /// </summary>
        public void UpdateTree(TreeNodeVM updateNode, TreeNodeVM selectedNode)
        {
            if (RootNode == null)
                return;
            TreeLayoutService.UpdateLayoutTree(updateNode);
            RebuildAll(RootNode);
            SelectedNode = selectedNode;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SyncSelectedToNodify();
                TreeRendered?.Invoke();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 清除当前树（用于关闭文件或重置状态）
        /// </summary>
        public void ClearTree()
        {
            RootNode = null;
            Decorators.Clear();
            Connections.Clear();
            SelectedItems.Clear();
            SelectedNode = null;
            CurrentChatTree = null;
            IsFileLoaded = false;
        }

        /// <summary>
        /// 重建 Decorators 和 Connections 集合
        /// </summary>
        private void RebuildAll(TreeNodeVM rootNode)
        {
            Decorators.Clear();
            RebuildFlatList(rootNode);

            Connections.Clear();
            RebuildConnections(rootNode);
        }

        private void RebuildFlatList(TreeNodeVM node)
        {
            Decorators.Add(node);
            foreach (var child in node.Children)
                RebuildFlatList(child);
        }

        private void RebuildConnections(TreeNodeVM node)
        {
            foreach (var child in node.Children)
            {
                Connections.Add(new ConnectionVM(node, child));
                RebuildConnections(child);
            }
        }
    }
}