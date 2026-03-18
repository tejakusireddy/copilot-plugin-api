using System.ComponentModel.DataAnnotations;

namespace CopilotPluginApi.Configuration;

/// <summary>
/// Represents configuration for session memory bounds and trimming.
/// </summary>
public sealed class MemoryConfig
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Memory";

    /// <summary>
    /// Gets or sets the maximum number of conversation turns to retain per session.
    /// </summary>
    [Range(1, 100)]
    public int MaxTurns { get; set; }

    /// <summary>
    /// Gets or sets the session time-to-live in hours.
    /// </summary>
    [Range(1, 168)]
    public int TtlHours { get; set; }

    /// <summary>
    /// Gets or sets the token budget reserved for history and the current user message.
    /// </summary>
    [Range(1, 32000)]
    public int TokenBudget { get; set; }
}
