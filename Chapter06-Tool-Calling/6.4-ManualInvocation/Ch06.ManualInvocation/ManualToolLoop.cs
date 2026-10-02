using Microsoft.Extensions.AI;

namespace Ch06.ManualInvocation;

/// <summary>
/// The tool-calling loop written by hand (Chapter 6.4). This is what UseFunctionInvocation
/// does for you, with a place to authorize, audit or redirect each call.
/// </summary>
public static class ManualToolLoop
{
    private const int MaxIterations = 5;

    public static async Task<string> RunAsync(
        IChatClient chatClient,               // No UseFunctionInvocation in this client's pipeline
        IReadOnlyList<AIFunction> tools,
        List<ChatMessage> history,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, AIFunction> toolsByName = tools.ToDictionary(t => t.Name);
        var chatOptions = new ChatOptions { Tools = [.. tools] };

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            ChatResponse response = await chatClient.GetResponseAsync(history, chatOptions, cancellationToken);
            history.AddMessages(response);

            List<FunctionCallContent> calls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .ToList();

            if (calls.Count == 0)
            {
                return response.Text;   // The model has produced its final answer.
            }

            var results = new List<AIContent>();
            foreach (FunctionCallContent call in calls)
            {
                if (!toolsByName.TryGetValue(call.Name, out AIFunction? function))
                {
                    results.Add(new FunctionResultContent(call.CallId, $"Unknown tool '{call.Name}'."));
                    continue;
                }

                auditLog.Record(call.Name, call.Arguments);

                try
                {
                    object? result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken);
                    results.Add(new FunctionResultContent(call.CallId, result));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Report the failure to the model without internal details, as UseFunctionInvocation does.
                    results.Add(new FunctionResultContent(call.CallId, $"Error: the {call.Name} tool failed.") { Exception = ex });
                }
            }

            history.Add(new ChatMessage(ChatRole.Tool, results));
        }

        throw new InvalidOperationException("The model did not finish within the iteration limit.");
    }
}
