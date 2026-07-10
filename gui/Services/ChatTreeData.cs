namespace TreeChat.Services;

/// <summary>
/// 聊天树数据传输对象，用于 JSON 序列化和文件存储。
/// 纯数据容器，不含映射逻辑（映射由 JsonSerializationService 统一处理）。
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
    /// 系统提示词
    /// </summary>
    public string SystemPrompt { get; set; } = "你是一个有帮助的AI助手。";

    /// <summary>
    /// 根节点
    /// </summary>
    public ChatTreeNodeData RootNode { get; set; } = new();
}
