using System.ComponentModel.DataAnnotations;

namespace CopilotPluginApi.Models;

/// <summary>
/// Represents the API response returned for a conversational request.
/// </summary>
public sealed class ChatResponse
{
    private const int ResponseMaxLength = 32000;
    private const int ModelUsedMaxLength = 64;

    /// <summary>
    /// Gets or initializes the LLM-generated response payload.
    /// </summary>
    [Required]
    [MaxLength(ResponseMaxLength)]
    public required string Response { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the response was served from the semantic cache.
    /// </summary>
    public bool CacheHit { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the response was degraded because of fallback or partial-service behavior.
    /// </summary>
    public bool Degraded { get; init; }

    /// <summary>
    /// Gets or initializes the model deployment that produced the response.
    /// </summary>
    [Required]
    [MaxLength(ModelUsedMaxLength)]
    public required string ModelUsed { get; init; }

    /// <summary>
    /// Gets or initializes the number of prompt tokens consumed to serve the request.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PromptTokens { get; init; }

    /// <summary>
    /// Gets or initializes the number of completion tokens generated to serve the request.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Gets or initializes the end-to-end response latency in milliseconds.
    /// </summary>
    [Range(0D, double.MaxValue)]
    public double LatencyMs { get; init; }
}
