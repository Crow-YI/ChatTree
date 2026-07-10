using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 文件管理视图的ViewModel，负责创建新文件和打开现有文件
    /// </summary>
    public class FileViewVM : BaseViewModel
    {
        private readonly IFileService _fileService;

        /// <summary>
        /// 最近打开的文件列表（占位，目前未实现持久化）
        /// </summary>
        public ObservableCollection<string> RecentFiles { get; } = new();

        /// <summary>
        /// 创建新文件命令
        /// </summary>
        public RelayCommand CreateNewFile { get; }

        /// <summary>
        /// 打开现有文件命令
        /// </summary>
        public RelayCommand OpenExistingFile { get; }

        /// <summary>
        /// 文件创建或打开后触发，通知 MainWindowVM 加载树
        /// </summary>
        public event Action<ChatTree>? FileCreatedOrOpened;

        public FileViewVM()
        {
            _fileService = new FileService();

            CreateNewFile = new RelayCommand(ExecuteCreateNewFile);
            OpenExistingFile = new RelayCommand(ExecuteOpenExistingFile);
        }

        /// <summary>
        /// 执行创建新文件
        /// </summary>
        private void ExecuteCreateNewFile(object? parameter)
        {
            var dialog = new Views.CreateChatDialog();
            if (dialog.ShowDialog() == true)
            {
                string treeTitle = string.IsNullOrWhiteSpace(dialog.TreeTitle)
                    ? "新对话"
                    : dialog.TreeTitle;

                string systemPrompt = dialog.SystemPrompt;

                ChatTree newTree = new ChatTree(systemPrompt);
                newTree.TreeTitle = treeTitle;

                FileCreatedOrOpened?.Invoke(newTree);
            }
        }

        /// <summary>
        /// 执行打开现有文件
        /// </summary>
        private async void ExecuteOpenExistingFile(object? parameter)
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
                    await LoadFromPathInternal(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从指定文件路径加载对话（供拖放和启动参数使用）
        /// </summary>
        public async void LoadFromPath(string filePath)
        {
            try
            {
                await LoadFromPathInternal(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 内部加载逻辑：优先使用 Python 后端反序列化，失败时回退到本地
        /// </summary>
        private async Task LoadFromPathInternal(string filePath)
        {
            var json = File.ReadAllText(filePath);
            string title = Path.GetFileNameWithoutExtension(filePath);

            try
            {
                var treeResponse = await App.Backend.DeserializeTreeAsync(json, title);
                var loadedTree = ConvertToLocalTree(treeResponse);
                loadedTree.TreeTitle = title;
                FileCreatedOrOpened?.Invoke(loadedTree);
                return;
            }
            catch
            {
                // Python 不可用时回退到本地反序列化
            }

            // 回退：使用本地加载
            var fallbackTree = _fileService.LoadChatTree(filePath);
            if (fallbackTree != null)
            {
                FileCreatedOrOpened?.Invoke(fallbackTree);
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
    }
}
