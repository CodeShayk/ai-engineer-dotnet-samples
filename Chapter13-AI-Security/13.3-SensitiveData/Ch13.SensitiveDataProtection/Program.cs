// Chapter 13, Section 13.3: Sensitive data exposure.
// 1. RedactingChatClient removes card numbers, bank details, email addresses and phone numbers
//    from user messages before every model call. A small middleware shows what actually arrives.
// 2. OutputLeakScanner checks model output on the way out, the last line of defense.
// 3. The pipeline records telemetry with EnableSensitiveData = false, so no message content is captured.
//
// Usage: dotnet run [--offline]
//   --offline  a fake model replies, so the redaction and scanning run without a provider.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;
using Northwind.Shared.Security;
using Northwind.Shared.Testing;

bool offline = args.Contains("--offline");
AIProviderOptions? options = offline ? null : SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 13.3: Sensitive data protection", options);

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();
ILogger providerLog = loggerFactory.CreateLogger("ReachedTheModel");

IChatClient provider = offline
    ? new FakeChatClient("Thank you. I've noted your details and passed your request to the team.")
    : AIClientFactory.CreateChatClient(options!);

// Outermost first: redact, then record telemetry (without content), then show what reaches the provider.
IChatClient chatClient = provider
    .AsBuilder()
    .Use(inner => new RedactingChatClient(inner, new PaymentAndContactRedactor(), loggerFactory.CreateLogger<RedactingChatClient>()))
    .UseOpenTelemetry(sourceName: "Northwind.AI", configure: telemetry => telemetry.EnableSensitiveData = false)
    .Use(async (messages, chatOptions, next, cancellationToken) =>
    {
        providerLog.LogInformation("\"{Text}\"", messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text);
        await next(messages, chatOptions, cancellationToken);
    })
    .Build();

// --- Redacting before sending -----------------------------------------------------------------------
string[] customerMessages =
[
    "Please refund order NW-10248 to my other card instead: 4539 1488 0343 6467, expiry 09/28.",
    "If it's easier, pay it into my bank account GB29 NWBK 6016 1331 9268 19.",
    "You can reach me on +44 20 7946 0958 or at thomas.hardy@example.com about the refund."
];

foreach (string message in customerMessages)
{
    SampleConsole.Section($"Customer: {message}");

    ChatResponse response = await chatClient.GetResponseAsync(
        [new ChatMessage(ChatRole.System, SupportPrompts.System), new ChatMessage(ChatRole.User, message)],
        new ChatOptions { MaxOutputTokens = 150 });

    Console.WriteLine($"Assistant: {response.Text}");
}

// --- Why the Luhn checksum matters ------------------------------------------------------------------
SampleConsole.Section("Card numbers are checked with the Luhn checksum");

// Card numbers carry a check digit. Long digit strings that fail the check, such as many order
// references and tracking numbers, are not cards, so the payment redactor leaves them alone.
var paymentOnly = new PaymentAndContactRedactor(includeContactDetails: false);
foreach (string candidate in new[] { "4539 1488 0343 6467", "4111 1111 1111 1112" })
{
    bool passes = PaymentAndContactRedactor.PassesLuhn(candidate.Replace(" ", ""));
    Console.WriteLine($"{candidate}: Luhn {(passes ? "valid" : "invalid")} -> \"{paymentOnly.Redact($"Reference {candidate}").Text}\"");
}

SampleConsole.Note("With contact details enabled, the broad phone number pattern would still catch the second number. " +
                   "Tune detectors to your data, and prefer redacting a little too much over too little.");

// --- Checking outputs for leaks -------------------------------------------------------------------
SampleConsole.Section("Scanning output before it leaves the application");

var scanner = new OutputLeakScanner();
const string customerEmail = "thomas.hardy@example.com";

string[] modelOutputs =
[
    "Your refund of $95.00 for the Harbor Rain Jacket is on its way, Thomas.",
    "I'll send the refund to card 4539 1488 0343 6467 as requested.",
    "For reference, our integration key is sk-live-a1b2c3d4e5f6g7h8i9j0k1l2.",
    "I've copied maria.anders@example.com and confirmed to thomas.hardy@example.com."
];

foreach (string output in modelOutputs)
{
    OutputScanResult result = scanner.Scan(output, customerEmail);
    Console.WriteLine(result.LeakDetected ? $"LEAK ({string.Join(", ", result.Findings)})" : "clean");
    Console.WriteLine($"  before: {output}");
    Console.WriteLine($"  after:  {result.SafeText}");
}
