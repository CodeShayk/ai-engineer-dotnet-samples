// Chapter 14, Section 14.3: Microsoft.Extensions.AI.Evaluation.
// Answers questions with the policy assistant from Chapter 9, then has a judge model score each
// answer for relevance, coherence, groundedness and completeness against a reference answer.
//
// The judge is AI:JudgeChatDeployment (default: the chat model). Use a capable model as the
// judge, preferably not the one being evaluated:
//   dotnet user-secrets set "AI:JudgeChatDeployment" "<a stronger model>" --id northwind-ai-engineer-samples

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 14.3: Quality evaluators", options);
Console.WriteLine($"Judge model: {options.JudgeChatDeployment}");

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();
ILogger logger = loggerFactory.CreateLogger("Evaluation");
CancellationToken cancellationToken = CancellationToken.None;

// --- The system under test: the policy assistant from Chapter 9 ------------------------------------
Console.WriteLine("Loading the policy library...");
IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(options, embedder, cancellationToken: cancellationToken);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);
IChatClient smallModel = AIClientFactory.CreateChatClient(options, options.SmallChatDeployment);
var policyAssistant = new PolicyAssistant(
    new QueryRewriter(smallModel), knowledge.Retriever, new LlmReranker(smallModel), new StructuredOutputService(chatClient));

// --- The judge ---------------------------------------------------------------------------------------
// The judge: ideally a capable model, and preferably not the one being evaluated.
IChatClient judgeClient = AIClientFactory.CreateChatClient(options, options.JudgeChatDeployment);
var chatConfiguration = new ChatConfiguration(judgeClient);

IEvaluator evaluator = new CompositeEvaluator(
    new RelevanceEvaluator(),
    new CoherenceEvaluator(),
    new GroundednessEvaluator(),
    new CompletenessEvaluator());

(string Question, string ReferenceAnswer)[] cases =
[
    ("I opened my earbuds ten days ago and they hurt my ears. Can I return them, and will I have to pay anything?",
     "Yes. Opened electronics can be returned within 14 days of delivery if all accessories and packaging are included. " +
     "There is no restocking fee, and the refund goes to the original payment method within 5 to 10 business days of the return arriving."),

    ("How long does a refund take once you have my return?",
     "Refunds are issued to the original payment method within 5 to 10 business days of the return arriving at the warehouse.")
];

var customer = new CustomerContext(Region: "uk");

foreach ((string question, string referenceAnswer) in cases)
{
    SampleConsole.Section(question);

    // The system under test answers the question.
    (List<ChatMessage> conversation, ChatResponse response, string retrievedPassages) =
        await policyAssistant.AnswerForEvaluationAsync(question, customer, cancellationToken);

    Console.WriteLine(response.Text);
    Console.WriteLine();

    EvaluationResult result = await evaluator.EvaluateAsync(
        conversation,
        response,
        chatConfiguration,
        additionalContext:
        [
            new GroundednessEvaluatorContext(retrievedPassages),
            new CompletenessEvaluatorContext(referenceAnswer)
        ]);

    foreach (EvaluationMetric metric in result.Metrics.Values)
    {
        if (metric is NumericMetric numeric)
        {
            Console.WriteLine($"{numeric.Name,-14} {numeric.Value,4:F1}  {numeric.Interpretation?.Rating,-12} {numeric.Reason}");
        }

        // Evaluators report problems, such as a judge reply they could not parse, as diagnostics.
        foreach (EvaluationDiagnostic diagnostic in metric.Diagnostics ?? [])
        {
            SampleConsole.Note($"  {metric.Name}: {diagnostic.Severity}: {diagnostic.Message}");
        }
    }

    // --- Understanding results: read one metric by name ------------------------------------------------
    NumericMetric groundedness = result.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);

    if (groundedness.Interpretation?.Failed == true)
    {
        logger.LogWarning("Ungrounded answer for {Question}: {Reason}", question, groundedness.Reason);
    }
}
