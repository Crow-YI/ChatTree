using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// VM基类
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            // 正确的相等性判断：处理null、值类型、引用类型
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName); // 触发属性变更通知
            return true;
        }
    }
}
