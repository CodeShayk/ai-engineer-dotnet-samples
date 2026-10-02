using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Shared.Domain;

namespace Ch10.ContextProviders;

/// <summary>
/// Durable, per-customer memory. In production this would be a database table with a way
/// for customers to see and remove what is remembered about them.
/// </summary>
public sealed class CustomerPreferenceStore
{
    private readonly ConcurrentDictionary<string, List<string>> _preferences = new();

    public IReadOnlyList<string> Get(string customerId)
    {
        List<string> list = _preferences.GetOrAdd(customerId, _ => []);
        lock (list)
        {
            return [.. list];
        }
    }

    public void Remember(string customerId, string preference)
    {
        List<string> list = _preferences.GetOrAdd(customerId, _ => []);
        lock (list)
        {
            if (!list.Contains(preference, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(preference);
            }
        }
    }
}

/// <summary>
/// A context provider that learns (Chapter 10.6). After each run it looks for preferences the
/// customer stated, such as "I prefer email", and stores them; before each run it supplies the
/// remembered preferences as instructions, in this conversation and every later one.
/// </summary>
/// <remarks>
/// The extraction is deliberately simple and explicit. Small, specific memories are far safer
/// than storing whole conversations, and a regular expression cannot be talked into storing
/// something it was not designed to store.
/// </remarks>
public sealed partial class PreferenceMemoryContextProvider(CustomerPreferenceStore store, ICurrentCustomer currentCustomer)
    : AIContextProvider
{
    [GeneratedRegex(@"\bI(?:'d| would)? prefer\b[^.!?\n]*|\bplease (?:contact|call|email|text) me\b[^.!?\n]*",
        RegexOptions.IgnoreCase)]
    private static partial Regex PreferenceStatement();

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> preferences = store.Get(currentCustomer.CustomerId);
        if (preferences.Count == 0)
        {
            return ValueTask.FromResult(new AIContext());
        }

        return ValueTask.FromResult(new AIContext
        {
            Instructions = $"""
                <remembered_preferences>
                The customer told us these preferences in earlier conversations. Respect them.
                {string.Join("\n", preferences.Select(p => $"- \"{p}\""))}
                </remembered_preferences>
                """
        });
    }

    protected override ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        foreach (ChatMessage message in context.RequestMessages.Where(m => m.Role == ChatRole.User))
        {
            foreach (Match match in PreferenceStatement().Matches(message.Text))
            {
                store.Remember(currentCustomer.CustomerId, match.Value.Trim());
            }
        }

        return ValueTask.CompletedTask;
    }
}
