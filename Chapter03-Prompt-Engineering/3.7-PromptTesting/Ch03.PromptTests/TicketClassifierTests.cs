using Ch03.FewShotClassification;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Xunit;

namespace Ch03.PromptTests;

/// <summary>Opt-in switch and client factory for tests that call a live model.</summary>
public static class ModelTests
{
    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_MODEL_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    public static IChatClient CreateChatClient()
    {
        AIProviderOptions options = SampleConfiguration.LoadAIOptions();
        return AIClientFactory.CreateChatClient(options, options.SmallChatDeployment);
    }
}

/// <summary>Statistical tests against a golden set (Chapter 3.7). Skipped unless RUN_MODEL_TESTS=true.</summary>
public sealed class TicketClassifierTests
{
    public static readonly (string Message, TicketCategory Expected)[] GoldenSet =
    [
        ("Where's my parcel? It was meant to arrive Tuesday.", TicketCategory.OrderStatus),
        ("Has my order shipped yet? I ordered three days ago.", TicketCategory.OrderStatus),
        ("The tracking number you sent doesn't work on the courier's site.", TicketCategory.OrderStatus),
        ("Can I change the delivery address for order NW-10250?", TicketCategory.OrderStatus),
        ("My bench was supposed to come today and nobody turned up.", TicketCategory.OrderStatus),
        ("Can I swap these boots for a size 10?", TicketCategory.ReturnsAndRefunds),
        ("The kettle stopped working after a week. I want a refund.", TicketCategory.ReturnsAndRefunds),
        ("How do I return a jacket I bought as a gift?", TicketCategory.ReturnsAndRefunds),
        ("I sent the speaker back ten days ago and still have no refund.", TicketCategory.ReturnsAndRefunds),
        ("The earbuds arrived with a cracked case. What can you do?", TicketCategory.ReturnsAndRefunds),
        ("Parcel marked delivered but it never came and I'd like my money back.", TicketCategory.ReturnsAndRefunds),
        ("Why is there a charge of $18 on my card from you?", TicketCategory.Billing),
        ("I was charged twice for the same order.", TicketCategory.Billing),
        ("Can I get a VAT invoice for my last order?", TicketCategory.Billing),
        ("My promo code didn't apply at checkout and I paid full price.", TicketCategory.Billing),
        ("You charged my old card instead of the new one.", TicketCategory.Billing),
        ("Is the teak bench okay to leave outside in winter?", TicketCategory.ProductQuestion),
        ("Does the Orbit watch track swimming?", TicketCategory.ProductQuestion),
        ("What's the battery life on the Aurora earbuds?", TicketCategory.ProductQuestion),
        ("Will the laptop sleeve fit a 14-inch MacBook?", TicketCategory.ProductQuestion),
        ("Is the chai tea caffeine free?", TicketCategory.ProductQuestion),
        ("How do I change the email address on my account?", TicketCategory.AccountAndPrivacy),
        ("I forgot my password and the reset email never arrives.", TicketCategory.AccountAndPrivacy),
        ("Please delete all the data you hold about me.", TicketCategory.AccountAndPrivacy),
        ("Can I see what personal information you store?", TicketCategory.AccountAndPrivacy),
        ("Someone else seems to have logged into my account.", TicketCategory.AccountAndPrivacy),
        ("Do you have any jobs going in your warehouse?", TicketCategory.Other),
        ("I'd like to become a supplier for Northwind.", TicketCategory.Other),
        ("Just wanted to say your delivery driver was lovely.", TicketCategory.Other),
        ("Are you open on bank holidays?", TicketCategory.Other),
    ];

    [Fact]
    public async Task Classifier_meets_accuracy_threshold_on_golden_set()
    {
        Assert.SkipUnless(ModelTests.Enabled, "Set RUN_MODEL_TESTS=true to run tests against a live model.");

        var classifier = new TicketClassifier(ModelTests.CreateChatClient());
        int correct = 0;
        var misses = new List<string>();

        foreach ((string message, TicketCategory expected) in GoldenSet)
        {
            TicketCategory actual = await classifier.ClassifyAsync(message, TestContext.Current.CancellationToken);
            if (actual == expected)
            {
                correct++;
            }
            else
            {
                misses.Add($"'{message}': expected {expected}, got {actual}");
            }
        }

        double accuracy = (double)correct / GoldenSet.Length;
        TestContext.Current.SendDiagnosticMessage($"Accuracy {accuracy:P0}. Misses: {string.Join(" | ", misses)}");

        // Set the threshold from measured performance on your model, with a margin (Chapter 3.7).
        Assert.True(accuracy >= 0.9, $"Accuracy was {accuracy:P0}; the threshold is 90%. Misses: {string.Join(" | ", misses)}");
    }
}
