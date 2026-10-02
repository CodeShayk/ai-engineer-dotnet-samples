using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Northwind.Shared.Domain;

namespace Ch16.NorthwindAssist.Api.Platform;

/// <summary>
/// A simulated sign-in for local development. X-Demo-Customer names the customer; X-Demo-Role:
/// supervisor adds the supervisor role used by the approval endpoints.
/// </summary>
/// <remarks>
/// FOR LOCAL DEVELOPMENT ONLY. Trusting headers to say who the caller is would let anyone act
/// as anyone. PlatformExtensions registers this scheme only in the Development environment.
/// </remarks>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    NorthwindStore store)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Demo";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? customerId = Request.Headers["X-Demo-Customer"].FirstOrDefault()?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(customerId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Customer? customer = store.FindCustomer(customerId);
        if (customer is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown customer."));
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, customer.CustomerId),
            new(ClaimTypes.Name, customer.Name)
        ];

        if (string.Equals(Request.Headers["X-Demo-Role"].FirstOrDefault(), "supervisor", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "supervisor"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

/// <summary>
/// The customer the current request acts for. Normally the signed-in customer, from the
/// authenticated user's claims and never from the model. When a supervisor decides an approval,
/// the endpoint explicitly acts for the conversation's owner, so tools still apply that
/// customer's permissions rather than the supervisor's.
/// </summary>
public sealed class RequestCustomer(IHttpContextAccessor httpContextAccessor) : ICurrentCustomer
{
    private string? _actingFor;

    public void ActFor(string customerId) => _actingFor = customerId;

    public string CustomerId =>
        _actingFor
        ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("There is no signed-in customer for this request.");
}
