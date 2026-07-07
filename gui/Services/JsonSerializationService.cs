using Newtonsoft.Json;
using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// JSON序列化服务，提供对话树的序列化和反序列化功能
    /// </summary>
    public class JsonSerializationService
    {
        /// <summary>
        /// 将对话树序列化为JSON字符串
        /// </summary>
        /// <param name="chatTree">要序列化的对话树</param>
        /// <returns>JSON字符串</returns>
        public string SerializeChatTree(ChatTree chatTree)
        {
            var data = new ChatTreeData(chatTree);
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            return JsonConvert.SerializeObject(data, settings);
        }

        /// <summary>
        /// 从JSON字符串反序列化对话树
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>对话树对象，失败返回null</returns>
        public ChatTree? DeserializeChatTree(string json)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                var data = JsonConvert.DeserializeObject<ChatTreeData>(json, settings);
                
                if (data == null || data.RootNode == null)
                {
                    return null;
                }

                // 获取当前的 nextNodeId
                int originalNextId = ChatTreeNode.GetCurrentNextNodeId();
                
                // 从当前的 nextNodeId 开始重新编号
                int nextNodeId = originalNextId;
                var chatTree = data.ToChatTree(ref nextNodeId);
                
                // 更新 nextNodeId 为最后使用的 ID + 1
                ChatTreeNode.ResetNextNodeId(nextNodeId);
                
                return chatTree;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
