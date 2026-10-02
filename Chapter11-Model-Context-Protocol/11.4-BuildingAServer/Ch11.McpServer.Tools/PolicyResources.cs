using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Northwind.Shared.Knowledge;

namespace Ch11.McpServer.Tools;

/// <summary>
/// Every current customer policy as an MCP resource, through one URI template (Chapter 11.4).
/// Archived and internal documents are never exposed.
/// </summary>
[McpServerResourceType]
public sealed class PolicyResources
{
    [McpServerResource(UriTemplate = "policy://{documentId}", Name = "Northwind policy", MimeType = "text/markdown")]
    [Description("A Northwind Traders customer policy document, such as returns-policy or refunds.")]
    public static string GetPolicy(string documentId) =>
        PolicyLibrary.LoadAll()
            .FirstOrDefault(p => p.Id == documentId && p.Status == "current" && p.Audience == "customer")?.Content
            ?? throw new McpException($"No current policy named '{documentId}' was found.");
}
