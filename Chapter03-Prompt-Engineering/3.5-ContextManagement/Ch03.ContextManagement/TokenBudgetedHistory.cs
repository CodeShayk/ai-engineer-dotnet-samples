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

    /// <summary>Returns the most recent messages that fit within the budget, oldest first.</summary>
    public IReadOnlyList<ChatMessage> GetWithinBudget()
    {
        var selected = new List<ChatMessage>();
        int used = 0;

        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            int cost = tokenizer.CountTokens(_messages[i].Text) + PerMessageOverhead;
            if (used + cost > historyTokenBudget)
            {
                break;
            }

            selected.Add(_messages[i]);
            used += cost;
        }

        selected.Reverse();
        return selected;
    }
}
