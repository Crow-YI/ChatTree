using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// 文件服务接口，提供对话树的保存和读取功能
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// 保存对话树到用户指定的文件
        /// </summary>
        /// <param name="chatTree">要保存的对话树</param>
        /// <returns>是否保存成功</returns>
        bool SaveChatTree(ChatTree chatTree);

        /// <summary>
        /// 从用户选择的文件加载对话树
        /// </summary>
        /// <returns>加载的对话树，失败返回null</returns>
        ChatTree? LoadChatTree();

        /// <summary>
        /// 从指定文件路径加载对话树
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的对话树，失败返回null</returns>
        ChatTree? LoadChatTree(string filePath);
    }
}
