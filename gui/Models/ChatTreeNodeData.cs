namespace TreeChat.Models
{
    /// <summary>
    /// 聊天树节点数据传输对象，用于JSON序列化
    /// </summary>
    public class ChatTreeNodeData
    {
        /// <summary>
        /// 节点唯一标识
        /// </summary>
        public int NodeId { get; set; }

        /// <summary>
        /// 用户消息
        /// </summary>
        public ChatMessageData UserMessage { get; set; } = new ChatMessageData();

        /// <summary>
        /// AI回复消息（可能为null）
        /// </summary>
        public ChatMessageData? ReplyMessage { get; set; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<ChatTreeNodeData> ChildNodes { get; set; } = new List<ChatTreeNodeData>();

        /// <summary>
        /// 无参构造函数，用于JSON反序列化
        /// </summary>
        public ChatTreeNodeData() { }

        /// <summary>
        /// 从ChatTreeNode对象创建数据传输对象
        /// </summary>
        /// <param name="node">原始节点对象</param>
        public ChatTreeNodeData(ChatTreeNode node)
        {
            NodeId = node.NodeID;
            UserMessage = new ChatMessageData(node.UserMessage);
            
            if (node.ReplyMessage != null)
            {
                ReplyMessage = new ChatMessageData(node.ReplyMessage);
            }

            foreach (var child in node.ChildNodes)
            {
                ChildNodes.Add(new ChatTreeNodeData(child));
            }
        }

        /// <summary>
        /// 转换为ChatTreeNode对象（使用指定的起始NodeID）
        /// </summary>
        /// <param name="parent">父节点（根节点为null）</param>
        /// <param name="nextNodeId">下一个可用的NodeID</param>
        /// <returns>ChatTreeNode对象</returns>
        public ChatTreeNode ToChatTreeNode(ChatTreeNode? parent, ref int nextNodeId)
        {
            // 使用连续的编号，忽略保存时的NodeId
            var node = new ChatTreeNode(parent, UserMessage.ToChatMessage(), nextNodeId++);
            
            if (ReplyMessage != null)
            {
                node.SetAiReply(ReplyMessage.ToChatMessage());
            }

            foreach (var childData in ChildNodes)
            {
                var childNode = childData.ToChatTreeNode(node, ref nextNodeId);
                node.ChildNodes.Add(childNode);
            }

            return node;
        }

        /// <summary>
        /// 转换为ChatTreeNode对象（使用默认的_nextNodeID）
        /// </summary>
        /// <param name="parent">父节点（根节点为null）</param>
        /// <returns>ChatTreeNode对象</returns>
        public ChatTreeNode ToChatTreeNode(ChatTreeNode? parent = null)
        {
            var node = new ChatTreeNode(parent, UserMessage.ToChatMessage());
            
            if (ReplyMessage != null)
            {
                node.SetAiReply(ReplyMessage.ToChatMessage());
            }

            foreach (var childData in ChildNodes)
            {
                var childNode = childData.ToChatTreeNode(node);
                node.ChildNodes.Add(childNode);
            }

            return node;
        }
    }
}
