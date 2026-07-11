using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Infrastructure;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 文件管理视图的ViewModel，负责创建新文件和打开现有文件
    /// </summary>
    public class FileViewVM : BaseViewModel
    {
        private readonly FileService _fileService;

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

        public FileViewVM(FileService fileService)
        {
            _fileService = fileService;

            CreateNewFile = new RelayCommand(ExecuteCreateNewFile);
            OpenExistingFile = new RelayCommand(ExecuteOpenExistingFile);
        }

        /// <summary>
        /// 执行创建新文件：弹出创建对话框 → 保存到文件 → 触发事件
        /// </summary>
        private void ExecuteCreateNewFile(object? parameter)
        {
            var dialog = new Views.CreateChatDialog();
            if (dialog.ShowDialog() == true)
            {
                string treeTitle = string.IsNullOrWhiteSpace(dialog.TreeTitle)
                    ? "未命名对话"
                    : dialog.TreeTitle;

                string systemPrompt = dialog.SystemPrompt;

                AppLogger.Info("Creating new chat: title={Title}", treeTitle);

                ChatTree newTree = new ChatTree(systemPrompt);
                newTree.TreeTitle = treeTitle;

                // 立即保存到文件（弹出 Save As 对话框）
                if (!_fileService.SaveChatTreeAs(newTree))
                    return;  // 用户取消 → 不创建

                // 尝试注册到后端
                TryRegisterBackend(newTree).ConfigureAwait(false);

                FileCreatedOrOpened?.Invoke(newTree);
            }
        }

        /// <summary>
        /// 执行打开现有文件：由 IFileService 处理对话框和路径
        /// </summary>
        private async void ExecuteOpenExistingFile(object? parameter)
        {
            try
            {
                var chatTree = _fileService.LoadChatTree();
                if (chatTree == null)
                    return;

                AppLogger.Info("Opening existing chat: {Path}", chatTree.FilePath);

                // 尝试注册到后端
                await TryRegisterBackend(chatTree);

                FileCreatedOrOpened?.Invoke(chatTree);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Open existing file failed");
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
                AppLogger.Info("Loading chat from path: {Path}", filePath);
                var chatTree = _fileService.LoadChatTree(filePath);
                if (chatTree == null)
                    return;

                await TryRegisterBackend(chatTree);

                FileCreatedOrOpened?.Invoke(chatTree);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Load from path failed: {Path}", filePath);
                MessageBox.Show($"加载失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 尝试将树注册到 Python 后端，使后端拥有该树的数据。
        /// 注册成功后会设置 chatTree.TreeId，后续 Chat 直接使用该树。
        /// </summary>
        private static async Task TryRegisterBackend(ChatTree chatTree)
        {
            try
            {
                var json = File.ReadAllText(chatTree.FilePath!);
                var response = await App.Backend.DeserializeTreeAsync(json, chatTree.TreeTitle);
                if (!string.IsNullOrEmpty(response?.TreeId))
                {
                    chatTree.TreeId = response.TreeId;
                    AppLogger.Info("Chat registered to backend: TreeId={TreeId}", response.TreeId);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Backend registration failed (will retry on first Chat): {Message}", ex.Message);
            }
        }
    }
}
