// Chapter 3, Section 3.1: Anatomy of a prompt.
// A conversation loop in which the application, not the model, maintains the history.
// Type /forget to clear the history and see that the model remembers nothing on its own.
// Press Enter on an empty line to exit.

using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 3.1: Message roles and conversation history", options);
SampleConsole.Note("Try: \"My order is NW-10249.\" then \"What order number did I give you?\" then /forget and ask again.\n");

IChatClient chatClient = AIClientFactory.CreateChatClient(options);

const string SystemPrompt = """
    You are Northwind Assist, the customer support assistant for Northwind Traders,
    an online retailer. Answer questions about orders, deliveries, returns and products.
    Keep answers under 120 words. If you do not know something, say so plainly.
    """;

List<ChatMessage> history = [new(ChatRole.System, SystemPrompt)];

while (true)
{
    string? input = SampleConsole.Prompt("You");
    if (input is null)
    {
        break;
    }

    if (input.Equals("/forget", StringComparison.OrdinalIgnoreCase))
    {
        history = [new(ChatRole.System, SystemPrompt)];
        SampleConsole.Note("History cleared. Only the system message remains.\n");
        continue;
    }

    history.Add(new ChatMessage(ChatRole.User, input));

    ChatResponse response = await chatClient.GetResponseAsync(history);

    // Append the assistant's reply so the next request includes it.
    history.AddMessages(response);

    Console.WriteLine($"Assistant: {response.Text}");
    SampleConsole.Note($"(The next request will send {history.Count} messages.)\n");
}

// The same instructions can be supplied through ChatOptions.Instructions instead of a system message.
SampleConsole.Section("Instructions supplied through ChatOptions");
ChatResponse viaOptions = await chatClient.GetResponseAsync(
    "In one sentence, what can you help me with?",
    new ChatOptions { Instructions = "You are Northwind Assist, the customer support assistant for Northwind Traders." });
Console.WriteLine(viaOptions.Text);
