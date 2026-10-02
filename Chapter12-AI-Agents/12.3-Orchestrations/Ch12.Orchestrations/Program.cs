// Chapter 12, Sections 12.2, 12.3 and 12.6: Orchestration patterns.
//
// Usage: dotnet run [sequential|concurrent|groupchat|astool]     (default: sequential)
//   sequential  drafter -> compliance reviewer -> style editor prepare a reply to a customer email
//   concurrent  legal, quality and customer care specialists assess a complaint at the same time
//   groupchat   a writer and a reviewer take turns refining a product description
//   astool      a policy expert agent used as a tool by the main assistant (agent-as-tool)

using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Northwind.Agents.ContextProviders;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Knowledge;
using Northwind.Shared.Tools;

string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "sequential";

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header($"Chapter 12: Orchestrations ({mode})", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);

switch (mode)
{
    case "sequential":
        await RunSequentialAsync();
        break;
    case "concurrent":
        await RunConcurrentAsync();
        break;
    case "groupchat":
        await RunGroupChatAsync();
        break;
    case "astool":
        await RunAgentAsToolAsync();
        break;
    default:
        Console.WriteLine("Choose one of: sequential, concurrent, groupchat, astool.");
        break;
}

// --- Sequential: a three-stage reply pipeline ------------------------------------------------------
async Task RunSequentialAsync()
{
    ChatClientAgent drafter = new(chatClient,
        "Draft a reply to the customer's email using only the facts provided. Plain English, under 150 words.",
        "Drafter", "Drafts replies to customer emails");

    ChatClientAgent complianceReviewer = new(chatClient,
        "Review the latest draft reply. Remove any promise of refunds, compensation or delivery dates that the " +
        "facts do not support, and anything that discloses internal information. Return only the corrected reply.",
        "ComplianceReviewer", "Checks draft replies against Northwind's commitments policy");

    ChatClientAgent styleEditor = new(chatClient,
        "Edit the latest reply into Northwind's voice: friendly, calm, plain English, no exclamation marks. " +
        "Return only the final reply.",
        "StyleEditor", "Applies Northwind's tone of voice");

    Workflow pipeline = AgentWorkflowBuilder.BuildSequential([drafter, complianceReviewer, styleEditor]);

    const string emailWithFacts = """
        Customer email:
        "Hi, I ordered the Aurora earbuds (NW-10249) five days ago and they still haven't arrived. Can you
        guarantee they'll come tomorrow? If not, I want a full refund and a discount on my next order!"

        Facts:
        - Order NW-10249: Aurora Wireless Earbuds, shipped by standard delivery, estimated delivery tomorrow.
        - Northwind cannot guarantee delivery dates; the courier sets them.
        - A refund for an undelivered order is only possible once the parcel is confirmed lost, ten business days after dispatch.
        - Discounts must never be offered in writing.
        """;

    List<ChatMessage> input = [new(ChatRole.User, emailWithFacts)];
    await RunAndPrintAsync(pipeline, input, finalLabel: "Final reply");
}

// --- Concurrent: three specialists assess the same complaint ----------------------------------------
async Task RunConcurrentAsync()
{
    ChatClientAgent legalReviewer = new(chatClient,
        "Assess the customer complaint for legal and safety exposure. Two or three bullet points.",
        "LegalReviewer", "Assesses complaints for legal and safety risk");

    ChatClientAgent qualityAnalyst = new(chatClient,
        "Assess what the complaint suggests about product quality and what should be checked. Two or three bullet points.",
        "QualityAnalyst", "Looks for product quality issues in complaints");

    ChatClientAgent customerCareSpecialist = new(chatClient,
        "Suggest how to respond to the customer to keep their trust, without promising compensation. Two or three bullet points.",
        "CustomerCare", "Recommends how to respond to the customer");

    Workflow assessment = AgentWorkflowBuilder.BuildConcurrent([legalReviewer, qualityAnalyst, customerCareSpecialist]);

    List<ChatMessage> input =
    [
        new(ChatRole.User,
            "Complaint about order NW-10251: \"The Brewmaster kettle started leaking after a month, and the base got so hot " +
            "it scorched my worktop. I want compensation for the worktop and an explanation.\"")
    ];

    await RunAndPrintAsync(assessment, input, finalLabel: "All assessments", concurrent: true);
}

