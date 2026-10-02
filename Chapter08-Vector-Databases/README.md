# Chapter 8: Vector Databases and Semantic Search

Companion code for Chapter 8. The projects move from raw vector arithmetic, through hybrid ranking, to a complete vector store lifecycle with `Microsoft.Extensions.VectorData`.

| Section | Project | What it shows |
|---|---|---|
| 8.2 | `8.2-SimilaritySearch/Ch08.SimilarityMetrics` | Cosine similarity, dot product and Euclidean distance computed with `TensorPrimitives` for every product, brute-force top-k search, and a timing benchmark over 10,000 random 1,536-dimension vectors. |
| 8.3 | `8.3-HybridSearch/Ch08.HybridSearch` | A small BM25 keyword index and vector search ranking the catalog side by side, merged with Reciprocal Rank Fusion. |
| 8.4, 8.6 | `8.6-VectorStore/Ch08.ProductSearch` | A collection created from a run-time schema, batch ingestion with upsert, filtered and unfiltered search, reads and deletes by key, and the automatic style in which the store generates embeddings itself. |

## Running

From this folder:

```bash
dotnet run --project 8.2-SimilaritySearch/Ch08.SimilarityMetrics
dotnet run --project 8.2-SimilaritySearch/Ch08.SimilarityMetrics -- --offline   # benchmark only, no model
dotnet run --project 8.3-HybridSearch/Ch08.HybridSearch
dotnet run --project 8.6-VectorStore/Ch08.ProductSearch
```

All three use the embedding model configured with `AI:EmbeddingDeployment` (by default `nomic-embed-text` on Ollama, or `text-embedding-3-small` on Azure OpenAI and OpenAI). `AI:EmbeddingDimensions` must match the model: 768 for `nomic-embed-text`, 1536 for `text-embedding-3-small`. The defaults are set for you when you choose a provider.

## What to look for

- **Normalized vectors.** `Ch08.SimilarityMetrics` prints the length of the query vector. When it is 1, the three metrics produce the same ranking, which you can confirm in the table.
- **Where each method wins.** In `Ch08.HybridSearch`, the question "Does the TD-35 fit a laptop?" mentions a laptop, which pulls vector search towards the laptop sleeve. With `nomic-embed-text`, vector search ranks the sleeve first and the TD-35 second, while keyword search matches the rare token `td-35` exactly and the hybrid result keeps the TD-35 on top. Other embedding models may rank these differently, which is itself the point: hybrid search makes the result less sensitive to the quirks of one method.
- **Filters run before ranking.** In `Ch08.ProductSearch`, the filtered search returns only two results for a top-3 request, because only two products pass the filter. A filter is a constraint, not a re-ranking.
- **Scores depend on the model.** The absolute scores differ between embedding models, so compare rankings, not raw numbers, when you switch providers.

## A note on embedding prefixes

Some embedding models, including `nomic-embed-text`, were trained to expect a task prefix such as `search_query:` or `search_document:` before the text. These samples embed plain text so the listings stay identical to the book. Chapter 9 adds the prefixes, which noticeably improves retrieval quality with those models.
