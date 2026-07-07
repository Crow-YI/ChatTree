using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class TreeVisualizationView : UserControl
    {
        private TreeVisualizationVM? _vm;

        private Dictionary<int, FrameworkElement> _nodeElements = new Dictionary<int, FrameworkElement>();

        /// <summary>
        /// 文件拖放事件，参数为文件路径
        /// </summary>
        public event Action<string>? FileDropped;

        //用于平移的变量
        private Point _lastMousePosition;
        private bool _isDragging = false;

        //用于缩放的变量
        private double _zoomFactor = 1.0;
        private const double ZoomStep = 0.1;
        private Point _zoomCenter;

        //选中节点
        public static readonly DependencyProperty SelectedNodeProperty =
            DependencyProperty.Register("SelectedNode", typeof(TreeNodeVM), typeof(TreeVisualizationView),
                new PropertyMetadata(null, OnSelectedNodeChanged));

        public TreeNodeVM SelectedNode
        {
            get => (TreeNodeVM)GetValue(SelectedNodeProperty);
            set => SetValue(SelectedNodeProperty, value);
        }

        public TreeVisualizationView()
        {
            InitializeComponent();

            DataContextChanged += TreeVisualizationView_DataContextChanged;
        }

        private void TreeVisualizationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 取消旧VM的订阅
            if (e.OldValue is TreeVisualizationVM oldVm)
            {
                oldVm.CanvasPropertyChanged -= RefreshView;
            }

            // 订阅新VM的事件
            if (e.NewValue is TreeVisualizationVM newVm)
            {
                _vm = newVm;
                newVm.CanvasPropertyChanged += RefreshView;
            }
        }

        private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeVisualizationView view && e.NewValue is TreeNodeVM node)
            {
                // 同步到VM（双向绑定保障）
                if (view._vm != null && view._vm.SelectedNode != node)
                {
                    view._vm.SelectedNode = node;
                }
                // 本地高亮
                view.HighlightSelectedNode(node);
            }
        }

        // 鼠标拖动平移功能
        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsClickOnNodeElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _zoomCenter = e.GetPosition(treeCanvas);
                e.Handled = true;
                return;
            }

            _isDragging = true;
            _lastMousePosition = e.GetPosition(scrollViewer);
            scrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && scrollViewer.IsMouseCaptured)
            {
                Point currentPos = e.GetPosition(scrollViewer);
                double deltaX = currentPos.X - _lastMousePosition.X;
                double deltaY = currentPos.Y - _lastMousePosition.Y;

                scrollViewer.ScrollToHorizontalOffset(
                    scrollViewer.HorizontalOffset - deltaX);
                scrollViewer.ScrollToVerticalOffset(
                    scrollViewer.VerticalOffset - deltaY);

                _lastMousePosition = currentPos;
                e.Handled = true;
            }
        }

        private void ScrollViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                scrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            }

            if (scrollViewer.IsMouseCaptured)
            {
                scrollViewer.ReleaseMouseCapture();
            }
        }

        // 鼠标滚轮缩放功能
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // 计算缩放中心点
                var mousePosition = e.GetPosition(treeCanvas);
                var transform = treeCanvas.RenderTransform as MatrixTransform ?? new MatrixTransform();
                var matrix = transform.Matrix;

                // 缩放比例
                double zoomDelta = e.Delta > 0 ? ZoomStep : -ZoomStep;
                double newZoomFactor = Math.Max(0.2, Math.Min(_zoomFactor + zoomDelta, 5.0));

                // 计算平移偏移，使缩放中心保持在鼠标位置
                double zoomRatio = newZoomFactor / _zoomFactor;
                double dx = (1 - zoomRatio) * (mousePosition.X * matrix.M11 + matrix.OffsetX);
                double dy = (1 - zoomRatio) * (mousePosition.Y * matrix.M22 + matrix.OffsetY);

                // 应用新缩放
                _zoomFactor = newZoomFactor;
                matrix.ScaleAt(zoomRatio, zoomRatio, mousePosition.X, mousePosition.Y);
                matrix.Translate(dx * (1 / zoomRatio), dy * (1 / zoomRatio)); // 调整平移

                treeCanvas.RenderTransform = new MatrixTransform(matrix);

                e.Handled = true;
            }
        }

        private void RefreshView()
        {
            RenderTree();
        }

        private void RenderTree()
        {
            if(treeCanvas == null || _vm == null || _vm.RootNode == null)
                return;

            treeCanvas.Children.Clear();
            _nodeElements.Clear();

            // 先绘制连接线（在节点下方）
            DrawConnections(_vm.RootNode);

            // 再绘制节点
            DrawNodes(_vm.RootNode);

            // 设置画布大小
            double maxWidth = _nodeElements.Values
                .Select(el => Canvas.GetLeft(el) + el.ActualWidth)
                .DefaultIfEmpty(800).Max();

            double maxHeight = _nodeElements.Values
                .Select(el => Canvas.GetTop(el) + el.ActualHeight)
                .DefaultIfEmpty(600).Max();

            treeCanvas.Width = maxWidth + 100; // 添加边距
            treeCanvas.Height = maxHeight + 100;
        }

        private void DrawConnections(TreeNodeVM rootNode)
        {
            if (rootNode.Children.Count == 0)
                return;

            // 父节点底部中心点
            double parentCenterX = rootNode.X + TreeNodeVM.WIDTH / 2;
            double parentBottomY = rootNode.Y + TreeNodeVM.HEIGHT;

            foreach (var child in rootNode.Children)
            {
                // 子节点顶部中心点
                double childCenterX = child.X + TreeNodeVM.WIDTH / 2;
                double childTopY = child.Y;

                // 绘制连接线
                var line = new Line
                {
                    X1 = parentCenterX,
                    Y1 = parentBottomY,
                    X2 = childCenterX,
                    Y2 = childTopY,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5
                };
                treeCanvas.Children.Add(line);

                // 递归绘制子节点的连接线
                DrawConnections(child);
            }
        }

        private void DrawNodes(TreeNodeVM rootNode)
        {
            // 节点内容
            var textBlock = new TextBlock
            {
                Text = rootNode.DisplayContent,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5)
            };
            
            // 测量文本大小
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = textBlock.DesiredSize.Width;
            var textHeight = textBlock.DesiredSize.Height;
            
            // 创建节点UI，根据文本大小调整宽度
            var nodeBorder = new Border
            {
                Width = Math.Max(textWidth + 10, 40), // 最小宽度40
                Height = Math.Max(textHeight + 10, 30), // 最小高度30
                Background = new SolidColorBrush(Color.FromRgb(220, 230, 240)),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand
            };
            
            nodeBorder.Child = textBlock;

            // 为节点添加点击事件
            var currentNode = rootNode; // 保存当前节点引用，避免闭包问题
            nodeBorder.PreviewMouseLeftButtonDown += (s, e) =>
            {
                SelectedNode = currentNode; // 触发选中逻辑
                e.Handled = true;           // 标记事件已处理，阻止冒泡到ScrollViewer
            };

            // 定位节点
            Canvas.SetLeft(nodeBorder, rootNode.X);
            Canvas.SetTop(nodeBorder, rootNode.Y);

            // 添加到画布
            treeCanvas.Children.Add(nodeBorder);
            _nodeElements[rootNode.ID] = nodeBorder;

            // 递归处理子节点
            foreach (var child in rootNode.Children)
            {
                DrawNodes(child);
            }
        }

        private void HighlightSelectedNode(TreeNodeVM node)
        {
            // 重置所有节点的边框
            foreach (var element in _nodeElements.Values)
            {
                if (element is Border border)
                {
                    border.BorderBrush = Brushes.Gray;
                    border.BorderThickness = new Thickness(1);
                }
            }

            // 高亮选中的节点
            if (_nodeElements.TryGetValue(node.ID, out var selectedElement) && selectedElement is Border selectedBorder)
            {
                selectedBorder.BorderBrush = Brushes.Blue;
                selectedBorder.BorderThickness = new Thickness(2);
            }
        }

        private bool IsClickOnNodeElement(DependencyObject clickedElement)
        {
            if (clickedElement == null) return false;

            // 向上遍历可视化树，查找是否是节点的Border
            while (clickedElement != null)
            {
                // 判断当前元素是否是节点集合中的Border
                if (clickedElement is Border border && _nodeElements.Values.Contains(border))
                {
                    return true;
                }
                // 继续向上找父元素
                clickedElement = VisualTreeHelper.GetParent(clickedElement);
            }
            return false;
        }

        // 文件拖放处理
        private void ScrollViewer_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ScrollViewer_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
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