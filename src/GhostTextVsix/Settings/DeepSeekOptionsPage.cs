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
    [Description("Reserved for future debounce-based automatic completion.")]
    public bool EnableAutoCompletion { get; set; }

    [Category("Completion")]
    [DisplayName("Max Prefix Lines")]
    [Description("Maximum number of lines to include before the cursor.")]
    public int MaxPrefixLines { get; set; } = 120;

    [Category("Completion")]
    [DisplayName("Max Suffix Lines")]
    [Description("Maximum number of lines to include after the cursor.")]
    public int MaxSuffixLines { get; set; } = 60;
}
