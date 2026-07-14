using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TreeChat.ViewModels;

namespace TreeChat.Views;

public partial class ChatInformationView : UserControl
{
    public ChatInformationView()
    {
        InitializeComponent();
    }

    private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var action = ChatInputKeyBehavior.Resolve(e.Key, Keyboard.Modifiers);
        if (action == ChatInputAction.None)
        {
            return;
        }

        e.Handled = true;

        if (action == ChatInputAction.InsertLineBreak && sender is TextBox textBox)
        {
            var insertionStart = textBox.SelectionStart;
            textBox.SelectedText = Environment.NewLine;
            textBox.CaretIndex = insertionStart + Environment.NewLine.Length;
            return;
        }

        if (DataContext is ChatInformationVM vm && vm.SendMessage.CanExecute(null))
        {
            vm.SendMessage.Execute(null);
        }
    }

    // ==================== 附件拖放 ====================

    private void InputArea_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void InputArea_PreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && DataContext is ChatInformationVM vm)
            {
                vm.AddAttachmentFilesCommand.Execute(files);
            }
        }
        e.Handled = true;
    }
}
