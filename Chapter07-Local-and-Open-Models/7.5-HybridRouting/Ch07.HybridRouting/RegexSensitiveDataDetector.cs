using System.Text.RegularExpressions;

namespace Ch07.HybridRouting;

public interface ISensitiveDataDetector
{
    bool ContainsSensitiveData(string? text);
}

/// <summary>
/// A deliberately simple detector based on regular expressions. It errs on the side of
/// caution: a false positive costs a slightly less capable answer from the local model,
/// while a false negative sends data outside the boundary you promised to keep it within.
/// Chapter 13 builds a more thorough redactor.
/// </summary>
public sealed partial class RegexSensitiveDataDetector : ISensitiveDataDetector
{
    // 13 to 19 digits, optionally separated by spaces or hyphens. No checksum test here:
    // anything that looks like a card number is treated as one.
    [GeneratedRegex(@"\b(?:\d[ -]?){12,18}\d\b")]
    private static partial Regex CardNumber();

    // International Bank Account Numbers, such as GB29 NWBK 6016 1331 9268 19.
    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:[ ]?[A-Z0-9]{4}){2,7}(?:[ ]?[A-Z0-9]{1,3})?\b")]
    private static partial Regex Iban();

    // A UK sort code, such as 12-34-56. On its own it is enough to treat the text as
    // containing bank details, wherever the account number appears.
    [GeneratedRegex(@"\b\d{2}-\d{2}-\d{2}\b")]
    private static partial Regex UkSortCode();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex Email();

    // Ten or more digits with optional country code, spaces, dots, hyphens or brackets.
    [GeneratedRegex(@"(?<!\w)\+?\d[\d\s().-]{8,}\d(?!\w)")]
    private static partial Regex Phone();

    public bool ContainsSensitiveData(string? text) =>
        !string.IsNullOrEmpty(text) &&
        (CardNumber().IsMatch(text) ||
         Iban().IsMatch(text) ||
         UkSortCode().IsMatch(text) ||
         Email().IsMatch(text) ||
         Phone().IsMatch(text));
}
