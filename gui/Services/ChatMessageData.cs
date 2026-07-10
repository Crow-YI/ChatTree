namespace TreeChat.Services;

/// <summary>
/// 消息数据传输对象，包含角色和内容。
/// 与 Python 端的 {"Role": "user", "Content": "..."} 格式对应。
/// </summary>
public class ChatMessageData
{
    /// <summary>
    /// 消息角色："user" / "assistant" / "system"
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
