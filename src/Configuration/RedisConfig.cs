using System.ComponentModel.DataAnnotations;

namespace CopilotPluginApi.Configuration;

/// <summary>
/// Represents configuration for Redis connectivity.
/// </summary>
public sealed class RedisConfig
{
    private const int ConnectionStringMaxLength = 512;

    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    [Required]
    [MaxLength(ConnectionStringMaxLength)]
    public string ConnectionString { get; set; } = string.Empty;
}
