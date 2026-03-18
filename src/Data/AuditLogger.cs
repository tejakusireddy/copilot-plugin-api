using Microsoft.EntityFrameworkCore;

namespace CopilotPluginApi.Data;

/// <summary>
/// Defines the contract for persisting audit log entries.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Persists an audit log entry without allowing audit failures to disrupt the request pipeline.
    /// </summary>
    /// <param name="entry">The audit entry to persist.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task LogAsync(AuditLogEntry entry, CancellationToken ct = default);
}

/// <summary>
/// Represents an immutable audit record for a completed request.
/// </summary>
/// <param name="UserId">The user identifier associated with the request.</param>
/// <param name="SessionId">The session identifier associated with the request.</param>
/// <param name="RequestId">The request identifier associated with the request.</param>
/// <param name="PromptTokens">The number of prompt tokens consumed.</param>
/// <param name="CompletionTokens">The number of completion tokens generated.</param>
/// <param name="ModelUsed">The model deployment that served the request.</param>
/// <param name="LatencyMs">The end-to-end latency in milliseconds.</param>
/// <param name="CacheHit">A value indicating whether the response was served from cache.</param>
/// <param name="Degraded">A value indicating whether the request completed in degraded mode.</param>
/// <param name="CostEstimateUsd">The estimated request cost in U.S. dollars.</param>
public sealed record AuditLogEntry(
    string UserId,
    string SessionId,
    string RequestId,
    int PromptTokens,
    int CompletionTokens,
    string ModelUsed,
    double LatencyMs,
    bool CacheHit,
    bool Degraded,
    decimal CostEstimateUsd);

/// <summary>
/// Writes audit log records to the database.
/// </summary>
/// <param name="dbContext">The database context used for persistence.</param>
/// <param name="logger">The logger used for non-fatal audit failures.</param>
public sealed class AuditLogger(AppDbContext dbContext, ILogger<AuditLogger> logger) : IAuditLogger
{
    /// <inheritdoc />
    public async Task LogAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var entity = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = entry.UserId,
            SessionId = entry.SessionId,
            RequestId = entry.RequestId,
            PromptTokens = entry.PromptTokens,
            CompletionTokens = entry.CompletionTokens,
            ModelUsed = entry.ModelUsed,
            LatencyMs = entry.LatencyMs,
            CacheHit = entry.CacheHit,
            Degraded = entry.Degraded,
            CostEstimateUsd = entry.CostEstimateUsd,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await dbContext.AuditLogs.AddAsync(entity, ct);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist audit log entry for request {RequestId}.",
                entry.RequestId);
        }
    }
}
