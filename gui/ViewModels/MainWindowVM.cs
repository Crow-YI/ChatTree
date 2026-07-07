using TreeChat.Models;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 主窗口VM
    /// </summary>
    public class MainWindowVM : BaseViewModel
    {
        public ChatManagementPanelVM ChatManagementPanelVM { get; set; }
        public TreeVisualizationVM TreeVisualizationVM { get; set; }
        public ChatInformationVM ChatInformationVM { get; set; }

        public MainWindowVM()
        {
            ChatManagementPanelVM = new ChatManagementPanelVM();
            TreeVisualizationVM = new TreeVisualizationVM();
            ChatInformationVM = new ChatInformationVM();

            //事件绑定
            ChatManagementPanelVM.SelectedChatChanged += ChangeNodeVMTree;
            TreeVisualizationVM.SelectedNodeChanged += (nodeVM) => { ChatInformationVM.SelectedNode = nodeVM; };
            ChatInformationVM.ChatTreeChanged += TreeVisualizationVM.UpdateTree;
        }

    /// <summary>
    /// 当选中对话变更时，创建对应的节点 VM 树
    /// </summary>
    private void ChangeNodeVMTree(ChatTree tree)
    {
        // 直接创建根节点 VM，TreeNodeVM 构造函数会自动递归创建所有子节点
        TreeNodeVM rootNodeVM = new TreeNodeVM(tree.RootNode, null);
        TreeVisualizationVM.SetTree(rootNodeVM);
        // 传递当前ChatTree给ChatInformationVM和TreeVisualizationVM
        ChatInformationVM.CurrentChatTree = tree;
        TreeVisualizationVM.CurrentChatTree = tree;
    }
    }
}