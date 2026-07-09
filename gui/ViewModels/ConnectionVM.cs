using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 连线 ViewModel，提供连接两个节点的端点坐标。
    /// SourcePoint = 父节点底部中心, TargetPoint = 子节点顶部中心。
    /// </summary>
    public class ConnectionVM : INotifyPropertyChanged
    {
        private Point _sourcePoint;
        private Point _targetPoint;

        /// <summary>
        /// 连线起点（父节点底部中心）
        /// </summary>
        public Point SourcePoint
        {
            get => _sourcePoint;
            private set
            {
                _sourcePoint = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 连线终点（子节点顶部中心）
        /// </summary>
        public Point TargetPoint
        {
            get => _targetPoint;
            private set
            {
                _targetPoint = value;
                OnPropertyChanged();
            }
        }

        public TreeNodeVM From { get; }
        public TreeNodeVM To { get; }

        public ConnectionVM(TreeNodeVM from, TreeNodeVM to)
        {
            From = from;
            To = to;
            UpdatePoints();
        }

        /// <summary>
        /// 根据 From/To 节点的位置计算端点坐标。
        /// 布局变化后调用此方法更新连线位置。
        /// </summary>
        public void UpdatePoints()
        {
            double srcX = From.X + TreeNodeVM.WIDTH / 2;
            double srcY = From.Y + TreeNodeVM.HEIGHT;
            double tgtX = To.X + TreeNodeVM.WIDTH / 2;
            double tgtY = To.Y;

            SourcePoint = new Point(srcX, srcY);
            TargetPoint = new Point(tgtX, tgtY);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
