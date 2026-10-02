using System.Diagnostics;
using Ch15.ObservableAssistant.Telemetry;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

namespace Ch15.ObservableAssistant;

/// <summary>
/// The policy assistant pipeline from Chapter 9 with a span for each stage, feature attribution
/// for cost, and behavioral metrics for retrieval misses and escalations (Chapters 15.3 to 15.5).
/// </summary>
public sealed class ObservablePolicyAssistant(
    QueryRewriter rewriter,
    TracedPolicyRetriever retriever,
    LlmReranker reranker,
    StructuredOutputService structuredOutput,
    AssistantMetrics metrics)
{
    private const string Feature = "policy-assistant";

    public async Task<AssistantAnswer> AskAsync(
        string question, IReadOnlyList<ChatMessage> history, CustomerContext customer, CancellationToken cancellationToken)
    {
        using IDisposable feature = AIFeature.Begin(Feature);

        string query;
        using (TracedPolicyRetriever.Source.StartActivity("rewrite query"))
        {
            query = await rewriter.RewriteAsync(history, question, cancellationToken);
        }

        IReadOnlyList<RetrievedChunk> candidates = await retriever.SearchAsync(query, customer, top: 12, cancellationToken);

        IReadOnlyList<RetrievedChunk> sources;
        using (Activity? rerank = TracedPolicyRetriever.Source.StartActivity("rerank"))
        {
            sources = candidates.Count == 0 ? [] : await reranker.RerankAsync(query, candidates, top: 4, cancellationToken);
            rerank?.SetTag("northwind.rerank.candidates", candidates.Count);
            rerank?.SetTag("northwind.rerank.kept", sources.Count);
            rerank?.SetTag("northwind.rerank.chunk_ids", string.Join(",", sources.Select(s => s.ChunkId)));
        }

        if (sources.Count == 0)
        {
            metrics.RetrievalMissed(Feature);
            metrics.Escalated("no-relevant-policy");
            return AssistantAnswer.NotFound();
        }

        List<ChatMessage> messages =
        [
            new(ChatRole.System, GroundedPrompt.Instructions),
            .. history.TakeLast(6),
            new(ChatRole.User, GroundedPrompt.Build(question, sources, customer))
        ];

        StructuredResult<GroundedAnswer> result = await structuredOutput.GetAsync<GroundedAnswer>(
            messages,
            additionalChecks: answer => CitationValidator.Validate(answer, sources),
            options: new ChatOptions { Temperature = 0.1f },
            cancellationToken: cancellationToken);

        Activity.Current?.SetTag("northwind.answer.attempts", result.Attempts);

        if (!result.Succeeded || !result.Value!.AnswerFound)
        {
            metrics.Escalated(result.Succeeded ? "answer-not-found" : "validation-failed");
            return AssistantAnswer.NotFound();
        }

        return AssistantAnswer.From(result.Value, sources);
    }
}
