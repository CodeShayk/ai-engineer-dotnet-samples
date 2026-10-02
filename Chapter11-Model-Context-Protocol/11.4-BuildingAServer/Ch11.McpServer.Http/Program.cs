// Chapter 11, Section 11.4: An MCP server hosted over Streamable HTTP.
// The MCP endpoint is an ordinary ASP.NET Core endpoint, so it is secured like any web API.
// The book shows JWT bearer authentication for production. To keep local experimentation
// simple, this sample requires an API key in the X-Api-Key header instead:
//
//   dotnet user-secrets set "McpServer:ApiKey" "<a long random value>" --id northwind-ai-engineer-samples
//
// In Development, if no key is configured, a random one is generated and printed at startup.

using System.Security.Cryptography;
using System.Text;
using Ch11.McpServer.Tools;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(SampleConfiguration.UserSecretsId);

builder.Services.AddSingleton(NorthwindStore.CreateSeeded());
builder.Services.AddSingleton<ReturnPolicyRules>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(OrderMcpTools).Assembly)
    .WithResourcesFromAssembly(typeof(OrderMcpTools).Assembly)
    .WithPromptsFromAssembly(typeof(OrderMcpTools).Assembly);

var app = builder.Build();

string apiKey = app.Configuration["McpServer:ApiKey"] ?? string.Empty;
if (apiKey.Length == 0)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("McpServer:ApiKey must be configured outside Development.");
    }

    apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    app.Logger.LogWarning("No McpServer:ApiKey configured. Using a temporary key for this run: {ApiKey}", apiKey);
}

byte[] expectedKey = Encoding.UTF8.GetBytes(apiKey);

// The security boundary: every request to the MCP endpoint must carry the key.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        byte[] providedKey = Encoding.UTF8.GetBytes(context.Request.Headers["X-Api-Key"].ToString());
        if (!CryptographicOperations.FixedTimeEquals(providedKey, expectedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next(context);
});

app.MapMcp("/mcp");
app.MapGet("/", () => "Northwind MCP server. Connect an MCP client to /mcp with an X-Api-Key header.");

app.Run();
