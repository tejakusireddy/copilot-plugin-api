using System.ComponentModel.DataAnnotations;

namespace CopilotPluginApi.Models;

/// <summary>
/// Represents a single turn in a stored conversation history.
/// </summary>
public sealed class ConversationTurn
{
    private const int RoleMaxLength = 16;
    private const int ContentMaxLength = 8000;

    /// <summary>
    /// Gets or initializes the role that produced the turn.
    /// </summary>
    [Required]
    [MaxLength(RoleMaxLength)]
    public required string Role { get; init; }

    /// <summary>
    /// Gets or initializes the content associated with the conversation turn.
    /// </summary>
    [Required]
    [MaxLength(ContentMaxLength)]
    public required string Content { get; init; }

    /// <summary>
    /// Gets or initializes the UTC timestamp for when the turn was created.
    /// </summary>
    public DateTime Timestamp { get; init; }
}
