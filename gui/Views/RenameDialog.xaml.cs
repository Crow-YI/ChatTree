using System.Windows;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class RenameDialog : Window
    {
        public RenameDialogViewModel ViewModel { get; }

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            ViewModel = new RenameDialogViewModel(currentName);
            DataContext = ViewModel;
            ViewModel.CloseRequest += result =>
            {
                DialogResult = result;
                Close();
            };
        }

        // 对外暴露属性（方便调用方获取新名称）
        public string NewName => ViewModel.NewName;
    }
}