using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Northwind.Shared.AI;

/// <summary>
/// Loads configuration the same way in every console sample: an optional
/// appsettings.json next to the executable, the shared User Secrets store,
/// then environment variables (which win).
/// </summary>
public static class SampleConfiguration
{
    /// <summary>Matches the UserSecretsId set in Directory.Build.props.</summary>
    public const string UserSecretsId = "northwind-ai-engineer-samples";

    public static IConfiguration Load() =>
        new ConfigurationBuilder()
            .AddNorthwindSampleSources()
            .Build();

    public static IConfigurationBuilder AddNorthwindSampleSources(this IConfigurationBuilder builder) =>
        builder
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(UserSecretsId)
            .AddEnvironmentVariables();

    /// <summary>Reads and validates the AI section in one call.</summary>
    public static AIProviderOptions LoadAIOptions() => AIProviderOptions.FromConfiguration(Load());
}

/// <summary>Dependency injection helpers used by the hosted samples.</summary>
public static class NorthwindAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers the configured chat model as <see cref="IChatClient"/> and returns the
    /// builder so the caller can add middleware such as function invocation or telemetry.
    /// </summary>
    public static ChatClientBuilder AddNorthwindChatClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        AIProviderOptions options = services.AddNorthwindAIOptions(configuration);
        return services.AddChatClient(_ => AIClientFactory.CreateChatClient(options));
    }

    /// <summary>Registers the configured embedding model as <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.</summary>
    public static EmbeddingGeneratorBuilder<string, Embedding<float>> AddNorthwindEmbeddingGenerator(
        this IServiceCollection services, IConfiguration configuration)
    {
        AIProviderOptions options = services.AddNorthwindAIOptions(configuration);
        return services.AddEmbeddingGenerator(_ => AIClientFactory.CreateEmbeddingGenerator(options));
    }

    private static AIProviderOptions AddNorthwindAIOptions(this IServiceCollection services, IConfiguration configuration)
    {
        AIProviderOptions options = AIProviderOptions.FromConfiguration(configuration);
        services.TryAddSingleton(options);
        return options;
    }
}
