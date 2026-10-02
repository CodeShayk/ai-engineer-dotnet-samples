// Chapter 11, Section 11.5: Consuming MCP servers from a .NET client.
// Launches the stdio server from 11.4-BuildingAServer, discovers its tools, and uses them from a
// chat client and from an agent. Then reads a resource and runs a prompt.
//
// Usage: dotnet run [--http]
//   --http  also connects to the HTTP server (start Ch11.McpServer.Http first). The client reads
//           McpServer:Endpoint (default http://localhost:5181/mcp) and McpServer:ApiKey from the
//           shared User Secrets.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 11.5: An MCP client", options);

// The relative path in Arguments is resolved from this project's folder, wherever you run it from.
string projectDirectory = FindProjectDirectory("Ch11.McpClient.csproj");

// --- Connecting and discovering ---------------------------------------------------------------
await using McpClient mcpClient = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "Northwind Orders",
        Command = "dotnet",
        Arguments = ["run", "--project", "../../11.4-BuildingAServer/Ch11.McpServer.Stdio", "--no-build"],
        WorkingDirectory = projectDirectory
    }));

SampleConsole.Section($"Connected to {mcpClient.ServerInfo.Name} {mcpClient.ServerInfo.Version}");

IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

foreach (McpClientTool tool in tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// --- MCP tools with a chat client ---------------------------------------------------------------
SampleConsole.Section("MCP tools with IChatClient");

IChatClient chatClient = AIClientFactory.CreateChatClient(options)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

ChatResponse response = await chatClient.GetResponseAsync(
    "Has order NW-10249 shipped yet?",
    new ChatOptions { Tools = [.. tools] });

Console.WriteLine(response.Text);

// --- MCP tools with an agent ---------------------------------------------------------------------
SampleConsole.Section("MCP tools with an agent");

AIAgent agent = new ChatClientAgent(
    AIClientFactory.CreateChatClient(options),
    name: "NorthwindAssist",
    instructions: SupportPrompts.System,
    tools: [.. tools]);

AgentResponse answer = await agent.RunAsync("Can I return the speaker from order NW-10252? I've opened it.");
Console.WriteLine(answer.Text);

// --- Resources -----------------------------------------------------------------------------------
SampleConsole.Section("Reading the policy://returns-policy resource");

ReadResourceResult policy = await mcpClient.ReadResourceAsync("policy://returns-policy");
string policyText = policy.Contents.OfType<TextResourceContents>().First().Text;
Console.WriteLine(policyText.Length > 400 ? policyText[..400] + "..." : policyText);

// --- Prompts -----------------------------------------------------------------------------------
SampleConsole.Section("Running the summarize_order_issue prompt");

GetPromptResult prompt = await mcpClient.GetPromptAsync(
    "summarize_order_issue",
    new Dictionary<string, object?>
    {
        ["orderNumber"] = "NW-10249",
        ["issue"] = "Tracking hasn't changed since it shipped and I need it before Monday."
    });

ChatResponse summary = await chatClient.GetResponseAsync(prompt.ToChatMessages(), new ChatOptions { Tools = [.. tools] });
Console.WriteLine(summary.Text);

// --- The same server over HTTP (optional) ---------------------------------------------------------
if (args.Contains("--http"))
{
    SampleConsole.Section("Connecting over Streamable HTTP");

    IConfiguration config = SampleConfiguration.Load();
    string endpoint = config["McpServer:Endpoint"] ?? "http://localhost:5181/mcp";
    string apiKey = config["McpServer:ApiKey"]
        ?? throw new InvalidOperationException(
            "Set McpServer:ApiKey to the key the HTTP server uses: dotnet user-secrets set \"McpServer:ApiKey\" \"<value>\" --id northwind-ai-engineer-samples");

    await using McpClient remoteClient = await McpClient.CreateAsync(
        new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpoint),
            AdditionalHeaders = new Dictionary<string, string> { ["X-Api-Key"] = apiKey }
        }));

    IList<McpClientTool> remoteTools = await remoteClient.ListToolsAsync();
    Console.WriteLine($"{endpoint}: {string.Join(", ", remoteTools.Select(t => t.Name))}");
}

static string FindProjectDirectory(string projectFileName)
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, projectFileName)))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException($"Could not find {projectFileName} above {AppContext.BaseDirectory}.");
}
