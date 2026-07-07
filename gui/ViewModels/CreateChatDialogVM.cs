using TreeChat.Commands;

namespace TreeChat.ViewModels
{
    public class CreateChatDialogViewModel : BaseViewModel
    {
        private string _treeTitle = string.Empty;
        public string TreeTitle
        {
            get => _treeTitle;
            set
            {
                if (SetProperty(ref _treeTitle, value))
                {
                    Validate();
                    ConfirmCommand.OnCanExecuteChanged();
                }
            }
        }

        private string _systemPrompt = string.Empty;
        public string SystemPrompt
        {
            get => _systemPrompt;
            set => SetProperty(ref _systemPrompt, value);
        }

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public event System.Action<bool?>? CloseRequest;

        public CreateChatDialogViewModel()
        {
            TreeTitle = "";
            SystemPrompt = "";
            Validate();
            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => IsValid);
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        private void Validate()
        {
            IsValid = !string.IsNullOrWhiteSpace(TreeTitle);
        }

        private void Confirm()
        {
            if (IsValid)
                CloseRequest?.Invoke(true);
        }

        private void Cancel() => CloseRequest?.Invoke(false);
    }
}