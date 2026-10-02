// Chapter 2, Section 2.7: Do the cost arithmetic early.
// Reads a workload description and candidate model prices from costsettings.json and prints
// a monthly comparison. No model or API key is needed.

using System.Globalization;
using System.Text.Json;

// Prices in the settings file are in US dollars; format them that way on every machine.
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

string path = Path.Combine(AppContext.BaseDirectory, args.Length > 0 ? args[0] : "costsettings.json");
var settings = JsonSerializer.Deserialize<CostSettings>(
    File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
    ?? throw new InvalidOperationException($"Could not read {path}.");

Workload w = settings.Workload;
double turnsPerMonth = (double)w.ConversationsPerDay * w.TurnsPerConversation * w.DaysPerMonth;
double inputTokens = turnsPerMonth * w.InputTokensPerTurn;
double cachedInput = inputTokens * w.CachedInputShare;
double uncachedInput = inputTokens - cachedInput;
double outputTokens = turnsPerMonth * w.OutputTokensPerTurn;

Console.WriteLine($"Workload: {w.Name}");
Console.WriteLine($"  {w.ConversationsPerDay:N0} conversations/day x {w.TurnsPerConversation} turns, {w.DaysPerMonth} days/month");
Console.WriteLine($"  {inputTokens / 1_000_000:N0}M input tokens ({w.CachedInputShare:P0} cached), {outputTokens / 1_000_000:N0}M output tokens per month");
Console.WriteLine();
Console.WriteLine($"{"Model",-24} {"Input",12} {"Output",12} {"Monthly",12} {"Per conversation",18}");
Console.WriteLine(new string('-', 82));

foreach (ModelPrice model in settings.Models)
{
    double inputCost = uncachedInput / 1_000_000 * model.InputPerMillion + cachedInput / 1_000_000 * model.CachedInputPerMillion;
    double outputCost = outputTokens / 1_000_000 * model.OutputPerMillion;
    double monthly = inputCost + outputCost;
    double perConversation = monthly / (w.ConversationsPerDay * (double)w.DaysPerMonth);

    Console.WriteLine($"{model.Name,-24} {inputCost,12:C0} {outputCost,12:C0} {monthly,12:C0} {perConversation,18:C4}");
}

Console.WriteLine();
Console.WriteLine("Prices in costsettings.json are illustrative. Substitute your provider's current rates.");

internal sealed record CostSettings(Workload Workload, IReadOnlyList<ModelPrice> Models);

internal sealed record Workload(
    string Name,
    int ConversationsPerDay,
    int TurnsPerConversation,
    int InputTokensPerTurn,
    int OutputTokensPerTurn,
    double CachedInputShare,
    int DaysPerMonth);

internal sealed record ModelPrice(string Name, double InputPerMillion, double CachedInputPerMillion, double OutputPerMillion);
