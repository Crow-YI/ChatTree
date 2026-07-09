using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeChat.Models
{
    /// <summary>
    /// 聊天树结构，包含系统提示、根节点和当前节点等信息
    /// </summary>
    public class ChatTree : INotifyPropertyChanged
    {
        private string _treeTitle = "新对话";

        /// <summary>
        /// Python 后端中的树 ID（首次发消息时自动创建）
        /// </summary>
        public string? TreeId { get; set; }

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 设置属性并触发通知
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public ChatTreeNode RootNode { get; private set; }
        public ChatTreeNode CurrentNode { get; private set; }

        /// <summary>
        /// 对话树标题
        /// </summary>
        public string TreeTitle
        {
            get => _treeTitle;
            set => SetProperty(ref _treeTitle, value);
        }

        /// <summary>
        /// 系统提示词（存储在树层级，不在根节点中）
        /// </summary>
        public string SystemPrompt { get; set; } = "你是一个有帮助的AI助手。";

        // 节点 ID 计数器：根节点固定为 1，子节点从 2 开始计数
        private int _nextNodeID = 2;

        /// <summary>
        /// 获取下一个节点 ID 并递增计数器
        /// </summary>
        public int GetNextNodeId()
        {
            return _nextNodeID++;
        }

        /// <summary>
        /// 重设计数器（用于反序列化后扫描所有节点避免冲突）
        /// </summary>
        public void ResetNextNodeId(int value)
        {
            _nextNodeID = value;
        }

        /// <summary>
        /// 获取当前计数器值（用于序列化时保存状态）
        /// </summary>
        public int NextNodeID => _nextNodeID;

        /// <summary>
        /// 无参构造函数，用于JSON反序列化
        /// </summary>
        public ChatTree()
        {
            RootNode = new ChatTreeNode(null, "", 1);
            CurrentNode = RootNode;
        }

        /// <summary>
        /// 带系统提示的构造函数
        /// </summary>
        /// <param name="systemPrompt">系统提示词，若为空则使用默认提示</param>
        public ChatTree(string? systemPrompt = null)
        {
            var finalSystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? "你是一个有帮助的AI助手。"
                : systemPrompt;
            RootNode = new ChatTreeNode(null, "", 1);
            CurrentNode = RootNode;
            SystemPrompt = finalSystemPrompt;
        }

        /// <summary>
        /// 设置根节点（用于从文件加载）
        /// </summary>
        public void SetRootNode(ChatTreeNode rootNode)
        {
            RootNode = rootNode;
            CurrentNode = rootNode;
        }

        private ChatTreeNode? FindNodeById(ChatTreeNode startNode, int nodeID)
        {
            if (startNode.NodeID == nodeID) return startNode;
            foreach (var child in startNode.ChildNodes)
            {
                var found = FindNodeById(child, nodeID);
                if (found != null) return found;
            }
            return null;
        }
    }
}
