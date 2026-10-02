using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Configuration;
using PortfolioStarter.AI;
using PortfolioStarter.Features;
using Xunit;

namespace PortfolioStarter.Tests;

/// <summary>
/// One golden case, to grow into a dataset (Chapter 14). Runs against a live model only when
/// RUN_MODEL_TESTS=true. Responses are cached under eval-results, so re-runs are fast and stable.
/// </summary>
public sealed class GoldenCaseTests
{
    private static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_MODEL_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    private static readonly Lazy<IChatClient> Model = new(() =>
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        ModelOptions options = configuration.GetSection("AI").Get<ModelOptions>() ?? new ModelOptions();
        return ModelClients.CreateChatClient(options);
    });

    private static readonly Lazy<ReportingConfiguration> Reporting = new(() => DiskBasedReportingConfiguration.Create(
        storageRootPath: Path.Combine(AppContext.BaseDirectory, "eval-results"),
        evaluators: [new RelevanceEvaluator()],
        chatConfiguration: new ChatConfiguration(Model.Value),
        enableResponseCaching: true,
        executionName: Environment.GetEnvironmentVariable("EVAL_EXECUTION_NAME") ?? "local"));

    [Fact]
    public async Task Late_order_message_is_triaged_correctly()
    {
        Assert.SkipUnless(Enabled, "Set RUN_MODEL_TESTS=true to run the golden case against a live model.");

        ExtractionResult result = await new TicketExtractionService(Model.Value).ExtractAsync(
            "My order A-12345 still hasn't arrived after two weeks and I need it for a trip on Friday.",
            TestContext.Current.CancellationToken);

        // Deterministic checks on the fields that matter.
        Assert.True(result.Succeeded, string.Join("; ", result.Problems));
        Assert.Equal(TicketCategory.OrderStatus, result.Ticket!.Category);
        Assert.Contains("A-12345", result.Ticket.OrderNumbers);
    }

    [Fact]
    public async Task Answer_is_relevant_to_the_question()
    {
        Assert.SkipUnless(Enabled, "Set RUN_MODEL_TESTS=true to run the golden case against a live model.");

        const string question = "What should I check before returning headphones?";
        await using ScenarioRun scenario = await Reporting.Value.CreateScenarioRunAsync(
            "Answering.returning-headphones", cancellationToken: TestContext.Current.CancellationToken);

        var answers = new AnswerService(scenario.ChatConfiguration!.ChatClient);
        string answer = await answers.AnswerAsync(question, TestContext.Current.CancellationToken);

        EvaluationResult result = await scenario.EvaluateAsync(
            [new ChatMessage(ChatRole.System, AnswerService.Instructions), new ChatMessage(ChatRole.User, question)],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)),
            cancellationToken: TestContext.Current.CancellationToken);

        NumericMetric relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);

        // Diagnostics explain judge problems, such as a reply the evaluator could not parse;
        // small local models make unreliable judges, so use a capable one (AI:ChatDeployment).
        string diagnostics = string.Join(" ", relevance.Diagnostics?.Select(d => d.Message) ?? []);
        Assert.True(relevance.Interpretation?.Failed != true, $"Relevance {relevance.Value}: {relevance.Reason} {diagnostics}");
    }
}
