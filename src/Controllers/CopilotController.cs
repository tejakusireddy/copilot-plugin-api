using System.Diagnostics;
using CopilotPluginApi.Configuration;
using CopilotPluginApi.Data;
using CopilotPluginApi.Models;
using CopilotPluginApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CopilotPluginApi.Controllers;

/// <summary>
/// Exposes the primary conversational chat endpoint.
/// </summary>
[ApiController]
[Route("api/chat")]
public sealed class CopilotController : ControllerBase
{
    private const int RetryAfterSeconds = 60;
    private readonly IRateLimiterService rateLimiter;
    private readonly IIdempotencyService idempotency;
    private readonly IMemoryService memory;
    private readonly ISemanticCacheService semanticCache;
    private readonly IPromptBuilderService promptBuilder;
    private readonly ILlmOrchestratorService llmOrchestrator;
    private readonly IAuditLogger auditLogger;
    private readonly IOptions<PromptConfig> promptOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotController"/> class.
    /// </summary>
    /// <param name="rateLimiter">The per-user rate limiter.</param>
    /// <param name="idempotency">The idempotency response store.</param>
    /// <param name="memory">The conversation memory service.</param>
    /// <param name="semanticCache">The semantic cache service.</param>
    /// <param name="promptBuilder">The prompt assembly service.</param>
    /// <param name="llmOrchestrator">The LLM orchestration service.</param>
    /// <param name="auditLogger">The audit log persistence service.</param>
    /// <param name="promptOptions">The configured prompt options.</param>
    public CopilotController(
        IRateLimiterService rateLimiter,
        IIdempotencyService idempotency,
        IMemoryService memory,
        ISemanticCacheService semanticCache,
        IPromptBuilderService promptBuilder,
        ILlmOrchestratorService llmOrchestrator,
        IAuditLogger auditLogger,
        IOptions<PromptConfig> promptOptions)
    {
        this.rateLimiter = rateLimiter;
        this.idempotency = idempotency;
        this.memory = memory;
        this.semanticCache = semanticCache;
        this.promptBuilder = promptBuilder;
        this.llmOrchestrator = llmOrchestrator;
        this.auditLogger = auditLogger;
        this.promptOptions = promptOptions;
    }

    /// <summary>
    /// Executes a chat completion request through the configured orchestration pipeline.
    /// </summary>
    /// <param name="request">The validated chat request payload.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The chat response when the request succeeds.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ChatResponse>> PostAsync(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!await rateLimiter.IsAllowedAsync(request.UserId, ct).ConfigureAwait(false))
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new
                {
                    error = "Rate limit exceeded",
                    retryAfterSeconds = RetryAfterSeconds
                });
        }

        var existingResponse = await idempotency.GetAsync(request.RequestId, ct).ConfigureAwait(false);
        if (existingResponse is not null)
        {
            return Ok(existingResponse);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var cachedResponse = await semanticCache
                .GetAsync(request.UserId, request.SessionId, request.Message, ct)
                .ConfigureAwait(false);

            if (cachedResponse is not null)
            {
                var response = CreateResponse(
                    cachedResponse.Response,
                    cacheHit: true,
                    cachedResponse.Degraded,
                    cachedResponse.ModelUsed,
                    cachedResponse.PromptTokens,
                    cachedResponse.CompletionTokens,
                    stopwatch.Elapsed.TotalMilliseconds);

                await PersistConversationStateAsync(request, response, costEstimateUsd: 0D, ct).ConfigureAwait(false);
                return Ok(response);
            }

            var history = await memory.GetHistoryAsync(request.SessionId, ct).ConfigureAwait(false);
            var prompt = promptBuilder.Build(promptOptions.Value.SystemPrompt, history, request.Message);
            var llmResult = await llmOrchestrator.CompleteAsync(prompt, ct).ConfigureAwait(false);

            var llmResponse = CreateResponse(
                llmResult.Content,
                cacheHit: false,
                llmResult.Degraded,
                llmResult.ModelUsed,
                llmResult.PromptTokens,
                llmResult.CompletionTokens,
                stopwatch.Elapsed.TotalMilliseconds);

            await PersistConversationStateAsync(request, llmResponse, llmResult.CostEstimateUsd, ct).ConfigureAwait(false);
            return Ok(llmResponse);
        }
        catch (PromptTooLargeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task PersistConversationStateAsync(
        ChatRequest request,
        ChatResponse response,
        double costEstimateUsd,
        CancellationToken ct)
    {
        var timestamp = DateTime.UtcNow;

        await memory.AppendTurnAsync(
            request.SessionId,
            new ConversationTurn
            {
                Role = "user",
                Content = request.Message,
                Timestamp = timestamp
            },
            ct).ConfigureAwait(false);

        await memory.AppendTurnAsync(
            request.SessionId,
            new ConversationTurn
            {
                Role = "assistant",
                Content = response.Response,
                Timestamp = timestamp
            },
            ct).ConfigureAwait(false);

        await semanticCache
            .SetAsync(request.UserId, request.SessionId, request.Message, response, ct)
            .ConfigureAwait(false);

        await idempotency.SetAsync(request.RequestId, response, ct).ConfigureAwait(false);

        await auditLogger.LogAsync(
            new AuditLogEntry(
                request.UserId,
                request.SessionId,
                request.RequestId,
                response.PromptTokens,
                response.CompletionTokens,
                response.ModelUsed,
                response.LatencyMs,
                response.CacheHit,
                response.Degraded,
                (decimal)costEstimateUsd),
            ct).ConfigureAwait(false);
    }

    private static ChatResponse CreateResponse(
        string content,
        bool cacheHit,
        bool degraded,
        string modelUsed,
        int promptTokens,
        int completionTokens,
        double latencyMilliseconds) =>
        new()
        {
            Response = content,
            CacheHit = cacheHit,
            Degraded = degraded,
            ModelUsed = modelUsed,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            LatencyMs = Math.Round(latencyMilliseconds, 2, MidpointRounding.AwayFromZero)
        };
}
