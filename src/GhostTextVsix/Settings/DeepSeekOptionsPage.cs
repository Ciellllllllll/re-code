using System.ComponentModel;
using GhostTextVsix.Completion.Providers;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Settings;

public sealed class DeepSeekOptionsPage : DialogPage
{
    [Category("DeepSeek Compatibility")]
    [DisplayName("API Key")]
    [Description("DeepSeek API key. Leave empty to use the DEEPSEEK_API_KEY environment variable.")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [Browsable(false)]
    public string ApiKey { get; set; } = string.Empty;

    [Category("DeepSeek Compatibility")]
    [DisplayName("Endpoint")]
    [Description("OpenAI-compatible DeepSeek chat completions endpoint.")]
    [DefaultValue("https://api.deepseek.com/chat/completions")]
    [Browsable(false)]
    public string Endpoint { get; set; } = "https://api.deepseek.com/chat/completions";

    [Category("Manual Completion")]
    [DisplayName("Manual Timeout Seconds")]
    [Description("Request timeout in seconds for manual completion.")]
    [DefaultValue(20)]
    [Browsable(false)]
    public int TimeoutSeconds { get; set; } = 20;

    [Category("Providers")]
    [DisplayName("Auto Provider")]
    [Description("Completion provider for automatic completion.")]
    [DefaultValue(CompletionProviderType.NotConfigured)]
    public CompletionProviderType AutoCompletionProvider { get; set; } = CompletionProviderType.NotConfigured;

    [Category("Providers")]
    [DisplayName("Auto Model")]
    [Description("Model name used for automatic completion.")]
    [DefaultValue("")]
    public string AutoCompletionModel { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("Auto Base Url")]
    [Description("OpenAI-compatible chat completions endpoint for automatic completion. Empty uses the selected provider default.")]
    [DefaultValue("")]
    [Browsable(false)]
    public string AutoCompletionBaseUrl { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("Auto API Key")]
    [Description("Provider API key for automatic completion.")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    public string AutoCompletionApiKey { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("Manual Provider")]
    [Description("Completion provider for manual completion.")]
    [DefaultValue(CompletionProviderType.NotConfigured)]
    public CompletionProviderType ManualCompletionProvider { get; set; } = CompletionProviderType.NotConfigured;

    [Category("Providers")]
    [DisplayName("Manual Model")]
    [Description("Model name used for manual completion.")]
    [DefaultValue("")]
    public string ManualCompletionModel { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("Manual Base Url")]
    [Description("OpenAI-compatible chat completions endpoint for manual completion. Empty uses the selected provider default.")]
    [DefaultValue("")]
    [Browsable(false)]
    public string ManualCompletionBaseUrl { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("Manual API Key")]
    [Description("Provider API key for manual completion.")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    public string ManualCompletionApiKey { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("OpenRouter API Key")]
    [Description("OpenRouter API key. Leave empty to use OPENROUTER_API_KEY.")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [Browsable(false)]
    public string OpenRouterApiKey { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("OpenAI-compatible API Key")]
    [Description("Generic OpenAI-compatible API key.")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [Browsable(false)]
    public string OpenAICompatibleApiKey { get; set; } = string.Empty;

    [Category("Providers")]
    [DisplayName("Local Base Url")]
    [Description("Local OpenAI-compatible chat completions endpoint.")]
    [DefaultValue("http://localhost:1234/v1/chat/completions")]
    [Browsable(false)]
    public string LocalBaseUrl { get; set; } = "http://localhost:1234/v1/chat/completions";

    [Category("Completion")]
    [DisplayName("Enable Auto Completion")]
    [Description("Automatically request a completion after typing stops in supported C/C++ files.")]
    [DefaultValue(true)]
    public bool EnableAutoCompletion { get; set; } = true;

    [Category("Completion")]
    [DisplayName("Debounce Time Milliseconds")]
    [Description("Delay after the last typed character before automatic completion starts.")]
    [DefaultValue(500)]
    public int DebounceMilliseconds { get; set; } = 500;

    [Category("Completion")]
    [DisplayName("Cache TTL Seconds")]
    [Description("How long formatted completions are reused for the same editor context.")]
    [DefaultValue(30)]
    public int CacheTtlSeconds { get; set; } = 30;

    [Category("Manual Completion")]
    [DisplayName("Manual Prefix Lines")]
    [Description("Maximum number of lines before the cursor for manual completion.")]
    [DefaultValue(120)]
    [Browsable(false)]
    public int MaxPrefixLines { get; set; } = 120;

    [Category("Manual Completion")]
    [DisplayName("Manual Suffix Lines")]
    [Description("Maximum number of lines after the cursor for manual completion.")]
    [DefaultValue(60)]
    [Browsable(false)]
    public int MaxSuffixLines { get; set; } = 60;

    [Category("Auto Completion")]
    [DisplayName("Auto Prefix Lines")]
    [Description("Maximum number of lines before the cursor for automatic completion.")]
    [DefaultValue(40)]
    [Browsable(false)]
    public int AutoMaxPrefixLines { get; set; } = 40;

    [Category("Auto Completion")]
    [DisplayName("Auto Suffix Lines")]
    [Description("Maximum number of lines after the cursor for automatic completion.")]
    [DefaultValue(10)]
    [Browsable(false)]
    public int AutoMaxSuffixLines { get; set; } = 10;

    [Category("Auto Completion")]
    [DisplayName("Auto Max Completion Lines")]
    [Description("Maximum number of completion lines shown for automatic completion.")]
    [DefaultValue(3)]
    [Browsable(false)]
    public int AutoMaxCompletionLines { get; set; } = 3;

    [Category("Auto Completion")]
    [DisplayName("Auto Max Completion Characters")]
    [Description("Maximum number of completion characters shown for automatic completion.")]
    [DefaultValue(300)]
    [Browsable(false)]
    public int AutoMaxCompletionCharacters { get; set; } = 300;

    [Category("Auto Completion")]
    [DisplayName("Auto Timeout Milliseconds")]
    [Description("Request timeout for automatic completion.")]
    [DefaultValue(2000)]
    [Browsable(false)]
    public int AutoTimeoutMilliseconds { get; set; } = 2000;

    [Category("Auto Completion")]
    [DisplayName("Auto Max Tokens")]
    [Description("Provider max_tokens value for automatic completion.")]
    [DefaultValue(128)]
    public int AutoMaxTokens { get; set; } = 128;

    [Category("Auto Completion")]
    [DisplayName("Auto Temperature")]
    [Description("Provider temperature value for automatic completion.")]
    [DefaultValue(0.1)]
    [Browsable(false)]
    public double AutoTemperature { get; set; } = 0.1;

    [Category("Manual Completion")]
    [DisplayName("Manual Max Tokens")]
    [Description("Provider max_tokens value for manual completion. Use 0 to omit the setting.")]
    [DefaultValue(0)]
    public int ManualMaxTokens { get; set; } = 0;

    [Category("Manual Completion")]
    [DisplayName("Manual Temperature")]
    [Description("Provider temperature value for manual completion.")]
    [DefaultValue(0.2)]
    [Browsable(false)]
    public double ManualTemperature { get; set; } = 0.2;
}
