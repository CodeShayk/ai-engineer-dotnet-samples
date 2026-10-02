// Chapter 6, Section 6.4: Automatic versus manual function invocation.
// 1. Runs the tool-calling loop by hand, auditing every call before it executes.
// 2. Uses automatic invocation with an approval-required refund tool: the loop pauses,
//    a support agent approves or declines at the console, and the conversation resumes.
//
// Usage: dotnet run [--offline]
//   --offline  a scripted stand-in plays the model's part, so you can see the mechanics
//              without calling a model.

using Ch06.ManualInvocation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

bool offline = args.Contains("--offline");
AIProviderOptions? aiOptions = offline ? null : SampleConfiguration.LoadAIOptions();

SampleConsole.Header("Chapter 6.4: Manual invocation and approvals", aiOptions);
if (offline)
{
    SampleConsole.Note("Offline mode: a scripted stand-in plays the model's part. No model is called.");
}

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();
CancellationToken cancellationToken = CancellationToken.None;

var store = NorthwindStore.CreateSeeded();
var returnRules = new ReturnPolicyRules(store);
var orderTools = new OrderTools(store, returnRules);

// The signed-in customer for this session: Thomas Hardy (C003).
var refundTools = new RefundTools(store, returnRules, new FixedCurrentCustomer("C003"), loggerFactory.CreateLogger<RefundTools>());

// --- Part 1: the loop written by hand ----------------------------------------------------------
SampleConsole.Section("Manual invocation with an audit log");

IChatClient manualClient = offline
    ? new ScriptedChatClient(
        ScriptedChatClient.Call("GetOrderStatus", ("orderNumber", "NW-10252")),
        ScriptedChatClient.Call("CheckReturnEligibility", ("orderNumber", "NW-10252"), ("productId", "P02"), ("opened", true)),
        ScriptedChatClient.SummarizeLastResult)
    : AIClientFactory.CreateChatClient(aiOptions!);   // No UseFunctionInvocation

AIFunction[] readOnlyTools =
[
    AIFunctionFactory.Create(orderTools.GetOrderStatus),
    AIFunctionFactory.Create(orderTools.CheckReturnEligibility)
];

var auditLog = new AuditLog();
List<ChatMessage> manualHistory =
[
    new(ChatRole.System, SupportPrompts.System),
    new(ChatRole.User, "I opened the speaker from order NW-10252 last week. Can I still send it back?")
];

string answer = await ManualToolLoop.RunAsync(manualClient, readOnlyTools, manualHistory, auditLog, cancellationToken);
Console.WriteLine(answer);
SampleConsole.Note($"{auditLog.Entries.Count} tool call(s) audited; {manualHistory.Count} messages in the history.");

// --- Part 2: automatic invocation with human approval -------------------------------------------
SampleConsole.Section("Automatic invocation with an approval-required refund tool");

IChatClient innerClient = offline
    ? new ScriptedChatClient(
        ScriptedChatClient.Call("GetOrderStatus", ("orderNumber", "NW-10248")),
        ScriptedChatClient.Call("CheckReturnEligibility", ("orderNumber", "NW-10248"), ("productId", "P05"), ("opened", false)),
        ScriptedChatClient.Call("RequestRefund", ("orderNumber", "NW-10248"), ("productId", "P05"), ("reason", "DoesNotFit")),
        ScriptedChatClient.SummarizeLastResult)
    : AIClientFactory.CreateChatClient(aiOptions!);

IChatClient chatClient = innerClient
    .AsBuilder()
    .UseFunctionInvocation(configure: invoker =>
    {
        invoker.MaximumIterationsPerRequest = 5;          // Stop runaway loops
        invoker.AllowConcurrentInvocation = false;         // The refund tool changes state
        invoker.MaximumConsecutiveErrorsPerRequest = 2;    // Give up if tools keep failing
        invoker.IncludeDetailedErrors = false;             // Never leak exception details to the model
    })
    .Build();

var approvals = new ConsoleApprovals();

var chatOptions = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(orderTools.GetOrderStatus),
        AIFunctionFactory.Create(orderTools.CheckReturnEligibility),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(refundTools.RequestRefund))
    ]
};

List<ChatMessage> history =
[
    new(ChatRole.System, SupportPrompts.System),
    new(ChatRole.User, "The Harbor Rain Jacket from order NW-10248 doesn't fit and I haven't worn it. Please refund it.")
];

ChatResponse response = await chatClient.GetResponseAsync(history, chatOptions, cancellationToken);
history.AddMessages(response);

List<ToolApprovalRequestContent> pending = GetApprovalRequests(response);

while (pending.Count > 0)
{
    var decisions = new List<AIContent>();

    foreach (ToolApprovalRequestContent request in pending)
    {
        var call = (FunctionCallContent)request.ToolCall;
        bool approved = await approvals.AskAgentAsync(call.Name, call.Arguments, cancellationToken);
        decisions.Add(request.CreateResponse(approved, approved ? null : "Declined by the support agent."));
    }

    // The decisions go back as a user message; approved calls are then executed.
    history.Add(new ChatMessage(ChatRole.User, decisions));
    response = await chatClient.GetResponseAsync(history, chatOptions, cancellationToken);
    history.AddMessages(response);
    pending = GetApprovalRequests(response);
}

Console.WriteLine(response.Text);

SampleConsole.Section("Refund requests in the store");
if (store.RefundRequests.Count == 0)
{
    Console.WriteLine("None. Nothing was submitted without approval.");
}

foreach (RefundRequest refund in store.RefundRequests)
{
    Console.WriteLine($"{refund.RequestId}: {refund.OrderId}/{refund.ProductId} {refund.Amount:0.00} ({refund.Reason}) {refund.Status}");
}

static List<ToolApprovalRequestContent> GetApprovalRequests(ChatResponse response) =>
    response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
