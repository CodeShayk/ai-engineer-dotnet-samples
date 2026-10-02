// Chapter 11, Section 11.4: An MCP server hosted over stdio.
// A host such as Visual Studio Code, or the client in 11.5-ConsumingServers, launches this
// process and talks MCP over its standard input and output. Do not run it directly unless you
// want to type JSON-RPC by hand.

using Ch11.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Northwind.Shared.Domain;

var builder = Host.CreateApplicationBuilder(args);

// Standard output carries MCP messages; send all log output to standard error.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(NorthwindStore.CreateSeeded());
builder.Services.AddSingleton<ReturnPolicyRules>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(OrderMcpTools).Assembly)
    .WithResourcesFromAssembly(typeof(OrderMcpTools).Assembly)
    .WithPromptsFromAssembly(typeof(OrderMcpTools).Assembly);

await builder.Build().RunAsync();
