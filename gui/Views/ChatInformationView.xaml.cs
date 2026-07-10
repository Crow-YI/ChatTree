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
}
