namespace TreeChat.Services
{
    /// <summary>
    /// OpenAI兼容的Chat Completions请求体
    /// </summary>
    public class ChatCompletionRequest
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public required string model { get; set; }

        /// <summary>
        /// 对话消息列表
        /// </summary>
        public List<OpenAIMessage> messages { get; set; } = new();

        /// <summary>
        /// 随机性（0-1/0-2，依模型而定）
        /// </summary>
        public double temperature { get; set; }

        /// <summary>
        /// 核采样（0-1）
        /// </summary>
        public double top_p { get; set; }

        /// <summary>
        /// 候选词数量
        /// </summary>
        public int top_k { get; set; }
    }

    /// <summary>
    /// 单条消息的强类型（对应messages数组元素）
    /// </summary>
    public class OpenAIMessage
    {
        public required string role { get; set; }
        public string? content { get; set; }
    }
}
