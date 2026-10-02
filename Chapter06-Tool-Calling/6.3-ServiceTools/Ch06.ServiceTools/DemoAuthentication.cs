using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Northwind.Shared.Domain;

namespace Ch06.ServiceTools;

/// <summary>
/// A simulated sign-in for local demos. The caller names a customer in the X-Demo-Customer
/// header and is signed in as that customer.
/// </summary>
/// <remarks>
/// FOR LOCAL DEVELOPMENT ONLY. Trusting a header to say who the caller is would let anyone
/// impersonate any customer. A real application validates a token issued by an identity
/// provider, as Chapter 16 shows. Program.cs refuses to use this scheme outside Development.
/// </remarks>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    NorthwindStore store)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Demo";
    public const string HeaderName = "X-Demo-Customer";
    public const string CustomerIdClaim = "customer_id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? customerId = Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Customer? customer = store.FindCustomer(customerId.Trim().ToUpperInvariant());
        if (customer is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown customer."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(CustomerIdClaim, customer.CustomerId), new Claim(ClaimTypes.Name, customer.Name)],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Reads the customer's identity from the authenticated user, never from the model.</summary>
public sealed class ClaimsCurrentCustomer(IHttpContextAccessor httpContextAccessor) : ICurrentCustomer
{
    public string CustomerId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(DemoAuthenticationHandler.CustomerIdClaim)
        ?? throw new InvalidOperationException("There is no signed-in customer for this request.");
}
