using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

namespace Ch16.NorthwindAssist.Api.Knowledge;

/// <summary>
/// Builds the knowledge base once and shares it. In this local mode, ingestion runs in the
/// background at startup; in production it runs as a separate job against a real vector store.
/// </summary>
public sealed class KnowledgeBaseProvider(AIProviderOptions aiOptions, IEmbeddingGenerator<string, Embedding<float>> embedder)
{
    private readonly Lazy<Task<PolicyKnowledgeBase>> _knowledge =
        new(() => PolicyKnowledgeBase.CreateInMemoryAsync(aiOptions, embedder));

    public bool IsReady => _knowledge.IsValueCreated && _knowledge.Value.IsCompletedSuccessfully;

    public Task<PolicyKnowledgeBase> GetAsync() => _knowledge.Value;
}

/// <summary>Starts ingestion when the application starts, so the first customer does not wait for it.</summary>
public sealed class KnowledgeWarmup(KnowledgeBaseProvider knowledge, ILogger<KnowledgeWarmup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            PolicyKnowledgeBase knowledgeBase = await knowledge.GetAsync();
            logger.LogInformation("Knowledge base ready: {Chunks} chunks indexed", knowledgeBase.ChunkCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion failed; policy questions will fail until the application restarts");
        }
    }
}

/// <summary>Reports Degraded until ingestion has finished, without calling any model.</summary>
public sealed class KnowledgeHealthCheck(KnowledgeBaseProvider knowledge) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(knowledge.IsReady
            ? HealthCheckResult.Healthy("Policy knowledge base is ready.")
            : HealthCheckResult.Degraded("Policy knowledge base is still loading."));
}

public static class KnowledgeExtensions
{
    public static IServiceCollection AddNorthwindKnowledge(this IServiceCollection services, AIProviderOptions aiOptions)
    {
        services.AddSingleton<KnowledgeBaseProvider>();
        services.AddHostedService<KnowledgeWarmup>();
        services.AddHealthChecks().AddCheck<KnowledgeHealthCheck>("knowledge");
        return services;
    }
}
