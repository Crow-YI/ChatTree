using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Infrastructure;
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

        // ---- 原命令（仅保留 SelectNodeCommand）----

        public RelayCommand SelectNodeCommand { get; }

        // ---- 模型配置下拉框 ----

        public ObservableCollection<DropdownProfileItem> DropdownProfiles { get; } = new();

        private string _activeProfileDisplay = "";
        public string ActiveProfileDisplay
        {
            get => _activeProfileDisplay;
            set => SetProperty(ref _activeProfileDisplay, value);
        }

        public RelayCommand AddNewProfileCommand { get; }

        public event Action<TreeNodeVM>? SelectedNodeChanged;

        /// <summary>
        /// 树渲染完成后触发（供 View 执行 FitToScreen 等操作）
        /// </summary>
        public event Action? TreeRendered;

        public TreeVisualizationVM()
        {
            RootNode = null;
            SelectNodeCommand = new RelayCommand(ExecuteSelectNode);
            AddNewProfileCommand = new RelayCommand(_ => ExecuteAddNewProfile());
            SelectedNode = null;

            SelectedItems.CollectionChanged += OnSelectedItemsChanged;

            // 初始化下拉框
            RefreshDropdownProfiles();
        }

        // ================================================================
        // 模型配置下拉框
        // ================================================================

        /// <summary>
        /// 从 ApiConfig 刷新下拉框的 profile 列表。
        /// </summary>
        public void RefreshDropdownProfiles()
        {
            DropdownProfiles.Clear();

            var active = ApiConfig.GetActiveProfile();
            ActiveProfileDisplay = active != null
                ? $"当前: {active.Name} ({active.Provider}) — {active.Model}"
                : "当前: 未配置";

            foreach (var p in ApiConfig.Profiles)
            {
                var item = new DropdownProfileItem
                {
                    Name = p.Name,
                    Provider = p.Provider,
                    Model = p.Model,
                    IsActive = p.Name == ApiConfig.ActiveProfileName,
                };
                item.ActivateCommand = new RelayCommand(_ => ExecuteActivateProfile(item));
                item.EditCommand = new RelayCommand(_ => ExecuteEditProfile(item));
                DropdownProfiles.Add(item);
            }
        }

        private async void ExecuteActivateProfile(DropdownProfileItem item)
        {
            if (item.Name == ApiConfig.ActiveProfileName) return;

            try
            {
                await App.Backend.ActivateProfileAsync(item.Name);

                // 同步本地配置
                ApiConfig.LoadFromFile();

                // 推送激活 profile 的配置到后端
                await App.Backend.PushConfigAsync(new ChatConfigData
                {
                    Model = ApiConfig.ModelName,
                    Temperature = ApiConfig.Temperature,
                    TopP = ApiConfig.TopP,
                    MaxTokens = ApiConfig.MaxTokens,
                });

                AppLogger.Info("Profile activated: {Name}", item.Name);
                RefreshDropdownProfiles();
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to activate profile: {Name}", item.Name);
                MessageBox.Show($"切换配置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteEditProfile(DropdownProfileItem item)
        {
            // 从后端获取完整 profile 数据（含 API Key）
            _ = OpenEditDialogAsync(item.Name);
        }

        private async Task OpenEditDialogAsync(string profileName)
        {
            try
            {
                var detail = await App.Backend.GetProfileAsync(profileName);
                var dialog = new Views.ConfigDialog(existing: detail);

                if (dialog.ShowDialog() == true)
                {
                    AppLogger.Info("Profile edited: {Name}", profileName);
                    ApiConfig.LoadFromFile();
                    RefreshDropdownProfiles();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to edit profile: {Name}", profileName);
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteAddNewProfile()
        {
            var dialog = new Views.ConfigDialog(existing: null);

            if (dialog.ShowDialog() == true)
            {
                AppLogger.Info("New profile created");
                ApiConfig.LoadFromFile();
                RefreshDropdownProfiles();
            }
        }

        // ================================================================
        // 选中节点
        // ================================================================

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

        // ================================================================
        // 树操作
        // ================================================================

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

                // 加载树时刷新 profile 列表
                RefreshDropdownProfiles();
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

    // ================================================================
    // 下拉框 Profile 项
    // ================================================================

    public class DropdownProfileItem : BaseViewModel
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _provider = "";
        public string Provider
        {
            get => _provider;
            set => SetProperty(ref _provider, value);
        }

        private string _model = "";
        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                    OnPropertyChanged(nameof(DisplayText));
            }
        }

        public string DisplayText => IsActive
            ? $"✓ {Name} ({Provider}) — {Model}"
            : $"  {Name} ({Provider}) — {Model}";

        public RelayCommand? ActivateCommand { get; set; }
        public RelayCommand? EditCommand { get; set; }
    }
}
