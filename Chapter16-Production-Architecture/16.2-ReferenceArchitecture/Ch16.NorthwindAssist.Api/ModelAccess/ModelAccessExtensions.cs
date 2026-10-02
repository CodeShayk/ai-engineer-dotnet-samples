using Ch16.Resilience;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Security;

namespace Ch16.NorthwindAssist.Api.ModelAccess;

/// <summary>
/// The model access layer (Chapter 16.2): every model call passes through redaction, telemetry,
/// a fallback between deployments and a resilience pipeline per deployment. Clients behind a
/// pipeline are created with sdkRetries: false, so the pipeline is the only place that retries.
/// </summary>
public static class ModelAccessExtensions
{
    public static IServiceCollection AddNorthwindModelAccess(
        this IServiceCollection services, AIProviderOptions aiOptions, IConfiguration configuration)
    {
        services.AddSingleton(aiOptions);

        // Outermost first: redact before anything can record content, then trace each call that
        // reaches the deployments, then fall back between independently resilient deployments.
        services
            .AddChatClient(sp => CreateDeployments(sp, aiOptions, configuration))
            .Use((inner, sp) => new RedactingChatClient(
                inner, sp.GetRequiredService<ISensitiveDataRedactor>(), sp.GetRequiredService<ILogger<RedactingChatClient>>()))
            .UseOpenTelemetry(sourceName: "Northwind.AI", configure: telemetry => telemetry.EnableSensitiveData = false);

        // A smaller model for routing-style work such as query rewriting and reranking.
        services
            .AddKeyedChatClient("small", sp => new ResilientChatClient(
                AIClientFactory.CreateChatClient(aiOptions, aiOptions.SmallChatDeployment, sdkRetries: false), ModelResilience.CreatePipeline()))
            .UseOpenTelemetry(sourceName: "Northwind.AI");

        services
            .AddEmbeddingGenerator(_ => AIClientFactory.CreateEmbeddingGenerator(aiOptions))
            .UseOpenTelemetry(sourceName: "Northwind.AI");

        return services;
    }

    /// <summary>
    /// The primary deployment and, when AI:Fallback:ChatDeployment is configured, a secondary one,
    /// optionally on another endpoint such as a different region. Each has its own pipeline, so
    /// each has its own circuit breaker.
    /// </summary>
    private static IChatClient CreateDeployments(IServiceProvider services, AIProviderOptions aiOptions, IConfiguration configuration)
    {
        var deployments = new List<IChatClient>
        {
            new ResilientChatClient(AIClientFactory.CreateChatClient(aiOptions, sdkRetries: false), ModelResilience.CreatePipeline())
        };

        string? fallbackDeployment = configuration["AI:Fallback:ChatDeployment"];
        if (!string.IsNullOrWhiteSpace(fallbackDeployment))
        {
            AIProviderOptions fallback = aiOptions with
            {
                Endpoint = configuration["AI:Fallback:Endpoint"] ?? aiOptions.Endpoint,
                ChatDeployment = fallbackDeployment
            };

            deployments.Add(new ResilientChatClient(AIClientFactory.CreateChatClient(fallback, sdkRetries: false), ModelResilience.CreatePipeline()));
        }

        return deployments.Count == 1
            ? deployments[0]
            : new FallbackChatClient(deployments, services.GetRequiredService<ILogger<FallbackChatClient>>());
    }
}
