using System.Windows.Input;

namespace TreeChat.Views;

public enum ChatInputAction
{
    None,
    Send,
    InsertLineBreak,
}

public static class ChatInputKeyBehavior
{
    public static ChatInputAction Resolve(Key key, ModifierKeys modifiers)
    {
        if (key != Key.Enter)
        {
            return ChatInputAction.None;
        }

        return modifiers.HasFlag(ModifierKeys.Control)
            ? ChatInputAction.InsertLineBreak
            : ChatInputAction.Send;
    }
}
