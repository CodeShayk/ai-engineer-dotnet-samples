using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Northwind.Shared.Security;

/// <summary>Removes sensitive values from text before it is sent to a model (Chapter 13.3).</summary>
public interface ISensitiveDataRedactor
{
    /// <summary>Returns the redacted text and the number of values that were replaced.</summary>
    (string Text, int Count) Redact(string text);
}

/// <summary>
/// Recognizes payment card numbers (validated with the Luhn checksum to avoid false
/// positives), bank account formats and, optionally, email addresses and phone numbers,
/// and replaces each with a labeled placeholder the model can still reason about.
/// </summary>
public sealed partial class PaymentAndContactRedactor(bool includeContactDetails = true) : ISensitiveDataRedactor
{
    // 13 to 19 digits, optionally separated by single spaces or hyphens, starting and ending with a digit.
    [GeneratedRegex(@"\b\d(?:[ -]?\d){12,18}\b")]
    private static partial Regex CardCandidate();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:[ ]?[A-Z0-9]{4}){2,7}(?:[ ]?[A-Z0-9]{1,3})?\b")]
    private static partial Regex Iban();

    [GeneratedRegex(@"\b\d{2}-\d{2}-\d{2}\s+\d{8}\b")]
    private static partial Regex UkSortCodeAndAccount();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<!\w)\+?\d[\d\s().-]{8,}\d(?!\w)")]
    private static partial Regex Phone();

    public (string Text, int Count) Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (text, 0);
        }

        int count = 0;

        string result = CardCandidate().Replace(text, m =>
        {
            string digits = new(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is < 13 or > 19 || !PassesLuhn(digits))
            {
                return m.Value;
            }

            count++;
            return "[CARD NUMBER]";
        });

        result = Replace(Iban(), result, "[BANK ACCOUNT]", ref count);
        result = Replace(UkSortCodeAndAccount(), result, "[BANK ACCOUNT]", ref count);
        if (includeContactDetails)
        {
            result = Replace(Email(), result, "[EMAIL ADDRESS]", ref count);
            result = Replace(Phone(), result, "[PHONE NUMBER]", ref count);
        }

        return (result, count);
    }

    private static string Replace(Regex pattern, string input, string placeholder, ref int count)
    {
        int found = pattern.Matches(input).Count;
        count += found;
        return found == 0 ? input : pattern.Replace(input, placeholder);
    }

    public static bool PassesLuhn(string digits)
    {
        int sum = 0;
        bool alternate = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}

/// <summary>
/// Removes card numbers and bank details from user messages, for use in agent and chat client
/// middleware (Chapter 10.5). Contact details are left alone, because a customer may need to
/// give an email address or phone number for the conversation to make sense.
/// </summary>
public static class PaymentDataRedactor
{
    private static readonly PaymentAndContactRedactor Redactor = new(includeContactDetails: false);

    public static ChatMessage Redact(ChatMessage message)
    {
        if (message.Role != ChatRole.User)
        {
            return message;
        }

        bool changed = false;
        List<AIContent> contents = message.Contents
            .Select(content =>
            {
                if (content is TextContent text)
                {
                    (string redacted, int count) = Redactor.Redact(text.Text);
                    if (count > 0)
                    {
                        changed = true;
                        return new TextContent(redacted);
                    }
                }

                return content;   // Images and other content pass through unchanged.
            })
            .ToList();

        return changed
            ? new ChatMessage(message.Role, contents) { AuthorName = message.AuthorName, MessageId = message.MessageId }
            : message;
    }
}
