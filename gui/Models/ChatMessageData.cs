namespace TreeChat.Models
{
    /// <summary>
    /// 聊天消息数据传输对象，用于JSON序列化
    /// </summary>
    public class ChatMessageData
    {
        /// <summary>
        /// 消息角色
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 无参构造函数，用于JSON反序列化
        /// </summary>
        public ChatMessageData() { }

        /// <summary>
        /// 从ChatMessage对象创建数据传输对象
        /// </summary>
        /// <param name="message">原始消息对象</param>
        public ChatMessageData(ChatMessage message)
        {
            Role = message.Role;
            Content = message.Content;
        }

        /// <summary>
        /// 转换为ChatMessage对象
        /// </summary>
        /// <returns>ChatMessage对象</returns>
        public ChatMessage ToChatMessage()
        {
            return new ChatMessage(Role, Content);
        }
    }
}
