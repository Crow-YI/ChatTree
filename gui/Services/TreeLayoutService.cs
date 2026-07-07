using TreeChat.ViewModels;

namespace TreeChat.Services
{
    /// <summary>
    /// 绘制树形结构的布局服务类
    /// </summary>
    public static class TreeLayoutService
    {
        private const double HorizontalSpacing = 60;
        private const double VerticalSpacing = 60;
        private const double MinNodeWidth = 80;
        private const double CharWidth = 12;

        private static double CalculateNodeWidth(TreeNodeVM node)
        {
            string nodeName = node.DisplayContent ?? "";
            double width = nodeName.Length * CharWidth;
            return Math.Max(width, MinNodeWidth);
        }

        private static double CalculateWidthOfSubtree(TreeNodeVM currentNode)
        {
            currentNode.SubtreeWidth.Clear();

            if (currentNode.Children.Count == 0)
                return CalculateNodeWidth(currentNode);

            double totalWidth = 0;
            foreach (TreeNodeVM childNode in currentNode.Children)
            {
                double subWidth = CalculateWidthOfSubtree(childNode);
                currentNode.SubtreeWidth.Add(subWidth);
                totalWidth += subWidth + HorizontalSpacing;
            }
            totalWidth -= HorizontalSpacing;
            return Math.Max(totalWidth, CalculateNodeWidth(currentNode));
        }

        private static void UpdateWidthOfTree(TreeNodeVM updateNode)
        {
            updateNode.SubtreeWidth.Clear();
            double totalWidth = CalculateWidthOfSubtree(updateNode);

            TreeNodeVM currentNode = updateNode;
            TreeNodeVM? parentNode = currentNode.ParentNode;
            while (parentNode != null)
            {
                int index = parentNode.Children.IndexOf(currentNode);
                parentNode.SubtreeWidth[index] = totalWidth;
                totalWidth = 0;
                foreach (double childWidth in parentNode.SubtreeWidth)
                    totalWidth += childWidth + HorizontalSpacing;
                totalWidth -= HorizontalSpacing;
                currentNode = parentNode;
                parentNode = currentNode.ParentNode;
            }
        }

        private static void CalculatePositionOfSubtreeRoot(TreeNodeVM rootViewModel, double x, double y)
        {
            rootViewModel.Y = y;
            if (rootViewModel.SubtreeWidth.Count == 0)
            {
                rootViewModel.X = x;
                return;
            }

            double totalWidth = 0;
            foreach (double childWidth in rootViewModel.SubtreeWidth)
                totalWidth += childWidth + HorizontalSpacing;
            totalWidth -= HorizontalSpacing;
            rootViewModel.X = x + totalWidth / 2 - CalculateNodeWidth(rootViewModel) / 2;
        }

        /// <summary>
        /// 初始化树形结构的布局，计算每个节点的位置
        /// </summary>
        /// <param name="rootNode"></param>
        public static void LayoutTree(TreeNodeVM rootNode)
        {
            CalculateWidthOfSubtree(rootNode);
            LayoutSubtree(rootNode, 0, 0);
        }

        /// <summary>
        /// 更新树形结构的布局，传入修改节点的父节点，重新计算布局，节省计算资源
        /// </summary>
        /// <param name="updateNode"></param>
        public static void UpdateLayoutTree(TreeNodeVM updateNode)
        {
            UpdateWidthOfTree(updateNode);
            TreeNodeVM currentNode = updateNode;
            while (currentNode.ParentNode != null)
                currentNode = currentNode.ParentNode;
            LayoutSubtree(currentNode, 0, 0);
        }

        private static void LayoutSubtree(TreeNodeVM currentNode, double x, double y)
        {
            CalculatePositionOfSubtreeRoot(currentNode, x, y);
            double offsetX = 0;
            int count = currentNode.Children.Count;
            for(int i = 0; i < count; i++)
            {
                LayoutSubtree(currentNode.Children[i], x + offsetX, y + VerticalSpacing);
                offsetX += HorizontalSpacing + currentNode.SubtreeWidth[i];
            }
        }
    }
}
