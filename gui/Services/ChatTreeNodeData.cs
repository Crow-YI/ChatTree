namespace TreeChat.Services;

/// <summary>
/// 聊天树节点数据传输对象，用于 JSON 序列化。
/// 纯数据容器，不含映射逻辑（映射由 JsonSerializationService 统一处理）。
/// </summary>
public class ChatTreeNodeData
{
    /// <summary>
    /// 节点唯一标识
    /// </summary>
    public int NodeId { get; set; }

    /// <summary>
    /// 节点名称（可选，用于显示）
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 用户消息（含角色和内容）
    /// null 表示根节点（无实际用户消息）
    /// </summary>
    public ChatMessageData? UserMessage { get; set; }

    /// <summary>
    /// AI 回复消息（含角色和内容）
    /// null 表示尚未回复
    /// </summary>
    public ChatMessageData? ReplyMessage { get; set; }

    /// <summary>
    /// 子节点列表
    /// </summary>
    public List<ChatTreeNodeData> ChildNodes { get; set; } = new();
}
