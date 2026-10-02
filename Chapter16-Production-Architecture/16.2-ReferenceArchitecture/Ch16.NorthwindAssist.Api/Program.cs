// Chapter 16, Section 16.2: A reference architecture for production AI applications.
// Northwind Assist assembled layer by layer. Each AddNorthwind... method lives in its own file
// and registers one layer. Run it, open http://localhost:5116 and chat as a demo customer.

using Ch16.NorthwindAssist.Api.Endpoints;
using Ch16.NorthwindAssist.Api.Knowledge;
using Ch16.NorthwindAssist.Api.ModelAccess;
using Ch16.NorthwindAssist.Api.Orchestration;
using Ch16.NorthwindAssist.Api.Platform;
using Ch16.NorthwindAssist.Api.Safety;
using Ch16.NorthwindAssist.Api.Tools;
using Northwind.Shared.AI;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(SampleConfiguration.UserSecretsId);

AIProviderOptions aiOptions = AIProviderOptions.FromConfiguration(builder.Configuration);

// Platform: identity, sessions, rate limiting, telemetry (Chapters 6, 10, 13, 15)
builder.Services.AddNorthwindAuthentication(builder.Configuration, builder.Environment);   // JWT bearer, or a demo scheme in development
builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();                      // Use Redis or similar in production
builder.Services.AddSingleton<ISessionStore, DistributedCacheSessionStore>();
builder.Services.AddNorthwindRateLimiting();
builder.Services.AddNorthwindTelemetry(builder.Environment);

// Model access layer: resilience, fallback, redaction, telemetry (Chapters 4, 13, 15, 16)
builder.Services.AddNorthwindModelAccess(aiOptions, builder.Configuration);

// Knowledge layer: vector store, ingestion, retrieval (Chapters 8, 9)
builder.Services.AddNorthwindKnowledge(aiOptions);

// Tools and integration: domain services, order tools, refund tools, MCP client (Chapters 6, 11)
builder.Services.AddNorthwindTools(builder.Configuration);

// Safety: redaction, prompt attack screening, output sanitization (Chapter 13)
builder.Services.AddNorthwindSafety(builder.Configuration);

// Orchestration: the handoff team, exposed as a single agent (Chapters 10, 12)
builder.Services.AddNorthwindAssistAgent(builder.Configuration);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAssistantEndpoints();     // POST /api/assistant/chat (streaming), GET /api/assistant/conversations/{id}
app.MapApprovalEndpoints();      // Supervisor approval queue
app.MapHealthChecks("/health");

app.Run();
