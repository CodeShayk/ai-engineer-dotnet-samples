using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Northwind.Agents.Security;
using Northwind.Shared.Security;

namespace Ch16.NorthwindAssist.Api.Safety;

/// <summary>The safety layer (Chapter 13): redaction, prompt attack screening, output checks and action limits.</summary>
public static class SafetyExtensions
{
    public static IServiceCollection AddNorthwindSafety(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISensitiveDataRedactor>(new PaymentAndContactRedactor());
        services.AddSingleton(new OutputLeakScanner());
        services.AddSingleton(new ActionRateLimiter(maxCallsPerToolPerConversation: 3));

        string[] allowedHosts = configuration.GetSection("Safety:AllowedLinkHosts").Get<string[]>() ?? [];
        services.AddSingleton(new AssistantOutputSanitizer(new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase)));

        // Prompt Shields when Azure AI Content Safety is configured; heuristics otherwise.
        string? contentSafetyEndpoint = configuration["ContentSafety:Endpoint"];
        if (string.IsNullOrWhiteSpace(contentSafetyEndpoint))
        {
            services.AddSingleton<IPromptAttackDetector, HeuristicPromptAttackDetector>();
        }
        else
        {
            services.AddHttpClient<IPromptAttackDetector, PromptShieldsDetector>(http =>
                    http.BaseAddress = new Uri(contentSafetyEndpoint.EndsWith('/') ? contentSafetyEndpoint : contentSafetyEndpoint + "/"))
                .AddHttpMessageHandler(() => new EntraIdHandler());
        }

        return services;
    }

    /// <summary>Authenticates Content Safety calls with a managed identity or the developer's sign-in.</summary>
    private sealed class EntraIdHandler : DelegatingHandler
    {
        private static readonly TokenCredential Credential = new DefaultAzureCredential();
        private static readonly TokenRequestContext Scope = new(["https://cognitiveservices.azure.com/.default"]);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AccessToken token = await Credential.GetTokenAsync(Scope, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
