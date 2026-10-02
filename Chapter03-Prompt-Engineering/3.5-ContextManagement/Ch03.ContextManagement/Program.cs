// Chapter 3, Section 3.5: Context management.
// 1. Trims a long conversation to a token budget (no model call).
// 2. Summarizes older turns with SummarizingChatReducer (one model call).
// 3. Installs the reducer as middleware so every request is reduced automatically.

using Ch03.ContextManagement;
using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 3.5: Context management", options);

ChatMessage systemMessage = new(ChatRole.System, SupportPrompts.System);

// A long conversation in which the important fact (the order number) appears only at the start.
ChatMessage[] conversation =
[
    new(ChatRole.User, "Hi, I'm Thomas. My order number is NW-10248 and it's the rain jacket."),
    new(ChatRole.Assistant, "Thanks, Thomas. I can see order NW-10248 with the Harbor Rain Jacket. How can I help?"),
    new(ChatRole.User, "The jacket leaks at the seams when it rains heavily. I wore it twice."),
    new(ChatRole.Assistant, "I'm sorry to hear that. A leaking seam would count as a fault, which our policy covers."),
    new(ChatRole.User, "Do I need the original packaging to send it back?"),
    new(ChatRole.Assistant, "No, you don't need the original packaging for a faulty item. A prepaid label is provided."),
    new(ChatRole.User, "How long will the refund take once you have it?"),
    new(ChatRole.Assistant, "Refunds are issued within 5 to 10 business days of the return arriving at our warehouse."),
    new(ChatRole.User, "And can I drop it at a parcel shop rather than waiting in for a courier?"),
    new(ChatRole.Assistant, "Yes, you can show the QR code at a participating drop-off point."),
    new(ChatRole.User, "Great. Which order was this about again? I've lost track."),
];

// --- 1. Token-budgeted sliding window -----------------------------------------------
SampleConsole.Section("1. Sliding window by tokens");

var history = new TokenBudgetedHistory(TiktokenTokenizer.CreateForEncoding("o200k_base"), historyTokenBudget: 120);
history.AddRange(conversation);

IReadOnlyList<ChatMessage> withinBudget = history.GetWithinBudget();
Console.WriteLine($"{conversation.Length} messages in history; {withinBudget.Count} fit within a 120-token budget.");
Console.WriteLine($"Oldest message kept: \"{withinBudget[0].Text}\"");
SampleConsole.Note("The order number from the first message has fallen outside the window.");

IChatClient chatClient = AIClientFactory.CreateChatClient(options);
ChatResponse trimmedAnswer = await chatClient.GetResponseAsync([systemMessage, .. withinBudget]);
Console.WriteLine($"\nAssistant (trimmed history): {trimmedAnswer.Text}");

// --- 2. Summarizing older turns ------------------------------------------------------
SampleConsole.Section("2. Summarizing older turns");

// A smaller, cheaper model is ideal for summarization. The shared configuration
// exposes one as AI:SmallChatDeployment, falling back to the main chat model.
IChatClient summarizer = AIClientFactory.CreateChatClient(options, options.SmallChatDeployment);

// Keep roughly the last 4 messages verbatim; once the history exceeds that by 2,
// summarize the older messages into a single message.
IChatReducer reducer = new SummarizingChatReducer(summarizer, targetCount: 4, threshold: 2);

List<ChatMessage> reduced = (await reducer.ReduceAsync(conversation, CancellationToken.None)).ToList();
Console.WriteLine($"{conversation.Length} messages reduced to {reduced.Count}:");
foreach (ChatMessage message in reduced)
{
    Console.WriteLine($"  [{message.Role}] {Truncate(message.Text, 140)}");
}

ChatResponse summarizedAnswer = await chatClient.GetResponseAsync([systemMessage, .. reduced]);
Console.WriteLine($"\nAssistant (summarized history): {summarizedAnswer.Text}");

// --- 3. The reducer as middleware ------------------------------------------------------
SampleConsole.Section("3. Reducer installed as middleware");

IChatClient reducingClient = AIClientFactory.CreateChatClient(options)
    .AsBuilder()
    .UseChatReducer(reducer)
    .Build();

ChatResponse middlewareAnswer = await reducingClient.GetResponseAsync([systemMessage, .. conversation]);
Console.WriteLine($"Assistant (reduced automatically): {middlewareAnswer.Text}");

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length] + "...";
