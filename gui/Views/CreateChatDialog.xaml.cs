using System.Windows;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class CreateChatDialog : Window
    {
        public CreateChatDialogViewModel ViewModel { get; }

        public CreateChatDialog()
        {
            InitializeComponent();
            ViewModel = new CreateChatDialogViewModel();
            DataContext = ViewModel;
            ViewModel.CloseRequest += result =>
            {
                DialogResult = result;
                Close();
            };
        }

        public string TreeTitle => ViewModel.TreeTitle;
        public string SystemPrompt => ViewModel.SystemPrompt;
    }
}