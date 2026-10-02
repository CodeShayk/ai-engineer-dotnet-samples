// Chapter 4, Section 4.4: Streaming responses.
// An ASP.NET Core minimal API that relays a model's streamed response to the browser with
// Server-Sent Events. Run it and open the URL shown in the console.

using System.Runtime.CompilerServices;
using Ch04.StreamingApi;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(SampleConfiguration.UserSecretsId);
builder.Services.AddNorthwindChatClient(builder.Configuration);

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/chat/stream", (ChatRequest request, IChatClient chatClient, CancellationToken cancellationToken) =>
{
    List<ChatMessage> messages = [new(ChatRole.System, SupportPrompts.System), .. request.ToChatMessages()];

    return Results.ServerSentEvents(StreamTextAsync(chatClient, messages, cancellationToken), eventType: "delta");
});

app.Run();

static async IAsyncEnumerable<string> StreamTextAsync(
    IChatClient chatClient,
    List<ChatMessage> messages,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(
        messages, cancellationToken: cancellationToken))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            yield return update.Text;
        }
    }
}
