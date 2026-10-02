using Microsoft.Extensions.AI;

namespace Ch03.FewShotClassification;

public enum TicketCategory
{
    OrderStatus,
    ReturnsAndRefunds,
    Billing,
    ProductQuestion,
    AccountAndPrivacy,
    Other
}

public sealed record LabeledExample(string Message, TicketCategory Category);

/// <summary>A few-shot classifier for incoming support messages (Chapter 3.3).</summary>
public sealed class TicketClassifier(IChatClient chatClient)
{
    private static readonly LabeledExample[] Examples =
    [
        new("I ordered a kettle last week and still haven't had a tracking number.", TicketCategory.OrderStatus),
        new("The jacket I bought is too small. How do I send it back?", TicketCategory.ReturnsAndRefunds),
        new("I've been charged twice for order NW-10252.", TicketCategory.Billing),
        new("Does the Nimbus speaker work with Android phones?", TicketCategory.ProductQuestion),
        new("Please delete my account and everything you hold about me.", TicketCategory.AccountAndPrivacy),
        // An edge case: mentions delivery, but the customer wants their money back.
        new("Tracking says delivered but there's nothing here. I just want a refund.", TicketCategory.ReturnsAndRefunds),
    ];

    private const string Instructions = """
        Classify the customer's message into exactly one of these categories:
        OrderStatus, ReturnsAndRefunds, Billing, ProductQuestion, AccountAndPrivacy, Other.
        If a message fits more than one category, choose the one that matches what the
        customer wants to happen next. Reply with the category name only.
        """;

    public async Task<TicketCategory> ClassifyAsync(string message, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> messages = [new(ChatRole.System, Instructions)];

        foreach (LabeledExample example in Examples)
        {
            messages.Add(new ChatMessage(ChatRole.User, example.Message));
            messages.Add(new ChatMessage(ChatRole.Assistant, example.Category.ToString()));
        }

        messages.Add(new ChatMessage(ChatRole.User, message));

        ChatResponse response = await chatClient.GetResponseAsync(
            messages,
            new ChatOptions { Temperature = 0, MaxOutputTokens = 10 },
            cancellationToken);

        return Parse(response.Text);
    }

    /// <summary>The same instructions with no examples, for comparison.</summary>
    public async Task<TicketCategory> ClassifyZeroShotAsync(string message, CancellationToken cancellationToken = default)
    {
        ChatResponse response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instructions), new ChatMessage(ChatRole.User, message)],
            new ChatOptions { Temperature = 0, MaxOutputTokens = 10 },
            cancellationToken);

        return Parse(response.Text);
    }

    private static TicketCategory Parse(string text) =>
        Enum.TryParse(text.Trim().TrimEnd('.'), ignoreCase: true, out TicketCategory category)
            ? category
            : TicketCategory.Other;
}
