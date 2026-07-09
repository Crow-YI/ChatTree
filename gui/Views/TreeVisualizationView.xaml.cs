using System.Windows;
using System.Windows.Controls;
using Nodify;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class TreeVisualizationView : UserControl
    {
        private TreeVisualizationVM? _vm;

        /// <summary>
        /// 文件拖放事件，参数为文件路径
        /// </summary>
        public event Action<string>? FileDropped;

        public TreeVisualizationView()
        {
            InitializeComponent();

            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is TreeVisualizationVM oldVm)
                {
                    oldVm.TreeRendered -= OnTreeRendered;
                }

                if (e.NewValue is TreeVisualizationVM newVm)
                {
                    _vm = newVm;
                    newVm.TreeRendered += OnTreeRendered;
                }
            };
        }

        /// <summary>
        /// 树渲染完成后自动适配视图，使所有节点居中可见
        /// </summary>
        private void OnTreeRendered()
        {
            if (_vm?.RootNode == null) return;

            try
            {
                // 计算所有节点的包围盒
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                foreach (var node in _vm.Decorators)
                {
                    if (node.X < minX) minX = node.X;
                    if (node.Y < minY) minY = node.Y;
                    if (node.X + TreeNodeVM.WIDTH > maxX) maxX = node.X + TreeNodeVM.WIDTH;
                    if (node.Y + TreeNodeVM.HEIGHT > maxY) maxY = node.Y + TreeNodeVM.HEIGHT;
                }

                double treeWidth = maxX - minX;
                double treeHeight = maxY - minY;
                double treeCenterX = minX + treeWidth / 2;
                double treeCenterY = minY + treeHeight / 2;

                double viewportWidth = editor.ViewportSize.Width;
                double viewportHeight = editor.ViewportSize.Height;

                // 居中：将视口移动到(树中心 - 视口半宽, 树顶部(约占1/3处))
                double targetX = treeCenterX - viewportWidth / 2;
                double targetY = treeCenterY - viewportHeight / 3;

                editor.ViewportLocation = new System.Windows.Point(targetX, targetY);
            }
            catch { }
        }

        // 文件拖放处理
        private void ScrollViewer_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void ScrollViewer_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void ScrollViewer_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    FileDropped?.Invoke(files[0]);
                }
            }
            e.Handled = true;
        }
    }
}