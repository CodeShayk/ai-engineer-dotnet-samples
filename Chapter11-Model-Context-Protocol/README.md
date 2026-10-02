# Chapter 11: Model Context Protocol (MCP)

Companion code for Chapter 11. One set of MCP capabilities (Northwind's order tools, policy resources and a support prompt) is served over both MCP transports and consumed from a .NET client.

| Section | Project | What it shows |
|---|---|---|
| 11.4 | `11.4-BuildingAServer/Ch11.McpServer.Tools` | A class library with the tools (`get_order_status`, `check_return_eligibility`), the `policy://{documentId}` resource template and the `summarize_order_issue` prompt. |
| 11.4 | `11.4-BuildingAServer/Ch11.McpServer.Stdio` | The server hosted over stdio, with all logging sent to standard error. |
| 11.4 | `11.4-BuildingAServer/Ch11.McpServer.Http` | The server hosted over Streamable HTTP in ASP.NET Core, protected by an API key. |
| 11.5 | `11.5-ConsumingServers/Ch11.McpClient` | A client that launches the stdio server, lists its tools, uses them from an `IChatClient` and from an agent, reads a resource and runs a prompt. With `--http`, it also connects to the HTTP server. |

## Running the client

From this folder:

```bash
dotnet run --project 11.5-ConsumingServers/Ch11.McpClient
```

The client starts the stdio server itself. You do not need to run the server separately, and you should not: a stdio server waits for JSON-RPC messages on its standard input.

## Running the HTTP server

```bash
dotnet user-secrets set "McpServer:ApiKey" "<a long random value>" --id northwind-ai-engineer-samples
dotnet run --project 11.4-BuildingAServer/Ch11.McpServer.Http
```

The server listens on `http://localhost:5181/mcp` and rejects any request without a matching `X-Api-Key` header. If no key is configured, the server generates a temporary one for the run and prints it, in the Development environment only. With the server running, connect the client over HTTP from a second terminal:

```bash
dotnet run --project 11.5-ConsumingServers/Ch11.McpClient -- --http
```

The client reads the same `McpServer:ApiKey` from User Secrets, and `McpServer:Endpoint` if you changed the address.

The API key keeps local experimentation simple while showing where the security boundary sits. In production, protect the endpoint with Microsoft Entra ID or another OAuth provider, as Section 11.4 and the MCP authorization specification describe.

## Using the server from Visual Studio Code

With GitHub Copilot in agent mode, add the stdio server to `.vscode/mcp.json` in a workspace opened at the repository root (your `sample-code` folder):

```json
{
  "servers": {
    "northwind-orders": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "Chapter11-Model-Context-Protocol/11.4-BuildingAServer/Ch11.McpServer.Stdio"]
    }
  }
}
```

The book's version of this file opens the folder that contains `sample-code` instead, so its path starts with `sample-code/`. Either works, as long as the path is relative to the folder you opened.

Then ask Copilot something like "What's the status of Northwind order NW-10249?" and approve the tool call when prompted. Build the solution first, so the first launch is quick.

## Testing with the MCP Inspector

The [MCP Inspector](https://github.com/modelcontextprotocol/inspector) lets you call tools and read resources by hand. It needs Node.js:

```bash
npx @modelcontextprotocol/inspector dotnet run --project 11.4-BuildingAServer/Ch11.McpServer.Stdio
```

## What to look for

- **Annotations.** Both tools are declared `ReadOnly` and `Idempotent`. Hosts can use these hints, for example to skip confirmation for read-only calls. They are hints from the server, not guarantees, which is why Section 11.5 recommends requiring approval for consequential tools regardless.
- **What the resource refuses to serve.** Try reading `policy://goodwill-guidelines` or `policy://returns-policy-2024` with the Inspector. The internal guidance and the archived policy both exist in the library, and the resource refuses both.
- **Standard output is sacred.** The stdio host sends every log message to standard error. Add a `Console.WriteLine` to a tool and watch the client fail with a protocol error, then remove it again.
