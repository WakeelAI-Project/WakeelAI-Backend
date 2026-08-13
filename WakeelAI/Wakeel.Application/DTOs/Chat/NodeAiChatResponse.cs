using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Chat;

/// <summary>
/// Internal deserialization shape for the raw response returned by the Node.js AI service
/// from POST /api/ai/chat. This type is never returned to the client directly —
/// it is mapped into AskChatResponse before being sent.
/// </summary>
public record NodeAiChatResponse
{
    /// <summary>The conversation thread ID as assigned/tracked by the Node.js service.</summary>
    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>The AI-generated reply message text.</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>Knowledge sources cited by the AI in its reply.</summary>
    [JsonPropertyName("sources")]
    public List<CitationDto> Sources { get; init; } = new();

    /// <summary>Missing fields the AI still needs from the user to complete an action.</summary>
    [JsonPropertyName("missing_fields")]
    public object? MissingFields { get; init; }

    /// <summary>Structured result card (calculation, document_draft, or leave_draft).</summary>
    [JsonPropertyName("result_card")]
    public object? ResultCard { get; init; }
}
