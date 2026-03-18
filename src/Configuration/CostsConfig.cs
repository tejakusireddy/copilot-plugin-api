using System.ComponentModel.DataAnnotations;

namespace CopilotPluginApi.Configuration;

/// <summary>
/// Represents configuration for model pricing used in cost estimation.
/// </summary>
public sealed class CostsConfig
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Costs";

    /// <summary>
    /// Gets or sets the GPT-4o input price per thousand prompt tokens in USD.
    /// </summary>
    [Range(0D, double.MaxValue)]
    public double Gpt4oInputPerThousand { get; set; }

    /// <summary>
    /// Gets or sets the GPT-4o output price per thousand completion tokens in USD.
    /// </summary>
    [Range(0D, double.MaxValue)]
    public double Gpt4oOutputPerThousand { get; set; }

    /// <summary>
    /// Gets or sets the GPT-3.5 input price per thousand prompt tokens in USD.
    /// </summary>
    [Range(0D, double.MaxValue)]
    public double Gpt35InputPerThousand { get; set; }

    /// <summary>
    /// Gets or sets the GPT-3.5 output price per thousand completion tokens in USD.
    /// </summary>
    [Range(0D, double.MaxValue)]
    public double Gpt35OutputPerThousand { get; set; }
}
