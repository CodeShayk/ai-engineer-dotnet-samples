using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Northwind.Shared.AI;

namespace Northwind.Shared.Knowledge;

/// <summary>The policy assistant's answer, with the citations and the passages they refer to.</summary>
public sealed record AssistantAnswer(
    bool Found,
    string Text,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<RetrievedChunk> Sources)
{
    public const string NotFoundText =
        "I'm not sure about that one, and I'd rather not guess. I can connect you with a member of our support team who can help.";

    public static AssistantAnswer NotFound() => new(false, NotFoundText, [], []);

    public static AssistantAnswer From(GroundedAnswer answer, IReadOnlyList<RetrievedChunk> sources) =>
        new(true, answer.Answer, answer.Citations, sources);
}

/// <summary>
/// The query half of the RAG pipeline (Chapter 9.9): rewrite, retrieve, rerank, build a
/// grounded prompt, generate a structured answer and validate its citations.
/// </summary>
public sealed class PolicyAssistant(
    QueryRewriter rewriter,
    PolicyRetriever retriever,
    LlmReranker reranker,
    StructuredOutputService structuredOutput,
    ILogger<PolicyAssistant>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<PolicyAssistant>.Instance;

    public async Task<AssistantAnswer> AskAsync(
        string question, IReadOnlyList<ChatMessage> history, CustomerContext customer, CancellationToken cancellationToken)
    {
        (string query, IReadOnlyList<RetrievedChunk> candidates, IReadOnlyList<RetrievedChunk> sources) =
            await RetrieveAsync(question, history, customer, cancellationToken);

        if (sources.Count == 0)
        {
            _logger.LogInformation("No relevant policy passages found for query {Query}", query);
            return AssistantAnswer.NotFound();
        }

        List<ChatMessage> messages = BuildMessages(question, history, customer, sources);

        StructuredResult<GroundedAnswer> result = await structuredOutput.GetAsync<GroundedAnswer>(
            messages,
            additionalChecks: answer => CitationValidator.Validate(answer, sources),
            options: new ChatOptions { Temperature = 0.1f },
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Query {Query}: {CandidateCount} candidates, {SourceCount} sources, {Attempts} attempt(s), succeeded {Succeeded}",
            query, candidates.Count, sources.Count, result.Attempts, result.Succeeded);

        return result.Succeeded && result.Value!.AnswerFound
            ? AssistantAnswer.From(result.Value, sources)
            : AssistantAnswer.NotFound();
    }

    /// <summary>
    /// Runs the same pipeline but returns what an evaluator needs: the conversation sent to
    /// the model, the response as text with its citation block, and the retrieved passages
    /// (Chapter 14.3).
    /// </summary>
    public async Task<(List<ChatMessage> Conversation, ChatResponse Response, string RetrievedPassages)> AnswerForEvaluationAsync(
        string question, CustomerContext customer, CancellationToken cancellationToken)
    {
        (List<ChatMessage> conversation, ChatResponse response, IReadOnlyList<RetrievedChunk> sources) =
            await AnswerWithSourcesAsync(question, customer, cancellationToken);

        return (conversation, response, FormatPassages(sources));
    }

    /// <summary>
    /// The same as <see cref="AnswerForEvaluationAsync"/>, but returns the retrieved chunks
    /// themselves, for evaluators that check citations against them (Chapter 14.6).
    /// </summary>
    public async Task<(List<ChatMessage> Conversation, ChatResponse Response, IReadOnlyList<RetrievedChunk> Sources)> AnswerWithSourcesAsync(
        string question, CustomerContext customer, CancellationToken cancellationToken)
    {
        (_, _, IReadOnlyList<RetrievedChunk> sources) = await RetrieveAsync(question, [], customer, cancellationToken);
        List<ChatMessage> messages = BuildMessages(question, [], customer, sources);

        if (sources.Count == 0)
        {
            return (messages, new ChatResponse(new ChatMessage(ChatRole.Assistant, AssistantAnswer.NotFoundText)), sources);
        }

        StructuredResult<GroundedAnswer> result = await structuredOutput.GetAsync<GroundedAnswer>(
            messages,
            additionalChecks: answer => CitationValidator.Validate(answer, sources),
            options: new ChatOptions { Temperature = 0.1f },
            cancellationToken: cancellationToken);

        string text = result.Succeeded && result.Value!.AnswerFound ? result.Value.ToDisplayText() : AssistantAnswer.NotFoundText;
        return (messages, new ChatResponse(new ChatMessage(ChatRole.Assistant, text)), sources);
    }

    /// <summary>Formats retrieved chunks as the grounding material an evaluator reads.</summary>
    public static string FormatPassages(IEnumerable<RetrievedChunk> sources) =>
        string.Join("\n\n", sources.Select(s => $"[{s.ChunkId}] {s.Text}"));

    public async Task<(string Query, IReadOnlyList<RetrievedChunk> Candidates, IReadOnlyList<RetrievedChunk> Sources)> RetrieveAsync(
        string question, IReadOnlyList<ChatMessage> history, CustomerContext customer, CancellationToken cancellationToken)
    {
        string query = await rewriter.RewriteAsync(history, question, cancellationToken);

        IReadOnlyList<RetrievedChunk> candidates = await retriever.SearchAsync(query, customer, top: 12, cancellationToken);
        IReadOnlyList<RetrievedChunk> sources = candidates.Count == 0
            ? []
            : await reranker.RerankAsync(query, candidates, top: 4, cancellationToken);

        return (query, candidates, sources);
    }

    private static List<ChatMessage> BuildMessages(
        string question, IReadOnlyList<ChatMessage> history, CustomerContext customer, IReadOnlyList<RetrievedChunk> sources) =>
    [
        new(ChatRole.System, GroundedPrompt.Instructions),
        .. history.TakeLast(6),
        new(ChatRole.User, GroundedPrompt.Build(question, sources, customer))
    ];
}
