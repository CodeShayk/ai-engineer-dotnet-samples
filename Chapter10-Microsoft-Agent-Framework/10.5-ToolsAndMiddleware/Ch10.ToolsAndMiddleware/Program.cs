// Chapter 10, Section 10.5: Tools and middleware.
// 1. Gives the agent the order tools and an approval-required refund tool.
// 2. Runs a refund conversation that pauses for a support agent's decision and resumes in
//    the same session.
// 3. Wraps the agent with all three kinds of middleware: agent run middleware that redacts
//    payment details, function calling middleware that audits tool calls, and chat client
//    middleware that shows what each model call actually receives.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Ch10.ToolsAndMiddleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Security;
using Northwind.Shared.Tools;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 10.5: Tools and middleware", options);

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();
ILogger auditLogger = loggerFactory.CreateLogger("Audit");
ILogger modelLogger = loggerFactory.CreateLogger("ModelCall");
CancellationToken cancellationToken = CancellationToken.None;

// --- Chat client middleware: runs on every model call ------------------------------------------
IChatClient chatClient = AIClientFactory.CreateChatClient(options)
    .AsBuilder()
    .Use(async (messages, chatOptions, next, ct) =>
    {
        string? latest = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        modelLogger.LogInformation("{Count} messages sent to the model; latest user text: \"{Text}\"",
            messages.Count(), latest is { Length: > 80 } ? latest[..77] + "..." : latest);
        await next(messages, chatOptions, ct);
    })
    .Build();

// --- Giving the agent tools -------------------------------------------------------------------
var currentCustomer = new FixedCurrentCustomer("C003");   // Thomas Hardy is signed in
var supportDesk = new ConsoleSupportDesk();

var store = NorthwindStore.CreateSeeded();
var returnRules = new ReturnPolicyRules(store);
var orderTools = new OrderTools(store, returnRules);
var refundTools = new RefundTools(store, returnRules, currentCustomer, loggerFactory.CreateLogger<RefundTools>());

AIAgent agent = new ChatClientAgent(
    chatClient,
    name: "NorthwindAssist",
    instructions: SupportPrompts.System,
    tools:
    [
        AIFunctionFactory.Create(orderTools.GetOrderStatus),
        AIFunctionFactory.Create(orderTools.CheckReturnEligibility),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(refundTools.RequestRefund))
    ]);

// --- Agent run and function calling middleware ---------------------------------------------------
AIAgent guardedAgent = agent
    .AsBuilder()
    .Use(runFunc: RedactPaymentDetailsAsync, runStreamingFunc: RedactPaymentDetailsStreamingAsync)
    .Use(AuditToolCallAsync)
    .UseOpenTelemetry(sourceName: "Northwind.Agents")
    .Build();

// --- Approvals across runs -----------------------------------------------------------------------
SampleConsole.Section("A refund that needs a support agent's approval");

AgentSession session = await guardedAgent.CreateSessionAsync();

AgentResponse response = await guardedAgent.RunAsync(
    "The rain jacket from order NW-10248 leaks at the seams. I'd like a refund, please.", session);
PrintToolActivity(response);

List<ToolApprovalRequestContent> pending = GetApprovalRequests(response);

while (pending.Count > 0)
{
    var decisions = new List<AIContent>();

    foreach (ToolApprovalRequestContent request in pending)
    {
        var call = (FunctionCallContent)request.ToolCall;
        bool approved = await supportDesk.RequestApprovalAsync(call.Name, call.Arguments, cancellationToken);
        decisions.Add(request.CreateResponse(approved, approved ? null : "Declined by the support agent."));
    }

    // Resume the same conversation with the decisions.
    response = await guardedAgent.RunAsync(new ChatMessage(ChatRole.User, decisions), session);
    PrintToolActivity(response);
    pending = GetApprovalRequests(response);
}

Console.WriteLine($"Assistant: {response.Text}");

// --- Redaction in agent run middleware (streaming) ----------------------------------------------
SampleConsole.Section("Payment details are removed before the model sees them");

await foreach (AgentResponseUpdate update in guardedAgent.RunStreamingAsync(
    "Actually, could the refund go to my other card instead? The number is 4539 1488 0343 6467.", session))
{
    Console.Write(update.Text);
}

Console.WriteLine();
SampleConsole.Note("The ModelCall log line shows [CARD NUMBER] where the customer typed the card number.");

SampleConsole.Section("Refund requests in the store");
foreach (RefundRequest refund in store.RefundRequests)
{
    Console.WriteLine($"{refund.RequestId}: {refund.OrderId}/{refund.ProductId} {refund.Amount:0.00} ({refund.Reason}) {refund.Status}");
}

if (store.RefundRequests.Count == 0)
{
    Console.WriteLine("None.");
}

// --- Middleware implementations -------------------------------------------------------------------

async Task<AgentResponse> RedactPaymentDetailsAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    IEnumerable<ChatMessage> redacted = messages.Select(PaymentDataRedactor.Redact);
    return await innerAgent.RunAsync(redacted, session, options, cancellationToken);
}

async IAsyncEnumerable<AgentResponseUpdate> RedactPaymentDetailsStreamingAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    IEnumerable<ChatMessage> redacted = messages.Select(PaymentDataRedactor.Redact);
    await foreach (AgentResponseUpdate update in innerAgent.RunStreamingAsync(redacted, session, options, cancellationToken))
    {
        yield return update;
    }
}

async ValueTask<object?> AuditToolCallAsync(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    long started = Stopwatch.GetTimestamp();
    string outcome = "failed";
    try
    {
        object? result = await next(context, cancellationToken);
        outcome = "succeeded";
        return result;
    }
    finally
    {
        // Logged in a finally block, so calls that throw are audited too.
        auditLogger.LogInformation(
            "Agent {Agent} called {Tool} with {Arguments}: {Outcome} in {ElapsedMs:F0} ms",
            agent.Name, context.Function.Name, context.Arguments, outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
}

static List<ToolApprovalRequestContent> GetApprovalRequests(AgentResponse response) =>
    response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();

// Shows the tool calls and results behind an answer, which response.Text does not include.
static void PrintToolActivity(AgentResponse response)
{
    foreach (AIContent content in response.Messages.SelectMany(m => m.Contents))
    {
        string? line = content switch
        {
            FunctionCallContent call => $"[call]     {call.Name}({string.Join(", ", call.Arguments?.Select(a => $"{a.Key}: {a.Value}") ?? [])})",
            FunctionResultContent result => $"[result]   {System.Text.Json.JsonSerializer.Serialize(result.Result)}",
            ToolApprovalRequestContent => "[approval] requested",
            _ => null
        };

        if (line is not null)
        {
            SampleConsole.Note(line);
        }
    }
}
