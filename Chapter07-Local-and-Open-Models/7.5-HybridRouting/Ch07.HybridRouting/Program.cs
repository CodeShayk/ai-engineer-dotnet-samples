// Chapter 7, Section 7.5: Combining local and cloud models.
// Summarizes customer emails for the returns team. Emails that contain personal or payment
// data are routed to a local model; everything else goes to the configured cloud model.
//
// Usage: dotnet run [--offline]
//   --offline  replaces both models with stand-ins that report their name, so you can see
//              the routing decisions without calling any model.

using Ch07.HybridRouting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;
using Northwind.Shared.Testing;
using OllamaSharp;

bool offline = args.Contains("--offline");

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();

IChatClient cloudClient;
IChatClient localClient;

if (offline)
{
    SampleConsole.Header("Chapter 7.5: Hybrid routing (offline)");
    cloudClient = new FakeChatClient("[Summary written by the CLOUD model]");
    localClient = new FakeChatClient("[Summary written by the LOCAL model]");
}
else
{
    AIProviderOptions aiOptions = SampleConfiguration.LoadAIOptions();
    IConfiguration config = SampleConfiguration.Load();
    SampleConsole.Header("Chapter 7.5: Hybrid routing", aiOptions);

    if (aiOptions.Provider == AIProvider.Ollama)
    {
        SampleConsole.Note("AI:Provider is Ollama, so the \"cloud\" model is also local in this run. " +
                           "Configure Azure OpenAI or OpenAI to see the difference.");
    }

    cloudClient = AIClientFactory.CreateChatClient(aiOptions);
    localClient = new OllamaApiClient(
        new Uri(config["Ollama:Endpoint"] ?? "http://localhost:11434"),
        config["Ollama:ChatModel"] ?? "llama3.2");
}

IChatClient router = new SensitiveDataRoutingChatClient(
    cloudClient, localClient, new RegexSensitiveDataDetector(),
    loggerFactory.CreateLogger<SensitiveDataRoutingChatClient>());

const string Instructions = """
    You summarize customer emails for Northwind Traders' returns team.
    Write three short bullet points: what the customer wants, the order number if given,
    and anything the team must check. Never include bank details or card numbers.
    """;

string[] emails =
[
    "Hi, the Summit boots from order NW-10254 rub on my heel. I'd like to swap them for a half size up.",

    "Please refund order NW-10252 to a different card, 4539 1488 0343 6467, as my old one has expired.",

    "The kettle from NW-10251 leaks. Call me on +44 20 7946 0958 or email ana.trujillo@example.com to arrange collection.",

    "Does the Trailhead daypack come with a rain cover, and is there a larger size than 35 litres?",

    "My bank details for the refund are sort code 12-34-56, account 12345678. Order NW-10260, jacket not waterproof."
];

foreach (string email in emails)
{
    SampleConsole.Section(email.Length > 80 ? email[..77] + "..." : email);

    ChatResponse response = await router.GetResponseAsync(
        [new ChatMessage(ChatRole.System, Instructions), new ChatMessage(ChatRole.User, email)],
        new ChatOptions { Temperature = 0.2f });

    Console.WriteLine(response.Text);
    if (response.ModelId is not null)
    {
        SampleConsole.Note($"Answered by {response.ModelId}");
    }
}
