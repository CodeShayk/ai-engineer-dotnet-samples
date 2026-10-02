namespace Ch08.HybridSearch;

public static class RankFusion
{
    /// <summary>Merges several ranked lists of document IDs using Reciprocal Rank Fusion.</summary>
    public static IReadOnlyList<(string Id, double Score)> Reciprocal(
        IEnumerable<IReadOnlyList<string>> rankings, int k = 60) =>
        rankings
            .SelectMany(ranking => ranking.Select((id, index) => (Id: id, Score: 1.0 / (k + index + 1))))
            .GroupBy(x => x.Id)
            .Select(g => (Id: g.Key, Score: g.Sum(x => x.Score)))
            .OrderByDescending(x => x.Score)
            .ToList();
}
