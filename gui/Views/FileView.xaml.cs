using System.Windows;
using System.Windows.Controls;
using TreeChat.Models;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class FileView : UserControl
    {
        public FileView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 双击最近文件列表项时打开对应文件。
        /// </summary>
        private void RecentFiles_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is RecentFileItem item)
            {
                var vm = DataContext as FileViewVM;
                vm?.OpenRecentFileCommand.Execute(item);
            }
        }
    }
}
