using System;

namespace TreeChat.Models
{
    /// <summary>
    /// 聊天树数据传输对象，用于JSON序列化和文件存储
    /// </summary>
    public class ChatTreeData
    {
        /// <summary>
        /// 文件格式版本号，用于未来兼容性
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 对话树标题
        /// </summary>
        public string TreeTitle { get; set; } = "新对话";

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 根节点（包含系统提示）
        /// </summary>
        public ChatTreeNodeData RootNode { get; set; } = new ChatTreeNodeData();

        /// <summary>
        /// 无参构造函数，用于JSON反序列化
        /// </summary>
        public ChatTreeData() { }

        /// <summary>
        /// 从ChatTree对象创建数据传输对象
        /// </summary>
        /// <param name="chatTree">原始对话树对象</param>
        public ChatTreeData(ChatTree chatTree)
        {
            TreeTitle = chatTree.TreeTitle;
            RootNode = new ChatTreeNodeData(chatTree.RootNode);
        }

        /// <summary>
        /// 转换为ChatTree对象（重新编号）
        /// </summary>
        /// <param name="nextNodeId">下一个可用的NodeID</param>
        /// <returns>ChatTree对象</returns>
        public ChatTree ToChatTree(ref int nextNodeId)
        {
            var chatTree = new ChatTree();
            chatTree.TreeTitle = TreeTitle;
            
            var rootNode = RootNode.ToChatTreeNode(null, ref nextNodeId);
            chatTree.SetRootNode(rootNode);
            
            return chatTree;
        }

        /// <summary>
        /// 转换为ChatTree对象
        /// </summary>
        /// <returns>ChatTree对象</returns>
        public ChatTree ToChatTree()
        {
            var chatTree = new ChatTree();
            chatTree.TreeTitle = TreeTitle;
            
            var rootNode = RootNode.ToChatTreeNode(null);
            chatTree.SetRootNode(rootNode);
            
            return chatTree;
        }
    }
}
