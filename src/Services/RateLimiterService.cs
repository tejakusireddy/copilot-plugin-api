using CopilotPluginApi.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CopilotPluginApi.Services;

/// <summary>
/// Defines the contract for enforcing per-user request rate limits.
/// </summary>
public interface IRateLimiterService
{
    /// <summary>
    /// Determines whether the specified user is allowed to make a request at the current moment.
    /// </summary>
    /// <param name="userId">The user identifier used as the rate-limit bucket key.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>
    /// <see langword="true" /> when the request is allowed; otherwise, <see langword="false" />.
    /// </returns>
    Task<bool> IsAllowedAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Enforces per-user request rate limits using an atomic Redis token bucket.
/// </summary>
/// <param name="connectionMultiplexer">The Redis connection multiplexer.</param>
/// <param name="rateLimitOptions">The configured rate-limit options.</param>
/// <param name="timeProvider">The time provider used to calculate token refill.</param>
/// <param name="logger">The logger used for non-fatal Redis failures.</param>
public sealed class RateLimiterService(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RateLimitConfig> rateLimitOptions,
    TimeProvider timeProvider,
    ILogger<RateLimiterService> logger) : IRateLimiterService
{
    private const string RedisKeyPrefix = "ratelimit:";
    private const int WindowDurationSeconds = 60;
    private const int WindowDurationMilliseconds = WindowDurationSeconds * 1000;

    // Atomically refills the bucket based on elapsed time and decrements a token when available.
    private const string TokenBucketScript = """
        local key = KEYS[1]
        local capacity = tonumber(ARGV[1])
        local nowMs = tonumber(ARGV[2])
        local windowMs = tonumber(ARGV[3])
        local ttlSeconds = tonumber(ARGV[4])

        local bucket = redis.call('HMGET', key, 'tokens', 'updated_at_ms')
        local tokens = tonumber(bucket[1])
        local updatedAtMs = tonumber(bucket[2])

        if tokens == nil then
            tokens = capacity
            updatedAtMs = nowMs
        else
            local elapsedMs = math.max(0, nowMs - updatedAtMs)
            local refill = (elapsedMs * capacity) / windowMs
            tokens = math.min(capacity, tokens + refill)
        end

        local allowed = 0
        if tokens >= 1 then
            tokens = tokens - 1
            allowed = 1
        end

        redis.call('HMSET', key, 'tokens', tokens, 'updated_at_ms', nowMs)
        redis.call('EXPIRE', key, ttlSeconds)

        return allowed
        """;

    private readonly IDatabase database = connectionMultiplexer.GetDatabase();
    private readonly int requestsPerMinute = rateLimitOptions.Value.RequestsPerMinute;
    private readonly TimeProvider systemTimeProvider = timeProvider;
    private readonly ILogger<RateLimiterService> serviceLogger = logger;

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("A non-empty user identifier is required.", nameof(userId));
        }

        ct.ThrowIfCancellationRequested();

        var key = BuildRedisKey(userId);
        var nowMilliseconds = systemTimeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        try
        {
            var result = await database.ScriptEvaluateAsync(
                TokenBucketScript,
                [key],
                [requestsPerMinute, nowMilliseconds, WindowDurationMilliseconds, WindowDurationSeconds]).ConfigureAwait(false);

            return (int)result == 1;
        }
        catch (RedisException ex)
        {
            serviceLogger.LogWarning(
                ex,
                "Redis rate limiting failed for user {UserId}. Allowing the request to proceed.",
                userId);

            return true;
        }
    }

    private static RedisKey BuildRedisKey(string userId) => $"{RedisKeyPrefix}{{{userId}}}";
}
