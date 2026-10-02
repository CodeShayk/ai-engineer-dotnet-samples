using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Testing;
using Xunit;

namespace Ch04.ProviderSwitching.Tests;

/// <summary>
/// Reasoning models reject sampling settings such as temperature and count hidden reasoning tokens
/// against the output limit, so the shared factory adapts requests for those models (Section 4.7).
/// These tests check which models are recognized and how their requests are adapted.
/// </summary>
public sealed class ReasoningModelTests
{
    [Theory]
    [InlineData(AIProvider.AzureOpenAI, "gpt-5-mini", true)]
    [InlineData(AIProvider.OpenAI, "gpt-5", true)]
    [InlineData(AIProvider.AzureOpenAI, "o4-mini", true)]
    [InlineData(AIProvider.AzureOpenAI, "gpt-4.1-mini", false)]
    [InlineData(AIProvider.AzureOpenAI, "orders-chat", false)]
    [InlineData(AIProvider.Ollama, "gpt-oss", false)]
    public void Reasoning_models_are_recognized_by_name(AIProvider provider, string model, bool expected)
    {
        AIProviderOptions options = CreateOptions(provider);

        Assert.Equal(expected, options.IsReasoningModel(model));
    }

    [Fact]
    public void Listed_deployments_count_as_reasoning_models()
    {
        AIProviderOptions options = CreateOptions(AIProvider.AzureOpenAI) with { ReasoningDeployments = ["chat-main"] };

        Assert.True(options.IsReasoningModel("chat-main"));
        Assert.True(options.IsReasoningModel("CHAT-MAIN"));
    }

    [Fact]
    public async Task Sampling_settings_are_removed_and_reasoning_headroom_added()
    {
        (IChatClient client, Func<ChatOptions?> received) = CreateRecordingReasoningClient();
        var original = new ChatOptions { Temperature = 0, TopP = 0.9f, PresencePenalty = 0.5f, MaxOutputTokens = 50 };

        await client.GetResponseAsync("Where is order NW-10249?", original, TestContext.Current.CancellationToken);

        ChatOptions? sent = received();
        Assert.NotNull(sent);
        Assert.Null(sent.Temperature);
        Assert.Null(sent.TopP);
        Assert.Null(sent.PresencePenalty);
        Assert.Equal(ReasoningEffort.Low, sent.Reasoning?.Effort);
        Assert.Equal(50 + AIClientFactory.ReasoningTokenAllowance, sent.MaxOutputTokens);

        // The caller's options are left untouched.
        Assert.Equal(0, original.Temperature);
        Assert.Equal(50, original.MaxOutputTokens);
        Assert.Null(original.Reasoning);
    }

    [Fact]
    public async Task Requests_that_set_their_own_reasoning_options_keep_their_limit()
    {
        (IChatClient client, Func<ChatOptions?> received) = CreateRecordingReasoningClient();
        var original = new ChatOptions { MaxOutputTokens = 2_000, Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High } };

        await client.GetResponseAsync("Where is order NW-10249?", original, TestContext.Current.CancellationToken);

        Assert.Equal(ReasoningEffort.High, received()?.Reasoning?.Effort);
        Assert.Equal(2_000, received()?.MaxOutputTokens);
    }

    [Fact]
    public async Task Requests_without_a_limit_get_low_effort_and_no_limit()
    {
        (IChatClient client, Func<ChatOptions?> received) = CreateRecordingReasoningClient();

        await client.GetResponseAsync("Where is order NW-10249?", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ReasoningEffort.Low, received()?.Reasoning?.Effort);
        Assert.Null(received()?.MaxOutputTokens);
    }

    private static (IChatClient Client, Func<ChatOptions?> Received) CreateRecordingReasoningClient()
    {
        ChatOptions? received = null;
        IChatClient recorder = new FakeChatClient("ok")
            .AsBuilder()
            .Use((messages, options, next, cancellationToken) =>
            {
                received = options;
                return next(messages, options, cancellationToken);
            })
            .Build();

        return (AIClientFactory.ForReasoningModel(recorder), () => received);
    }

    private static AIProviderOptions CreateOptions(AIProvider provider) => new()
    {
        Provider = provider,
        Endpoint = "https://example.openai.azure.com/openai/v1/",
        ApiKey = "test",
        ChatDeployment = "gpt-5-mini",
        SmallChatDeployment = "gpt-5-mini",
        JudgeChatDeployment = "gpt-5-mini",
        EmbeddingDeployment = "text-embedding-3-small",
        EmbeddingDimensions = 1536
    };
}
