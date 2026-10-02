using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

namespace Ch14.EvaluationTests;

/// <summary>What an evaluation needs from one answer: the conversation, the response and what was retrieved.</summary>
public sealed record EvaluationInput(
    List<ChatMessage> Messages,
    ChatResponse Response,
    string RetrievedText,
    IReadOnlyList<RetrievedChunk> RetrievedChunks);

/// <summary>
/// Runs the policy assistant from Chapter 9 for a golden case. The knowledge base is built once
/// per test run; the chat client is supplied by the caller, so that when it is the scenario's
/// caching client, the system under test's responses are cached along with the judge's.
/// </summary>
public static class SystemUnderTest
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static PolicyKnowledgeBase? _knowledge;

    public static async Task<PolicyKnowledgeBase> GetKnowledgeBaseAsync(CancellationToken cancellationToken = default)
    {
        if (_knowledge is not null)
        {
            return _knowledge;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            AIProviderOptions options = EvaluationSetup.AIOptions;
            return _knowledge ??= await PolicyKnowledgeBase.CreateInMemoryAsync(
                options, AIClientFactory.CreateEmbeddingGenerator(options), cancellationToken: cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<EvaluationInput> AnswerAsync(GoldenCase testCase, IChatClient chatClient, CancellationToken cancellationToken = default)
    {
        PolicyKnowledgeBase knowledge = await GetKnowledgeBaseAsync(cancellationToken);

        var assistant = new PolicyAssistant(
            new QueryRewriter(chatClient), knowledge.Retriever, new LlmReranker(chatClient), new StructuredOutputService(chatClient));

        (List<ChatMessage> conversation, ChatResponse response, IReadOnlyList<RetrievedChunk> sources) =
            await assistant.AnswerWithSourcesAsync(testCase.Question, new CustomerContext(testCase.CustomerRegion), cancellationToken);

        return new EvaluationInput(conversation, response, PolicyAssistant.FormatPassages(sources), sources);
    }
}
