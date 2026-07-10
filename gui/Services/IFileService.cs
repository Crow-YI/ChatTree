using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// 文件服务接口，提供对话树的保存和读取功能
    /// </summary>
    public interface IFileService
    {
        // ========== 保存（同步） ==========

        /// <summary>
        /// 保存到 chatTree.FilePath。如果 FilePath 为 null，弹出 Save As 对话框并设置。
        /// 保存成功后设置 chatTree.IsModified = false。
        /// </summary>
        bool SaveChatTree(ChatTree chatTree);

        /// <summary>
        /// 强制弹出 Save As 对话框。更新 chatTree.FilePath 和 chatTree.IsModified。
        /// </summary>
        bool SaveChatTreeAs(ChatTree chatTree);

        // ========== 保存（异步） ==========

        /// <summary>
        /// 异步保存到 chatTree.FilePath，不阻塞 UI 线程。
        /// FilePath 为 null 时弹出 Save As 对话框。
        /// </summary>
        Task<bool> SaveChatTreeAsync(ChatTree chatTree);

        /// <summary>
        /// 异步强制弹出 Save As 对话框。
        /// </summary>
        Task<bool> SaveChatTreeAsAsync(ChatTree chatTree);

        // ========== 加载（同步，含模态对话框） ==========

        /// <summary>
        /// 打开对话框加载 .chat 文件。设置 chatTree.FilePath 为所选路径。
        /// </summary>
        ChatTree? LoadChatTree();

        /// <summary>
        /// 从指定路径加载 .chat 文件。设置 chatTree.FilePath 为该路径。
        /// </summary>
        ChatTree? LoadChatTree(string filePath);
    }
}
