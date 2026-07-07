using System.Collections.ObjectModel;
using TreeChat.Models;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 节点VM，包含节点数据和绘图属性
    /// </summary>
    public class TreeNodeVM : BaseViewModel
    {
        //绘图属性
        public const double WIDTH = 40;
        public const double HEIGHT = 30;

        public double X {  get; set; }
        public double Y { get; set; }

        public List<double> SubtreeWidth { get; set; } = new List<double>();

        public string DisplayContent => Node.Name ?? Node.NodeID.ToString();

        public ChatTreeNode Node { get; }

        public int ID => Node.NodeID;

        public TreeNodeVM? ParentNode { get; }

        private readonly ObservableCollection<TreeNodeVM> _children;

        public ReadOnlyObservableCollection<TreeNodeVM> Children { get; }

        public TreeNodeVM(ChatTreeNode node, TreeNodeVM? parentNode)
        {
            Node = node;
            _children = new ObservableCollection<TreeNodeVM>();
            Children = new ReadOnlyObservableCollection<TreeNodeVM>(_children);

            foreach (var child in Node.ChildNodes)
            {
                _children.Add(new TreeNodeVM(child, this));
            }

            ParentNode = parentNode;
        }

        /// <summary>
        /// 添加子节点，并返回对应的子节点VM
        /// </summary>
        /// <param name="childNode"></param>
        /// <returns></returns>
        public TreeNodeVM AddChild(ChatTreeNode childNode)
        {
            Node.ChildNodes.Add(childNode);
            var childViewModel = new TreeNodeVM(childNode, this);
            _children.Add(childViewModel);
            return childViewModel;
        }

        /// <summary>
        /// 移除子节点
        /// </summary>
        /// <param name="childNode"></param>
        public void RemoveChild(TreeNodeVM childNode)
        {
            Node.ChildNodes.Remove(childNode.Node);
            _children.Remove(childNode);
        }

    }
}