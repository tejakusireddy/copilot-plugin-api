using Microsoft.EntityFrameworkCore;

namespace CopilotPluginApi.Data;

/// <summary>
/// Represents the Entity Framework Core database context for the application.
/// </summary>
/// <param name="options">The database context options.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the audit log entries stored for request analytics and operational reporting.
    /// </summary>
    internal DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}

internal sealed class AuditLog
{
    internal Guid Id { get; init; }

    internal required string UserId { get; init; }

    internal required string SessionId { get; init; }

    internal required string RequestId { get; init; }

    internal int PromptTokens { get; init; }

    internal int CompletionTokens { get; init; }

    internal required string ModelUsed { get; init; }

    internal double LatencyMs { get; init; }

    internal bool CacheHit { get; init; }

    internal bool Degraded { get; init; }

    internal decimal CostEstimateUsd { get; init; }

    internal DateTime CreatedAt { get; init; }
}
