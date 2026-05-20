using System;
using System.Collections.Generic;
using System.IO;

namespace GhostTextVsix.Editor;

internal sealed class CppDocumentDetector
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".c", ".h", ".cpp", ".hpp", ".cc", ".cxx", ".hh", ".hxx"
    };

    public bool IsSupported(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }
}
