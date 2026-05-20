using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Settings;

public sealed class DeepSeekOptionsPage : DialogPage
{
    [Category("DeepSeek")]
    [DisplayName("API Key")]
    [Description("DeepSeek API key. Leave empty to use the DEEPSEEK_API_KEY environment variable.")]
    public string ApiKey { get; set; } = string.Empty;

    [Category("DeepSeek")]
    [DisplayName("Endpoint")]
    [Description("OpenAI-compatible DeepSeek chat completions endpoint.")]
    public string Endpoint { get; set; } = "https://api.deepseek.com/chat/completions";

    [Category("DeepSeek")]
    [DisplayName("Request Timeout Seconds")]
    [Description("Completion request timeout in seconds.")]
    public int TimeoutSeconds { get; set; } = 20;

    [Category("Completion")]
    [DisplayName("Enable Auto Completion")]
    [Description("Automatically request a completion after typing stops in supported C/C++ files.")]
    public bool EnableAutoCompletion { get; set; } = true;

    [Category("Completion")]
    [DisplayName("Debounce Time Milliseconds")]
    [Description("Delay after the last typed character before automatic completion starts.")]
    public int DebounceMilliseconds { get; set; } = 500;

    [Category("Completion")]
    [DisplayName("Cache TTL Seconds")]
    [Description("How long formatted completions are reused for the same editor context.")]
    public int CacheTtlSeconds { get; set; } = 30;

    [Category("Completion")]
    [DisplayName("Max Prefix Lines")]
    [Description("Maximum number of lines to include before the cursor.")]
    public int MaxPrefixLines { get; set; } = 120;

    [Category("Completion")]
    [DisplayName("Max Suffix Lines")]
    [Description("Maximum number of lines to include after the cursor.")]
    public int MaxSuffixLines { get; set; } = 60;
}
