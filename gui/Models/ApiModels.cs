using System.Text.Json.Serialization;

namespace TreeChat.Models
{
    /// <summary>
    /// API 请求/响应的 C# 序列化模型（与 Python Pydantic schemas 对应）
    /// </summary>

    // === Tree ===

    public class CreateTreeRequest
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "新对话";

        [JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = "你是一个有帮助的AI助手。";
    }

    public class RenameTreeRequest
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
    }

    public class TreeSummary
    {
        [JsonPropertyName("tree_id")]
        public string TreeId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("node_count")]
        public int NodeCount { get; set; }
    }

    public class TreeListResponse
    {
        [JsonPropertyName("trees")]
        public List<TreeSummary> Trees { get; set; } = new();
    }

    // === Node (serialized for tree rendering) ===

    public class NodeMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class TreeNodeData
    {
        [JsonPropertyName("node_id")]
        public int NodeId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("user_message")]
        public NodeMessage? UserMessage { get; set; }

        [JsonPropertyName("reply_message")]
        public NodeMessage? ReplyMessage { get; set; }

        [JsonPropertyName("children")]
        public List<TreeNodeData> Children { get; set; } = new();
    }

    public class TreeDetailResponse
    {
        [JsonPropertyName("tree_id")]
        public string TreeId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [JsonPropertyName("root_node")]
        public TreeNodeData? RootNode { get; set; }
    }

    // === Chat ===

    public class ChatRequest
    {
        [JsonPropertyName("parent_node_id")]
        public int ParentNodeId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("profile_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProfileName { get; set; }

        [JsonPropertyName("model")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Model { get; set; }

        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Temperature { get; set; }

        [JsonPropertyName("top_p")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TopP { get; set; }

        [JsonPropertyName("max_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxTokens { get; set; }
    }

    // === SSE Events ===

    public class SseCreatedEvent
    {
        [JsonPropertyName("node_id")]
        public int NodeId { get; set; }

        [JsonPropertyName("user_message")]
        public NodeMessage? UserMessage { get; set; }
    }

    public class SseDeltaEvent
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class SseDoneEvent
    {
        [JsonPropertyName("node_id")]
        public int NodeId { get; set; }

        [JsonPropertyName("reply_message")]
        public NodeMessage? ReplyMessage { get; set; }
    }

    public class SseErrorEvent
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonPropertyName("status_code")]
        public int? StatusCode { get; set; }
    }

    // === Node Operations ===

    public class RenameNodeRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    // === Config ===

    public class ChatConfigData
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "deepseek-v4";

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;

        [JsonPropertyName("top_p")]
        public double TopP { get; set; } = 0.8;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 800;
    }

    // === Profile ===

    public class ProfileData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "deepseek";

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = "";

        [JsonPropertyName("api_endpoint")]
        public string ApiEndpoint { get; set; } = "https://api.deepseek.com";

        [JsonPropertyName("model")]
        public string Model { get; set; } = "deepseek-v4-flash";

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;

        [JsonPropertyName("top_p")]
        public double TopP { get; set; } = 0.8;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 800;
    }

    public class ProfileListResponse
    {
        [JsonPropertyName("profiles")]
        public List<ProfileData> Profiles { get; set; } = new();

        [JsonPropertyName("active_profile")]
        public string ActiveProfile { get; set; } = "";
    }

    public class ActivateProfileResponse
    {
        [JsonPropertyName("active_profile")]
        public string ActiveProfile { get; set; } = "";

        [JsonPropertyName("config")]
        public ChatConfigData? Config { get; set; }
    }

    // === Recent Files ===

    public class RecentFileItem
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
    }

    // === Generic ===

    public class ApiSuccessResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public class ApiErrorDetail
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonPropertyName("status_code")]
        public int? StatusCode { get; set; }
    }

    public class SerializeResponse
    {
        [JsonPropertyName("json_content")]
        public string JsonContent { get; set; } = string.Empty;
    }

    public class DeserializeRequest
    {
        [JsonPropertyName("json_content")]
        public string JsonContent { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Title { get; set; }
    }
}
