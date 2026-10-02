// Chapter 10, Section 10.4: Multi-turn conversations with agent sessions.
// A chat loop whose session is saved to disk after every turn and restored at startup.
// Stop the program mid-conversation, run it again, and the agent remembers what you said.
//
// Usage: dotnet run [conversation-id]     (default: demo)
// Commands: /reset deletes the saved conversation. Enter on an empty line exits.

using Ch10.AgentSessions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 10.4: Agent sessions", options);

AIAgent agent = new ChatClientAgent(
    AIClientFactory.CreateChatClient(options),
    name: "NorthwindAssist",
    instructions: SupportPrompts.System);

string conversationId = args.Length > 0 ? args[0] : "demo";
string directory = Path.Combine(AppContext.BaseDirectory, "sessions");
Directory.CreateDirectory(directory);
var sessions = new FileSessionStore(directory);

bool resumed = sessions.Exists(conversationId);
AgentSession session = await sessions.LoadOrCreateAsync(agent, conversationId, CancellationToken.None);

Console.WriteLine(resumed
    ? $"Resumed conversation '{conversationId}' from {Path.Combine(directory, conversationId + ".json")}."
    : $"Started a new conversation '{conversationId}'.");
SampleConsole.Note(resumed
    ? "Try: \"What did I tell you earlier?\""
    : "Try: \"Hi, my name is Thomas. I ordered a rain jacket last week.\" Then exit, run again, and ask \"What did I say my name was?\"");

while (SampleConsole.Prompt("\nYou") is { } input)
{
    if (input.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        sessions.Delete(conversationId);
        session = await agent.CreateSessionAsync();
        Console.WriteLine("Conversation deleted. Starting fresh.");
        continue;
    }

    AgentResponse response = await agent.RunAsync(input, session);
    Console.WriteLine($"\nAssistant: {response.Text}");

    // Save after every turn, so nothing is lost if the process stops.
    await sessions.SaveAsync(agent, conversationId, session, CancellationToken.None);
}

SampleConsole.Note($"Session saved. Run again with the same id ('{conversationId}') to continue.");
