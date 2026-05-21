using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GhostTextVsix.Diagnostics;

internal static class SafeLog
{
    public static void Info(DeepSeekOutputLogger logger, string message)
    {
        try
        {
            logger?.Info(message);
        }
        catch
        {
            Debug.WriteLine(message);
        }
    }

    public static void Warning(DeepSeekOutputLogger logger, string message, Exception ex)
    {
        var safeMessage = $"{message}. ErrorType={ex.GetType().Name}";
        try
        {
            logger?.Warning(safeMessage);
        }
        catch
        {
            Debug.WriteLine(safeMessage);
        }
    }

    public static bool IsExpectedDisposeException(Exception ex)
    {
        return ex is ObjectDisposedException ||
               ex is COMException ||
               ex is InvalidOperationException;
    }
}
