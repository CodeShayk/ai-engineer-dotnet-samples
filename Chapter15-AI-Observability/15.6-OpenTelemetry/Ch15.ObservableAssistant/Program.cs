// Chapter 15, Section 15.6: OpenTelemetry for .NET AI workloads.
// Northwind Assist as an ASP.NET Core API, with traces, metrics and logs exported over OTLP.
//
// 1. Start the Aspire dashboard:   docker compose -f ../docker-compose.yml up -d
// 2. Run this project:            dotnet run
// 3. Generate some traffic:       POST http://localhost:5115/api/demo/traffic (see Ch15.ObservableAssistant.http)
// 4. Open the dashboard:          http://localhost:18888
//
// No Docker? Set Telemetry:ConsoleExporter to true to print spans to the console instead.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Ch15.ObservableAssistant;
using Ch15.ObservableAssistant.Telemetry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Knowledge;
using Northwind.Shared.Tools;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(SampleConfiguration.UserSecretsId);

AIProviderOptions aiOptions = AIProviderOptions.FromConfiguration(builder.Configuration);
bool consoleExporter = builder.Configuration.GetValue<bool>("Telemetry:ConsoleExporter");

// --- Wiring up OpenTelemetry --------------------------------------------------------------------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("northwind-assist"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Northwind.AI")            // Chat client spans (UseOpenTelemetry on IChatClient)
            .AddSource("Northwind.Agents")        // Agent run and tool spans (UseOpenTelemetry on agents)
            .AddSource("Northwind.Retrieval")     // Our own retrieval spans
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();

        if (consoleExporter)
        {
            tracing.AddConsoleExporter();
        }
    })
    .WithMetrics(metrics => metrics
        .AddMeter("Northwind.AI")             // Token usage, duration and time to first chunk
        .AddMeter("Northwind.Agents")
        .AddMeter("Northwind.Assistant")      // Behavioral metrics
        .AddMeter("Northwind.Cost")           // Estimated cost and time to first token
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithLogging(logging => logging.AddOtlpExporter());

// --- Instrumented clients ----------------------------------------------------------------------------
// Message content is captured in development only. Never enable it in production without an
// approved reason and protections for the telemetry store (Chapter 13.3).
bool captureContent = builder.Environment.IsDevelopment();

builder.Services.AddSingleton(new ModelPriceList(
    builder.Configuration.GetSection("ModelPrices").Get<Dictionary<string, ModelPrice>>() ?? []));
builder.Services.AddSingleton<AssistantMetrics>();

builder.Services
    .AddChatClient(_ => AIClientFactory.CreateChatClient(aiOptions))
    .Use((inner, services) => new CostTrackingChatClient(
        inner, services.GetRequiredService<ModelPriceList>(), services.GetRequiredService<IMeterFactory>().Create("Northwind.Cost")))
    .Use((inner, services) => new TimeToFirstTokenChatClient(
        inner, services.GetRequiredService<IMeterFactory>().Create("Northwind.Cost")))
    .UseOpenTelemetry(sourceName: "Northwind.AI", configure: telemetry => telemetry.EnableSensitiveData = captureContent);

builder.Services
    .AddEmbeddingGenerator(_ => AIClientFactory.CreateEmbeddingGenerator(aiOptions))
    .UseOpenTelemetry(sourceName: "Northwind.AI");

// --- Health checks that never call the model -----------------------------------------------------------
builder.Services.AddHealthChecks().AddCheck("model-endpoint", new ModelEndpointHealthCheck(aiOptions));
builder.Services.AddSingleton(aiOptions);

var app = builder.Build();

// --- Build the assistant ---------------------------------------------------------------------------------
IChatClient chatClient = app.Services.GetRequiredService<IChatClient>();
AssistantMetrics assistantMetrics = app.Services.GetRequiredService<AssistantMetrics>();

app.Logger.LogInformation("Loading the policy library into the vector store...");
PolicyKnowledgeBase knowledge = await PolicyKnowledgeBase.CreateInMemoryAsync(
    aiOptions, app.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());

var policyAssistant = new ObservablePolicyAssistant(
    new QueryRewriter(chatClient),
    new TracedPolicyRetriever(knowledge.Retriever),
    new LlmReranker(chatClient),
    new StructuredOutputService(chatClient, app.Services.GetRequiredService<ILogger<StructuredOutputService>>()),
    assistantMetrics);

var store = NorthwindStore.CreateSeeded();
var orderTools = new OrderTools(store, new ReturnPolicyRules(store));

AIAgent orderAgent = new ChatClientAgent(
        chatClient,
        name: "NorthwindAssist",
        instructions: SupportPrompts.System,
        tools: [AIFunctionFactory.Create(orderTools.GetOrderStatus), AIFunctionFactory.Create(orderTools.CheckReturnEligibility)])
    .AsBuilder()
    .Use(RecordToolOutcomeAsync)
    .UseOpenTelemetry(sourceName: "Northwind.Agents", configure: telemetry => telemetry.EnableSensitiveData = captureContent)
    .Build();

// --- Endpoints ----------------------------------------------------------------------------------------------
app.MapPost("/api/assistant/ask", async (AskRequest request, CancellationToken cancellationToken) =>
{
    AssistantAnswer answer = await policyAssistant.AskAsync(
        request.Question, [], new CustomerContext(request.Region ?? "global"), cancellationToken);
    return Results.Ok(new { answer = answer.Text, sources = answer.Citations.Select(c => c.SourceId).Distinct() });
});

app.MapPost("/api/assistant/chat", async (ChatTurnRequest request, CancellationToken cancellationToken) =>
{
    using IDisposable feature = AIFeature.Begin("order-chat");
    AgentResponse response = await orderAgent.RunAsync(request.Message, cancellationToken: cancellationToken);
    return Results.Ok(new { reply = response.Text });
});

app.MapPost("/api/assistant/stream", (ChatTurnRequest request, CancellationToken cancellationToken) =>
    Results.ServerSentEvents(StreamAsync(chatClient, request.Message, cancellationToken), eventType: "delta"));

// Sends a small, varied batch of requests through every path, so the dashboard has data to show.
app.MapPost("/api/demo/traffic", async (CancellationToken cancellationToken) =>
{
    var timings = new List<object>();

    async Task TimeAsync(string name, Func<Task> action)
    {
        long started = Stopwatch.GetTimestamp();
        await action();
        timings.Add(new { request = name, seconds = Math.Round(Stopwatch.GetElapsedTime(started).TotalSeconds, 2) });
    }

    await TimeAsync("ask: opened earbuds", () => policyAssistant.AskAsync(
        "Can I return earbuds I've already opened?", [], new CustomerContext("uk"), cancellationToken));
    await TimeAsync("ask: something not in the policies", () => policyAssistant.AskAsync(
        "Do you sell gift cards for restaurants?", [], new CustomerContext("global"), cancellationToken));
    await TimeAsync("chat: order status", async () =>
    {
        using IDisposable feature = AIFeature.Begin("order-chat");
        await orderAgent.RunAsync("When will order NW-10249 arrive?", cancellationToken: cancellationToken);
    });
    await TimeAsync("stream: delivery question", async () =>
    {
        await foreach (string _ in StreamAsync(chatClient, "In one sentence, how fast is express delivery?", cancellationToken))
        {
        }
    });

    return Results.Ok(timings);
});

app.MapHealthChecks("/health");
app.MapGet("/", () => "Northwind Assist (Chapter 15). POST /api/demo/traffic, then open the Aspire dashboard at http://localhost:18888.");

app.Run();

// --- Helpers ------------------------------------------------------------------------------------------------------

static async IAsyncEnumerable<string> StreamAsync(
    IChatClient chatClient, string message, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    using IDisposable feature = AIFeature.Begin("streaming-chat");

    await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(
        [new ChatMessage(ChatRole.System, SupportPrompts.System), new ChatMessage(ChatRole.User, message)],
        cancellationToken: cancellationToken))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            yield return update.Text;
        }
    }
}

