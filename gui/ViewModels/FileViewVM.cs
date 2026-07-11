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
    /// 文件管理视图的ViewModel，负责创建新文件、打开现有文件和最近文件历史
    /// </summary>
    public class FileViewVM : BaseViewModel
    {
        private readonly FileService _fileService;

        /// <summary>
        /// 最近打开的文件列表
        /// </summary>
        public ObservableCollection<RecentFileItem> RecentFiles { get; } = new();

        /// <summary>
        /// 创建新文件命令
        /// </summary>
        public RelayCommand CreateNewFile { get; }

        /// <summary>
        /// 打开现有文件命令
        /// </summary>
        public RelayCommand OpenExistingFile { get; }

        /// <summary>
        /// 打开最近文件命令
        /// </summary>
        public RelayCommand OpenRecentFileCommand { get; }

        /// <summary>
        /// 文件创建或打开后触发，通知 MainWindowVM 加载树
        /// </summary>
        public event Action<ChatTree>? FileCreatedOrOpened;

        public FileViewVM(FileService fileService)
        {
            _fileService = fileService;

            CreateNewFile = new RelayCommand(ExecuteCreateNewFile);
            OpenExistingFile = new RelayCommand(ExecuteOpenExistingFile);
            OpenRecentFileCommand = new RelayCommand(param =>
            {
                if (param is RecentFileItem item)
                    ExecuteOpenRecentFile(item);
            });

            // 加载最近文件历史
            ReloadRecentFiles();
        }

        /// <summary>
        /// 从持久化存储加载最近文件列表。
        /// </summary>
        private void ReloadRecentFiles()
        {
            RecentFiles.Clear();
            foreach (var item in RecentFilesManager.GetAll())
            {
                RecentFiles.Add(item);
            }
        }

        /// <summary>
        /// 将文件添加到最近文件列表（去重、移到顶部）。
        /// </summary>
        private void AddRecentFile(ChatTree tree)
        {
            if (tree.FilePath == null) return;

            var title = string.IsNullOrWhiteSpace(tree.TreeTitle)
                ? Path.GetFileNameWithoutExtension(tree.FilePath)
                : tree.TreeTitle;

            RecentFilesManager.AddFile(tree.FilePath, title);

            // 刷新 UI 集合（Insert 可能改变顺序）
            // 直接把新项插入到最前面，若已存在则先移除旧项
            var existing = RecentFiles.FirstOrDefault(f =>
                string.Equals(f.Path, tree.FilePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                RecentFiles.Remove(existing);
                existing.Title = title;
                RecentFiles.Insert(0, existing);
            }
            else
            {
                RecentFiles.Insert(0, new RecentFileItem
                {
                    Path = tree.FilePath,
                    Title = title,
                });
            }

            // 超出上限时移除末尾
            while (RecentFiles.Count > 10)
                RecentFiles.RemoveAt(RecentFiles.Count - 1);
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

                AddRecentFile(newTree);
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

                AddRecentFile(chatTree);
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
        /// 执行打开最近文件列表中的文件。
        /// 若文件已不存在，提示并自动从列表中移除。
        /// </summary>
        private async void ExecuteOpenRecentFile(RecentFileItem item)
        {
            if (item == null) return;

            try
            {
                // 检查文件是否存在
                if (!File.Exists(item.Path))
                {
                    var result = MessageBox.Show(
                        $"文件不存在或已被移动：\n{item.Path}\n\n是否从最近文件中移除？",
                        "文件不存在",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        RecentFilesManager.RemoveFile(item.Path);
                        RecentFiles.Remove(item);
                        AppLogger.Info("Removed missing file from recent files: {Path}", item.Path);
                    }
                    return;
                }

                AppLogger.Info("Opening recent file: {Path}", item.Path);

                var chatTree = _fileService.LoadChatTree(item.Path);
                if (chatTree == null)
                    return;

                await TryRegisterBackend(chatTree);

                AddRecentFile(chatTree);
                FileCreatedOrOpened?.Invoke(chatTree);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Open recent file failed: {Path}", item.Path);
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

                AddRecentFile(chatTree);
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
