using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopilotPluginApi.Models;
using StackExchange.Redis;

namespace CopilotPluginApi.Services;

/// <summary>
/// Defines the contract for storing and retrieving idempotent request responses.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Retrieves a previously stored response for the supplied request identifier.
    /// </summary>
    /// <param name="requestId">The client-supplied request identifier.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The stored response when one exists; otherwise, <see langword="null" />.</returns>
    Task<ChatResponse?> GetAsync(string requestId, CancellationToken ct = default);

    /// <summary>
    /// Stores a response for the supplied request identifier.
    /// </summary>
    /// <param name="requestId">The client-supplied request identifier.</param>
    /// <param name="response">The response to store.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetAsync(string requestId, ChatResponse response, CancellationToken ct = default);
}

/// <summary>
/// Stores idempotent responses in Redis using SHA-256 request keys.
/// </summary>
/// <param name="connectionMultiplexer">The Redis connection multiplexer.</param>
/// <param name="logger">The logger used for non-fatal Redis and serialization failures.</param>
public sealed class IdempotencyService(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<IdempotencyService> logger) : IIdempotencyService
{
    private const string RedisKeyPrefix = "idem:";
    private const int TimeToLiveSeconds = 60;
    private static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(TimeToLiveSeconds);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase database = connectionMultiplexer.GetDatabase();
    private readonly ILogger<IdempotencyService> serviceLogger = logger;

    /// <inheritdoc />
    public async Task<ChatResponse?> GetAsync(string requestId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("A non-empty request identifier is required.", nameof(requestId));
        }

        ct.ThrowIfCancellationRequested();

        var key = BuildRedisKey(requestId);

        try
        {
            var payload = await database.StringGetAsync(key).ConfigureAwait(false);
            if (payload.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<ChatResponse>(payload.ToString(), SerializerOptions);
        }
        catch (RedisException ex)
        {
            serviceLogger.LogWarning(
                ex,
                "Redis idempotency retrieval failed for request {RequestId}. Treating the response as a cache miss.",
                requestId);

            return null;
        }
        catch (JsonException ex)
        {
            serviceLogger.LogWarning(
                ex,
                "Failed to deserialize the stored idempotency response for request {RequestId}. Treating the response as a cache miss.",
                requestId);

            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string requestId, ChatResponse response, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("A non-empty request identifier is required.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(response);

        ct.ThrowIfCancellationRequested();

        var key = BuildRedisKey(requestId);

        try
        {
            var payload = JsonSerializer.Serialize(response, SerializerOptions);

            await database.StringSetAsync(
                key,
                payload,
                TimeToLive,
                when: When.NotExists).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            serviceLogger.LogWarning(
                ex,
                "Redis idempotency storage failed for request {RequestId}. Continuing without persisted idempotency state.",
                requestId);
        }
        catch (JsonException ex)
        {
            serviceLogger.LogWarning(
                ex,
                "Failed to serialize the idempotency response for request {RequestId}. Continuing without persisted idempotency state.",
                requestId);
        }
    }

    private static RedisKey BuildRedisKey(string requestId)
    {
        var requestIdBytes = Encoding.UTF8.GetBytes(requestId);
        var hashBytes = SHA256.HashData(requestIdBytes);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"{RedisKeyPrefix}{{{hash}}}";
    }
}
