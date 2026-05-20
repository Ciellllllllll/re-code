using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GhostTextVsix.Diagnostics;

namespace GhostTextVsix.Completion.Providers;

internal class OpenAICompatibleCompletionProvider : ICompletionProvider
{
    private static readonly HttpClient HttpClient = new();
    private readonly DeepSeekOutputLogger _logger;
    private readonly CompletionProviderConfig _config;

    public OpenAICompatibleCompletionProvider(CompletionProviderConfig config, DeepSeekOutputLogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public virtual string ProviderName => _config.ProviderName;

    public virtual bool SupportsChatCompletions => _config.SupportsChatCompletions;

    public virtual bool SupportsFimCompletions => _config.SupportsFimCompletions;

    public virtual bool SupportsStreaming => _config.SupportsStreaming;

    public async Task<CompletionProviderResponse> GenerateCompletionAsync(
        CompletionProviderRequest request,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var config = request.Config ?? _config;
        var response = new CompletionProviderResponse
        {
            ProviderName = ProviderName,
            ModelName = request.ModelName,
            RequestId = request.RequestId,
            Source = request.Source
        };

        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            response.ErrorMessage = "Provider base url is not configured.";
            _logger.Error($"Provider request failed. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}, Error={response.ErrorMessage}, LatencyMs={ElapsedMs(started):F0}");
            return response;
        }

        if (!config.IsLocal && string.IsNullOrWhiteSpace(config.ApiKey))
        {
            response.ErrorMessage = "Provider API key is not configured.";
            _logger.Warning($"Provider skipped due to missing api key. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}");
            return response;
        }

        var requestBody = CreateRequestBody(request);

        var requestUri = BuildRequestUri(config);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        AddProviderHeaders(httpRequest);
        httpRequest.Content = new StringContent(Serialize(requestBody), Encoding.UTF8, "application/json");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(request.Timeout);

        _logger.Info($"Provider request started. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}, RequestUrlHost={requestUri.Host}, RequestPath={requestUri.AbsolutePath}, MaxTokens={request.MaxTokens}, TimeoutMs={request.Timeout.TotalMilliseconds:F0}");

        try
        {
            using var httpResponse = await HttpClient.SendAsync(httpRequest, linkedCts.Token);
            var content = await httpResponse.Content.ReadAsStringAsync();
            response.LatencyMs = ElapsedMs(started);

            if (!httpResponse.IsSuccessStatusCode)
            {
                response.ErrorMessage = $"Provider returned HTTP {(int)httpResponse.StatusCode}.";
                LogFailure(request, response.ErrorMessage, started, requestUri);
                return response;
            }

            var parsed = ParseCompletionResponse(content);
            _logger.Info($"Provider response parsed. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}, HasChoices={parsed.HasChoices}, ChoicesCount={parsed.ChoicesCount}, HasMessage={parsed.HasMessage}, HasContent={parsed.HasContent}, ContentLength={parsed.ContentLength}, HasReasoningContent={parsed.HasReasoningContent}, ReasoningContentLength={parsed.ReasoningContentLength}, FinishReason={parsed.FinishReason}, HasErrorObject={parsed.HasErrorObject}, ErrorType={parsed.ErrorType}, ErrorCode={parsed.ErrorCode}, ErrorMessageSummary={parsed.ErrorMessageSummary}");

            if (parsed.HasErrorObject)
            {
                response.ErrorMessage = $"Provider error. Type={parsed.ErrorType}, Code={parsed.ErrorCode}, Message={parsed.ErrorMessageSummary}";
                LogFailure(request, response.ErrorMessage, started, requestUri);
                return response;
            }

            if (string.IsNullOrWhiteSpace(parsed.Text))
            {
                if (string.Equals(parsed.FinishReason, "length", StringComparison.OrdinalIgnoreCase) && !parsed.HasContent)
                {
                    _logger.Warning($"Completion content empty because generation reached max_tokens. Consider disabling thinking or increasing max_tokens. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}, HasReasoningContent={parsed.HasReasoningContent}, ReasoningContentLength={parsed.ReasoningContentLength}");
                }

                response.ErrorMessage = "Provider response did not contain completion content.";
                LogFailure(request, response.ErrorMessage, started, requestUri);
                return response;
            }

            response.Text = parsed.Text;
            response.IsSuccess = true;
            _logger.Info($"Provider request succeeded. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}, LatencyMs={response.LatencyMs:F0}");
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            response.LatencyMs = ElapsedMs(started);
            response.ErrorMessage = "Provider request timed out.";
            _logger.Error($"Provider timeout. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}, TimeoutMs={request.Timeout.TotalMilliseconds:F0}, LatencyMs={response.LatencyMs:F0}");
            return response;
        }
        catch (Exception ex)
        {
            response.LatencyMs = ElapsedMs(started);
            response.ErrorMessage = ex.GetType().Name;
            LogFailure(request, response.ErrorMessage, started, requestUri);
            return response;
        }
    }

