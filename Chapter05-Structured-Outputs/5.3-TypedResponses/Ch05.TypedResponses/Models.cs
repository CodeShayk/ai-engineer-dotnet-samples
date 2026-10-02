using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Ch05.TypedResponses;

[JsonConverter(typeof(JsonStringEnumConverter<TicketCategory>))]
public enum TicketCategory { OrderStatus, ReturnsAndRefunds, Billing, ProductQuestion, AccountAndPrivacy, Other }

[JsonConverter(typeof(JsonStringEnumConverter<Urgency>))]
public enum Urgency { Low, Normal, High }

public sealed record TicketTriage(
    [property: Description("The single best category for routing, based on what the customer wants to happen next.")]
    TicketCategory Category,

    [property: Description("High if the customer is blocked, has been waiting more than a week, or mentions safety. Low for general questions.")]
    Urgency Urgency,

    [property: Description("Customer sentiment from -1.0 (very negative) to 1.0 (very positive).")]
    double Sentiment,

    [property: Description("Northwind order numbers mentioned in the message, formatted NW-#####. Empty if none.")]
    IReadOnlyList<string> OrderNumbers,

    [property: Description("One neutral sentence summarizing the request for a support agent.")]
    string Summary);

/// <summary>Used in Section 5.2 to show the JSON schema generated from a C# type.</summary>
public sealed record DeliveryEstimate(
    [property: Description("Earliest expected delivery date, ISO 8601.")] DateOnly Earliest,
    [property: Description("Latest expected delivery date, ISO 8601.")] DateOnly Latest,
    [property: Description("True if the order is currently delayed.")] bool IsDelayed);
