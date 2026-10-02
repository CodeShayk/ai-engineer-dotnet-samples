using Ch16.NorthwindAssist.Api.Platform;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

namespace Ch16.NorthwindAssist.Api.Tools;

/// <summary>
/// Optional tools from a remote MCP server (Chapter 11), when McpServer:Endpoint is configured.
/// Only the allowlisted tools are exposed to the agents, whatever else the server offers.
/// </summary>
public sealed class McpToolSource(IConfiguration configuration, ILogger<McpToolSource> logger) : IAsyncDisposable
{
    private static readonly HashSet<string> Allowlist = ["get_order_status", "check_return_eligibility"];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;
    private IReadOnlyList<AITool>? _tools;

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken)
    {
        string? endpoint = configuration["McpServer:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return [];
        }

        if (_tools is not null)
        {
            return _tools;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_tools is null)
            {
                _client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = new Uri(endpoint),
                    AdditionalHeaders = new Dictionary<string, string> { ["X-Api-Key"] = configuration["McpServer:ApiKey"] ?? "" }
                }), cancellationToken: cancellationToken);

                IList<McpClientTool> tools = await _client.ListToolsAsync(cancellationToken: cancellationToken);
                _tools = tools.Where(t => Allowlist.Contains(t.Name)).ToList<AITool>();
                logger.LogInformation("Using {Count} allowlisted tools from the MCP server at {Endpoint}", _tools.Count, endpoint);
            }

            return _tools;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The MCP server at {Endpoint} is unavailable; continuing with in-process tools only", endpoint);
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}

/// <summary>The tools and integration layer: domain services and the tools built on them.</summary>
public static class ToolsExtensions
{
    public static IServiceCollection AddNorthwindTools(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(NorthwindStore.CreateSeeded());   // A database in production
        services.AddSingleton<ReturnPolicyRules>();
        services.AddScoped<RequestCustomer>();
        services.AddScoped<ICurrentCustomer>(sp => sp.GetRequiredService<RequestCustomer>());
        services.AddScoped<OrderTools>();
        services.AddScoped<RefundTools>();
        services.AddSingleton<McpToolSource>();
        return services;
    }
}