    protected virtual Uri BuildRequestUri(CompletionProviderConfig config)
    {
        return new Uri(config.BaseUrl, UriKind.Absolute);
    }

    protected virtual ChatCompletionRequest CreateRequestBody(CompletionProviderRequest request)
    {
        return new ChatCompletionRequest
        {
            Model = request.ModelName,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens > 0 ? request.MaxTokens : (int?)null,
            Stream = false,
            Messages = new[]
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = request.SystemPrompt
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = request.UserPrompt
                }
            }
        };
    }

    protected virtual void AddProviderHeaders(HttpRequestMessage request)
    {
    }

    private void LogFailure(CompletionProviderRequest request, string errorMessage, DateTimeOffset started, Uri requestUri)
    {
        _logger.Error($"Provider request failed. ProviderName={ProviderName}, ModelName={request.ModelName}, RequestId={request.RequestId}, Source={request.Source}, CompletionMode={request.CompletionMode}, RequestUrlHost={requestUri.Host}, RequestPath={requestUri.AbsolutePath}, Error={errorMessage}, LatencyMs={ElapsedMs(started):F0}");
    }

    private static double ElapsedMs(DateTimeOffset started)
    {
        return (DateTimeOffset.UtcNow - started).TotalMilliseconds;
    }

    private static string Serialize<T>(T value)
    {
        var serializer = new DataContractJsonSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static CompletionParseResult ParseCompletionResponse(string json)
    {
        var result = new CompletionParseResult();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (TryGetProperty(root, "error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            result.HasErrorObject = true;
            result.ErrorType = GetStringProperty(error, "type");
            result.ErrorCode = GetStringProperty(error, "code");
            result.ErrorMessageSummary = Summarize(GetStringProperty(error, "message"));
        }

        if (!TryGetProperty(root, "choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        result.HasChoices = true;
        result.ChoicesCount = choices.GetArrayLength();
        if (result.ChoicesCount == 0)
        {
            return result;
        }

        var firstChoice = choices[0];
        result.FinishReason = GetStringProperty(firstChoice, "finish_reason");

        if (!TryGetProperty(firstChoice, "message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        result.HasMessage = true;
        var content = GetStringProperty(message, "content");
        result.Text = content;
        result.HasContent = !string.IsNullOrWhiteSpace(content);
        result.ContentLength = content?.Length ?? 0;
        var reasoningContent = GetStringProperty(message, "reasoning_content");
        result.HasReasoningContent = !string.IsNullOrWhiteSpace(reasoningContent);
        result.ReasoningContentLength = reasoningContent?.Length ?? 0;
        return result;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string GetStringProperty(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return string.Empty;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString() ?? string.Empty;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return value.ToString();
            default:
                return string.Empty;
        }
    }

    private static string Summarize(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var singleLine = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= 120 ? singleLine : singleLine.Substring(0, 120);
    }

    [DataContract]
    protected sealed class ChatCompletionRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }

        [DataMember(Name = "temperature")]
        public double Temperature { get; set; }

        [DataMember(Name = "max_tokens", EmitDefaultValue = false)]
        public int? MaxTokens { get; set; }

        [DataMember(Name = "stream")]
        public bool Stream { get; set; }

        [DataMember(Name = "messages")]
        public ChatMessage[] Messages { get; set; }

        [DataMember(Name = "thinking", EmitDefaultValue = false)]
        public ThinkingOptions Thinking { get; set; }

        [DataMember(Name = "response_format", EmitDefaultValue = false)]
        public ResponseFormatOptions ResponseFormat { get; set; }
    }

    [DataContract]
    protected sealed class ChatMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }

        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    protected sealed class ThinkingOptions
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
    }

    [DataContract]
    protected sealed class ResponseFormatOptions
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
    }

    private sealed class CompletionParseResult
    {
        public string Text { get; set; }

        public bool HasChoices { get; set; }

        public int ChoicesCount { get; set; }

        public bool HasMessage { get; set; }

        public bool HasContent { get; set; }

        public int ContentLength { get; set; }

        public bool HasReasoningContent { get; set; }

        public int ReasoningContentLength { get; set; }

        public string FinishReason { get; set; } = string.Empty;

        public bool HasErrorObject { get; set; }

        public string ErrorType { get; set; } = string.Empty;

        public string ErrorCode { get; set; } = string.Empty;

        public string ErrorMessageSummary { get; set; } = string.Empty;
    }
}
