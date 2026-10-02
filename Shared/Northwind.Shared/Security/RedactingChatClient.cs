using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Northwind.Shared.Security;

/// <summary>
/// Chat client middleware that removes sensitive values from user messages before every model
/// call, streaming or not (Chapter 13.3). It logs how many values were redacted, never what they were.
/// </summary>
public sealed class RedactingChatClient(IChatClient innerClient, ISensitiveDataRedactor redactor, ILogger<RedactingChatClient> logger)
    : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(Redact(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(Redact(messages), options, cancellationToken);

    private List<ChatMessage> Redact(IEnumerable<ChatMessage> messages)
    {
        var result = new List<ChatMessage>();
        int redactions = 0;

        foreach (ChatMessage message in messages)
        {
            if (message.Role != ChatRole.User)
            {
                result.Add(message);
                continue;
            }

            ChatMessage? redactedMessage = null;
            for (int i = 0; i < message.Contents.Count; i++)
            {
                if (message.Contents[i] is not TextContent text)
                {
                    continue;   // Images, files and other content pass through unchanged.
                }

                (string redacted, int count) = redactor.Redact(text.Text);
                if (count > 0)
                {
                    // Copy the message once, with its own contents list, so the caller's message is not modified.
                    redactedMessage ??= new ChatMessage(message.Role, [.. message.Contents])
                    {
                        AuthorName = message.AuthorName,
                        AdditionalProperties = message.AdditionalProperties
                    };
                    redactedMessage.Contents[i] = new TextContent(redacted);
                    redactions += count;
                }
            }

            result.Add(redactedMessage ?? message);
        }

        if (redactions > 0)
        {
            logger.LogInformation("Redacted {Count} sensitive values before sending to the model", redactions);
        }

        return result;
    }
}
