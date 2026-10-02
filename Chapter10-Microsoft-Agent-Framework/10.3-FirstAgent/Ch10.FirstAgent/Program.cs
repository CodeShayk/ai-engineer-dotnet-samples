// Chapter 10, Section 10.3: Building your first agent.
// Creates an agent from the shared chat client in two equivalent ways, runs it, and streams
// a second answer.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 10.3: Your first agent", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);

// --- ChatClientAgent ----------------------------------------------------------------------------
AIAgent agent = new ChatClientAgent(
    chatClient,
    name: "NorthwindAssist",
    description: "Answers customer questions about Northwind Traders orders, deliveries and returns.",
    instructions: SupportPrompts.System);

SampleConsole.Section("RunAsync");

AgentResponse response = await agent.RunAsync("What's the difference between standard and express delivery?");
Console.WriteLine(response.Text);
SampleConsole.Note($"{response.Messages.Count} message(s), {response.Usage?.TotalTokenCount} tokens");

// --- The same agent from the client's side ------------------------------------------------------
AIAgent sameAgent = chatClient.AsAIAgent(
    name: "NorthwindAssist",
    instructions: SupportPrompts.System);

SampleConsole.Section("RunStreamingAsync");

await foreach (AgentResponseUpdate update in sameAgent.RunStreamingAsync("Do you deliver on Saturdays?"))
{
    Console.Write(update.Text);
}

Console.WriteLine();
SampleConsole.Note("Each RunAsync call without a session is a fresh conversation. Section 10.4 adds sessions.");
