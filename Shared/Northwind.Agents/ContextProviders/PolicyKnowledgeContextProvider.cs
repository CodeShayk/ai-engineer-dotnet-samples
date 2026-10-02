using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Shared.Knowledge;

namespace Northwind.Agents.ContextProviders;

/// <summary>
/// Brings Chapter 9's retrieval into an agent (Chapter 10.6). Before each run it retrieves the
/// policy passages most relevant to the customer's latest message and supplies them as
/// transient instructions, so retrieved text never accumulates in the conversation history.
/// </summary>
public sealed class PolicyKnowledgeContextProvider(PolicyRetriever retriever, CustomerContext customer)
    : AIContextProvider
{
    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        string? question = context.AIContext.Messages?.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        if (string.IsNullOrWhiteSpace(question))
        {
            return new AIContext();
        }

        IReadOnlyList<RetrievedChunk> passages = await retriever.SearchAsync(question, customer, top: 4, cancellationToken);
        if (passages.Count == 0)
        {
            return new AIContext();
        }

        return new AIContext
        {
            Instructions = $"""
                Policy excerpts that may be relevant to the customer's latest message. Use them only if they
                are relevant, rely on them rather than general knowledge, and cite the ids you use.
                <sources>
                {string.Join("\n", passages.Select(GroundedPrompt.FormatSource))}
                </sources>
                """
        };
    }
}
