using System.Text.Json.Serialization;

namespace Northwind.Shared.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<ProductCategory>))]
public enum ProductCategory
{
    Electronics,
    Accessories,
    Clothing,
    Footwear,
    HomeAndGarden,
    Kitchen,
    Grocery
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    ReturnRequested,
    Refunded
}

public sealed record Customer(
    string CustomerId,
    string Name,
    string Email,
    string City,
    string Country,
    string LoyaltyTier);

public sealed record Product(
    string ProductId,
    string Name,
    ProductCategory Category,
    decimal Price,
    string Description,
    bool FinalSale = false);

public sealed record OrderLine(string ProductId, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public sealed record Order(
    string OrderId,
    string CustomerId,
    DateOnly OrderDate,
    OrderStatus Status,
    IReadOnlyList<OrderLine> Lines,
    string ShippingMethod,
    string? TrackingNumber = null,
    DateOnly? EstimatedDelivery = null,
    DateOnly? DeliveredOn = null)
{
    public decimal Total => Lines.Sum(l => l.LineTotal);
}

[JsonConverter(typeof(JsonStringEnumConverter<RefundStatus>))]
public enum RefundStatus
{
    PendingApproval,
    Approved,
    Rejected
}

public sealed record RefundRequest(
    string RequestId,
    string OrderId,
    string ProductId,
    decimal Amount,
    string Reason,
    RefundStatus Status,
    DateTimeOffset CreatedAt);
