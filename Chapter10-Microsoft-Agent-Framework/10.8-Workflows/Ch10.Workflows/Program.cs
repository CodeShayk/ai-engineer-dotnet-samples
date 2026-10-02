// Chapter 10, Section 10.8: Building workflows in C#.
// 1. A function executor created with BindAsExecutor, run on its own.
// 2. The support triage workflow: a structured-output triage step, then conditional edges to an
//    order agent, the RAG policy assistant or escalation to a person.
// 3. The refund workflow: validation in code, a request port that pauses for a supervisor's
//    decision, and checkpoints saved at every superstep.
//
// Usage: dotnet run [triage|refund]     (default: both)

using Ch05.TypedResponses;
using Ch10.Workflows;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Knowledge;
using Northwind.Shared.Tools;

string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "both";

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 10.8: Workflows", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);
IChatClient smallModel = AIClientFactory.CreateChatClient(options, options.SmallChatDeployment);
var store = NorthwindStore.CreateSeeded();
var returnRules = new ReturnPolicyRules(store);
CancellationToken cancellationToken = CancellationToken.None;

// --- A function as an executor -------------------------------------------------------------------
SampleConsole.Section("A function executor");

Func<string, string> normalize = text => text.Trim().ReplaceLineEndings(" ");
var normalizeExecutor = normalize.BindAsExecutor("Normalize");

Workflow normalizeOnly = new WorkflowBuilder(normalizeExecutor).WithOutputFrom(normalizeExecutor).Build();
Run normalized = await InProcessExecution.RunAsync(normalizeOnly, "   Where is\r\nmy order?   ");
foreach (WorkflowOutputEvent output in normalized.NewEvents.OfType<WorkflowOutputEvent>())
{
    Console.WriteLine($"Normalized: \"{output.Data}\"");
}

if (mode is "both" or "triage")
{
    await RunTriageWorkflowAsync();
}

if (mode is "both" or "refund")
{
    await RunRefundWorkflowAsync();
}

// --- The support triage workflow -------------------------------------------------------------------
async Task RunTriageWorkflowAsync()
{
    SampleConsole.Section("Support triage workflow");
    Console.WriteLine("Loading the policy library for the policy support step...");

    IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
    PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(options, embedder);

    var policyAssistant = new PolicyAssistant(
        new QueryRewriter(smallModel), knowledge.Retriever, new LlmReranker(smallModel), new StructuredOutputService(chatClient));

    var orderTools = new OrderTools(store, returnRules);
    AIAgent orderAgent = new ChatClientAgent(
        chatClient,
        name: "OrderSupport",
        instructions: SupportPrompts.System,
        tools: [AIFunctionFactory.Create(orderTools.GetOrderStatus), AIFunctionFactory.Create(orderTools.CheckReturnEligibility)]);

    var triage = new TriageExecutor(chatClient);
    var orders = new OrderSupportExecutor(orderAgent);
    var policies = new PolicySupportExecutor(policyAssistant, store);
    var escalation = new EscalationExecutor();

    static bool IsUrgentOrUnclear(TriagedRequest? t) =>
        t!.Triage.Urgency == Urgency.High || t.Triage.Category == TicketCategory.Other;

    Workflow workflow = new WorkflowBuilder(triage)
        .AddEdge<TriagedRequest>(triage, escalation, t => IsUrgentOrUnclear(t))
        .AddEdge<TriagedRequest>(triage, orders, t => !IsUrgentOrUnclear(t) && t!.Triage.Category == TicketCategory.OrderStatus)
        .AddEdge<TriagedRequest>(triage, policies, t => !IsUrgentOrUnclear(t) && t!.Triage.Category != TicketCategory.OrderStatus)
        .WithOutputFrom(orders, policies, escalation)
        .Build();

    SupportRequest[] requests =
    [
        new("C003", "My earbuds from order NW-10249 still haven't arrived and the tracking hasn't moved in days."),
        new("C005", "Hello, could you tell me when order NW-10255 is due to arrive?"),
        new("C003", "Can I return a kettle if I've already used it once?"),
        new("C001", "You charged my card twice for the same order and nobody answers the phone. Fix this now!")
    ];

    foreach (SupportRequest request in requests)
    {
        Console.WriteLine();
        Console.WriteLine($"{request.CustomerId}: {request.Message}");

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, request);

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case ExecutorCompletedEvent completed:
                    SampleConsole.Note($"  [{completed.ExecutorId}] completed");
                    break;

                case WorkflowOutputEvent output when output.As<SupportOutcome>() is { } outcome:
                    Console.WriteLine($"Route: {outcome.Route}\n{outcome.Reply}");
                    break;

                case WorkflowErrorEvent error:
                    Console.WriteLine($"Workflow error: {error.Data}");
                    break;
            }
        }
    }
}

// --- The refund workflow with a request port and checkpoints ----------------------------------------
async Task RunRefundWorkflowAsync()
{
    SampleConsole.Section("Refund workflow with supervisor approval");

    var validate = new ValidateRefundExecutor(store, returnRules);
    var execute = new ExecuteRefundExecutor(store);
    var approvalPort = RequestPort.Create<RefundApprovalRequest, RefundDecision>("SupervisorApproval");
    var supportDesk = new ConsoleSupportDesk();

    Workflow refunds = new WorkflowBuilder(validate)
        .AddEdge(validate, approvalPort)
        .AddEdge(approvalPort, execute)
        .WithOutputFrom(validate, execute)
        .Build();

    RefundCase[] cases =
    [
        new("C003", "NW-10248", "P05", RefundReason.DoesNotFit),        // Thomas Hardy's own jacket: needs approval
        new("C003", "NW-10252", "P02", RefundReason.DamagedOrFaulty)    // Someone else's order: rejected in code
    ];

    foreach (RefundCase refundCase in cases)
    {
        Console.WriteLine();
        Console.WriteLine($"Refund case: {refundCase}");

        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();   // Use durable storage in production
        int checkpointCount = 0;

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(refunds, refundCase, checkpoints);

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is RequestInfoEvent request && request.Request.TryGetDataAs(out RefundApprovalRequest? approval))
            {
                RefundDecision decision = await supportDesk.DecideAsync(approval, cancellationToken);
                await run.SendResponseAsync(request.Request.CreateResponse(decision));
            }
            else if (evt is SuperStepCompletedEvent { CompletionInfo.Checkpoint: not null })
            {
                checkpointCount++;
            }
            else if (evt is WorkflowOutputEvent output && output.As<RefundOutcome>() is { } outcome)
            {
                Console.WriteLine($"Outcome: {outcome.Status}. {outcome.Message}");
            }
            else if (evt is WorkflowErrorEvent error)
            {
                Console.WriteLine($"Workflow error: {error.Data}");
            }
        }

        SampleConsole.Note($"{checkpointCount} checkpoint(s) saved. A durable store would let this run resume after a restart.");
    }
}
