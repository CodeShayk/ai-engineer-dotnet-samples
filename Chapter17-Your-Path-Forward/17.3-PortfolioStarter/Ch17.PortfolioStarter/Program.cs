// Chapter 17, Section 17.3: a portfolio starter.
// An ASP.NET Core API with a configured IChatClient pipeline, OpenTelemetry, health checks and
// two features: question answering and validated structured extraction. Replace the features
// with your own; keep the engineering.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PortfolioStarter.AI;
using PortfolioStarter.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

// --- Model access: one factory, configuration-driven, validated at startup ----------------------------
ModelOptions modelOptions = builder.Configuration.GetSection("AI").Get<ModelOptions>() ?? new ModelOptions();
modelOptions.Validate();
builder.Services.AddSingleton(modelOptions);

builder.Services
    .AddChatClient(_ => ModelClients.CreateChatClient(modelOptions))
    .UseLogging()
    .UseOpenTelemetry(sourceName: "PortfolioStarter.AI",
        configure: telemetry => telemetry.EnableSensitiveData = builder.Environment.IsDevelopment());

builder.Services.AddTransient<AnswerService>();
builder.Services.AddTransient<TicketExtractionService>();

// --- Telemetry: traces and metrics over OTLP (Chapter 15) ----------------------------------------------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("portfolio-starter"))
    .WithTracing(tracing => tracing
        .AddSource("PortfolioStarter.AI")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("PortfolioStarter.AI")
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

// --- Health: configuration is valid; never send a prompt from a probe ----------------------------------
builder.Services.AddHealthChecks()
    .AddCheck("model-configuration", () => HealthCheckResult.Healthy($"{modelOptions.Provider} / {modelOptions.ChatModel}"));

var app = builder.Build();

app.MapPost("/api/ask", async (AskRequest request, AnswerService answers, CancellationToken cancellationToken) =>
    string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2_000
        ? Results.BadRequest("Questions must be between 1 and 2,000 characters.")
        : Results.Ok(new { answer = await answers.AnswerAsync(request.Question, cancellationToken) }));

app.MapPost("/api/extract", async (ExtractRequest request, TicketExtractionService extraction, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 5_000)
    {
        return Results.BadRequest("Messages must be between 1 and 5,000 characters.");
    }

    ExtractionResult result = await extraction.ExtractAsync(request.Message, cancellationToken);
    return result.Succeeded
        ? Results.Ok(new { result.Ticket, result.Attempts })
        : Results.UnprocessableEntity(new { result.Problems, result.Attempts });   // Route to a person
});

app.MapHealthChecks("/health");
app.MapGet("/", () => "Portfolio starter: POST /api/ask, POST /api/extract, GET /health.");

app.Run();

public sealed record AskRequest(string Question);

public sealed record ExtractRequest(string Message);

// Makes the entry point visible to the test project.
public partial class Program;
