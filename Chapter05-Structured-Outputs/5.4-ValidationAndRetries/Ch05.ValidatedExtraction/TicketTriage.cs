using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Ch05.ValidatedExtraction;

[JsonConverter(typeof(JsonStringEnumConverter<TicketCategory>))]
public enum TicketCategory { OrderStatus, ReturnsAndRefunds, Billing, ProductQuestion, AccountAndPrivacy, Other }

[JsonConverter(typeof(JsonStringEnumConverter<Urgency>))]
public enum Urgency { Low, Normal, High }

/// <summary>The triage type from Section 5.3 with validation rules added (Section 5.4).</summary>
public sealed partial record TicketTriage(
    [property: Description("The single best category for routing, based on what the customer wants to happen next.")]
    TicketCategory Category,

    [property: Description("High if the customer is blocked, has been waiting more than a week, or mentions safety. Low for general questions.")]
    Urgency Urgency,

    [property: Description("Customer sentiment from -1.0 (very negative) to 1.0 (very positive).")]
    [property: Range(-1.0, 1.0)]
    double Sentiment,

    [property: Description("Northwind order numbers mentioned in the message, formatted NW-#####. Empty if none.")]
    IReadOnlyList<string> OrderNumbers,

    [property: Description("One neutral sentence summarizing the request for a support agent.")]
    [property: Required, MaxLength(300)]
    string Summary) : IValidatableObject
{
    [GeneratedRegex(@"^NW-\d{5}$")]
    private static partial Regex OrderNumberPattern();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Model output is untrusted input: even a "required" list can arrive as null.
        foreach (string orderNumber in (OrderNumbers ?? []).Where(n => !OrderNumberPattern().IsMatch(n)))
        {
            yield return new ValidationResult(
                $"'{orderNumber}' is not a valid order number; expected the format NW-#####.",
                [nameof(OrderNumbers)]);
        }
    }
}
