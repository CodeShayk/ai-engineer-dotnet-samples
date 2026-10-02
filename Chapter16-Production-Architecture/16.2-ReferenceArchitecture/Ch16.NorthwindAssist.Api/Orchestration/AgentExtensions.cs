using Ch16.NorthwindAssist.Api.Deployment;
using Ch16.NorthwindAssist.Api.Knowledge;
using Ch16.NorthwindAssist.Api.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Northwind.Agents.ContextProviders;
using Northwind.Agents.Security;
using Northwind.Shared.Domain;
using Northwind.Shared.Knowledge;
using Northwind.Shared.Tools;

namespace Ch16.NorthwindAssist.Api.Orchestration;

/// <summary>
/// Builds Northwind Assist for the signed-in customer (Chapters 10 and 12): the handoff team from
/// Chapter 12, with context providers, least-privilege tools, an approval-required refund tool and
/// an action limit, exposed as a single agent. Agents are cheap to create, so one is built per
/// request around the expensive shared pieces (the chat client and the knowledge base).
/// </summary>
public sealed class NorthwindAssistFactory(
    IChatClient chatClient,
    KnowledgeBaseProvider knowledge,
    NorthwindStore store,
    ICurrentCustomer currentCustomer,
    OrderTools orderTools,
    RefundTools refundTools,
    McpToolSource mcpTools,
    ActionRateLimiter actionLimiter,
    CanaryAssignment canary)
{
    private const string TriageInstructions =
        "You are Northwind Assist's front desk. Greet the customer, work out what they need and hand off to the right " +
        "specialist. Do not answer order or policy questions yourself. If the request is outside Northwind's services, " +
        "say so politely.";

    // The canary variant: the same role, phrased to ask one clarifying question when the request is unclear.
    private const string TriageInstructionsCanary = TriageInstructions +
        " If you cannot tell what the customer needs, ask one short clarifying question before handing off.";

    public string VariantFor(string conversationId) => canary.IsCanary(conversationId) ? "canary" : "stable";

    public async Task<AIAgent> CreateAsync(string variant, CancellationToken cancellationToken)
    {
        PolicyKnowledgeBase knowledgeBase = await knowledge.GetAsync();
        Customer customer = store.FindCustomer(currentCustomer.CustomerId)
            ?? throw new InvalidOperationException("The signed-in customer does not exist.");
        CustomerContext customerContext = CustomerContext.For(customer);

        // In-process tools by default; allowlisted MCP tools replace them when a server is configured.
        IReadOnlyList<AITool> remoteTools = await mcpTools.GetToolsAsync(cancellationToken);
        List<AITool> orderToolSet = remoteTools.Count > 0
            ? [.. remoteTools]
            : [AIFunctionFactory.Create(orderTools.GetOrderStatus), AIFunctionFactory.Create(orderTools.CheckReturnEligibility)];

        var profile = new CustomerProfileContextProvider(store, currentCustomer);

        // Every agent gets a stable Id. The team is rebuilt on each request, and a persisted session
        // records the structure of the workflow it came from: a team rebuilt with new random agent
        // ids would refuse to resume it.
        ChatClientAgent triage = new(chatClient, new ChatClientAgentOptions
        {
            Id = "triage_agent",
            Name = "triage_agent",
            Description = "Routes customers to the right Northwind specialist",
            ChatOptions = new ChatOptions { Instructions = variant == "canary" ? TriageInstructionsCanary : TriageInstructions }
        });

        ChatClientAgent orders = new(chatClient, new ChatClientAgentOptions
        {
            Id = "order_agent",
            Name = "order_agent",
            Description = "Handles order status, delivery and tracking questions",
            ChatOptions = new ChatOptions
            {
                Instructions = "You handle order status, delivery and tracking questions for the signed-in customer. Use your " +
                               "tools to look up orders; never guess. Hand back to the front desk for anything else.",
                Tools = orderToolSet
            },
            AIContextProviders = [profile]
        });

        ChatClientAgent returns = new(chatClient, new ChatClientAgentOptions
        {
            Id = "returns_agent",
            Name = "returns_agent",
            Description = "Explains returns, refunds and warranty policies and checks whether items can be returned",
            ChatOptions = new ChatOptions
            {
                Instructions = "You explain Northwind's returns, refunds and warranty policies using the policy excerpts " +
                               "provided, and check eligibility with your tools. If the customer wants a refund for an " +
                               "eligible item, hand off to the refund agent.",
                Tools = orderToolSet
            },
            AIContextProviders = [profile, new PolicyKnowledgeContextProvider(knowledgeBase.Retriever, customerContext)]
        });

        // The only write tool in the system: approval required, and limited per conversation.
        AIAgent refunds = new ChatClientAgent(chatClient, new ChatClientAgentOptions
            {
                Id = "refund_agent",
                Name = "refund_agent",
                Description = "Raises refund requests for eligible items, subject to supervisor approval",
                ChatOptions = new ChatOptions
                {
                    Instructions = "You raise refund requests for items the returns agent has confirmed are eligible. Confirm the " +
                                   "order number and item with the customer first. Every request is reviewed by a supervisor; " +
                                   "tell the customer this.",
                    Tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(refundTools.RequestRefund))]
                }
            })
            .AsBuilder()
            .Use(actionLimiter.EnforceAsync)
            .Build();

        Workflow supportTeam = AgentWorkflowBuilder.CreateHandoffBuilderWith(triage)
            .WithHandoffs(triage, [orders, returns])
            .WithHandoffs(returns, [refunds])
            .WithHandoffs([orders, returns, refunds], triage)
            .Build();

        return supportTeam
            .AsAIAgent(id: "northwind-assist", name: "NorthwindAssist")
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "Northwind.Agents", configure: telemetry => telemetry.EnableSensitiveData = false)
            .Build();
    }
}

public static class AgentExtensions
{
    public static IServiceCollection AddNorthwindAssistAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new CanaryAssignment(configuration.GetValue("Canary:Percent", 10)));
        services.AddScoped<NorthwindAssistFactory>();
        services.AddSingleton<Endpoints.ConversationRunner>();
        return services;
    }
}
