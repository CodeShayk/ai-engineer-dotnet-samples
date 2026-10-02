using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ch16.NorthwindAssist.Api.Platform;

/// <summary>The platform layer: identity, rate limiting and telemetry.</summary>
public static class PlatformExtensions
{
    public const string ChatRateLimitPolicy = "chat";
    public const string SupervisorPolicy = "supervisor";

    /// <summary>
    /// JWT bearer authentication when Authentication:Authority is configured. Otherwise, in
    /// Development only, a demo scheme that signs callers in from request headers, so the sample
    /// runs without an identity provider. Outside Development, an identity provider is required.
    /// </summary>
    public static IServiceCollection AddNorthwindAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        string? authority = configuration["Authentication:Authority"];

        if (!string.IsNullOrWhiteSpace(authority))
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;
                    options.Audience = configuration["Authentication:Audience"];
                });
        }
        else if (environment.IsDevelopment())
        {
            services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, null);
        }
        else
        {
            throw new InvalidOperationException(
                "Configure Authentication:Authority (and Authentication:Audience) for an identity provider. " +
                "The demo authentication scheme is only available in the Development environment.");
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(SupervisorPolicy, policy => policy.RequireRole("supervisor"));

        services.AddHttpContextAccessor();
        return services;
    }

    /// <summary>A token bucket per signed-in user, so one heavy or abusive user cannot consume everyone's capacity.</summary>
    public static IServiceCollection AddNorthwindRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ChatRateLimitPolicy, context => RateLimitPartition.GetTokenBucketLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 20,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

    /// <summary>Traces and metrics for every layer, exported over OTLP (Chapter 15).</summary>
    public static IServiceCollection AddNorthwindTelemetry(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("northwind-assist-api"))
            .WithTracing(tracing => tracing
                .AddSource("Northwind.AI", "Northwind.Agents", "Northwind.Retrieval")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddMeter("Northwind.AI", "Northwind.Agents", "Northwind.Assistant")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter());

        return services;
    }
}
