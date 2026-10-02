// Chapter 6, Section 6.2: Exposing C# methods as tools.
// 1. Shows what AIFunctionFactory produces from a method: name, description and parameter schema.
// 2. Answers questions that need the order tools, with automatic function invocation.
// 3. Prints every tool call and result, so you can see what the model did.

using System.Text.Json;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

AIProviderOptions aiOptions = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 6.2: Function tools", aiOptions);

var store = NorthwindStore.CreateSeeded();
var tools = new OrderTools(store, new ReturnPolicyRules(store));

IChatClient chatClient = AIClientFactory.CreateChatClient(aiOptions)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var options = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(tools.GetOrderStatus),
        AIFunctionFactory.Create(tools.CheckReturnEligibility)
    ]
};

// --- How the factory reads your method ------------------------------------------------------
SampleConsole.Section("What the model sees");

foreach (AIFunction function in options.Tools.OfType<AIFunction>())
{
    Console.WriteLine($"{function.Name}: {function.Description}");
    Console.WriteLine(JsonSerializer.Serialize(function.JsonSchema, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine();
}

// --- Questions that need the tools ----------------------------------------------------------
string[] questions =
[
    "I opened the speaker from order NW-10252 last week. Can I still send it back?",
    "When will order NW-10249 arrive?",
    "Can you check order NW-99999 for me?"
];

foreach (string question in questions)
{
    SampleConsole.Section(question);

    List<ChatMessage> history =
    [
        new(ChatRole.System, SupportPrompts.System),
        new(ChatRole.User, question)
    ];

    ChatResponse response = await chatClient.GetResponseAsync(history, options);
    Console.WriteLine(response.Text);

    // --- Seeing what happened ---------------------------------------------------------------
    Console.WriteLine();
    SampleConsole.Note("Trace:");
    foreach (ChatMessage message in response.Messages)
    {
        foreach (AIContent content in message.Contents)
        {
            string line = content switch
            {
                FunctionCallContent call => $"[call]   {call.Name}({string.Join(", ", call.Arguments?.Select(a => $"{a.Key}: {a.Value}") ?? [])})",
                FunctionResultContent result => $"[result] {JsonSerializer.Serialize(result.Result)}",
                TextContent text => $"[{message.Role}] {Truncate(text.Text, 100)}",
                _ => $"[{content.GetType().Name}]"
            };
            SampleConsole.Note(line);
        }
    }
}

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length].ReplaceLineEndings(" ") + "...";
