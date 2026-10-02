namespace Northwind.Shared.Domain;

/// <summary>
/// An in-memory stand-in for Northwind's order management system. Dates are relative to
/// today, so the scenarios in the book (an order delivered 10 days ago, one in transit,
/// one outside its return window) stay true whenever you run the samples.
/// </summary>
public sealed class NorthwindStore
{
    private readonly Lock _gate = new();
    private readonly List<RefundRequest> _refundRequests = [];

    public NorthwindStore(IEnumerable<Customer> customers, IEnumerable<Product> products, IEnumerable<Order> orders)
    {
        Customers = customers.ToList();
        Products = products.ToList();
        Orders = orders.ToList();
    }

    public IReadOnlyList<Customer> Customers { get; }

    public IReadOnlyList<Product> Products { get; }

    public IReadOnlyList<Order> Orders { get; }

    public IReadOnlyList<RefundRequest> RefundRequests
    {
        get
        {
            lock (_gate)
            {
                return _refundRequests.ToList();
            }
        }
    }

    public static NorthwindStore CreateSeeded() =>
        new(SeedData.Customers(), SeedData.Products(), SeedData.Orders(DateOnly.FromDateTime(DateTime.Today)));

    public Customer? FindCustomer(string customerId) =>
        Customers.FirstOrDefault(c => c.CustomerId.Equals(customerId, StringComparison.OrdinalIgnoreCase));

    public Order? FindOrder(string orderId) =>
        Orders.FirstOrDefault(o => o.OrderId.Equals(orderId.Trim(), StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Order> OrdersForCustomer(string customerId) =>
        Orders.Where(o => o.CustomerId.Equals(customerId, StringComparison.OrdinalIgnoreCase)).ToList();

    public Product? FindProduct(string productId) =>
        Products.FirstOrDefault(p => p.ProductId.Equals(productId.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Idempotent: there is at most one refund request per order line, so a repeated call
    /// returns the existing request instead of creating a duplicate (Chapter 6.5).
    /// </summary>
    public RefundRequest GetOrCreateRefundRequest(string orderId, string productId, decimal amount, string reason)
    {
        lock (_gate)
        {
            RefundRequest? existing = _refundRequests.FirstOrDefault(r =>
                r.OrderId == orderId && r.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                return existing;
            }

            var request = new RefundRequest(
                $"RF-{_refundRequests.Count + 1001}", orderId, productId.ToUpperInvariant(), amount, reason,
                RefundStatus.PendingApproval, DateTimeOffset.UtcNow);

            _refundRequests.Add(request);
            return request;
        }
    }

    /// <summary>Records a supervisor's decision on a pending refund request.</summary>
    public RefundRequest? Decide(string requestId, bool approved)
    {
        lock (_gate)
        {
            int index = _refundRequests.FindIndex(r => r.RequestId == requestId);
            if (index < 0)
            {
                return null;
            }

            RefundRequest updated = _refundRequests[index] with
            {
                Status = approved ? RefundStatus.Approved : RefundStatus.Rejected
            };
            _refundRequests[index] = updated;
            return updated;
        }
    }
}
