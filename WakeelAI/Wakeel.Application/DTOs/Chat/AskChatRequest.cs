using System;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Wakeel.Application.DTOs.Chat;

/// <summary>
/// The request body sent by the client (Web/Mobile) to POST /api/chat/ask.
/// </summary>
public record AskChatRequest
{
    /// <summary>
    /// The ID of an existing conversation thread. If null or empty, .NET will
    /// generate a new UUID and start a new conversation.
    /// </summary>
    [JsonPropertyName("conversation_id")]
    public string? ConversationId { get; init; }

    /// <summary>The user's message text.</summary>
    [JsonPropertyName("message")]
    [Required(ErrorMessage = "message is required.")]
    public string Message { get; init; } = string.Empty;

    /// <summary>The preferred response language (e.g., "AR", "EN").</summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// Optional field values collected via missing_fields forms
    /// for multi-turn skill interactions.
    /// </summary>
    [JsonPropertyName("field_values")]
    public object? FieldValues { get; init; }
}
