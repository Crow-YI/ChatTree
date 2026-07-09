namespace TreeChat.Models
{
    /// <summary>
    /// 聊天树节点，包含用户消息、AI回复、父节点和子节点等信息
    /// </summary>
    public class ChatTreeNode
    {
        public ChatTreeNode? ParentNode { get; }
        public List<ChatTreeNode> ChildNodes { get; } = new List<ChatTreeNode>();
        public string UserMessage { get; }
        public string? ReplyMessage { get; private set; }
        public int NodeID { get; }
        public string? Name { get; set; }

        private static int _nextNodeID = 1;

        /// <summary>
        /// 用于创建新节点的构造函数，自动分配 NodeID
        /// </summary>
        public ChatTreeNode(ChatTreeNode? parentNode, string userMessage)
        {
            ParentNode = parentNode;
            UserMessage = userMessage;
            NodeID = _nextNodeID++;
        }

        /// <summary>
        /// 用于从文件加载节点的构造函数，使用指定的 NodeID（不递增 _nextNodeID）
        /// </summary>
        public ChatTreeNode(ChatTreeNode? parentNode, string userMessage, int nodeId)
        {
            ParentNode = parentNode;
            UserMessage = userMessage;
            NodeID = nodeId;
        }

        /// <summary>
        /// 重置 _nextNodeID 到指定值（用于从文件加载后）
        /// </summary>
        public static void ResetNextNodeId(int value)
        {
            _nextNodeID = value;
        }

        /// <summary>
        /// 获取当前的 nextNodeID
        /// </summary>
        public static int GetCurrentNextNodeId()
        {
            return _nextNodeID;
        }

        /// <summary>
        /// 添加一个新的子节点，包含用户消息，并返回新创建的子节点
        /// </summary>
        public ChatTreeNode AddChildNode(string userMessage)
        {
            var childNode = new ChatTreeNode(this, userMessage);
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
