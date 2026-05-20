using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace GhostTextVsix.Security;

internal sealed class SecurityFilter
{
    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", "*.key", "*.pem", "*.pfx", "*.cer", "*.crt"
    };

    private static readonly string[] ExcludedNamePrefixes =
    {
        ".env.",
        "secrets.",
        "credentials."
    };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "build", "out", "x64", "x86", "Debug", "Release", ".vscode", "node_modules", "vcpkg_installed"
    };

    private static readonly Regex SecretRegex = new(
        @"(?im)\b(API_KEY|SECRET|TOKEN|PASSWORD|PRIVATE_KEY|ACCESS_TOKEN|CLIENT_SECRET|Bearer)\b[^\r\n]*",
        RegexOptions.Compiled);

    public bool IsPathAllowed(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var fileName = Path.GetFileName(filePath);
        if (string.Equals(fileName, ".env", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in ExcludedNamePrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (fileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".cer", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".crt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directory = new DirectoryInfo(Path.GetDirectoryName(filePath) ?? string.Empty);
        while (directory != null && !string.IsNullOrWhiteSpace(directory.Name))
        {
            if (ExcludedDirectories.Contains(directory.Name))
            {
                return false;
            }

            directory = directory.Parent;
        }

        return true;
    }

    public string MaskSecrets(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return SecretRegex.Replace(input, m => $"{m.Groups[1].Value}=***");
    }
}
