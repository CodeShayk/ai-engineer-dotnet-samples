using Xunit;

namespace Ch14.EvaluationTests;

/// <summary>
/// Cheap, exact checks that run before the model-graded evaluators (Chapter 14.4). A required
/// fact may list alternative phrasings separated by '|', such as "two years|2 years".
/// </summary>
public static class DeterministicChecks
{
    public static void AssertRequiredFactsPresent(string response, IReadOnlyList<string> requiredFacts)
    {
        foreach (string fact in requiredFacts)
        {
            string[] alternatives = fact.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Assert.True(
                alternatives.Any(a => response.Contains(a, StringComparison.OrdinalIgnoreCase)),
                $"The response does not contain the required fact \"{fact}\".\nResponse: {response}");
        }
    }

    public static void AssertForbiddenContentAbsent(string response, IReadOnlyList<string> forbiddenContent)
    {
        foreach (string forbidden in forbiddenContent)
        {
            Assert.False(
                response.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"The response contains forbidden content \"{forbidden}\".\nResponse: {response}");
        }
    }
}
