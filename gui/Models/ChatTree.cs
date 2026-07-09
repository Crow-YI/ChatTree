using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeChat.Models
{
    /// <summary>
    /// 聊天树结构，包含根节点和当前节点等信息
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
        /// <param name="propertyName">属性名称</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 设置属性并触发通知
        /// </summary>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>是否发生了改变</returns>
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
        /// 无参构造函数，用于JSON反序列化
        /// </summary>
        public ChatTree()
        {
            RootNode = new ChatTreeNode(null, "你是一个有帮助的AI助手。");
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
            RootNode = new ChatTreeNode(null, finalSystemPrompt);
            CurrentNode = RootNode;
        }

        /// <summary>
        /// 设置根节点（用于从文件加载）
        /// </summary>
        /// <param name="rootNode">新的根节点</param>
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
