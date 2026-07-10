using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using TreeChat.Models;
using TreeChat.Services;
using TreeChat.ViewModels;
using TreeChat.Views;

namespace TreeChat.Gui.Tests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Sidebar starts expanded and toggles both ways", SidebarToggles),
            ("Markdown creates a readable FlowDocument", MarkdownCreatesDocument),
            ("Incomplete markdown remains readable", IncompleteMarkdownIsReadable),
            ("Markdown view renders immediately while idle", MarkdownViewRendersImmediately),
            ("Chat streaming state starts idle", StreamingStateStartsIdle),
            ("Enter resolves to send", EnterResolvesToSend),
            ("Control Enter resolves to line break", ControlEnterResolvesToLineBreak),
            ("Non-enter keys do not submit", NonEnterKeysDoNotSubmit),
            ("Application icon is packaged", ApplicationIconIsPackaged),
        };

        try
        {
            foreach (var test in tests)
            {
                test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL: {ex.Message}");
            return 1;
        }
    }

    private static void SidebarToggles()
    {
        var vm = new MainWindowVM(new FakeFileService());
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert(vm.IsSidebarExpanded, "Sidebar should be expanded by default.");
        Assert(vm.NavigationColumnWidth == new GridLength(48),
            "Expanded navigation rail should be 48 pixels wide.");
        Assert(vm.WorkspaceColumnWidth.IsStar && vm.WorkspaceColumnWidth.Value == 1,
            "Expanded workspace panel should use one star width.");

        vm.ToggleSidebarCommand.Execute(null);
        Assert(!vm.IsSidebarExpanded, "Sidebar should collapse after the first toggle.");
        Assert(vm.NavigationColumnWidth.Value == 0 && vm.WorkspaceColumnWidth.Value == 0,
            "Both sidebar columns should collapse to zero width.");

        vm.ToggleSidebarCommand.Execute(null);
        Assert(vm.IsSidebarExpanded, "Sidebar should expand after the second toggle.");
        Assert(changedProperties.Contains(nameof(MainWindowVM.IsSidebarExpanded)),
            "Toggling should raise PropertyChanged for IsSidebarExpanded.");
        Assert(changedProperties.Contains(nameof(MainWindowVM.NavigationColumnWidth)),
            "Toggling should notify the navigation column width.");
        Assert(changedProperties.Contains(nameof(MainWindowVM.WorkspaceColumnWidth)),
            "Toggling should notify the workspace column width.");
    }

    private static void MarkdownCreatesDocument()
    {
        const string markdown = "# 标题\n\n- 第一项\n- 第二项\n\n```csharp\nConsole.WriteLine(\"ok\");\n```";
        var document = MarkdownDocumentRenderer.Render(markdown);
        var text = ReadDocument(document);

        Assert(document.Blocks.Count >= 3, "Rendered markdown should contain multiple blocks.");
        Assert(text.Contains("标题"), "Rendered markdown should contain the heading text.");
        Assert(text.Contains("第一项"), "Rendered markdown should contain list items.");
        Assert(document.Blocks.OfType<BlockUIContainer>().Any(),
            "Rendered markdown should contain a native code block container.");
    }

    private static void IncompleteMarkdownIsReadable()
    {
        const string markdown = "## 正在生成\n\n```csharp\nvar value = 42;";
        var document = MarkdownDocumentRenderer.Render(markdown);
        var text = ReadDocument(document);

        Assert(text.Contains("正在生成"), "Incomplete markdown should preserve heading text.");
        Assert(text.Contains("var value = 42"), "Incomplete markdown should preserve code text.");
    }

    private static void MarkdownViewRendersImmediately()
    {
        var view = new MarkdownMessageView
        {
            IsStreaming = false,
            MarkdownText = "**重点内容**",
        };

        Assert(ReadDocument(view.Document).Contains("重点内容"),
            "Idle markdown updates should render synchronously.");
    }

    private static void StreamingStateStartsIdle()
    {
        var vm = new ChatInformationVM();
        Assert(!vm.IsStreaming, "ChatInformationVM should start in an idle state.");
    }

    private static void EnterResolvesToSend()
    {
        Assert(ChatInputKeyBehavior.Resolve(Key.Enter, ModifierKeys.None) == ChatInputAction.Send,
            "Enter should send the current message.");
    }

    private static void ControlEnterResolvesToLineBreak()
    {
        Assert(ChatInputKeyBehavior.Resolve(Key.Enter, ModifierKeys.Control) == ChatInputAction.InsertLineBreak,
            "Control+Enter should insert a line break.");
    }

    private static void NonEnterKeysDoNotSubmit()
    {
        Assert(ChatInputKeyBehavior.Resolve(Key.A, ModifierKeys.None) == ChatInputAction.None,
            "Regular typing should not submit the message.");
        Assert(ChatInputKeyBehavior.Resolve(Key.ImeProcessed, ModifierKeys.None) == ChatInputAction.None,
            "IME processing keys should not submit the message.");
    }

    private static void ApplicationIconIsPackaged()
    {
        var iconUri = new Uri("pack://application:,,,/TreeChat;component/Assets/ChatTree.ico");
        var resource = Application.GetResourceStream(iconUri);

        Assert(resource != null, "The ChatTree application icon should be packaged as a WPF resource.");
        Assert(resource!.Stream.Length > 0, "The packaged application icon should not be empty.");
    }

    private static string ReadDocument(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeFileService : IFileService
    {
        public bool SaveChatTree(ChatTree chatTree) => true;
        public bool SaveChatTreeAs(ChatTree chatTree) => true;
        public Task<bool> SaveChatTreeAsync(ChatTree chatTree) => Task.FromResult(true);
        public Task<bool> SaveChatTreeAsAsync(ChatTree chatTree) => Task.FromResult(true);
        public ChatTree? LoadChatTree() => null;
        public ChatTree? LoadChatTree(string filePath) => null;
    }
}
