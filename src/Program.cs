using CopilotPluginApi.Configuration;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var repositoryRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));
var localAppSettingsPath = Path.Combine(builder.Environment.ContentRootPath, ApplicationConfigurationFiles.Default);
var repositoryAppSettingsPath = Path.Combine(repositoryRoot, ApplicationConfigurationFiles.Default);

if (!File.Exists(localAppSettingsPath) && File.Exists(repositoryAppSettingsPath))
{
    builder.Configuration
        .SetBasePath(repositoryRoot)
        .AddJsonFile(ApplicationConfigurationFiles.Default, optional: false, reloadOnChange: true)
        .AddJsonFile(
            ApplicationConfigurationFiles.GetEnvironmentSpecific(builder.Environment.EnvironmentName),
            optional: true,
            reloadOnChange: true);
}

// Config
builder.Services
    .AddOptions<AzureOpenAIConfig>()
    .BindConfiguration(AzureOpenAIConfig.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RedisConfig>()
    .BindConfiguration(RedisConfig.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<MemoryConfig>()
    .BindConfiguration(MemoryConfig.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RateLimitConfig>()
    .BindConfiguration(RateLimitConfig.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<CostsConfig>()
    .BindConfiguration(CostsConfig.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Redis
// TODO: Register Redis connectivity and related infrastructure when the Redis layer exists.

// Database
// TODO: Register the application database context and audit persistence when the data layer exists.

// Services
// TODO: Register application services when their implementations are added.

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

internal static class ApplicationConfigurationFiles
{
    internal const string Default = "appsettings.json";

    internal static string GetEnvironmentSpecific(string environmentName) =>
        $"appsettings.{environmentName}.json";
}
