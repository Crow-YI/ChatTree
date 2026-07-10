using System.Windows.Documents;
using MdXaml;

namespace TreeChat.Views;

public static class MarkdownDocumentRenderer
{
    public static FlowDocument Render(string? markdown)
    {
        try
        {
            return new Markdown().Transform(markdown ?? string.Empty);
        }
        catch
        {
            return new FlowDocument(new Paragraph(new Run(markdown ?? string.Empty)));
        }
    }
}
