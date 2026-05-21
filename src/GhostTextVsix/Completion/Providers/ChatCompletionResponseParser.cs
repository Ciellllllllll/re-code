using System;
using System.Text.Json;

namespace GhostTextVsix.Completion.Providers;

internal static class ChatCompletionResponseParser
{
    public static ChatCompletionParseResult Parse(string json)
    {
        var result = new ChatCompletionParseResult();
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
}
