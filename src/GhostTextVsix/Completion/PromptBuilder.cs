using System.Text;
using GhostTextVsix.Editor;

namespace GhostTextVsix.Completion;

internal sealed class PromptBuilder
{
    private const string SystemPrompt =
        "You are an AI code completion engine for Visual Studio 2022.\n" +
        "Generate the next C or C++ code completion at the cursor position.\n" +
        "Return only the code that should be inserted.\n" +
        "Do not include Markdown.\n" +
        "Do not include explanations.\n" +
        "Do not repeat code that already exists before the cursor.\n" +
        "Do not add unnecessary include directives.\n" +
        "Preserve the existing coding style and indentation.";

    private const string AutoSystemPrompt =
        "You are a C/C++ code completion engine.\n" +
        "Return only the missing code to insert at the cursor.\n" +
        "Do not repeat the existing prefix.\n" +
        "Do not use Markdown.\n" +
        "Do not explain.\n" +
        "Keep the completion short.\n" +
        "Preserve indentation and style.";

    public string SystemMessage => SystemPrompt;

    public string AutoSystemMessage => AutoSystemPrompt;

    public string BuildUserPrompt(EditorContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Complete the code at <cursor>.");
        builder.AppendLine("Return only the missing text after the current line prefix.");
        builder.AppendLine("Do not repeat the current line prefix.");
        builder.AppendLine($"File: {context.FilePath}");
        builder.AppendLine("Current line prefix already typed by the user:");
        builder.AppendLine(context.CurrentLinePrefix);
        builder.AppendLine("Prefix:");
        builder.AppendLine(context.Prefix);
        builder.AppendLine("<cursor>");
        builder.AppendLine("Suffix:");
        builder.AppendLine(context.Suffix);
        return builder.ToString();
    }

    public string BuildAutoUserPrompt(EditorContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Complete at <cursor>. Return only inserted code.");
        builder.AppendLine($"File: {context.FilePath}");
        builder.AppendLine("Current line prefix:");
        builder.AppendLine(context.CurrentLinePrefix);
        builder.AppendLine("Prefix:");
        builder.AppendLine(context.Prefix);
        builder.AppendLine("<cursor>");
        builder.AppendLine("Suffix:");
        builder.AppendLine(context.Suffix);
        return builder.ToString();
    }
}
