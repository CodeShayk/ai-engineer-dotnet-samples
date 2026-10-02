// Chapter 4, Section 4.6: Dependency injection, middleware and the client pipeline.
// Registers a chat client with built-in and custom middleware, resolves services that
// depend on IChatClient, and uses a keyed "small" client for ticket triage.

using System.Diagnostics;
using Ch04.ClientPipeline;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets(SampleConfiguration.UserSecretsId);

// A console logger that writes synchronously, so log lines appear in order with the output.
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new SampleConsoleLoggerProvider());
// The built-in logging middleware writes at Debug level, and message content at Trace level.
builder.Logging.AddFilter("Microsoft.Extensions.AI", LogLevel.Debug);

// Read and validate configuration once, at startup, rather than on the first request.
AIProviderOptions aiOptions = AIProviderOptions.FromConfiguration(builder.Configuration);

builder.Services.AddDistributedMemoryCache();

// The first component added is the outermost:
//   logging      sees every request, including cache hits
//   date         added before the cache, so a cached answer is only reused on the same day
//   cache        a hit returns here, and nothing inside it runs
//   usage        logs latency and token usage for real model calls only
//   telemetry    one span per actual model call (exporters are configured in Chapter 15)
builder.Services
    .AddChatClient(_ => AIClientFactory.CreateChatClient(aiOptions))
    .UseLogging()
    .Use(async (messages, options, next, cancellationToken) =>
    {
        IEnumerable<ChatMessage> withDate = messages.Prepend(
            new ChatMessage(ChatRole.System, $"Today's date is {DateTime.UtcNow:yyyy-MM-dd}."));

        await next(withDate, options, cancellationToken);
    })
    .UseDistributedCache()
    .Use((inner, services) => new UsageTrackingChatClient(
        inner, services.GetRequiredService<ILogger<UsageTrackingChatClient>>()))
    .UseOpenTelemetry(sourceName: "Northwind.AI");

// A second, cheaper model for simple tasks, resolved by key.
builder.Services.AddKeyedChatClient("small", _ => AIClientFactory.CreateChatClient(aiOptions, aiOptions.SmallChatDeployment))
    .UseOpenTelemetry(sourceName: "Northwind.AI");

builder.Services.AddTransient<SupportAnswerService>();
builder.Services.AddTransient<TicketTriageService>();

using IHost host = builder.Build();

SampleConsole.Header("Chapter 4.6: The client pipeline", aiOptions);

// --- The same question twice: the second answer comes from the cache ------------------------
SampleConsole.Section("The same question twice");

var support = host.Services.GetRequiredService<SupportAnswerService>();

for (int attempt = 1; attempt <= 2; attempt++)
{
    long started = Stopwatch.GetTimestamp();
    string answer = await support.AnswerAsync("How long does a refund take to reach my account?", CancellationToken.None);
    Console.WriteLine($"[{attempt}] {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms: {answer}");
    Console.WriteLine();
}

SampleConsole.Note("The usage tracker logged one model call. The second request never got past the cache.");

// --- Streaming goes through the same pipeline ------------------------------------------------
SampleConsole.Section("Streaming through the pipeline");

IChatClient chatClient = host.Services.GetRequiredService<IChatClient>();

await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(
    [
        new ChatMessage(ChatRole.System, SupportPrompts.System),
        new ChatMessage(ChatRole.User, "Suggest a gift under $50 for someone who loves tea.")
    ]))
{
    Console.Write(update.Text);
}

Console.WriteLine();

// --- A keyed client for a simple task --------------------------------------------------------
SampleConsole.Section($"Ticket triage with the \"small\" client ({aiOptions.SmallChatDeployment})");

var triage = host.Services.GetRequiredService<TicketTriageService>();

string[] tickets =
[
    "Where is my order? It was supposed to arrive yesterday.",
    "Can I get a refund for boots that don't fit?",
    "Is the Orbit watch water resistant?",
    "You charged my card twice this morning."
];

foreach (string ticket in tickets)
{
    Console.WriteLine($"{await triage.TriageAsync(ticket),-18} {ticket}");
}
