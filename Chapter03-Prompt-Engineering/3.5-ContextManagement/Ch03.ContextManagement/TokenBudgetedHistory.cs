using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;

namespace Ch03.ContextManagement;

/// <summary>
/// Keeps the most recent messages that fit within a token budget (Chapter 3.5).
/// The system message is kept separately by the caller.
/// </summary>
public sealed class TokenBudgetedHistory(Tokenizer tokenizer, int historyTokenBudget)
{
    // Rough allowance for the per-message formatting the provider adds.
    private const int PerMessageOverhead = 4;

    private readonly List<ChatMessage> _messages = [];

    public int Count => _messages.Count;

    public void Add(ChatMessage message) => _messages.Add(message);

    public void AddRange(IEnumerable<ChatMessage> messages) => _messages.AddRange(messages);

    /// <summary>
    /// Returns the most recent messages that fit within the budget, oldest first. The window always
    /// starts at a user message, so a tool call is never separated from its result.
    /// </summary>
    public IReadOnlyList<ChatMessage> GetWithinBudget()
    {
        int start = _messages.Count;
        int used = 0;

        while (start > 0)
        {
            int cost = CountTokens(_messages[start - 1]);
            if (used + cost > historyTokenBudget)
            {
                break;
            }

            used += cost;
            start--;
        }

        // Never begin mid-turn: providers reject a tool result whose call is missing.
        while (start < _messages.Count && _messages[start].Role != ChatRole.User)
        {
            start++;
        }

        return _messages.GetRange(start, _messages.Count - start);
    }

    // Counts every kind of content, because tool calls and results use tokens too.
    private int CountTokens(ChatMessage message) =>
        PerMessageOverhead + message.Contents.Sum(content => content switch
        {
            TextContent text => tokenizer.CountTokens(text.Text),
            FunctionCallContent call => tokenizer.CountTokens($"{call.Name} {JsonSerializer.Serialize(call.Arguments, AIJsonUtilities.DefaultOptions)}"),
            FunctionResultContent result => tokenizer.CountTokens(JsonSerializer.Serialize(result.Result, AIJsonUtilities.DefaultOptions)),
            _ => 0
        });
}
