using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using PortfolioStarter.Features;
using Xunit;

namespace PortfolioStarter.Tests;

/// <summary>
/// Unit tests for the code around the model: they use a fake model, run in milliseconds and
/// need no credentials. Every portfolio project should have tests like these.
/// </summary>
public sealed class ExtractionTests
{
    private const string Message = "My order A-12345 still hasn't arrived after two weeks and I need it for a trip on Friday.";

    private const string ValidTicket = """
        {"category":"OrderStatus","urgent":true,"orderNumbers":["A-12345"],"missingInformation":[],"summary":"Customer's order A-12345 is two weeks late and needed by Friday."}
        """;

    private const string InventedOrderNumber = """
        {"category":"OrderStatus","urgent":true,"orderNumbers":["A-99999"],"missingInformation":[],"summary":"Customer's order is late."}
        """;

    [Fact]
    public async Task Valid_output_succeeds_first_time()
    {
        var model = new CannedChatClient(ValidTicket);

        ExtractionResult result = await new TicketExtractionService(model).ExtractAsync(Message, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(TicketCategory.OrderStatus, result.Ticket!.Category);
    }

    [Fact]
    public async Task Invented_order_number_is_caught_and_corrected()
    {
        var model = new CannedChatClient(InventedOrderNumber, ValidTicket);

        ExtractionResult result = await new TicketExtractionService(model).ExtractAsync(Message, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Attempts);
        Assert.Contains(model.Requests[1], m => m.Text.Contains("A-99999 does not appear in the message"));
    }

    [Fact]
    public async Task Persistent_failure_reports_problems_instead_of_guessing()
    {
        var model = new CannedChatClient("This is not JSON at all.");

        ExtractionResult result = await new TicketExtractionService(model).ExtractAsync(Message, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.Attempts);
        Assert.NotEmpty(result.Problems);
    }

    /// <summary>A fake model that returns canned replies in order and records each request.</summary>
    private sealed class CannedChatClient(params string[] replies) : IChatClient
    {
        private int _next;

        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            string reply = replies[Math.Min(_next++, replies.Length - 1)];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
