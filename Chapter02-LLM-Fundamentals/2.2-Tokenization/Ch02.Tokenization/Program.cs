// Chapter 2, Section 2.2: Tokens and tokenization.
// Counts tokens for different kinds of text, then shows each token individually.
// Runs entirely offline: no model or API key is needed.

using Microsoft.ML.Tokenizers;

Tokenizer tokenizer = TiktokenTokenizer.CreateForEncoding("o200k_base");

string[] samples =
[
    "Where is my order?",
    "OrderStatusRepository.GetAsync(orderId, cancellationToken)",
    "Order NW-2024-118734 was dispatched on 14/03 via courier ref ZX99812044."
];

Console.WriteLine("Token counts (o200k_base encoding)");
Console.WriteLine("----------------------------------");

foreach (string text in samples)
{
    int count = tokenizer.CountTokens(text);
    Console.WriteLine($"{count,3} tokens | {text.Length,3} chars | {text}");
}

Console.WriteLine();
Console.WriteLine("How each sample is split");
Console.WriteLine("------------------------");

foreach (string text in samples)
{
    IReadOnlyList<EncodedToken> tokens = tokenizer.EncodeToTokens(text, out _);
    Console.WriteLine(text);
    Console.WriteLine("  " + string.Join(" | ", tokens.Select(t => $"{Visible(t.Value)}:{t.Id}")));
    Console.WriteLine();
}

// Budgeting: how much of a context window does a typical support request use?
string systemPrompt = """
    You are Northwind Assist, the customer support assistant for Northwind Traders.
    Answer questions about orders, deliveries, returns and products. Keep answers under 120 words.
    """;
string retrievedPolicy = string.Concat(Enumerable.Repeat(
    "Most items can be returned within 30 days of delivery for a full refund. ", 40));

int systemTokens = tokenizer.CountTokens(systemPrompt);
int policyTokens = tokenizer.CountTokens(retrievedPolicy);

Console.WriteLine("A simple context budget");
Console.WriteLine("-----------------------");
Console.WriteLine($"System prompt:       {systemTokens,6} tokens");
Console.WriteLine($"Retrieved passages:  {policyTokens,6} tokens");
Console.WriteLine($"Total input so far:  {systemTokens + policyTokens,6} tokens");
Console.WriteLine();
Console.WriteLine("Local counts are estimates; the provider's reported usage is the source of truth for billing.");

static string Visible(string token) => token.Replace(" ", "·").Replace("\n", "\\n");
