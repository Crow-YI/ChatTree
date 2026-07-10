using System.Text.Json;
using System.Text.Json.Serialization;
using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// JSON 序列化服务，处理 ChatTree ↔ ChatTreeData 的映射与序列化。
    /// 使用 System.Text.Json，通过 ChatMessageDataConverter 支持旧格式兼容。
    /// </summary>
    public class JsonSerializationService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new ChatMessageDataConverter() },
        };

        /// <summary>
        /// 将对话树序列化为 JSON 字符串
        /// </summary>
        public string SerializeChatTree(ChatTree chatTree)
        {
            var data = MapToData(chatTree);
            return JsonSerializer.Serialize(data, _jsonOptions);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化对话树
        /// </summary>
        public ChatTree? DeserializeChatTree(string json)
        {
            try
            {
                var data = JsonSerializer.Deserialize<ChatTreeData>(json, _jsonOptions);
                if (data == null || data.RootNode == null)
                {
                    return null;
                }

                var chatTree = MapToDomain(data);

                // 反序列化后扫描所有节点，确保树内计数器不冲突
                int maxId = 0;
                foreach (var node in GetAllNodes(chatTree.RootNode))
                {
                    if (node.NodeID > maxId)
                        maxId = node.NodeID;
                }
                chatTree.ResetNextNodeId(maxId + 1);

                return chatTree;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ======================== 私有映射方法 ========================

        private static ChatTreeData MapToData(ChatTree chatTree)
        {
            return new ChatTreeData
            {
                TreeTitle = chatTree.TreeTitle,
                CreatedTime = chatTree.CreatedTime,
                SystemPrompt = chatTree.SystemPrompt,
                RootNode = MapNodeToData(chatTree.RootNode),
            };
        }

        private static ChatTreeNodeData MapNodeToData(ChatTreeNode node)
        {
            return new ChatTreeNodeData
            {
                NodeId = node.NodeID,
                Name = node.Name,
                UserMessage = string.IsNullOrEmpty(node.UserMessage)
                    ? null
                    : new ChatMessageData { Role = "user", Content = node.UserMessage },
                ReplyMessage = node.ReplyMessage != null
                    ? new ChatMessageData { Role = "assistant", Content = node.ReplyMessage }
                    : null,
                ChildNodes = node.ChildNodes.Select(MapNodeToData).ToList(),
            };
        }

        private static ChatTree MapToDomain(ChatTreeData data)
        {
            var chatTree = new ChatTree
            {
                CreatedTime = data.CreatedTime,
                TreeTitle = data.TreeTitle,
                SystemPrompt = data.SystemPrompt,
            };
            var rootNode = MapNodeToDomain(data.RootNode, null);
            chatTree.SetRootNode(rootNode);
            return chatTree;
        }

        private static ChatTreeNode MapNodeToDomain(ChatTreeNodeData data, ChatTreeNode? parent)
        {
            var node = new ChatTreeNode(parent, data.UserMessage?.Content ?? "", data.NodeId);

            if (data.Name != null)
                node.Name = data.Name;

            if (data.ReplyMessage?.Content != null)
                node.SetAiReply(data.ReplyMessage.Content);

            foreach (var child in data.ChildNodes)
            {
                node.ChildNodes.Add(MapNodeToDomain(child, node));
            }

            return node;
        }

        private static List<ChatTreeNode> GetAllNodes(ChatTreeNode root)
        {
            var result = new List<ChatTreeNode>();
            CollectNodes(root, result);
            return result;
        }

        private static void CollectNodes(ChatTreeNode node, List<ChatTreeNode> result)
        {
            result.Add(node);
            foreach (var child in node.ChildNodes)
            {
                CollectNodes(child, result);
            }
        }
    }

    /// <summary>
    /// 向后兼容的 ChatMessageData JSON 转换器。
    /// 支持两种格式：
    ///   旧格式："消息内容" (string)
    ///   新格式：{"Role": "user", "Content": "消息内容"} (object)
    /// </summary>
    public class ChatMessageDataConverter : JsonConverter<ChatMessageData>
    {
        public override ChatMessageData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                // 旧格式：纯字符串 → 包装为 ChatMessageData
                return new ChatMessageData { Role = "user", Content = reader.GetString() ?? "" };
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                // 新格式：{Role, Content} 对象（手动解析，避免递归调用 JsonSerializer.Deserialize）
                string? role = null;
                string? content = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propName = reader.GetString()!;
                        reader.Read();
                        switch (propName)
                        {
                            case "Role":
                                role = reader.GetString();
                                break;
                            case "Content":
                                content = reader.GetString();
                                break;
                            default:
                                reader.TrySkip();
                                break;
                        }
                    }
                }

                return new ChatMessageData
                {
                    Role = role ?? "user",
                    Content = content ?? "",
                };
            }

            if (reader.TokenType == JsonTokenType.Null)
                return null;

            throw new JsonException($"Unexpected token {reader.TokenType} for ChatMessageData");
        }

        public override void Write(Utf8JsonWriter writer, ChatMessageData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Role", value.Role);
            writer.WriteString("Content", value.Content);
            writer.WriteEndObject();
        }
    }
}
