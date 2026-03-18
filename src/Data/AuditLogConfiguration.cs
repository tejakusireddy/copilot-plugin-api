using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CopilotPluginApi.Data;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    private const string TableName = "audit_logs";
    private const string CurrentTimestampSql = "CURRENT_TIMESTAMP";
    private const int UserIdMaxLength = 128;
    private const int SessionIdMaxLength = 128;
    private const int RequestIdMaxLength = 36;
    private const int ModelUsedMaxLength = 64;

    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.Id)
            .ValueGeneratedNever();

        builder.Property(auditLog => auditLog.UserId)
            .HasColumnName("userId")
            .HasMaxLength(UserIdMaxLength)
            .IsRequired();

        builder.Property(auditLog => auditLog.SessionId)
            .HasColumnName("sessionId")
            .HasMaxLength(SessionIdMaxLength)
            .IsRequired();

        builder.Property(auditLog => auditLog.RequestId)
            .HasColumnName("requestId")
            .HasMaxLength(RequestIdMaxLength)
            .IsRequired();

        builder.Property(auditLog => auditLog.PromptTokens)
            .HasColumnName("prompt_tokens")
            .IsRequired();

        builder.Property(auditLog => auditLog.CompletionTokens)
            .HasColumnName("completion_tokens")
            .IsRequired();

        builder.Property(auditLog => auditLog.ModelUsed)
            .HasColumnName("model_used")
            .HasMaxLength(ModelUsedMaxLength)
            .IsRequired();

        builder.Property(auditLog => auditLog.LatencyMs)
            .HasColumnName("latency_ms")
            .IsRequired();

        builder.Property(auditLog => auditLog.CacheHit)
            .HasColumnName("cache_hit")
            .IsRequired();

        builder.Property(auditLog => auditLog.Degraded)
            .HasColumnName("degraded")
            .IsRequired();

        builder.Property(auditLog => auditLog.CostEstimateUsd)
            .HasColumnName("cost_estimate_usd")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(auditLog => auditLog.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(
                timestamp => timestamp,
                timestamp => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc))
            .HasDefaultValueSql(CurrentTimestampSql)
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.HasIndex(auditLog => new { auditLog.UserId, auditLog.CreatedAt });
    }
}
