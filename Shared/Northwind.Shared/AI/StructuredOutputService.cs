using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Northwind.Shared.AI;

/// <summary>The outcome of a structured output request, including how many attempts it took.</summary>
public sealed record StructuredResult<T>(T? Value, int Attempts, IReadOnlyList<string> Problems)
{
    public bool Succeeded => Value is not null && Problems.Count == 0;
}

/// <summary>Runs data annotation validation and returns readable error messages (Chapter 5.4).</summary>
public static class ModelValidation
{
    public static IReadOnlyList<string> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "Unknown validation error.").ToList();
    }
}

/// <summary>
/// Requests structured output, validates it, and retries with specific feedback when the
/// output is malformed, truncated or invalid (Chapter 5.4). Reused by Chapters 9 and 14.
/// </summary>
public sealed class StructuredOutputService(IChatClient chatClient, ILogger<StructuredOutputService>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<StructuredOutputService>.Instance;

    public async Task<StructuredResult<T>> GetAsync<T>(
        IEnumerable<ChatMessage> messages,
        Func<T, IEnumerable<string>>? additionalChecks = null,
        ChatOptions? options = null,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default) where T : class
    {
        List<ChatMessage> conversation = [.. messages];
        IReadOnlyList<string> problems = [];

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ChatResponse<T> response = await chatClient.GetResponseAsync<T>(
                conversation, options, cancellationToken: cancellationToken);

            if (response.FinishReason == ChatFinishReason.Length)
            {
                problems = ["The response was cut off before it was complete. Keep text fields shorter."];
            }
            else if (!response.TryGetResult(out T? result) || result is null)
            {
                problems = ["The response did not match the required JSON structure."];
            }
            else
            {
                problems = [.. ModelValidation.Validate(result), .. additionalChecks?.Invoke(result) ?? []];
                if (problems.Count == 0)
                {
                    return new StructuredResult<T>(result, attempt, []);
                }
            }

            _logger.LogWarning("Structured output attempt {Attempt} for {Type} failed: {Problems}",
                attempt, typeof(T).Name, string.Join("; ", problems));

            // Show the model its previous answer and exactly what was wrong with it.
            conversation.AddMessages(response);
            conversation.Add(new ChatMessage(ChatRole.User,
                "Your previous response had these problems:\n- " + string.Join("\n- ", problems) +
                "\nReturn a corrected response."));
        }

        return new StructuredResult<T>(null, maxAttempts, problems);
    }
}
