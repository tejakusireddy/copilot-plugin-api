using System.ComponentModel.DataAnnotations;

namespace CopilotPluginApi.Models;

/// <summary>
/// Represents an inbound conversational request submitted by a client.
/// </summary>
public sealed class ChatRequest
{
    private const int UserIdMaxLength = 128;
    private const int SessionIdMaxLength = 128;
    private const int RequestIdMaxLength = 36;
    private const int MessageMaxLength = 8000;

    /// <summary>
    /// Gets or initializes the user identifier used for rate limiting.
    /// </summary>
    [Required]
    [MaxLength(UserIdMaxLength)]
    public required string UserId { get; init; }

    /// <summary>
    /// Gets or initializes the session identifier used to retrieve conversation memory.
    /// </summary>
    [Required]
    [MaxLength(SessionIdMaxLength)]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or initializes the client-supplied request identifier used for idempotency.
    /// </summary>
    [Required]
    [MaxLength(RequestIdMaxLength)]
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets or initializes the user's message content.
    /// </summary>
    [Required]
    [MaxLength(MessageMaxLength)]
    public required string Message { get; init; }
}
