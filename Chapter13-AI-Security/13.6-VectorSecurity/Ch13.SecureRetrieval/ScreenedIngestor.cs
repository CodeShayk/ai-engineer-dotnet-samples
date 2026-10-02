using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Northwind.Shared.Security;

namespace Ch13.SecureRetrieval;

/// <summary>A document offered for ingestion, with its scope and provenance.</summary>
public sealed record IncomingDocument(
    string TenantId, string Audience, string Status, string Title, string Text, string Source, string AddedBy);

/// <summary>
/// Controls what gets into the knowledge base (Chapter 13.6). Each document is screened for
/// injected instructions before it is embedded; anything suspicious is quarantined for review
/// instead of being indexed, where it could become an indirect prompt injection.
/// </summary>
public sealed class ScreenedIngestor(
    VectorStoreCollection<string, KnowledgeChunkRecord> collection,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IPromptAttackDetector detector)
{
    public List<(IncomingDocument Document, string Reason)> Quarantine { get; } = [];

    public async Task<int> IngestAsync(IReadOnlyList<IncomingDocument> documents, CancellationToken cancellationToken)
    {
        var accepted = new List<IncomingDocument>();

        foreach (IncomingDocument document in documents)
        {
            PromptAttackResult screening = await detector.AnalyzeAsync("Index this document.", [document.Text], cancellationToken);
            if (screening.DocumentAttack)
            {
                Quarantine.Add((document, "Possible injected instructions"));
                continue;
            }

            accepted.Add(document);
        }

        if (accepted.Count == 0)
        {
            return 0;
        }

        GeneratedEmbeddings<Embedding<float>> vectors =
            await embedder.GenerateAsync(accepted.Select(d => $"{d.Title}\n\n{d.Text}"), cancellationToken: cancellationToken);

        string now = DateTimeOffset.UtcNow.ToString("O");
        await collection.UpsertAsync(accepted.Zip(vectors, (d, v) => new KnowledgeChunkRecord
        {
            ChunkId = $"{d.TenantId}:{d.Title.ToLowerInvariant().Replace(' ', '-')}",
            TenantId = d.TenantId,
            Audience = d.Audience,
            Status = d.Status,
            Title = d.Title,
            Text = d.Text,
            Source = d.Source,
            AddedBy = d.AddedBy,
            AddedAt = now,
            Embedding = v.Vector
        }), cancellationToken);

        return accepted.Count;
    }
}
