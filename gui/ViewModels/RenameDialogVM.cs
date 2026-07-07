using TreeChat.Commands;

namespace TreeChat.ViewModels
{
    public class RenameDialogViewModel : BaseViewModel
    {
        private string _newName;
        public string NewName
        {
            get => _newName;
            set
            {
                if (SetProperty(ref _newName, value))
                    ConfirmCommand.OnCanExecuteChanged();
            }
        }

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public event System.Action<bool?> CloseRequest;

        public RenameDialogViewModel(string currentName)
        {
            _newName = currentName;
            Validate();
            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => IsValid);
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        private void Validate()
        {
            IsValid = !string.IsNullOrWhiteSpace(NewName);
        }

        private void Confirm()
        {
            if (IsValid)
                CloseRequest?.Invoke(true);
        }

        private void Cancel() => CloseRequest?.Invoke(false);
    }
}