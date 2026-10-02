// Chapter 14, Section 14.5: Writing your own evaluators.
// 1. CitationValidityEvaluator, a deterministic evaluator, checks three canned answers against
//    the same retrieved sources. No model is needed.
// 2. PolicyCommitmentEvaluator, a model-graded evaluator with a Northwind rubric, checks whether
//    an answer promises something the policies do not support.
//
// Usage: dotnet run [--offline]
//   --offline  runs only the deterministic citation evaluator.

using Ch14.CustomEvaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

bool offline = args.Contains("--offline");
SampleConsole.Header("Chapter 14.5: Custom evaluators");

IReadOnlyList<RetrievedChunk> sources =
[
    new("electronics-returns#1", "electronics-returns", "Electronics Returns", "Opened electronics",
        "Opened electronics can be returned within 14 days of delivery, as long as all accessories and packaging are included.",
        "2026-01-15", 0.81),
    new("refunds#1", "refunds", "Refunds", "How long refunds take",
        "Refunds are issued to your original payment method within 5 to 10 business days of your return arriving at our warehouse.",
        "2026-01-15", 0.77)
];

List<ChatMessage> conversation = [new(ChatRole.User, "Can I return opened earbuds, and how long will the refund take?")];
var sourcesContext = new RetrievedSourcesContext(sources);

(string Description, string Answer)[] answers =
[
    ("Valid citations",
     """
     Yes, opened earbuds can be returned within 14 days of delivery with all accessories, and the refund arrives within 5 to 10 business days.

     Sources:
     [electronics-returns#1] "Opened electronics can be returned within 14 days of delivery"
     [refunds#1] "within 5 to 10 business days of your return arriving"
     """),

    ("A citation to a source that was never retrieved",
     """
     Yes, you can return them within 30 days.

     Sources:
     [returns-policy#0] "Most items can be returned within 30 days"
     """),

    ("A paraphrase presented as a quote",
     """
     Yes, opened electronics can come back within two weeks.

     Sources:
     [electronics-returns#1] "Electronics can come back within two weeks of arriving"
     """)
];

// --- A deterministic evaluator ---------------------------------------------------------------------
SampleConsole.Section("Citation validity (deterministic)");

var citationEvaluator = new CitationValidityEvaluator();

foreach ((string description, string answer) in answers)
{
    EvaluationResult result = await citationEvaluator.EvaluateAsync(
        conversation, new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)), additionalContext: [sourcesContext]);

    BooleanMetric metric = result.Get<BooleanMetric>(CitationValidityEvaluator.MetricName);
    Console.WriteLine($"{description,-48} {(metric.Interpretation!.Failed ? "FAIL" : "pass")}  {metric.Reason}");
}

if (offline)
{
    return;
}

// --- A model-graded evaluator with a custom rubric ----------------------------------------------------
AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Section($"Policy commitments (judged by {options.JudgeChatDeployment})");

var chatConfiguration = new ChatConfiguration(AIClientFactory.CreateChatClient(options, options.JudgeChatDeployment));
var commitmentEvaluator = new PolicyCommitmentEvaluator();

(string Description, string Answer)[] commitmentAnswers =
[
    ("Supported by the policies",
     "Yes, you can return them within 14 days of delivery, and the refund arrives within 5 to 10 business days."),

    ("Promises compensation the policies never mention",
     "Yes, return them within 14 days. Because of the trouble, we'll also add a $20 voucher and refund you by tomorrow.")
];

foreach ((string description, string answer) in commitmentAnswers)
{
    EvaluationResult result = await commitmentEvaluator.EvaluateAsync(
        conversation, new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)), chatConfiguration, [sourcesContext]);

    BooleanMetric metric = result.Get<BooleanMetric>(PolicyCommitmentEvaluator.MetricName);
    string verdict = metric.Interpretation is null ? "ERROR" : metric.Interpretation.Failed ? "FAIL" : "pass";
    Console.WriteLine($"{description,-48} {verdict}  {metric.Reason}");

    foreach (EvaluationDiagnostic diagnostic in metric.Diagnostics ?? [])
    {
        SampleConsole.Note($"  {diagnostic.Severity}: {diagnostic.Message}");
    }
}
