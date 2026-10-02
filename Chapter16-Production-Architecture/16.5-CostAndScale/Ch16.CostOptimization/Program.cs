// Chapter 16, Section 16.5: Managing cost and scale.
// 1. Routes a mix of customer messages to a small or a large model by complexity, then reports
//    the share of traffic each handled and the estimated cost compared with sending everything
//    to the large model.
// 2. Shows a semantic cache answering paraphrases, refusing a near miss with a different answer,
//    and invalidating everything when the knowledge version changes.
//
// The small and large models are AI:SmallChatDeployment and AI:ChatDeployment. Prices are
// illustrative and applied per tier, so the report is meaningful even when both are local models.

using System.Globalization;
using Ch16.CostOptimization;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 16.5: Cost optimization", options);

var prices = new Dictionary<string, TierPrice>
{
    ["small"] = new(InputPerMillion: 0.05, OutputPerMillion: 0.40),
    ["large"] = new(InputPerMillion: 1.25, OutputPerMillion: 10.00)
};

var meter = new UsageMeter();
IChatClient small = meter.Wrap(AIClientFactory.CreateChatClient(options, options.SmallChatDeployment), "small");
IChatClient large = meter.Wrap(AIClientFactory.CreateChatClient(options), "large");
var router = new ComplexityRoutingChatClient(small, large);

// --- Routing by complexity -----------------------------------------------------------------------------
SampleConsole.Section("Routing by complexity");

string[] messages =
[
    "Do you deliver on Saturdays?",
    "How long do refunds take?",
    "What's your returns window?",
    "Is the Lumen lamp dimmable?",
    "I've been waiting two weeks, the tracking hasn't moved, and nobody replies to my emails. This is unacceptable. What are you going to do about it?",
    "Can you compare the warranty on the Nimbus speaker with the one on the Aurora earbuds?",
    "My parcel shows as delivered but I wasn't home, my neighbour hasn't got it, and I also want to change the address on my next order.",
    "Can I use loyalty points on sale items?",
];

foreach (string message in messages)
{
    ChatResponse response = await router.GetResponseAsync(
        [new ChatMessage(ChatRole.System, SupportPrompts.System), new ChatMessage(ChatRole.User, message)],
        new ChatOptions { MaxOutputTokens = 200 });

    Console.WriteLine($"{(message.Length > 70 ? message[..67] + "..." : message),-72} -> {router.LastRoute}");
}

SampleConsole.Section("Cost report");

foreach ((string route, int count) in router.Decisions.OrderBy(d => d.Key))
{
    Console.WriteLine($"{route,-34} {count} request(s)");
}

double routedCost = meter.Cost(prices);
long totalInput = meter.Usage.Values.Sum(u => u.Input);
long totalOutput = meter.Usage.Values.Sum(u => u.Output);
double allLargeCost = totalInput * prices["large"].InputPerMillion / 1_000_000 + totalOutput * prices["large"].OutputPerMillion / 1_000_000;

foreach ((string tier, (long input, long output, int calls)) in meter.Usage.OrderBy(u => u.Key))
{
    Console.WriteLine($"{tier,-6} {calls,3} call(s) {input,8:N0} input {output,8:N0} output tokens");
}

Console.WriteLine($"Estimated cost with routing:          ${routedCost:F5}");
Console.WriteLine($"Estimated cost with the large model:  ${allLargeCost:F5}");
if (allLargeCost > 0)
{
    Console.WriteLine($"Saving:                               {1 - routedCost / allLargeCost:P0}");
}

SampleConsole.Note("The small-model classifier's own calls are included in the routed cost. Measure answer quality on both " +
                   "paths before trusting the saving.");

// --- Semantic caching, carefully -------------------------------------------------------------------------
SampleConsole.Section("Semantic caching");

IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
var cache = new SemanticAnswerCache(embedder, similarityThreshold: 0.95f);
string knowledgeVersion = "policies-2026-01-15";

await cache.AddAsync("Can I return opened earbuds?", knowledgeVersion,
    "Yes. Opened electronics can be returned within 14 days of delivery with all accessories and packaging.", CancellationToken.None);
await cache.AddAsync("How long do refunds take?", knowledgeVersion,
    "Refunds arrive within 5 to 10 business days of your return reaching our warehouse.", CancellationToken.None);

string[] incoming =
[
    "How long do refunds take?",                     // Identical
    "How long does a refund take?",                  // A close paraphrase
    "Can I return unopened earbuds?",                // A near miss with a different answer
    "Do you deliver on Saturdays?"                   // Unrelated
];

foreach (string question in incoming)
{
    float best = await cache.BestScoreAsync(question, CancellationToken.None);
    string? cached = await cache.TryGetAsync(question, knowledgeVersion, CancellationToken.None);
    Console.WriteLine($"{question,-36} best similarity {best:F3} -> {(cached is null ? "MISS (call the model)" : "HIT")}");
}

SampleConsole.Note("Look at the near miss: opened and unopened earbuds have different answers. If its similarity is above the " +
                   "threshold with your embedding model, the threshold is too low. Calibrate it on real questions.");

string? afterPolicyChange = await cache.TryGetAsync("How long do refunds take?", "policies-2026-03-01", CancellationToken.None);
Console.WriteLine($"After a policy update (new knowledge version): {(afterPolicyChange is null ? "MISS, as it should be" : "HIT")}");
