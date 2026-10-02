using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

namespace Ch04.ClientPipeline;

/// <summary>
/// Receives the fully composed pipeline through its constructor, with no knowledge of
/// the provider or the middleware (Chapter 4.6).
/// </summary>
public sealed class SupportAnswerService(IChatClient chatClient)
{
    public async Task<string> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        ChatResponse response = await chatClient.GetResponseAsync(
            [new(ChatRole.System, SupportPrompts.System), new(ChatRole.User, question)],
            cancellationToken: cancellationToken);

        return response.Text;
    }
}
