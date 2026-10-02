using System.Text.Json;

namespace Ch14.EvaluationTests;

/// <summary>One case in the golden dataset (Chapter 14.4).</summary>
public sealed record GoldenCase(
    string Id,
    string Category,
    string Question,
    string CustomerRegion,
    string ReferenceAnswer,
    IReadOnlyList<string> RequiredFacts,
    IReadOnlyList<string> ForbiddenContent,
    IReadOnlyList<string> RelevantDocuments,
    IReadOnlyList<string> Tags);

/// <summary>Loads the versioned golden dataset that ships with the tests.</summary>
public static class GoldenDataset
{
    private static readonly Lazy<IReadOnlyList<GoldenCase>> Cases = new(() =>
        JsonSerializer.Deserialize<List<GoldenCase>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "golden-dataset.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!);

    public static IReadOnlyList<GoldenCase> All => Cases.Value;

    public static GoldenCase Get(string id) => All.Single(c => c.Id == id);
}
