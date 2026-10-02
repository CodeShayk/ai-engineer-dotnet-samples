using System.Security.Cryptography;

namespace Northwind.Shared.Security;

/// <summary>
/// Marks untrusted text, such as a customer email or a retrieved web page, with a boundary its
/// author cannot predict (Chapter 13.2).
/// </summary>
public static class UntrustedContent
{
    /// <summary>
    /// Wraps untrusted text in a boundary the author of the text cannot guess, and returns the
    /// boundary so the system prompt can refer to it.
    /// </summary>
    public static (string Wrapped, string Boundary) Wrap(string source, string content)
    {
        string boundary = $"untrusted-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}";

        // Remove anything that looks like our boundary marker, however unlikely.
        string sanitized = content.Replace(boundary, "[removed]", StringComparison.OrdinalIgnoreCase);

        return ($"<{boundary} source=\"{source}\">\n{sanitized}\n</{boundary}>", boundary);
    }
}
