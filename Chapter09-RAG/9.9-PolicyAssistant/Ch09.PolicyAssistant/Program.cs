// Chapter 9, Section 9.9: Building a RAG application in .NET.
// An interactive policy assistant. At startup it ingests the policy library into an in-memory
// vector store; each question then goes through rewrite, filtered retrieval, reranking,
// a grounded prompt, structured generation and citation validation.
//
// Commands: /region uk|us|global  changes the customer's region (default uk)
//           /debug                 toggles the retrieval details for each answer
//           /new                   starts a new conversation
//           Enter on an empty line exits.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 9.9: The Northwind policy assistant", options);

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory(LogLevel.Warning);

IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
IChatClient chatModel = AIClientFactory.CreateChatClient(options);
IChatClient smallModel = AIClientFactory.CreateChatClient(options, options.SmallChatDeployment);

// --- Ingestion -------------------------------------------------------------------------------
Console.WriteLine("Ingesting the policy library (chunk, embed, upsert)...");
PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(options, embedder);
Console.WriteLine($"{PolicyLibrary.LoadAll().Count} documents, {knowledge.ChunkCount} chunks. " +
                  "Archived and internal documents are indexed too; the retrieval filter excludes them.");

// --- The query pipeline ------------------------------------------------------------------------
var assistant = new PolicyAssistant(
    new QueryRewriter(smallModel),
    knowledge.Retriever,
    new LlmReranker(smallModel),
    new StructuredOutputService(chatModel, loggerFactory.CreateLogger<StructuredOutputService>()),
    loggerFactory.CreateLogger<PolicyAssistant>());

var customer = new CustomerContext(Region: "uk");
var history = new List<ChatMessage>();
bool debug = false;

Console.WriteLine();
SampleConsole.Note("Try: \"I opened my earbuds ten days ago and they hurt my ears. Can I return them, and will I have to pay anything?\"");
SampleConsole.Note("Then a follow-up: \"What about the speaker I bought last month?\"  Commands: /region, /debug, /new");

while (SampleConsole.Prompt("\nYou") is { } input)
{
    if (input.StartsWith("/region ", StringComparison.OrdinalIgnoreCase))
    {
        customer = customer with { Region = input["/region ".Length..].Trim().ToLowerInvariant() };
        Console.WriteLine($"Customer region is now '{customer.Region}'.");
        continue;
    }

    if (input.Equals("/debug", StringComparison.OrdinalIgnoreCase))
    {
        debug = !debug;
        Console.WriteLine($"Retrieval details {(debug ? "on" : "off")}.");
        continue;
    }

    if (input.Equals("/new", StringComparison.OrdinalIgnoreCase))
    {
        history.Clear();
        Console.WriteLine("New conversation.");
        continue;
    }

    if (debug)
    {
        (string query, IReadOnlyList<RetrievedChunk> candidates, IReadOnlyList<RetrievedChunk> sources) =
            await assistant.RetrieveAsync(input, history, customer, CancellationToken.None);

        SampleConsole.Note($"Search query: {query}");
        SampleConsole.Note($"Candidates:   {string.Join(", ", candidates.Select(c => c.ChunkId))}");
        SampleConsole.Note($"Reranked:     {string.Join(", ", sources.Select(c => c.ChunkId))}");
    }

    AssistantAnswer answer = await assistant.AskAsync(input, history, customer, CancellationToken.None);

    Console.WriteLine();
    Console.WriteLine($"Assistant: {answer.Text}");

    if (answer.Found)
    {
        Console.WriteLine();
        Console.WriteLine("Sources:");
        foreach (Citation citation in answer.Citations.DistinctBy(c => c.SourceId))
        {
            RetrievedChunk? source = answer.Sources.FirstOrDefault(s => s.ChunkId == citation.SourceId);
            Console.WriteLine($"  [{citation.SourceId}] {source?.Title} > {source?.Section}");
        }
    }

    history.Add(new ChatMessage(ChatRole.User, input));
    history.Add(new ChatMessage(ChatRole.Assistant, answer.Text));
}
