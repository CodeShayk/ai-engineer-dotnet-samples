// Chapter 6, Section 6.3: Connecting models to APIs, databases and business services.
// An ASP.NET Core API whose tools depend on scoped services and on the signed-in customer.
// Run it, open the URL shown in the console, choose a customer and ask about "my orders".

using Ch06.ServiceTools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(SampleConfiguration.UserSecretsId);

builder.Services.AddNorthwindChatClient(builder.Configuration)
    .UseFunctionInvocation(configure: invoker =>
    {
        invoker.MaximumIterationsPerRequest = 5;
        invoker.IncludeDetailedErrors = false;
    });

builder.Services.AddSingleton(NorthwindStore.CreateSeeded());
builder.Services.AddScoped<IOrderService, InMemoryOrderService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCustomer, ClaimsCurrentCustomer>();
builder.Services.AddScoped<CustomerSupportTools>();

// The simulated sign-in trusts a request header, so it must never run outside local development.
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "This sample uses a demo authentication scheme and only runs in the Development environment.");
}

builder.Services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/support/chat", async (
    ChatRequest request,
    IChatClient chatClient,
    CustomerSupportTools tools,
    CancellationToken cancellationToken) =>
{
    var options = new ChatOptions
    {
        Tools =
        [
            AIFunctionFactory.Create(tools.GetMyRecentOrdersAsync),
            AIFunctionFactory.Create(tools.GetMyOrderAsync)
        ]
    };

    ChatResponse response = await chatClient.GetResponseAsync(
        [new(ChatRole.System, SupportPrompts.System), .. request.ToChatMessages()],
        options, cancellationToken);

    // The tool calls are returned alongside the reply so the demo page can show them.
    string[] toolCalls = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<FunctionCallContent>()
        .Select(c => $"{c.Name}({string.Join(", ", c.Arguments?.Select(a => $"{a.Key}: {a.Value}") ?? [])})")
        .ToArray();

    return Results.Ok(new { reply = response.Text, toolCalls });
})
.RequireAuthorization();

// Lists the demo customers for the sign-in drop-down on the page.
app.MapGet("/api/demo/customers", (NorthwindStore store) =>
    store.Customers.Select(c => new { c.CustomerId, c.Name }));

app.Run();
