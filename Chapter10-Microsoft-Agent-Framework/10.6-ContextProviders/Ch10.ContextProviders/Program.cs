// Chapter 10, Section 10.6: Agent memory with context providers.
// An agent with three context providers:
//   CustomerProfileContextProvider  who the customer is and their recent orders (Northwind.Agents)
//   PolicyKnowledgeContextProvider  policy passages relevant to the latest message (Northwind.Agents)
//   PreferenceMemoryContextProvider preferences the customer stated, remembered across conversations
// Runs one conversation, then starts a second, separate conversation to show what is remembered.

using Ch10.ContextProviders;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Agents.ContextProviders;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Knowledge;
using Northwind.Shared.Tools;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 10.6: Context providers", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);
IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);

Console.WriteLine("Loading the policy library into the vector store...");
PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(options, embedder);

var store = NorthwindStore.CreateSeeded();
var orderTools = new OrderTools(store, new ReturnPolicyRules(store));

// Thomas Hardy (C003) lives in the UK, so UK-specific policies apply to him.
var currentCustomer = new FixedCurrentCustomer("C003");
CustomerContext customerContext = CustomerContext.For(store.FindCustomer(currentCustomer.CustomerId)!);
var preferences = new CustomerPreferenceStore();
PolicyRetriever retriever = knowledge.Retriever;

AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    Name = "NorthwindAssist",
    ChatOptions = new ChatOptions
    {
        Instructions = SupportPrompts.System,
        Tools = [AIFunctionFactory.Create(orderTools.GetOrderStatus), AIFunctionFactory.Create(orderTools.CheckReturnEligibility)]
    },
    AIContextProviders =
    [
        new CustomerProfileContextProvider(store, currentCustomer),
        new PolicyKnowledgeContextProvider(retriever, customerContext),
        new PreferenceMemoryContextProvider(preferences, currentCustomer)
    ]
});

// --- Conversation 1 --------------------------------------------------------------------------------
SampleConsole.Section("Conversation 1");

AgentSession first = await agent.CreateSessionAsync();

string[] firstConversation =
[
    "Hi! What's the status of my most recent order?",                       // The profile provider knows the orders
    "How long is the warranty on electronics where I live?",                // The policy provider retrieves the UK warranty
    "Thanks. By the way, I prefer to be contacted by email rather than phone."  // The memory provider learns
];

foreach (string message in firstConversation)
{
    await AskAsync(agent, first, message);
}

SampleConsole.Note($"Remembered for {currentCustomer.CustomerId}: " +
                   string.Join("; ", preferences.Get(currentCustomer.CustomerId).Select(p => $"\"{p}\"")));

// --- Conversation 2: a new session, so no shared history ------------------------------------------
SampleConsole.Section("Conversation 2 (a new session)");

AgentSession second = await agent.CreateSessionAsync();
await AskAsync(agent, second, "If you need to follow up with me about a return, how will you get in touch?");

SampleConsole.Note("The second session has no history from the first. The preference came from the memory provider.");

static async Task AskAsync(AIAgent agent, AgentSession session, string message)
{
    Console.WriteLine($"You: {message}");
    AgentResponse response = await agent.RunAsync(message, session);
    Console.WriteLine($"Assistant: {response.Text}");
    Console.WriteLine();
}
