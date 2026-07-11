using System.IO;
using System.Windows;
using Microsoft.Win32;
using TreeChat.Infrastructure;
using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// 文件服务实现类，处理对话树的文件保存和读取操作。
    /// 支持静默保存（FilePath 已设置时）和 Save As 对话框两种模式。
    /// </summary>
    public class FileService
    {
        private readonly JsonSerializationService _serializationService;
        private const string FileExtension = ".chat";
        private const string FileFilter = "聊天文件 (*.chat)|*.chat|所有文件 (*.*)|*.*";

        public FileService()
        {
            _serializationService = new JsonSerializationService();
        }

        // ==================== 保存（同步） ====================

        /// <summary>
        /// 保存到 chatTree.FilePath。如果 FilePath 为 null，弹出 Save As 对话框。
        /// 保存成功后设置 chatTree.IsModified = false。
        /// </summary>
        public bool SaveChatTree(ChatTree chatTree)
        {
            if (chatTree.FilePath == null)
                return SaveChatTreeAs(chatTree);  // 首次保存 → 弹出对话框

            return SaveToPath(chatTree, chatTree.FilePath);
        }

        /// <summary>
        /// 强制弹出 Save As 对话框。更新 chatTree.FilePath 和 chatTree.IsModified。
        /// </summary>
        public bool SaveChatTreeAs(ChatTree chatTree)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = FileFilter,
                    DefaultExt = FileExtension,
                    FileName = chatTree.TreeTitle,
                    Title = "保存对话"
                };

                if (dialog.ShowDialog() != true)
                {
                    AppLogger.Debug("Save cancelled by user");
                    return false;
                }

                return SaveToPath(chatTree, dialog.FileName);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Save As failed");
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 内部：写入指定路径（同步）
        /// </summary>
        private bool SaveToPath(ChatTree chatTree, string filePath)
        {
            try
            {
                chatTree.FilePath = filePath;
                var json = _serializationService.SerializeChatTree(chatTree);
                File.WriteAllText(filePath, json);
                chatTree.IsModified = false;
                AppLogger.Info("Chat saved: {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Save failed: {Path}", filePath);
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ==================== 保存（异步） ====================

        /// <summary>
        /// 异步保存，不阻塞 UI 线程。
        /// FilePath 为 null 时弹出 Save As 对话框（对话框必须在 UI 线程）。
        /// </summary>
        public async Task<bool> SaveChatTreeAsync(ChatTree chatTree)
        {
            if (chatTree.FilePath == null)
            {
                // 首次保存 → 同步弹出对话框（必须在 UI 线程）
                return SaveChatTreeAs(chatTree);
            }

            return await SaveToPathAsync(chatTree, chatTree.FilePath);
        }

        /// <summary>
        /// 异步强制弹出 Save As 对话框。
        /// </summary>
        public async Task<bool> SaveChatTreeAsAsync(ChatTree chatTree)
        {
            // 对话框必须在 UI 线程
            if (!SaveChatTreeAs(chatTree))
                return false;

            // 对话框已设置 chatTree.FilePath，异步写入
            return await SaveToPathAsync(chatTree, chatTree.FilePath!);
        }

        /// <summary>
        /// 内部：异步写入指定路径
        /// </summary>
        private async Task<bool> SaveToPathAsync(ChatTree chatTree, string filePath)
        {
            try
            {
                chatTree.FilePath = filePath;
                var json = _serializationService.SerializeChatTree(chatTree);
                await File.WriteAllTextAsync(filePath, json);
                chatTree.IsModified = false;
                AppLogger.Info("Chat saved (async): {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Save failed (async): {Path}", filePath);
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ==================== 加载 ====================

        /// <summary>
        /// 打开对话框加载 .chat 文件。设置 chatTree.FilePath 为所选路径。
        /// </summary>
        public ChatTree? LoadChatTree()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = FileFilter,
                    DefaultExt = FileExtension,
                    Title = "打开对话"
                };

                if (dialog.ShowDialog() != true)
                {
                    AppLogger.Debug("Open cancelled by user");
                    return null;
                }

                return LoadChatTree(dialog.FileName);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Open dialog failed");
                MessageBox.Show($"读取失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// 从指定文件路径加载对话树。
        /// 设置 chatTree.FilePath 和 chatTree.IsModified = false。
        /// </summary>
        public ChatTree? LoadChatTree(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var chatTree = _serializationService.DeserializeChatTree(json);

                if (chatTree != null)
                {
                    chatTree.FilePath = filePath;
                    chatTree.IsModified = false;

                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    chatTree.TreeTitle = fileName;
                }

                AppLogger.Info("Chat loaded: {Path}", filePath);
                return chatTree;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Load failed: {Path}", filePath);
                MessageBox.Show($"读取失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
