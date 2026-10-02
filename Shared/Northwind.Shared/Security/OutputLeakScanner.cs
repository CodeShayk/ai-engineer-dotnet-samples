using System.Text.RegularExpressions;

namespace Northwind.Shared.Security;

/// <summary>The result of scanning model output before it leaves the application.</summary>
public sealed record OutputScanResult(string SafeText, IReadOnlyList<string> Findings)
{
    public bool LeakDetected => Findings.Count > 0;
}

/// <summary>
/// The last line of defense (Chapter 13.3): scans model output for values that should never
/// appear in a reply, such as card numbers, bank details, credentials and email addresses other
/// than the customer's own, and redacts them whatever caused them to appear.
/// </summary>
public sealed partial class OutputLeakScanner
{
    private readonly PaymentAndContactRedactor _paymentRedactor = new(includeContactDetails: false);

    // Common credential formats: API keys with well-known prefixes, bearer tokens and connection string secrets.
    [GeneratedRegex(@"\b(sk-[A-Za-z0-9_-]{16,}|AKIA[0-9A-Z]{16}|ghp_[A-Za-z0-9]{30,})\b|(?i:(AccountKey|SharedAccessKey|Password|pwd)\s*=\s*[^;\s]{6,})")]
    private static partial Regex Credential();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex Email();

    public OutputScanResult Scan(string output, string? customerEmail = null)
    {
        var findings = new List<string>();

        (string text, int paymentCount) = _paymentRedactor.Redact(output);
        if (paymentCount > 0)
        {
            findings.Add($"{paymentCount} payment detail(s)");
        }

        int credentialCount = Credential().Matches(text).Count;
        if (credentialCount > 0)
        {
            findings.Add($"{credentialCount} credential(s)");
            text = Credential().Replace(text, "[REDACTED CREDENTIAL]");
        }

        // The customer's own address may legitimately appear in a reply; any other address may not.
        int foreignEmails = 0;
        text = Email().Replace(text, m =>
        {
            if (customerEmail is not null && m.Value.Equals(customerEmail, StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }

            foreignEmails++;
            return "[EMAIL ADDRESS]";
        });

        if (foreignEmails > 0)
        {
            findings.Add($"{foreignEmails} email address(es) other than the customer's");
        }

        return new OutputScanResult(text, findings);
    }
}