// --- Group chat: a writer and a reviewer take turns -------------------------------------------------
async Task RunGroupChatAsync()
{
    ChatClientAgent writer = new(chatClient,
        "You write short, factual product descriptions for Northwind's catalog. When the reviewer gives feedback, " +
        "revise the description and return only the new version.",
        "Writer", "Writes and revises product descriptions");

    ChatClientAgent reviewer = new(chatClient,
        "You review product descriptions. Point out at most two concrete improvements: unsupported claims, missing " +
        "facts or unclear wording. If the description is good, say so in one sentence.",
        "Reviewer", "Reviews product descriptions");

    Workflow refinement = AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 4 })
        .AddParticipants(writer, reviewer)
        .Build();

    List<ChatMessage> input =
    [
        new(ChatRole.User,
            "Write a 60-word description of the Lumen Desk Lamp: dimmable LED, adjustable colour temperature, " +
            "USB charging port in the base, $45.")
    ];

    await RunAndPrintAsync(refinement, input, finalLabel: "Final turn");
}

// --- Agent as tool: a specialist the main assistant can consult --------------------------------------
async Task RunAgentAsToolAsync()
{
    Console.WriteLine("Loading the policy library for the policy expert...");
    IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
    PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(options, embedder);

    var store = NorthwindStore.CreateSeeded();
    var orderTools = new OrderTools(store, new ReturnPolicyRules(store));
    PolicyRetriever retriever = knowledge.Retriever;
    var customerContext = new CustomerContext(Region: "uk", CustomerId: "C003");

    const string PolicyExpertInstructions =
        "You answer questions about Northwind Traders customer policies using only the policy excerpts provided. " +
        "Cite the ids of the excerpts you use. If the excerpts do not answer the question, say so.";

    AIAgent policyExpert = new ChatClientAgent(chatClient, new ChatClientAgentOptions
    {
        Name = "PolicyExpert",
        Description = "Answers questions about Northwind Traders customer policies, with citations.",
        ChatOptions = new ChatOptions { Instructions = PolicyExpertInstructions },
        AIContextProviders = [new PolicyKnowledgeContextProvider(retriever, customerContext)]
    });

    AIAgent assistant = new ChatClientAgent(
        chatClient,
        name: "NorthwindAssist",
        instructions: SupportPrompts.System,
        tools:
        [
            policyExpert.AsAIFunction(),
            AIFunctionFactory.Create(orderTools.GetOrderStatus)
        ]);

    string question = "My order NW-10248 arrived six days ago. How long do I have to send the jacket back, and who pays for the return label?";
    Console.WriteLine($"\nYou: {question}");

    AgentResponse response = await assistant.RunAsync(question);

    foreach (FunctionCallContent call in response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
    {
        SampleConsole.Note($"[call] {call.Name}({string.Join(", ", call.Arguments?.Select(a => $"{a.Key}: {a.Value}") ?? [])})");
    }

    Console.WriteLine($"Assistant: {response.Text}");
}

// --- Running an orchestration: the same pattern for every kind -----------------------------------------
static async Task RunAndPrintAsync(Workflow workflow, List<ChatMessage> input, string finalLabel, bool concurrent = false)
{
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    string? currentAgent = null;
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        // Concurrent agents work at the same time, so their streamed tokens would interleave;
        // for them, show only the collected results.
        if (evt is AgentResponseUpdateEvent update && !concurrent)
        {
            if (update.ExecutorId != currentAgent)
            {
                currentAgent = update.ExecutorId;
                Console.WriteLine($"\n\n--- {AgentName(update)} ---");
            }

            Console.Write(update.Update.Text);
        }
        else if (evt is WorkflowOutputEvent output && output.As<List<ChatMessage>>() is { } conversation)
        {
            Console.WriteLine($"\n\n=== {finalLabel} ===");

            // Sequential and group chat outputs end with the final turn; concurrent outputs hold one reply per agent.
            IEnumerable<ChatMessage> shown = concurrent
                ? conversation.Where(m => m.Role == ChatRole.Assistant)
                : [conversation[^1]];

            foreach (ChatMessage message in shown)
            {
                Console.WriteLine($"[{message.AuthorName ?? message.Role.ToString()}] {message.Text}");
            }

            break;
        }
        else if (evt is WorkflowErrorEvent error)
        {
            Console.WriteLine($"\nWorkflow error: {error.Data}");
            break;
        }
    }
}

// An agent's executor id is its name plus a unique suffix; prefer the author name for display.
static string AgentName(AgentResponseUpdateEvent update) =>
    update.Update.AuthorName ?? Regex.Replace(update.ExecutorId, "_[0-9a-f]{32}$", "");
