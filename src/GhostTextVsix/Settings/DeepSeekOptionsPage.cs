using System.ComponentModel;
using GhostTextVsix.Completion.Providers;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Settings;

[TypeConverter(typeof(DeepSeekOptionsPageTypeConverter))]
public sealed class DeepSeekOptionsPage : DialogPage
{
    private CompletionProviderType _autoCompletionProvider = CompletionProviderType.NotConfigured;
    private string _autoCompletionModel = string.Empty;

    [Category("DeepSeek 互換設定")]
    [DisplayName("API キー")]
    [Description("DeepSeek API キー。空欄の場合は DEEPSEEK_API_KEY 環境変数を使用します。")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [Browsable(false)]
    public string ApiKey { get; set; } = string.Empty;

    [Category("DeepSeek 互換設定")]
    [DisplayName("エンドポイント")]
    [Description("OpenAI 互換の DeepSeek chat completions エンドポイント。")]
    [DefaultValue("https://api.deepseek.com/chat/completions")]
    [Browsable(false)]
    public string Endpoint { get; set; } = "https://api.deepseek.com/chat/completions";

    [Category("手動補完")]
    [DisplayName("手動補完タイムアウト秒数")]
    [Description("手動補完リクエストのタイムアウト秒数。")]
    [DefaultValue(20)]
    [Browsable(false)]
    public int TimeoutSeconds { get; set; } = 20;

    [Category("プロバイダー")]
    [DisplayName("自動補完プロバイダー")]
    [Description("自動補完で使用する補完プロバイダー。")]
    [DefaultValue(CompletionProviderType.NotConfigured)]
    [RefreshProperties(RefreshProperties.All)]
    [PropertyOrder(10)]
    public CompletionProviderType AutoCompletionProvider
    {
        get => _autoCompletionProvider;
        set
        {
            _autoCompletionProvider = value;
            _autoCompletionModel = ProviderRegistry.ResolveModelNameForProviderChange(value, _autoCompletionModel);
        }
    }

    [Category("プロバイダー")]
    [DisplayName("自動補完モデル")]
    [Description("自動補完で使用するモデル名。")]
    [DefaultValue("")]
    [TypeConverter(typeof(CompletionModelTypeConverter))]
    [PropertyOrder(20)]
    public string AutoCompletionModel
    {
        get => _autoCompletionModel;
        set => _autoCompletionModel = value ?? string.Empty;
    }

    [Category("プロバイダー")]
    [DisplayName("自動補完ベース URL")]
    [Description("自動補完で使用する OpenAI 互換 chat completions エンドポイント。空欄の場合は選択中プロバイダーの既定値を使用します。")]
    [DefaultValue("")]
    [Browsable(false)]
    public string AutoCompletionBaseUrl { get; set; } = string.Empty;

    [Category("プロバイダー")]
    [DisplayName("自動補完 API キー")]
    [Description("自動補完で使用するプロバイダー API キー。")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [PropertyOrder(30)]
    public string AutoCompletionApiKey { get; set; } = string.Empty;

    [Category("プロバイダー")]
    [DisplayName("手動補完プロバイダー")]
    [Description("手動補完で使用する補完プロバイダー。")]
    [DefaultValue(CompletionProviderType.NotConfigured)]
    [Browsable(false)]
    public CompletionProviderType ManualCompletionProvider { get; set; } = CompletionProviderType.NotConfigured;

    [Category("プロバイダー")]
    [DisplayName("手動補完モデル")]
    [Description("手動補完で使用するモデル名。")]
    [DefaultValue("")]
    [Browsable(false)]
    public string ManualCompletionModel { get; set; } = string.Empty;

    [Category("プロバイダー")]
    [DisplayName("手動補完ベース URL")]
    [Description("手動補完で使用する OpenAI 互換 chat completions エンドポイント。空欄の場合は選択中プロバイダーの既定値を使用します。")]
    [DefaultValue("")]
    [Browsable(false)]
    public string ManualCompletionBaseUrl { get; set; } = string.Empty;

    [Category("プロバイダー")]
    [DisplayName("手動補完 API キー")]
    [Description("手動補完で使用するプロバイダー API キー。")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [Browsable(false)]
    public string ManualCompletionApiKey { get; set; } = string.Empty;

    [Category("プロバイダー")]
    [DisplayName("OpenRouter API キー")]
    [Description("OpenRouter API キー。空欄の場合は OPENROUTER_API_KEY 環境変数を使用します。")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [Browsable(false)]
    public string OpenRouterApiKey { get; set; } = string.Empty;

    [Category("プロバイダー")]
    [DisplayName("OpenAI 互換 API キー")]
    [Description("汎用 OpenAI 互換プロバイダーの API キー。")]
    [DefaultValue("")]
    [PasswordPropertyText(true)]
    [Browsable(false)]
    public string OpenAICompatibleApiKey { get; set; } = string.Empty;

    [Category("プロバイダー")]
    [DisplayName("ローカルベース URL")]
    [Description("ローカル OpenAI 互換 chat completions エンドポイント。")]
    [DefaultValue("http://localhost:1234/v1/chat/completions")]
    [Browsable(false)]
    public string LocalBaseUrl { get; set; } = "http://localhost:1234/v1/chat/completions";

    [Category("補完")]
    [DisplayName("自動補完を有効化")]
    [Description("対応する C/C++ ファイルで入力停止後に自動で補完をリクエストします。")]
    [DefaultValue(false)]
    [PropertyOrder(100)]
    public bool EnableAutoCompletion { get; set; } = false;

    [Category("補完")]
    [DisplayName("デバウンス時間ミリ秒")]
    [Description("最後の入力から自動補完を開始するまでの待機時間。")]
    [DefaultValue(500)]
    [PropertyOrder(110)]
    public int DebounceMilliseconds { get; set; } = 500;

    [Category("補完")]
    [DisplayName("キャッシュ保持秒数")]
    [Description("同じエディターコンテキストで整形済み補完を再利用する時間。")]
    [DefaultValue(30)]
    [PropertyOrder(120)]
    public int CacheTtlSeconds { get; set; } = 30;

    [Category("手動補完")]
    [DisplayName("手動補完プレフィックス行数")]
    [Description("手動補完でカーソル前から収集する最大行数。")]
    [DefaultValue(120)]
    [Browsable(false)]
    public int MaxPrefixLines { get; set; } = 120;

    [Category("手動補完")]
    [DisplayName("手動補完サフィックス行数")]
    [Description("手動補完でカーソル後から収集する最大行数。")]
    [DefaultValue(60)]
    [Browsable(false)]
    public int MaxSuffixLines { get; set; } = 60;

    [Category("自動補完")]
    [DisplayName("自動補完プレフィックス行数")]
    [Description("自動補完でカーソル前から収集する最大行数。")]
    [DefaultValue(40)]
    [Browsable(false)]
    public int AutoMaxPrefixLines { get; set; } = 40;

    [Category("自動補完")]
    [DisplayName("自動補完サフィックス行数")]
    [Description("自動補完でカーソル後から収集する最大行数。")]
    [DefaultValue(10)]
    [Browsable(false)]
    public int AutoMaxSuffixLines { get; set; } = 10;

    [Category("自動補完")]
    [DisplayName("自動補完最大表示行数")]
    [Description("自動補完で表示する補完候補の最大行数。")]
    [DefaultValue(3)]
    [Browsable(false)]
    public int AutoMaxCompletionLines { get; set; } = 3;

    [Category("自動補完")]
    [DisplayName("自動補完最大表示文字数")]
    [Description("自動補完で表示する補完候補の最大文字数。")]
    [DefaultValue(300)]
    [Browsable(false)]
    public int AutoMaxCompletionCharacters { get; set; } = 300;

    [Category("自動補完")]
    [DisplayName("自動補完タイムアウトミリ秒")]
    [Description("自動補完リクエストのタイムアウト。")]
    [DefaultValue(2000)]
    [Browsable(false)]
    public int AutoTimeoutMilliseconds { get; set; } = 2000;

    [Category("自動補完")]
    [DisplayName("自動補完最大トークン数")]
    [Description("自動補完でプロバイダーへ送信する max_tokens の値。")]
    [DefaultValue(128)]
    [PropertyOrder(130)]
    public int AutoMaxTokens { get; set; } = 128;

    [Category("自動補完")]
    [DisplayName("自動補完 Temperature")]
    [Description("自動補完でプロバイダーへ送信する temperature の値。")]
    [DefaultValue(0.1)]
    [Browsable(false)]
    public double AutoTemperature { get; set; } = 0.1;

    [Category("手動補完")]
    [DisplayName("手動補完最大トークン数")]
    [Description("手動補完でプロバイダーへ送信する max_tokens の値。0 の場合は送信しません。")]
    [DefaultValue(0)]
    [Browsable(false)]
    public int ManualMaxTokens { get; set; } = 0;

    [Category("手動補完")]
    [DisplayName("手動補完 Temperature")]
    [Description("手動補完でプロバイダーへ送信する temperature の値。")]
    [DefaultValue(0.2)]
    [Browsable(false)]
    public double ManualTemperature { get; set; } = 0.2;
}
