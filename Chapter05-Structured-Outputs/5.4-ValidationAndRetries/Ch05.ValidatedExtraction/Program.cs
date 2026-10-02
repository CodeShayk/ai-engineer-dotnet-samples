// Chapter 5, Section 5.4: Validation, error handling and retries.
// 1. Validates hand-made instances with data annotations (no model call).
// 2. Triages several messages with StructuredOutputService, which validates each response,
//    checks extracted order numbers against the source text and retries with feedback.

using Ch05.ValidatedExtraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 5.4: Validation and retries", options);

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();

// --- Validating with data annotations --------------------------------------------------------
SampleConsole.Section("Validation without a model");

var outOfRange = new TicketTriage(TicketCategory.Billing, Urgency.Normal, Sentiment: 7.5,
    OrderNumbers: ["NW-10250"], Summary: new string('x', 400));

var badOrderNumber = new TicketTriage(TicketCategory.OrderStatus, Urgency.High, Sentiment: -0.4,
    OrderNumbers: ["NW-1"], Summary: "Customer is chasing a late delivery.");

// IValidatableObject.Validate runs only after every attribute rule passes, which is why the
// order number rule is demonstrated on a second instance.
foreach ((string label, TicketTriage instance) in new[] { ("Out of range", outOfRange), ("Bad order number", badOrderNumber) })
{
    Console.WriteLine($"{label}:");
    foreach (string problem in ModelValidation.Validate(instance))
    {
        Console.WriteLine($"  - {problem}");
    }
}

// --- Validation, source checks and retries ---------------------------------------------------
IChatClient chatClient = AIClientFactory.CreateChatClient(options);
var structuredOutput = new StructuredOutputService(chatClient, loggerFactory.CreateLogger<StructuredOutputService>());

string[] customerMessages =
[
    "My Nimbus speaker from order NW-10252 arrived with a cracked grille. Not happy.",

    // The customer omits the NW- prefix. A normalized number fails the verbatim source check,
    // so the service asks the model to correct itself. Whether that is the behavior you want
    // is a product decision; the point is that the check makes it a deliberate one.
    "I'm writing about order 10249. The earbuds still haven't arrived and the tracking is stuck.",

    "You charged me twice for my last two orders, NW-10250 and NW-10251!! Fix this today please."
];

foreach (string customerMessage in customerMessages)
{
    SampleConsole.Section(customerMessage);

    List<ChatMessage> messages =
    [
        new(ChatRole.System, "You triage customer support messages for Northwind Traders."),
        new(ChatRole.User, customerMessage)
    ];

    StructuredResult<TicketTriage> result = await structuredOutput.GetAsync<TicketTriage>(
        messages,
        additionalChecks: t => (t.OrderNumbers ?? [])
            .Where(n => !customerMessage.Contains(n, StringComparison.OrdinalIgnoreCase))
            .Select(n => $"Order number {n} does not appear in the customer's message. Only include numbers that appear verbatim."),
        options: new ChatOptions { Temperature = 0 });

    if (result.Succeeded)
    {
        TicketTriage triage = result.Value!;
        Console.WriteLine($"{triage.Category} | {triage.Urgency} | sentiment {triage.Sentiment:+0.00;-0.00} | " +
                          $"orders [{string.Join(", ", triage.OrderNumbers)}] | {result.Attempts} attempt(s)");
        Console.WriteLine(triage.Summary);
    }
    else
    {
        Console.WriteLine($"Failed after {result.Attempts} attempts. Routing to a human. Problems:");
        foreach (string problem in result.Problems)
        {
            Console.WriteLine($"  - {problem}");
        }
    }
}
