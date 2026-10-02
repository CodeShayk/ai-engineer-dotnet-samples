using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Northwind.Shared.Knowledge;

/// <summary>
/// The ingestion half of the RAG pipeline (Chapter 9.2): chunk each document, embed the
/// chunks in batches, and upsert them with their metadata. Unchanged chunks are skipped
/// using a content hash, so re-running ingestion is cheap (Chapter 8.6).
/// </summary>
public sealed class PolicyIngestor(
    VectorStoreCollection<string, PolicyChunkRecord> collection,
    IEmbeddingGenerator<string, Embedding<float>> documentEmbedder,
    MarkdownSectionChunker chunker,
    int batchSize = 16)
{
    public async Task<int> IngestAsync(IEnumerable<PolicyDocument> documents, CancellationToken cancellationToken = default)
    {
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        var pending = new List<(DocumentChunk Chunk, PolicyDocument Document, string Hash)>();

        foreach (PolicyDocument document in documents)
        {
            foreach (DocumentChunk chunk in chunker.Chunk(document.Id, document.Title, document.Content))
            {
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunk.Text)));

                PolicyChunkRecord? existing = await collection.GetAsync(chunk.ChunkId, cancellationToken: cancellationToken);
                if (existing?.ContentHash == hash)
                {
                    continue;   // Already indexed with identical text.
                }

                pending.Add((chunk, document, hash));
            }
        }

        foreach ((DocumentChunk Chunk, PolicyDocument Document, string Hash)[] batch in pending.Chunk(batchSize))
        {
            GeneratedEmbeddings<Embedding<float>> vectors =
                await documentEmbedder.GenerateAsync(batch.Select(b => b.Chunk.Text), cancellationToken: cancellationToken);

            IEnumerable<PolicyChunkRecord> records = batch.Zip(vectors, (item, vector) => new PolicyChunkRecord
            {
                ChunkId = item.Chunk.ChunkId,
                DocumentId = item.Document.Id,
                Title = item.Document.Title,
                Section = item.Chunk.Section,
                Text = item.Chunk.Text,
                Category = item.Document.Category,
                Status = item.Document.Status,
                Region = item.Document.Region,
                Audience = item.Document.Audience,
                EffectiveDate = item.Document.EffectiveDate.ToString("yyyy-MM-dd"),
                ContentHash = item.Hash,
                Embedding = vector.Vector
            });

            await collection.UpsertAsync(records, cancellationToken);
        }

        return pending.Count;
    }
}
