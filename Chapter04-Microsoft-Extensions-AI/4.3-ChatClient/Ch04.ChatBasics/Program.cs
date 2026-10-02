// Chapter 4, Section 4.3: IChatClient, chat completions the .NET way.
// Usage: dotnet run [path-to-photo.jpg]
// Pass the path of a photo to try multimodal input (requires a vision-capable model).

using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 4.3: IChatClient basics", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);

// --- Discovering what is on the other end ----------------------------------------------
ChatClientMetadata? metadata = chatClient.GetService<ChatClientMetadata>();
Console.WriteLine($"Provider: {metadata?.ProviderName}, default model: {metadata?.DefaultModelId}");

// --- Requests and responses ---------------------------------------------------------------
SampleConsole.Section("Request and response");

ChatResponse response = await chatClient.GetResponseAsync(
    [
        new ChatMessage(ChatRole.System, SupportPrompts.System),
        new ChatMessage(ChatRole.User, "What's the difference between standard and express delivery?")
    ],
    new ChatOptions { MaxOutputTokens = 300 });

Console.WriteLine(response.Text);
Console.WriteLine();
Console.WriteLine($"Model: {response.ModelId} | Finish: {response.FinishReason} | " +
                  $"Tokens: {response.Usage?.InputTokenCount} in, {response.Usage?.OutputTokenCount} out");

// --- Cancellation and timeouts --------------------------------------------------------------
SampleConsole.Section("A deliberately short timeout");

// In ASP.NET Core you would use the request's token. A console app makes its own from Ctrl+C.
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token))
{
    // A realistic timeout is tens of seconds. 200 ms guarantees the cancellation path runs.
    timeout.CancelAfter(TimeSpan.FromMilliseconds(200));
    try
    {
        await chatClient.GetResponseAsync("Write a 500-word history of tea.", cancellationToken: timeout.Token);
        Console.WriteLine("The model answered within 200 ms. Impressive.");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("The call was cancelled after 200 ms, as intended. Always pass a cancellation token.");
    }
}

// --- Multimodal input ----------------------------------------------------------------------
SampleConsole.Section("Multimodal input");

if (args.Length > 0 && File.Exists(args[0]))
{
    byte[] photo = await File.ReadAllBytesAsync(args[0]);
    string mediaType = Path.GetExtension(args[0]).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    var message = new ChatMessage(ChatRole.User,
    [
        new TextContent("A customer attached this photo to a damage claim. " +
                        "Describe any visible damage in one sentence. If none is visible, say so."),
        new DataContent(photo, mediaType)
    ]);

    ChatResponse description = await chatClient.GetResponseAsync([message], cancellationToken: shutdown.Token);
    Console.WriteLine(description.Text);
}
else
{
    SampleConsole.Note("Pass the path of a photo as an argument to try multimodal input, for example: dotnet run -- damaged-kettle.jpg");
}
