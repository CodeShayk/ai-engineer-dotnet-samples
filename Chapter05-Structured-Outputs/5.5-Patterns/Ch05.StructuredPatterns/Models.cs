using System.ComponentModel;
using System.Text.Json.Serialization;
using Northwind.Shared.Domain;

namespace Ch05.StructuredPatterns;

// --- Classification -------------------------------------------------------------------------

[JsonConverter(typeof(JsonStringEnumConverter<RequestedOutcome>))]
public enum RequestedOutcome { Refund, Exchange, Repair, Information, Unclear }

public sealed record RequestedOutcomeClassification(
    [property: Description("Quote the sentence or sentences from the message that indicate what the customer wants.")]
    string Evidence,

    [property: Description("What the customer is asking for.")]
    RequestedOutcome Outcome);

// --- Extraction -----------------------------------------------------------------------------

public sealed record ReturnRequestDetails(
    [property: Description("Customer's full name if stated, otherwise null.")]
    string? CustomerName,

    [property: Description("Order number in the format NW-#####, if stated, otherwise null.")]
    string? OrderNumber,

    [property: Description("Names of the items the customer wants to return, as the customer described them.")]
    IReadOnlyList<string> Items,

    [property: Description("The customer's reason for returning, in one short sentence.")]
    string? Reason,

    [property: Description("What the customer wants to happen.")]
    RequestedOutcome DesiredOutcome,

    [property: Description("Information needed to process a return that the customer did not provide, " +
                           "for example the order number or which item. Empty if nothing is missing.")]
    IReadOnlyList<string> MissingInformation);

// --- Transformation -------------------------------------------------------------------------

public sealed record CatalogListing(
    [property: Description("Customer-facing product title, at most 80 characters, no marketing superlatives.")]
    string Title,

    [property: Description("The best matching catalog category.")]
    ProductCategory Category,

    [property: Description("Three to five short feature bullets, each under 12 words, taken only from the supplier text.")]
    IReadOnlyList<string> KeyFeatures,

    [property: Description("Weight in kilograms if stated in the supplier text, otherwise null. Convert from pounds if needed.")]
    double? WeightKg,

    [property: Description("Care or safety instructions stated in the supplier text, otherwise null.")]
    string? CareInstructions);

// --- Bulk processing ------------------------------------------------------------------------

public sealed record InboundEmail(string Id, string Body);
