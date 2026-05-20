using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GhostTextVsix.Diagnostics;
using GhostTextVsix.Settings;

namespace GhostTextVsix.Completion;

internal sealed class DeepSeekApiClient
{
    private readonly DeepSeekSettingsManager _settingsManager;
    private readonly DeepSeekOutputLogger _logger;
    private readonly HttpClient _httpClient = new();

    public DeepSeekApiClient(DeepSeekSettingsManager settingsManager, DeepSeekOutputLogger logger)
    {
        _settingsManager = settingsManager;
        _logger = logger;
    }

    public async Task<string> RequestCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var apiKey = _settingsManager.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("DeepSeek API key is not configured.");
        }

        var requestBody = new ChatCompletionRequest
        {
            Model = "deepseek-v4-flash",
            Temperature = 0.2,
            Messages = new[]
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = systemPrompt
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = userPrompt
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _settingsManager.GetEndpoint());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(Serialize(requestBody), Encoding.UTF8, "application/json");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_settingsManager.GetTimeout());

        var started = DateTimeOffset.UtcNow;
        _logger.Info("DeepSeek request started.");

        using var response = await _httpClient.SendAsync(request, linkedCts.Token);
        var content = await response.Content.ReadAsStringAsync();
        var latency = DateTimeOffset.UtcNow - started;

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error($"DeepSeek request failed. Status={(int)response.StatusCode}, LatencyMs={latency.TotalMilliseconds:F0}");
            throw new InvalidOperationException($"DeepSeek API returned {(int)response.StatusCode}.");
        }

        var responseObject = Deserialize<ChatCompletionResponse>(content);
        if (responseObject?.Choices == null || responseObject.Choices.Length == 0)
        {
            _logger.Error($"DeepSeek response missing choices. LatencyMs={latency.TotalMilliseconds:F0}");
            throw new InvalidOperationException("DeepSeek response did not contain choices.");
        }

        var text = responseObject.Choices[0]?.Message?.Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.Error($"DeepSeek response missing content. LatencyMs={latency.TotalMilliseconds:F0}");
            throw new InvalidOperationException("DeepSeek response did not contain completion content.");
        }

        _logger.Info($"DeepSeek request succeeded. LatencyMs={latency.TotalMilliseconds:F0}, CacheUsed=false");
        return text;
    }

    private static string Serialize<T>(T value)
    {
        var serializer = new DataContractJsonSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static T Deserialize<T>(string json)
    {
        var serializer = new DataContractJsonSerializer(typeof(T));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return (T)serializer.ReadObject(stream);
    }

    [DataContract]
    private sealed class ChatCompletionRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }

        [DataMember(Name = "temperature")]
        public double Temperature { get; set; }

        [DataMember(Name = "messages")]
        public ChatMessage[] Messages { get; set; }
    }

    [DataContract]
    private sealed class ChatMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }

        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    private sealed class ChatCompletionResponse
    {
        [DataMember(Name = "choices")]
        public Choice[] Choices { get; set; }
    }

    [DataContract]
    private sealed class Choice
    {
        [DataMember(Name = "message")]
        public ChatMessage Message { get; set; }
    }
}
