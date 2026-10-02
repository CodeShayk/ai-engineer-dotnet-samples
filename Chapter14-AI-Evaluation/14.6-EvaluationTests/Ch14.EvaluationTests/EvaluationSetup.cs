using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

namespace Ch14.EvaluationTests;

/// <summary>Switches and settings for the evaluation run, driven by environment variables in CI.</summary>
public static class EvaluationSetup
{
    private static readonly Lazy<AIProviderOptions> Options = new(SampleConfiguration.LoadAIOptions);

    /// <summary>Evaluations call live models, so they run only when RUN_MODEL_TESTS=true.</summary>
    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_MODEL_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Where results and cached responses are stored: EVAL_RESULTS_PATH, or eval-results beside the solution.</summary>
    public static string ResultsDirectory =>
        Environment.GetEnvironmentVariable("EVAL_RESULTS_PATH") is { Length: > 0 } path
            ? path
            : Path.Combine(FindSolutionDirectory(), "eval-results");

    /// <summary>The name results are stored under, typically the build number in CI.</summary>
    public static string ExecutionName { get; } =
        Environment.GetEnvironmentVariable("EVAL_EXECUTION_NAME") is { Length: > 0 } name
            ? name
            : $"local-{DateTime.Now:yyyyMMdd-HHmmss}";

    public static AIProviderOptions AIOptions => Options.Value;

    /// <summary>The judge model: AI:JudgeChatDeployment, which defaults to the chat model.</summary>
    public static IChatClient CreateJudgeClient() =>
        AIClientFactory.CreateChatClient(Options.Value, Options.Value.JudgeChatDeployment);

    private static string FindSolutionDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NorthwindAI.slnx")))
            {
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