// Function calling middleware that records every tool call's outcome as a behavioral metric.
async ValueTask<object?> RecordToolOutcomeAsync(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    try
    {
        object? result = await next(context, cancellationToken);
        assistantMetrics.ToolCalled(context.Function.Name, "success");
        return result;
    }
    catch
    {
        assistantMetrics.ToolCalled(context.Function.Name, "error");
        throw;
    }
}

public sealed record AskRequest(string Question, string? Region);

public sealed record ChatTurnRequest(string Message);

/// <summary>
/// Checks that the model endpoint is reachable without sending a prompt, which would cost money
/// and count against rate limits on every probe (Chapter 15.5).
/// </summary>
public sealed class ModelEndpointHealthCheck(AIProviderOptions options) : IHealthCheck
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (options.Provider != AIProvider.Ollama)
        {
            // For cloud providers, configuration was validated at startup; real traffic metrics
            // (error rates on actual model calls) are the signal for model problems.
            return HealthCheckResult.Healthy($"{options.Provider} configured with {options.ChatDeployment}.");
        }

        try
        {
            using HttpResponseMessage response = await Http.GetAsync(options.Endpoint, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Ollama reachable at {options.Endpoint}.")
                : HealthCheckResult.Unhealthy($"Ollama returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy($"Ollama is not reachable at {options.Endpoint}.", ex);
        }
    }
}
