using System.Windows;
using TreeChat.Models;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class ConfigDialog : Window
    {
        public ConfigDialogViewModel ViewModel { get; }

        /// <summary>
        /// 创建/编辑模型配置。
        /// </summary>
        /// <param name="existing">null 表示新建，非 null 表示编辑已有 profile</param>
        public ConfigDialog(ProfileData? existing = null)
        {
            InitializeComponent();
            ViewModel = new ConfigDialogViewModel(existing);
            DataContext = ViewModel;
            ViewModel.CloseRequest += result =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
