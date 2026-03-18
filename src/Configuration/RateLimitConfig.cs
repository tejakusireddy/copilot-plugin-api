using System.ComponentModel.DataAnnotations;

namespace CopilotPluginApi.Configuration;

/// <summary>
/// Represents configuration for request rate limiting.
/// </summary>
public sealed class RateLimitConfig
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Gets or sets the maximum number of allowed requests per minute for a user.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int RequestsPerMinute { get; set; }
}
