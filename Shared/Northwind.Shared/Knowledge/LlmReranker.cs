using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Northwind.Shared.Knowledge;

public sealed record RelevanceJudgment(
    [property: Description("The passage id, exactly as given.")] string PassageId,
    [property: Description("3 = directly answers the question, 2 = clearly relevant, 1 = loosely related, 0 = irrelevant.")] int Score);

public sealed record RerankResponse(IReadOnlyList<RelevanceJudgment> Judgments);

/// <summary>
/// Uses a (small, fast) chat model to grade candidate passages in one call and keeps the
/// best few (Chapter 9.8). Passages graded zero are dropped entirely.
/// </summary>
public sealed class LlmReranker(IChatClient chatClient)
{
    public async Task<IReadOnlyList<RetrievedChunk>> RerankAsync(
        string question, IReadOnlyList<RetrievedChunk> candidates, int top, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        string passages = string.Join("\n", candidates.Select(c =>
            $"<passage id=\"{c.ChunkId}\">\n{c.Text}\n</passage>"));

        ChatResponse<RerankResponse> response = await chatClient.GetResponseAsync<RerankResponse>(
            [
                new ChatMessage(ChatRole.System,
                    "Grade how useful each passage is for answering the question. Grade every passage."),
                new ChatMessage(ChatRole.User, $"<question>{question}</question>\n\n{passages}")
            ],
            new ChatOptions { Temperature = 0 },
            cancellationToken: cancellationToken);

        if (!response.TryGetResult(out RerankResponse? result) || result?.Judgments is null)
        {
            // If grading fails, fall back to the similarity order rather than failing the request.
            return candidates.Take(top).ToList();
        }

        Dictionary<string, int> scores = result.Judgments
            .GroupBy(j => j.PassageId)
            .ToDictionary(g => g.Key, g => g.Max(j => j.Score));

        return candidates
            .Select(c => (Chunk: c, Score: scores.GetValueOrDefault(c.ChunkId)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Chunk.SimilarityScore)
            .Take(top)
            .Select(x => x.Chunk)
            .ToList();
    }
}
