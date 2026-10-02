using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Northwind.Shared.Security;

/// <summary>Whether an attack was detected in the user's prompt, in supplied documents, or both.</summary>
public sealed record PromptAttackResult(bool UserPromptAttack, bool DocumentAttack)
{
    public bool AttackDetected => UserPromptAttack || DocumentAttack;
}

/// <summary>Screens input for prompt injection before it reaches a model (Chapter 13.2).</summary>
public interface IPromptAttackDetector
{
    Task<PromptAttackResult> AnalyzeAsync(string userPrompt, IReadOnlyList<string> documents, CancellationToken cancellationToken);
}

/// <summary>
/// Azure AI Content Safety Prompt Shields, which analyzes the user's prompt for direct attacks
/// and supplied documents for indirect attacks. The HttpClient must be configured with the
/// Content Safety endpoint as its base address and with authentication, ideally a managed identity.
/// </summary>
public sealed class PromptShieldsDetector(HttpClient http) : IPromptAttackDetector
{
    public async Task<PromptAttackResult> AnalyzeAsync(
        string userPrompt, IReadOnlyList<string> documents, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(
            "contentsafety/text:shieldPrompt?api-version=2024-09-01",
            new { userPrompt, documents },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        ShieldPromptResponse? result = await response.Content.ReadFromJsonAsync<ShieldPromptResponse>(cancellationToken);

        return new PromptAttackResult(
            UserPromptAttack: result?.UserPromptAnalysis?.AttackDetected ?? false,
            DocumentAttack: result?.DocumentsAnalysis?.Any(d => d.AttackDetected) ?? false);
    }

    private sealed record ShieldPromptResponse(Analysis? UserPromptAnalysis, IReadOnlyList<Analysis>? DocumentsAnalysis);
    private sealed record Analysis(bool AttackDetected);
}

/// <summary>
/// A simple detector for local experimentation that looks for common injection phrasing.
/// Useful as a second signal, and easy to evade, so never rely on it alone.
/// </summary>
public sealed partial class HeuristicPromptAttackDetector : IPromptAttackDetector
{
    [GeneratedRegex(
        @"ignore (all |any |the )?(previous|prior|above|earlier) (instructions|rules|prompts?)" +
        @"|disregard (your|all|the) (instructions|rules|guidelines)" +
        @"|you are now (?!able)\w+" +
        @"|new instructions\s*:" +
        @"|(reveal|print|show|repeat) (your|the) (system prompt|instructions|hidden prompt)" +
        @"|developer mode|jailbreak" +
        @"|(note|message|instruction)s? (to|for) (the )?(ai|assistant|model|agent)\b" +
        @"|act as (an? )?(unrestricted|unfiltered)",
        RegexOptions.IgnoreCase)]
    private static partial Regex InjectionPhrasing();

    public Task<PromptAttackResult> AnalyzeAsync(
        string userPrompt, IReadOnlyList<string> documents, CancellationToken cancellationToken) =>
        Task.FromResult(new PromptAttackResult(
            UserPromptAttack: InjectionPhrasing().IsMatch(userPrompt),
            DocumentAttack: documents.Any(d => InjectionPhrasing().IsMatch(d))));
}
