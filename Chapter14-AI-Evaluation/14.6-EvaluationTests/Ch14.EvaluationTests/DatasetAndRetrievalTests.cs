using Northwind.Shared.Knowledge;
using Xunit;

namespace Ch14.EvaluationTests;

public sealed class DatasetAndRetrievalTests
{
    // --- Runs on every build: the dataset itself must be valid -----------------------------------------

    [Fact]
    public void Golden_dataset_is_well_formed()
    {
        IReadOnlyList<GoldenCase> cases = GoldenDataset.All;
        HashSet<string> documentIds = PolicyLibrary.LoadAll().Select(d => d.Id).ToHashSet();

        Assert.NotEmpty(cases);
        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct().Count());

        foreach (GoldenCase testCase in cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(testCase.Question), $"{testCase.Id}: question is empty");
            Assert.False(string.IsNullOrWhiteSpace(testCase.ReferenceAnswer), $"{testCase.Id}: reference answer is empty");
            Assert.All(testCase.RelevantDocuments, d =>
                Assert.True(documentIds.Contains(d), $"{testCase.Id}: unknown relevant document '{d}'"));
        }
    }

    [Fact]
    public void Golden_dataset_covers_safety_cases()
    {
        // A dataset of only happy paths hides the failures that matter most.
        Assert.Contains(GoldenDataset.All, c => c.Tags.Contains("safety"));
        Assert.Contains(GoldenDataset.All, c => c.Category == "out-of-scope");
    }

    // --- Runs with RUN_MODEL_TESTS=true: retrieval hit rate (Section 14.2) -------------------------------

    [Fact]
    public async Task Retrieval_finds_a_relevant_document_for_most_cases()
    {
        Assert.SkipUnless(EvaluationSetup.Enabled, "Set RUN_MODEL_TESTS=true to measure retrieval against live models.");

        PolicyKnowledgeBase knowledge = await SystemUnderTest.GetKnowledgeBaseAsync(TestContext.Current.CancellationToken);
        List<GoldenCase> cases = GoldenDataset.All.Where(c => c.RelevantDocuments.Count > 0).ToList();

        var misses = new List<string>();
        foreach (GoldenCase testCase in cases)
        {
            IReadOnlyList<RetrievedChunk> results = await knowledge.Retriever.SearchAsync(
                testCase.Question, new CustomerContext(testCase.CustomerRegion), top: 5, TestContext.Current.CancellationToken);

            if (!results.Any(r => testCase.RelevantDocuments.Contains(r.DocumentId)))
            {
                misses.Add(testCase.Id);
            }
        }

        double hitRate = 1.0 - (double)misses.Count / cases.Count;
        TestContext.Current.SendDiagnosticMessage($"Retrieval hit rate (top 5): {hitRate:P0}");

        Assert.True(hitRate >= 0.8, $"Hit rate {hitRate:P0} is below 80%. Missed: {string.Join(", ", misses)}");
    }

    [Fact]
    public async Task Internal_documents_are_never_retrieved_for_customers()
    {
        Assert.SkipUnless(EvaluationSetup.Enabled, "Set RUN_MODEL_TESTS=true to measure retrieval against live models.");

        PolicyKnowledgeBase knowledge = await SystemUnderTest.GetKnowledgeBaseAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<RetrievedChunk> results = await knowledge.Retriever.SearchAsync(
            "goodwill credit for late deliveries", new CustomerContext("global"), top: 10, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(results, r => r.DocumentId == "goodwill-guidelines");
    }
}
