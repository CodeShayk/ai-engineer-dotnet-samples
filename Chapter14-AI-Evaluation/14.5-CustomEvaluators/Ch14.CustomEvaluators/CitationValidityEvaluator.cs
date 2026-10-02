using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Northwind.Shared.Knowledge;

namespace Ch14.CustomEvaluators;

/// <summary>Supplies the retrieved chunks to evaluators that need them, such as <see cref="CitationValidityEvaluator"/>.</summary>
public sealed class RetrievedSourcesContext(IReadOnlyList<RetrievedChunk> chunks)
    : EvaluationContext("Retrieved Sources", PolicyAssistant.FormatPassages(chunks))
{
    public IReadOnlyList<RetrievedChunk> Chunks { get; } = chunks;
}

/// <summary>
/// A deterministic evaluator (Chapter 14.5) that applies the citation validation from Chapter 9:
/// every cited id must match a retrieved source, and every quote must appear in that source.
/// It costs nothing to run and is immune to judge bias.
/// </summary>
public sealed class CitationValidityEvaluator : IEvaluator
{
    public const string MetricName = "Citation Validity";

    public IReadOnlyCollection<string> EvaluationMetricNames => [MetricName];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var sources = additionalContext?.OfType<RetrievedSourcesContext>().SingleOrDefault();
        IReadOnlyList<string> problems = sources is null
            ? ["No retrieved sources were supplied to the evaluator."]
            : CitationValidator.Validate(GroundedAnswerParser.Parse(modelResponse.Text), sources.Chunks);

        var metric = new BooleanMetric(MetricName, value: problems.Count == 0, reason: string.Join(" ", problems));
        metric.Interpretation = new EvaluationMetricInterpretation(
            problems.Count == 0 ? EvaluationRating.Exceptional : EvaluationRating.Unacceptable,
            failed: problems.Count > 0,
            reason: problems.Count == 0 ? "All citations are valid." : "One or more citations are invalid.");

        return ValueTask.FromResult(new EvaluationResult(metric));
    }
}
