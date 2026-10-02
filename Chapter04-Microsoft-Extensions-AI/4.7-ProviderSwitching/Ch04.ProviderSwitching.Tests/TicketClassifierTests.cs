using Ch03.FewShotClassification;
using Microsoft.Extensions.AI;
using Northwind.Shared.Testing;
using Xunit;

namespace Ch04.ProviderSwitching.Tests;

public sealed class TicketClassifierTests
{
    [Fact]
    public async Task Classifier_falls_back_to_Other_for_unrecognized_output()
    {
        var classifier = new TicketClassifier(new FakeChatClient("I think this is about shoes?"));

        TicketCategory category = await classifier.ClassifyAsync("Hello", TestContext.Current.CancellationToken);

        Assert.Equal(TicketCategory.Other, category);
    }

    [Fact]
    public async Task Classifier_sends_examples_before_the_message()
    {
        var fake = new FakeChatClient("Billing");
        await new TicketClassifier(fake).ClassifyAsync("Why was I charged twice?", TestContext.Current.CancellationToken);

        List<ChatMessage> request = fake.Requests.Single();
        Assert.Equal(ChatRole.System, request[0].Role);
        Assert.Equal("Why was I charged twice?", request[^1].Text);
    }

    [Theory]
    [InlineData("Billing", TicketCategory.Billing)]
    [InlineData("billing.", TicketCategory.Billing)]
    [InlineData("  ReturnsAndRefunds\n", TicketCategory.ReturnsAndRefunds)]
    public async Task Classifier_tolerates_case_whitespace_and_a_trailing_period(string reply, TicketCategory expected)
    {
        var classifier = new TicketClassifier(new FakeChatClient(reply));

        TicketCategory category = await classifier.ClassifyAsync("Any message", TestContext.Current.CancellationToken);

        Assert.Equal(expected, category);
    }
}
