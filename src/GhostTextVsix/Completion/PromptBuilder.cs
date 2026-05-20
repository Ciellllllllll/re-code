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

    public string SystemMessage => SystemPrompt;

    public string BuildUserPrompt(EditorContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Complete the code at <cursor>.");
        builder.AppendLine($"File: {context.FilePath}");
        builder.AppendLine("Prefix:");
        builder.AppendLine(context.Prefix);
        builder.AppendLine("<cursor>");
        builder.AppendLine("Suffix:");
        builder.AppendLine(context.Suffix);
        return builder.ToString();
    }
}
