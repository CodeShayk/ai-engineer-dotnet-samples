namespace Northwind.Shared.Domain;

/// <summary>
/// The authenticated customer for the current request. Tools take the customer's identity
/// from here, never from arguments supplied by the model (Chapter 6.5).
/// </summary>
public interface ICurrentCustomer
{
    string CustomerId { get; }
}

/// <summary>
/// A fixed identity for console samples and tests. In a web application, an implementation
/// would read the customer ID from the authenticated user's claims.
/// </summary>
public sealed class FixedCurrentCustomer(string customerId) : ICurrentCustomer
{
    public string CustomerId { get; } = customerId;
}
