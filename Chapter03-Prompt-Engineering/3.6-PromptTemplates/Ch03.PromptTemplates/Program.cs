// Chapter 3, Section 3.6: Building reusable prompts and templates in C#.
// Loads versioned prompt files, renders the latest (or a pinned) version, and records which
// prompt version produced the answer.
//
// To pin a version, set Prompts:grounded-answer, for example:
//   dotnet user-secrets set "Prompts:grounded-answer" "1" --id northwind-ai-engineer-samples

using Ch03.PromptTemplates;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

IConfiguration configuration = SampleConfiguration.Load();
AIProviderOptions options = AIProviderOptions.FromConfiguration(configuration);
SampleConsole.Header("Chapter 3.6: Versioned prompt templates", options);

Dictionary<string, int> pinned = configuration.GetSection("Prompts").GetChildren()
    .Where(c => int.TryParse(c.Value, out _))
    .ToDictionary(c => c.Key, c => int.Parse(c.Value!), StringComparer.OrdinalIgnoreCase);

var prompts = new PromptLibrary(PromptFiles.LoadAll("Prompts"), pinned);

Console.WriteLine("Prompts in the library:");
foreach (PromptTemplate prompt in prompts.All)
{
    Console.WriteLine($"  {prompt.Name} v{prompt.Version}: {prompt.Description} (variables: {string.Join(", ", prompt.Variables)})");
}

// Use two policy sections as excerpts. Chapter 9 replaces this with real retrieval.
var chunker = MarkdownSectionChunker.CreateDefault();
IEnumerable<DocumentChunk> excerpts = PolicyLibrary.LoadAll()
    .Where(p => p.Id is "electronics-returns" or "refunds")
    .SelectMany(p => chunker.Chunk(p.Id, p.Title, p.Content))
    .Where(c => c.Section is "Opened electronics" or "How long refunds take");

string formattedExcerpts = string.Join("\n", excerpts.Select(e => $"<document id=\"{e.ChunkId}\">\n{e.Text}\n</document>"));
const string Question = "I opened my earbuds ten days ago and they hurt my ears. Can I return them, and how long will the refund take?";

PromptTemplate template = prompts.Get("grounded-answer");
string userPrompt = template.Render(new Dictionary<string, string>
{
    ["excerpts"] = formattedExcerpts,
    ["question"] = Question
});

SampleConsole.Section($"Rendered {template.Name} v{template.Version}");
Console.WriteLine(userPrompt);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);
ChatResponse response = await chatClient.GetResponseAsync(
    [new ChatMessage(ChatRole.System, SupportPrompts.System), new ChatMessage(ChatRole.User, userPrompt)],
    new ChatOptions { Temperature = template.Temperature });

SampleConsole.Section("Answer");
Console.WriteLine(response.Text);
SampleConsole.Note($"\nAnswered with prompt {template.Name} v{template.Version}.");

// A template refuses to render with a missing value rather than sending a prompt with a hole in it.
SampleConsole.Section("Missing values fail loudly");
try
{
    prompts.Get("ticket-summary").Render(new Dictionary<string, string>());
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message);
}
