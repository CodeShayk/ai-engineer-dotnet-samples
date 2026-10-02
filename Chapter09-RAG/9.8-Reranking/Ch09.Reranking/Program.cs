// Chapter 9, Section 9.8: Reranking.
// Loads the policy library into an in-memory vector store, retrieves twelve candidates with
// filtered vector search, then asks a small model to grade each one. Prints both orderings
// side by side so you can see what the reranker changed and what it dropped.
//
// LlmReranker lives in Shared/Northwind.Shared/Knowledge/LlmReranker.cs.

using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 9.8: Reranking", options);

IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
IChatClient smallModel = AIClientFactory.CreateChatClient(options, options.SmallChatDeployment);

Console.WriteLine("Ingesting the policy library...");
PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(options, embedder);
Console.WriteLine($"{knowledge.ChunkCount} chunks indexed.");

var reranker = new LlmReranker(smallModel);
var customer = new CustomerContext(Region: "uk");

string[] questions =
[
    "Can I return earbuds I've already opened?",
    "How long until my refund shows up?",
    "Do you charge a restocking fee?"
];

foreach (string question in questions)
{
    SampleConsole.Section(question);

    IReadOnlyList<RetrievedChunk> candidates = await knowledge.Retriever.SearchAsync(question, customer, top: 12, CancellationToken.None);
    IReadOnlyList<RetrievedChunk> reranked = await reranker.RerankAsync(question, candidates, top: 4, CancellationToken.None);

    Console.WriteLine($"{"#",-3}{"Vector search (top 12)",-52}{"After reranking (top 4)",-52}");
    for (int i = 0; i < candidates.Count; i++)
    {
        string left = $"{candidates[i].SimilarityScore:F3} {candidates[i].ChunkId} {candidates[i].Section}";
        string right = i < reranked.Count ? $"{reranked[i].ChunkId} {reranked[i].Section}" : "";
        Console.WriteLine($"{i + 1,-3}{Fit(left, 50),-52}{Fit(right, 50),-52}");
    }

    if (reranked.Count == 0)
    {
        SampleConsole.Note("Every candidate was graded irrelevant: the caller treats this as a retrieval miss.");
    }
}

static string Fit(string text, int width) => text.Length <= width ? text : text[..(width - 3)] + "...";
