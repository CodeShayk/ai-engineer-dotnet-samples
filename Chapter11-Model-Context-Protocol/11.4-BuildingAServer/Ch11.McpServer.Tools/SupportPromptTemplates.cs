using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Ch11.McpServer.Tools;

/// <summary>A reusable prompt that hosts can offer to their users as a command (Chapter 11.4).</summary>
[McpServerPromptType]
public static class SupportPromptTemplates
{
    [McpServerPrompt(Name = "summarize_order_issue")]
    [Description("Creates a prompt that summarizes a customer's problem with an order for a support agent.")]
    public static ChatMessage SummarizeOrderIssue(
        [Description("The order number, in the format NW-#####.")] string orderNumber,
        [Description("The customer's description of the problem.")] string issue) =>
        new(ChatRole.User,
            $"Use get_order_status to look up order {orderNumber}, then summarize the customer's problem " +
            $"for a support agent in three short bullet points. The customer wrote: {issue}");
}
