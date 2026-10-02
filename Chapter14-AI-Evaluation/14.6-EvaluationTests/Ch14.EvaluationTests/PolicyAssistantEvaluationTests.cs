using Ch14.CustomEvaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Xunit;

namespace Ch14.EvaluationTests;

/// <summary>The golden dataset as an xUnit theory, one test per case (Chapter 14.6).</summary>
public sealed class PolicyAssistantEvaluationTests
{
    private static readonly Lazy<ReportingConfiguration> Reporting = new(() => DiskBasedReportingConfiguration.Create(
        storageRootPath: EvaluationSetup.ResultsDirectory,
        evaluators:
        [
            new RelevanceEvaluator(),
            new GroundednessEvaluator(),
            new CompletenessEvaluator(),
            new CitationValidityEvaluator()
        ],
        chatConfiguration: new ChatConfiguration(EvaluationSetup.CreateJudgeClient()),
        enableResponseCaching: true,
        executionName: EvaluationSetup.ExecutionName));

    public static TheoryData<string> CaseIds => new(GoldenDataset.All.Select(c => c.Id));

    [Theory]
    [MemberData(nameof(CaseIds))]
    public async Task Policy_answer_meets_the_quality_bar(string caseId)
    {
        Assert.SkipUnless(EvaluationSetup.Enabled, "Set RUN_MODEL_TESTS=true to run evaluations against live models.");

        GoldenCase testCase = GoldenDataset.Get(caseId);
        await using ScenarioRun scenario = await Reporting.Value.CreateScenarioRunAsync(
            $"PolicyAssistant.{caseId}", cancellationToken: TestContext.Current.CancellationToken);

        // Answer using the scenario's chat client, so the system under test's responses are cached too.
        EvaluationInput input = await SystemUnderTest.AnswerAsync(
            testCase, scenario.ChatConfiguration!.ChatClient, TestContext.Current.CancellationToken);

        // Cheap, exact checks first.
        DeterministicChecks.AssertRequiredFactsPresent(input.Response.Text, testCase.RequiredFacts);
        DeterministicChecks.AssertForbiddenContentAbsent(input.Response.Text, testCase.ForbiddenContent);

        EvaluationResult result = await scenario.EvaluateAsync(
            input.Messages,
            input.Response,
            additionalContext:
            [
                new GroundednessEvaluatorContext(input.RetrievedText),
                new CompletenessEvaluatorContext(testCase.ReferenceAnswer),
                new RetrievedSourcesContext(input.RetrievedChunks)
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        string failures = string.Join("; ", result.Metrics.Values
            .Where(m => m.Interpretation?.Failed == true)
            .Select(m => $"{m.Name}: {m.Reason}"));

        Assert.True(failures.Length == 0, failures);
    }
}
