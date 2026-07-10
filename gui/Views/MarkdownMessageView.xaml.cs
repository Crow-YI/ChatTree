using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace TreeChat.Views;

public partial class MarkdownMessageView : UserControl
{
    public static readonly DependencyProperty MarkdownTextProperty = DependencyProperty.Register(
        nameof(MarkdownText), typeof(string), typeof(MarkdownMessageView),
        new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

    public static readonly DependencyProperty IsStreamingProperty = DependencyProperty.Register(
        nameof(IsStreaming), typeof(bool), typeof(MarkdownMessageView),
        new PropertyMetadata(false, OnIsStreamingChanged));

    private readonly DispatcherTimer _renderTimer;
    private string _pendingMarkdown = string.Empty;

    public MarkdownMessageView()
    {
        _renderTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _renderTimer.Tick += (_, _) =>
        {
            _renderTimer.Stop();
            RenderPendingMarkdown();
        };

        InitializeComponent();
        RenderPendingMarkdown();
    }

    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    public bool IsStreaming
    {
        get => (bool)GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    public FlowDocument Document => Viewer.Document;

    private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MarkdownMessageView)d;
        view._pendingMarkdown = e.NewValue as string ?? string.Empty;

        if (view.IsStreaming)
        {
            if (!view._renderTimer.IsEnabled)
            {
                view._renderTimer.Start();
            }
        }
        else
        {
            view._renderTimer.Stop();
            view.RenderPendingMarkdown();
        }
    }

    private static void OnIsStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MarkdownMessageView)d;
        if (!(bool)e.NewValue)
        {
            view._renderTimer.Stop();
            view.RenderPendingMarkdown();
        }
    }

    private void RenderPendingMarkdown()
    {
        var document = MarkdownDocumentRenderer.Render(_pendingMarkdown);
        ApplyDocumentTheme(document);
        Viewer.Document = document;
    }

    private static void ApplyDocumentTheme(FlowDocument document)
    {
        document.PagePadding = new Thickness(0);
        document.FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
        document.FontSize = 14;
        document.Foreground = new SolidColorBrush(Color.FromRgb(39, 40, 36));
        document.LineHeight = 22;
        document.TextAlignment = TextAlignment.Left;

        foreach (var block in document.Blocks)
        {
            block.Margin = block is Section ? new Thickness(0) : new Thickness(0, 0, 0, 8);

            if (block is BlockUIContainer { Child: Control control })
            {
                control.Background = new SolidColorBrush(Color.FromRgb(240, 240, 236));
                control.Foreground = new SolidColorBrush(Color.FromRgb(39, 40, 36));
                control.FontFamily = new FontFamily("Cascadia Mono, Consolas");
                control.FontSize = 12.5;
            }
        }
    }
}
