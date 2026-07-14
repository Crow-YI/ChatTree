using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nodify;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class TreeVisualizationView : UserControl
    {
        private TreeVisualizationVM? _vm;
        private Point _panStartPoint;
        private Point _panStartViewport;
        private bool _isPanning;

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

        // ==================== 缩放控制 ====================

        /// <summary>
        /// 控件加载完成后初始化缩放设置。
        /// </summary>
        private void TreeVisualizationView_Loaded(object sender, RoutedEventArgs e)
        {
            editor.MinViewportZoom = 0.2;
            editor.MaxViewportZoom = 3.0;
            UpdateZoomDisplay();
        }

        /// <summary>
        /// 更新缩放百分比显示。
        /// </summary>
        private void UpdateZoomDisplay()
        {
            int percent = (int)Math.Round(editor.ViewportZoom * 100);
            ZoomLevelText.Text = $"{percent}%";
        }

        /// <summary>
        /// 视口变化时更新缩放显示。
        /// </summary>
        private void Editor_ViewportUpdated(object sender, EventArgs e)
        {
            UpdateZoomDisplay();
        }

        /// <summary>
        /// 放大（步进 1.3x）。
        /// </summary>
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            double newZoom = Math.Min(editor.ViewportZoom * 1.3, editor.MaxViewportZoom);
            editor.ViewportZoom = newZoom;
        }

        /// <summary>
        /// 缩小（步进 1.3x）。
        /// </summary>
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            double newZoom = Math.Max(editor.ViewportZoom / 1.3, editor.MinViewportZoom);
            editor.ViewportZoom = newZoom;
        }

        /// <summary>
        /// 重置缩放为 1.0。
        /// </summary>
        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            editor.ViewportZoom = 1.0;
        }

        /// <summary>
        /// 树渲染完成后自动适配视图，使所有节点居中可见。
        /// </summary>
        private void OnTreeRendered()
        {
            if (_vm?.RootNode == null) return;

            try
            {
                // 禁用 Nodify 默认中键/右键平移和边缘滚动
                editor.DisablePanning = true;
                editor.DisableAutoPanning = true;

                // 初始视图重置缩放为 1.0，确保居中计算一致性
                editor.ViewportZoom = 1.0;

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

        // ==================== 左键拖拽平移 ====================

        private void Editor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _panStartPoint = e.GetPosition(editor);
            _panStartViewport = editor.ViewportLocation;
            _isPanning = false;
        }

        private void Editor_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isPanning = false;
                return;
            }

            Point currentPos = e.GetPosition(editor);
            Vector delta = currentPos - _panStartPoint;

            // 移动超过阈值后进入平移模式（防止误触节点选择）
            if (!_isPanning && (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5))
            {
                _isPanning = true;
                editor.Cursor = Cursors.Hand;
            }

            if (_isPanning)
            {
                // delta 是屏幕像素，ViewportLocation 是图空间坐标，需除以缩放比
                double zoom = editor.ViewportZoom;
                editor.ViewportLocation = new Point(
                    _panStartViewport.X - delta.X / zoom,
                    _panStartViewport.Y - delta.Y / zoom);
            }
        }

        private void Editor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                editor.Cursor = Cursors.Arrow;
            }
            _isPanning = false;
        }

        // ==================== 文件拖放 ====================

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
