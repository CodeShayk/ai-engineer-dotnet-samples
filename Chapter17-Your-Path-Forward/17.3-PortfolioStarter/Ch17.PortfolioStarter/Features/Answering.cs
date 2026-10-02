using Microsoft.Extensions.AI;

namespace PortfolioStarter.Features;

/// <summary>
/// A minimal question-answering feature. Replace the instructions and add retrieval (Chapter 9)
/// for portfolio project 1. The system prompt stays on the server, always.
/// </summary>
public sealed class AnswerService(IChatClient chatClient)
{
    public const string Instructions = """
        You are a helpful assistant for a small online store.
        Answer briefly and in plain English. If you are not sure, say so rather than guessing.
        """;

    public async Task<string> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        ChatResponse response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instructions), new ChatMessage(ChatRole.User, question)],
            new ChatOptions { MaxOutputTokens = 400 },
            cancellationToken);

        return response.Text;
    }
}
