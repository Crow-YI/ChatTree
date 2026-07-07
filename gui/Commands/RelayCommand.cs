using System.Windows.Input;

namespace TreeChat.Commands
{
    /// <summary>
    /// 通用命令基类
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecte;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecte = null)
        {
            _execute = execute;
            _canExecte = canExecte;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecte == null || _canExecte(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// 手动触发CanExecuteChanged事件
        /// </summary>
        public void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
