using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 聊天管理面板ViewModel，管理多个对话的创建、保存、读取和重命名
    /// </summary>
    public class ChatManagementPanelVM : BaseViewModel
    {
        private ObservableCollection<ChatTree> _chatList;
        private ChatTree? _selectedChat;
        private readonly IFileService _fileService;

        /// <summary>
        /// 对话列表
        /// </summary>
        public ObservableCollection<ChatTree> ChatList
        {
            get => _chatList;
        }

        /// <summary>
        /// 当前选中的对话
        /// </summary>
        public ChatTree? SelectedChat
        {
            get => _selectedChat;
            set
            {
                SetProperty(ref _selectedChat, value);
                if (value != null)
                    SelectedChatChanged?.Invoke(value);
                
                SaveChat.OnCanExecuteChanged();
                RenameChat.OnCanExecuteChanged();
            }
        }

        /// <summary>
        /// 创建新对话命令
        /// </summary>
        public RelayCommand CreateNewChat { get; }

        /// <summary>
        /// 保存对话命令
        /// </summary>
        public RelayCommand SaveChat { get; }

        /// <summary>
        /// 读取对话命令
        /// </summary>
        public RelayCommand LoadChat { get; }

        /// <summary>
        /// 重命名对话命令
        /// </summary>
        public RelayCommand RenameChat { get; }

        /// <summary>
        /// 选中对话变更事件
        /// </summary>
        public event Action<ChatTree>? SelectedChatChanged;

        /// <summary>
        /// 构造函数，初始化命令和服务
        /// </summary>
        public ChatManagementPanelVM()
        {
            _chatList = new ObservableCollection<ChatTree>();
            _fileService = new FileService();

            CreateNewChat = new RelayCommand(ExecuteCreateNewChat);
            SaveChat = new RelayCommand(ExecuteSaveChat, CanExecuteSaveChat);
            LoadChat = new RelayCommand(ExecuteLoadChat);
            RenameChat = new RelayCommand(ExecuteRenameChat, CanExecuteRenameChat);
        }

        /// <summary>
        /// 执行创建新对话
        /// </summary>
        private void ExecuteCreateNewChat(object? parameter)
        {
            // 弹出新建对话专用窗口（仅树名 + 系统信息）
            var dialog = new Views.CreateChatDialog();
            if (dialog.ShowDialog() == true)
            {
                // 处理树名：空值则使用默认
                string treeTitle = string.IsNullOrWhiteSpace(dialog.TreeTitle)
                    ? "新对话"
                    : dialog.TreeTitle;

                // 处理系统信息：空值则使用 ChatTree 构造函数中的默认值（"你是一个有帮助的AI助手。"）
                // 注意：ChatTree 构造函数会自己处理空字符串，因此直接传递 dialog.SystemPrompt 即可
                string systemPrompt = dialog.SystemPrompt;

                // 创建新对话树（API 配置使用全局默认，无需传入）
                ChatTree newTree = new ChatTree(systemPrompt);
                newTree.TreeTitle = treeTitle;

                // 添加到列表并选中
                ChatList.Add(newTree);
                SelectedChat = newTree;
            }
        }

        /// <summary>
        /// 判断是否可以保存对话（必须选中一个对话）
        /// </summary>
        private bool CanExecuteSaveChat(object? parameter)
        {
            return SelectedChat != null;
        }

        /// <summary>
        /// 执行保存对话到文件（优先使用 Python 序列化）
        /// </summary>
        private async void ExecuteSaveChat(object? parameter)
        {
            if (SelectedChat == null) return;

            try
            {
                // 尝试使用 Python 后端序列化
                if (!string.IsNullOrEmpty(SelectedChat.TreeId))
                {
                    var serializeResp = await App.Backend.SerializeTreeAsync(SelectedChat.TreeId);
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "聊天文件 (*.chat)|*.chat|所有文件 (*.*)|*.*",
                        DefaultExt = ".chat",
                        FileName = SelectedChat.TreeTitle,
                        Title = "保存对话"
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        File.WriteAllText(dialog.FileName, serializeResp.JsonContent);
                        MessageBox.Show("保存成功！", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                // 回退：使用本地序列化
                bool success = _fileService.SaveChatTree(SelectedChat);
                if (success)
                {
                    MessageBox.Show("保存成功！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行从文件读取对话（优先使用 Python 反序列化）
        /// </summary>
        private async void ExecuteLoadChat(object? parameter)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "聊天文件 (*.chat)|*.chat|所有文件 (*.*)|*.*",
                    DefaultExt = ".chat",
                    Title = "打开对话"
                };
                if (dialog.ShowDialog() == true)
                {
                    var json = File.ReadAllText(dialog.FileName);
                    // 使用 Python 后端反序列化
                    try
                    {
                        var treeResponse = await App.Backend.DeserializeTreeAsync(
                            json, Path.GetFileNameWithoutExtension(dialog.FileName));
                        // 从 API 响应构建本地 ChatTree
                        var loadedTree = ConvertToLocalTree(treeResponse);
                        ChatList.Add(loadedTree);
                        SelectedChat = loadedTree;
                        return;
                    }
                    catch
                    {
                        // Python 不可用时回退到本地反序列化
                    }
                }
                // 回退：使用本地反序列化
                var fallbackTree = _fileService.LoadChatTree();
                if (fallbackTree != null)
                {
                    ChatList.Add(fallbackTree);
                    SelectedChat = fallbackTree;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从指定文件路径加载对话
        /// </summary>
        public async void LoadChatFromPath(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                try
                {
                    var treeResponse = await App.Backend.DeserializeTreeAsync(
                        json, Path.GetFileNameWithoutExtension(filePath));
                    var loadedTree = ConvertToLocalTree(treeResponse);
                    ChatList.Add(loadedTree);
                    SelectedChat = loadedTree;
                    return;
                }
                catch { /* 回退到本地加载 */ }
            }
            catch { }

            // 回退：使用本地加载
            var fallbackTree = _fileService.LoadChatTree(filePath);
            if (fallbackTree != null)
            {
                ChatList.Add(fallbackTree);
                SelectedChat = fallbackTree;
            }
        }

        /// <summary>
        /// 将 Python API 的 TreeNodeData 结构转换为本地 ChatTree 模型
        /// </summary>
        private static ChatTree ConvertToLocalTree(Models.TreeDetailResponse response)
        {
            var tree = new ChatTree();
            tree.TreeId = response.TreeId;
            tree.TreeTitle = response.Title;
            tree.SystemPrompt = response.SystemPrompt;

            if (response.RootNode != null)
            {
                var rootNode = new ChatTreeNode(null, response.RootNode.UserMessage?.Content ?? "", response.RootNode.NodeId);
                tree.SetRootNode(rootNode);
                ConvertChildren(rootNode, response.RootNode.Children);
            }

            return tree;
        }

        private static void ConvertChildren(ChatTreeNode parent, List<Models.TreeNodeData> children)
        {
            foreach (var childData in children)
            {
                var node = new ChatTreeNode(
                    parent,
                    childData.UserMessage?.Content ?? "",
                    childData.NodeId);

                if (childData.ReplyMessage != null)
                {
                    node.SetAiReply(childData.ReplyMessage.Content);
                }

                if (childData.Name != null)
                    node.Name = childData.Name;

                parent.ChildNodes.Add(node);
                ConvertChildren(node, childData.Children);
            }
        }

        /// <summary>
        /// 判断是否可以重命名对话（必须选中一个对话）
        /// </summary>
        private bool CanExecuteRenameChat(object? parameter)
        {
            return SelectedChat != null;
        }

        /// <summary>
        /// 执行重命名对话
        /// </summary>
        private void ExecuteRenameChat(object? parameter)
        {
            if (SelectedChat == null) return;

            var dialog = new Views.RenameDialog(SelectedChat.TreeTitle);
            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.NewName;
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    SelectedChat.TreeTitle = newName;
                    
                    // 刷新列表显示
                    int index = ChatList.IndexOf(SelectedChat);
                    if (index >= 0)
                    {
                        ChatList[index] = SelectedChat;
                    }
                }
            }
        }
    }
}
