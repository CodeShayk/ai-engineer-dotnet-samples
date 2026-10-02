// Chapter 12, Section 12.7: Keeping agents bounded and predictable.
// An order agent with three kinds of limit:
//   iterations  function invocation configured by us, used as provided (UseProvidedChatClientAsIs)
//   errors      a courier tracking tool that always fails, stopped after two consecutive errors
//   tokens      a per-session token budget enforced in agent run middleware
//   time        a timeout on every run

using Ch12.BoundedAgent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 12.7: A bounded agent", options);

var store = NorthwindStore.CreateSeeded();
var orderTools = new OrderTools(store, new ReturnPolicyRules(store));

// --- Iterations and tool errors ---------------------------------------------------------------------
IChatClient boundedClient = AIClientFactory.CreateChatClient(options)
    .AsBuilder()
    .UseFunctionInvocation(configure: invoker =>
    {
        invoker.MaximumIterationsPerRequest = 6;
        invoker.MaximumConsecutiveErrorsPerRequest = 2;
    })
    .Build();

const string OrderAgentInstructions =
    "You handle order status, delivery and tracking questions for Northwind Traders. Use your tools; never guess. " +
    "If the courier's tracking is unavailable, say so and give the order status instead.";

AIFunction[] orderToolList =
[
    AIFunctionFactory.Create(orderTools.GetOrderStatus),
    AIFunctionFactory.Create(GetCourierTracking)
];

AIAgent agent = new ChatClientAgent(boundedClient, new ChatClientAgentOptions
{
    Name = "order_agent",
    ChatOptions = new ChatOptions { Instructions = OrderAgentInstructions, Tools = orderToolList },
    UseProvidedChatClientAsIs = true   // Use our function invocation settings rather than the defaults
});

// --- Tokens: a small budget so you can see it run out ----------------------------------------------
var budget = new SessionTokenBudget(maxTokensPerSession: 4_000);

AIAgent boundedAgent = agent
    .AsBuilder()
    .Use(runFunc: budget.EnforceAsync, runStreamingFunc: null)
    .Build();

AgentSession session = await boundedAgent.CreateSessionAsync();

string[] questions =
[
    "Where exactly is my parcel for order NW-10249 right now? Check the courier tracking.",
    "And what's the status of order NW-10255?",
    "Can you check NW-10252 as well?",
    "One more: what about NW-10262?",
    "Last one, I promise: NW-10248?"
];

foreach (string question in questions)
{
    SampleConsole.Section(question);

    // --- Time: every run gets a deadline -----------------------------------------------------------
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

    try
    {
        AgentResponse response = await boundedAgent.RunAsync(question, session, cancellationToken: timeout.Token);
        Console.WriteLine(response.Text);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("The run did not finish within 90 seconds and was stopped. A stuck run is rarely about to succeed.");
    }
    catch (Exception ex)
    {
        // The invoker stops the run once a tool has failed MaximumConsecutiveErrorsPerRequest times in a row.
        Console.WriteLine($"The run was stopped: {ex.GetType().Name}: {ex.Message}");
    }

    SampleConsole.Note($"Tokens used by this session so far: {budget.UsedBy(session):N0} of 4,000");
}

// A courier integration that is down. It always throws, to show the consecutive error limit.
[System.ComponentModel.Description("Gets live courier tracking events for a Northwind order.")]
static string GetCourierTracking(
    [System.ComponentModel.Description("The order number, in the format NW-#####.")] string orderNumber) =>
    throw new TimeoutException("The courier's tracking service did not respond.");
