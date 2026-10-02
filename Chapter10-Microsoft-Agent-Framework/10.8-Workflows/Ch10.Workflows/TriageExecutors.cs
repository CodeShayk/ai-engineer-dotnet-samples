using Ch05.TypedResponses;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Knowledge;

namespace Ch10.Workflows;

/// <summary>Classifies the request with structured output (Chapter 5). Its return value flows along the edges.</summary>
internal sealed partial class TriageExecutor(IChatClient chatClient) : Executor("Triage")
{
    [MessageHandler]
    private async ValueTask<TriagedRequest> HandleAsync(SupportRequest request, IWorkflowContext context)
    {
        ChatResponse<TicketTriage> response = await chatClient.GetResponseAsync<TicketTriage>(
            [
                new ChatMessage(ChatRole.System, "You triage customer support messages for Northwind Traders."),
                new ChatMessage(ChatRole.User, request.Message)
            ],
            new ChatOptions { Temperature = 0 });

        return new TriagedRequest(request, response.Result);
    }
}

/// <summary>Delegates order questions to an agent with the order tools, and yields its reply.</summary>
[YieldsOutput(typeof(SupportOutcome))]
internal sealed partial class OrderSupportExecutor(AIAgent orderAgent) : Executor("OrderSupport")
{
    [MessageHandler]
    private async ValueTask HandleAsync(TriagedRequest request, IWorkflowContext context)
    {
        AgentResponse response = await orderAgent.RunAsync(request.Request.Message);
        await context.YieldOutputAsync(new SupportOutcome("Orders", response.Text));
    }
}

/// <summary>Answers policy questions with the RAG assistant from Chapter 9.</summary>
[YieldsOutput(typeof(SupportOutcome))]
internal sealed partial class PolicySupportExecutor(PolicyAssistant policyAssistant, NorthwindStore store) : Executor("PolicySupport")
{
    [MessageHandler]
    private async ValueTask HandleAsync(TriagedRequest request, IWorkflowContext context)
    {
        Customer? customer = store.FindCustomer(request.Request.CustomerId);
        CustomerContext customerContext = customer is null ? new CustomerContext("global") : CustomerContext.For(customer);

        AssistantAnswer answer = await policyAssistant.AskAsync(request.Request.Message, [], customerContext, CancellationToken.None);
        await context.YieldOutputAsync(new SupportOutcome("Policies", answer.Text));
    }
}

/// <summary>Hands urgent or unclear requests to a person.</summary>
[YieldsOutput(typeof(SupportOutcome))]
internal sealed partial class EscalationExecutor() : Executor("Escalation")
{
    [MessageHandler]
    private async ValueTask HandleAsync(TriagedRequest request, IWorkflowContext context)
    {
        string reference = $"NW-SUP-{Random.Shared.Next(100000, 999999)}";
        await context.YieldOutputAsync(new SupportOutcome("Escalated",
            $"Thank you for getting in touch. A member of our support team will contact you shortly (reference {reference})."));
    }
}
