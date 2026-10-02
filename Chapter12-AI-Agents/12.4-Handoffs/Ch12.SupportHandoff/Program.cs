// Chapter 12, Sections 12.4 and 12.6: The Northwind handoff team.
// A front-desk triage agent hands customers to an order specialist or a returns specialist;
// the returns specialist can hand off to a refund agent, whose only tool requires supervisor
// approval. Every specialist can hand back to the front desk.
//
// You are signed in as Thomas Hardy (C003). Try: "Hi, the jacket from my order NW-10248 leaks at the seams."
// then "Yes please" when offered a refund. Enter on an empty line exits.
//
// Usage: dotnet run [--as-agent]
//   --as-agent  also runs one question through the team presented as a single AIAgent.

using Ch12.SupportHandoff;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Northwind.Agents.ContextProviders;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Knowledge;
using Northwind.Shared.Tools;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 12.4: The Northwind handoff team", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);
IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);

Console.WriteLine("Loading the policy library for the returns specialist...");
PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(options, embedder);
PolicyRetriever retriever = knowledge.Retriever;

var store = NorthwindStore.CreateSeeded();
var returnRules = new ReturnPolicyRules(store);
var currentCustomer = new FixedCurrentCustomer("C003");
CustomerContext customerContext = CustomerContext.For(store.FindCustomer(currentCustomer.CustomerId)!);
var orderTools = new OrderTools(store, returnRules);
var refundTools = new RefundTools(store, returnRules, currentCustomer);
var supervisor = new ConsoleSupervisor();

// --- The team: narrow instructions, their own tools, precise descriptions ---------------------------
ChatClientAgent triage = new(chatClient,
    "You are Northwind Assist's front desk. Greet the customer, work out what they need and hand off to the right " +
    "specialist. Do not answer order or policy questions yourself. If the request is outside Northwind's services, " +
    "say so politely.",
    "triage_agent", "Routes customers to the right Northwind specialist");

ChatClientAgent orders = new(chatClient,
    "You handle order status, delivery and tracking questions for the signed-in customer. Use your tools to look up " +
    "orders; never guess. Hand back to the front desk for anything else.",
    "order_agent", "Handles order status, delivery and tracking questions",
    [AIFunctionFactory.Create(orderTools.GetOrderStatus)]);

ChatClientAgent returns = new(chatClient, new ChatClientAgentOptions
{
    Name = "returns_agent",
    Description = "Explains returns, refunds and warranty policies and checks whether items can be returned",
    ChatOptions = new ChatOptions
    {
        Instructions = "You explain Northwind's returns, refunds and warranty policies using the policy excerpts " +
                       "provided, and check eligibility with your tool. If the customer wants a refund for an " +
                       "eligible item, hand off to the refund agent.",
        Tools = [AIFunctionFactory.Create(orderTools.GetOrderStatus), AIFunctionFactory.Create(orderTools.CheckReturnEligibility)]
    },
    AIContextProviders = [new PolicyKnowledgeContextProvider(retriever, customerContext)]
});

ChatClientAgent refunds = new(chatClient,
    "You raise refund requests for items the returns agent has confirmed are eligible. Confirm the order number and " +
    "item with the customer first. Every request is reviewed by a supervisor; tell the customer this.",
    "refund_agent", "Raises refund requests for eligible items, subject to supervisor approval",
    [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(refundTools.RequestRefund))]);

// --- The allowed handoffs ----------------------------------------------------------------------------
Workflow supportTeam = AgentWorkflowBuilder.CreateHandoffBuilderWith(triage)
    .WithHandoffs(triage, [orders, returns])            // The front desk routes to two specialists
    .WithHandoffs(returns, [refunds])                   // Refunds are only reachable via returns
    .WithHandoffs([orders, returns, refunds], triage)   // Every specialist can hand back
    .Build();

// --- The conversation loop ------------------------------------------------------------------------------
List<ChatMessage> conversation = [];

while (SampleConsole.Prompt("\nYou") is { } input)
{
    conversation.Add(new ChatMessage(ChatRole.User, input));

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(supportTeam, conversation);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    string? currentAgent = null;
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (evt is AgentResponseUpdateEvent update)
        {
            if (update.ExecutorId != currentAgent && !string.IsNullOrEmpty(update.Update.Text))
            {
                currentAgent = update.ExecutorId;
                SampleConsole.Note($"\n[{update.Update.AuthorName ?? currentAgent}]");
            }

            Console.Write(update.Update.Text);
        }
        else if (evt is RequestInfoEvent request &&
                 request.Request.TryGetDataAs(out ToolApprovalRequestContent? approval))
        {
            bool approved = await supervisor.ReviewAsync((FunctionCallContent)approval.ToolCall);
            await run.SendResponseAsync(request.Request.CreateResponse(approval.CreateResponse(approved)));
        }
        else if (evt is WorkflowOutputEvent output && output.As<List<ChatMessage>>() is { } updated)
        {
            conversation.AddRange(updated.Skip(conversation.Count));
            break;   // The current agent is waiting for the customer's next message.
        }
        else if (evt is WorkflowErrorEvent error)
        {
            Console.WriteLine($"\nWorkflow error: {error.Data}");
            break;
        }
    }

    Console.WriteLine();
}

SampleConsole.Section("Refund requests in the store");
foreach (RefundRequest refund in store.RefundRequests)
{
    Console.WriteLine($"{refund.RequestId}: {refund.OrderId}/{refund.ProductId} {refund.Amount:0.00} ({refund.Reason}) {refund.Status}");
}

if (store.RefundRequests.Count == 0)
{
    Console.WriteLine("None.");
}

// --- Presenting the team as one agent -------------------------------------------------------------------
if (args.Contains("--as-agent"))
{
    SampleConsole.Section("The whole team as a single AIAgent");

    AIAgent northwindAssist = supportTeam.AsAIAgent(name: "NorthwindAssist");
    AgentSession session = await northwindAssist.CreateSessionAsync();

    AgentResponse response = await northwindAssist.RunAsync("When will my order NW-10249 arrive?", session);
    Console.WriteLine(response.Text);
    SampleConsole.Note("The caller sees one agent; the handoffs happened behind it.");
}
