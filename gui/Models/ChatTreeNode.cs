namespace TreeChat.Models
{
    /// <summary>
    /// 聊天树节点，包含用户消息、AI回复、附件文件名、父节点和子节点等信息
    /// </summary>
    public class ChatTreeNode
    {
        public ChatTreeNode? ParentNode { get; }
        public List<ChatTreeNode> ChildNodes { get; } = new List<ChatTreeNode>();
        public string UserMessage { get; }
        public string? ReplyMessage { get; private set; }
        public int NodeID { get; }
        public string? Name { get; set; }
        public List<string> AttachmentFileNames { get; set; } = new();

        /// <summary>
        /// 用于创建节点的构造函数，需传入明确 NodeID（不再使用全局 static 计数器）
        /// </summary>
        public ChatTreeNode(ChatTreeNode? parentNode, string userMessage, int nodeId,
                            List<string>? attachmentFileNames = null)
        {
            ParentNode = parentNode;
            UserMessage = userMessage;
            NodeID = nodeId;
            if (attachmentFileNames != null)
                AttachmentFileNames = attachmentFileNames;
        }

        /// <summary>
        /// 此构造函数已废弃——调用方必须提供明确的 NodeID。
        /// 请使用三参构造函数 ChatTreeNode(parentNode, userMessage, nodeId)。
        /// 该构造函数保留仅为编译兼容，调用将抛出异常。
        /// </summary>
        [Obsolete("请使用 ChatTreeNode(parentNode, userMessage, nodeId) 并传入明确的 NodeID", true)]
        public ChatTreeNode(ChatTreeNode? parentNode, string userMessage) : this(parentNode, userMessage, 0)
        {
            throw new InvalidOperationException(
                "不再支持无 NodeID 的构造函数。请通过 ChatTree.GetNextNodeId() 获取 ID。");
        }

        /// <summary>
        /// 添加一个新的子节点，包含用户消息，并返回新创建的子节点
        /// </summary>
        public ChatTreeNode AddChildNode(string userMessage, int nodeId)
        {
            var childNode = new ChatTreeNode(this, userMessage, nodeId);
            ChildNodes.Add(childNode);
            return childNode;
        }

        /// <summary>
        /// 设置AI回复消息
        /// </summary>
        public void SetAiReply(string replyMessage)
        {
            ReplyMessage = replyMessage;
        }
    }
}
