// Chapter 5, Section 5.5: Common patterns, classification, extraction and transformation.
// Runs one example of each pattern, then processes a batch of return emails with bounded
// concurrency, recording failures instead of stopping the job.

using System.Collections.Concurrent;
using System.Diagnostics;
using Ch05.StructuredPatterns;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 5.5: Structured output patterns", options);

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();
IChatClient chatClient = AIClientFactory.CreateChatClient(options);
var structuredOutput = new StructuredOutputService(chatClient, loggerFactory.CreateLogger<StructuredOutputService>());
var deterministic = new ChatOptions { Temperature = 0 };

// --- Classification: evidence first, then the label -------------------------------------------
SampleConsole.Section("Classification");

foreach (string message in SampleInputs.ClassificationMessages)
{
    StructuredResult<RequestedOutcomeClassification> result = await structuredOutput.GetAsync<RequestedOutcomeClassification>(
        ReturnPrompts.ForClassification(message), options: deterministic);

    Console.WriteLine(result.Succeeded
        ? $"{result.Value!.Outcome,-11} evidence: \"{result.Value.Evidence}\""
        : $"Failed: {string.Join("; ", result.Problems)}");
}

// --- Extraction: report what is missing instead of guessing -----------------------------------
SampleConsole.Section("Extraction");

foreach (InboundEmail email in SampleInputs.Emails.Where(e => e.Id is "E-1001" or "E-1002"))
{
    StructuredResult<ReturnRequestDetails> result = await structuredOutput.GetAsync<ReturnRequestDetails>(
        ReturnPrompts.ForEmail(email.Body),
        // Verify extracted values against the source before acting on them.
        additionalChecks: d => d.OrderNumber is { } n && !email.Body.Contains(n, StringComparison.OrdinalIgnoreCase)
            ? [$"Order number {n} does not appear in the email. Use null when no order number is stated."]
            : [],
        options: deterministic);

    if (result.Succeeded)
    {
        ReturnRequestDetails d = result.Value!;
        Console.WriteLine($"{email.Id}: {d.CustomerName ?? "(no name)"} | {d.OrderNumber ?? "(no order number)"} | " +
                          $"{string.Join(", ", d.Items)} | {d.DesiredOutcome}");
        Console.WriteLine($"        Reason: {d.Reason ?? "(none given)"}");
        Console.WriteLine($"        Missing: {(d.MissingInformation.Count == 0 ? "nothing" : string.Join(", ", d.MissingInformation))}");
    }
    else
    {
        Console.WriteLine($"{email.Id}: failed: {string.Join("; ", result.Problems)}");
    }
}

// --- Transformation: supplier text to a consistent catalog listing ----------------------------
SampleConsole.Section("Transformation");

foreach (string supplierText in SampleInputs.SupplierTexts)
{
    StructuredResult<CatalogListing> result = await structuredOutput.GetAsync<CatalogListing>(
        ReturnPrompts.ForCatalogListing(supplierText),
        additionalChecks: CheckListing,
        options: deterministic);

    if (result.Succeeded)
    {
        CatalogListing listing = result.Value!;
        Console.WriteLine($"{listing.Title} [{listing.Category}]");
        foreach (string feature in listing.KeyFeatures)
        {
            Console.WriteLine($"  * {feature}");
        }

        Console.WriteLine($"  Weight: {(listing.WeightKg is { } kg ? $"{kg:0.00} kg" : "not stated")}");
        Console.WriteLine($"  Care:   {listing.CareInstructions ?? "not stated"}");
        SampleConsole.Note($"  {result.Attempts} attempt(s). Listings shown to customers still go to human review.");
    }
    else
    {
        Console.WriteLine($"Failed: {string.Join("; ", result.Problems)}");
    }
}

// --- Processing in bulk -------------------------------------------------------------------------
SampleConsole.Section($"Processing {SampleInputs.Emails.Length} emails, four at a time");

var returnsQueue = new ReturnsQueue();
var failures = new ConcurrentBag<(string Id, IReadOnlyList<string> Problems)>();
long started = Stopwatch.GetTimestamp();

await Parallel.ForEachAsync(
    SampleInputs.Emails,
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (email, ct) =>
    {
        StructuredResult<ReturnRequestDetails> result = await structuredOutput.GetAsync<ReturnRequestDetails>(
            ReturnPrompts.ForEmail(email.Body), options: new ChatOptions { Temperature = 0 }, cancellationToken: ct);

        if (result.Succeeded)
        {
            await returnsQueue.EnqueueAsync(email.Id, result.Value!, ct);
        }
        else
        {
            failures.Add((email.Id, result.Problems));
        }
    });

Console.WriteLine();
Console.WriteLine($"Queued {returnsQueue.Count}, failed {failures.Count}, in {Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s.");
foreach ((string id, IReadOnlyList<string> problems) in failures)
{
    Console.WriteLine($"  {id}: {string.Join("; ", problems)}");
}

// Deterministic checks for the limits that matter in a listing.
static IEnumerable<string> CheckListing(CatalogListing listing)
{
    if (listing.Title.Length > 80)
    {
        yield return $"The title is {listing.Title.Length} characters long; the maximum is 80.";
    }

    if (listing.KeyFeatures.Count is < 3 or > 5)
    {
        yield return $"There are {listing.KeyFeatures.Count} feature bullets; provide between three and five.";
    }

    foreach (string feature in listing.KeyFeatures.Where(f => f.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 12))
    {
        yield return $"The bullet \"{feature}\" has 12 or more words; keep each bullet under 12 words.";
    }
}
