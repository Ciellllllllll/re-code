using Microsoft.VisualStudio.Text;

namespace GhostTextVsix.Editor;

internal static class TextBufferExtensions
{
    private const string FilePathKey = "TextBufferFilePath";

    public static string GetFileName(this ITextBuffer textBuffer)
    {
        if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
        {
            return document.FilePath;
        }

        if (textBuffer.Properties.TryGetProperty(FilePathKey, out string filePath))
        {
            return filePath;
        }

        return string.Empty;
    }
}
