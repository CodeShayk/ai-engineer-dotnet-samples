# Chapter 4: AI Integration with Microsoft.Extensions.AI

Companion code for Chapter 4. These projects cover the two core abstractions, `IChatClient` and `IEmbeddingGenerator`, and the client pipeline that the rest of the book builds on.

| Section | Project | What it shows |
|---|---|---|
| 4.3 | `4.3-ChatClient/Ch04.ChatBasics` | Requests and responses, client metadata, cancellation with a linked timeout, and multimodal input with an image. |
| 4.4 | `4.4-Streaming/Ch04.StreamingApi` | An ASP.NET Core minimal API that streams responses to the browser with Server-Sent Events, plus a small HTML client. The server always owns the system prompt. |
| 4.5 | `4.5-Embeddings/Ch04.EmbeddingGenerator` | Embeds the product catalog in one batched call, runs semantic queries, requests shorter vectors with the `Dimensions` option and caches repeated inputs. |
| 4.6 | `4.6-Middleware/Ch04.ClientPipeline` | Registers a chat client with logging, a date-injecting delegate, caching, the custom `UsageTrackingChatClient` and OpenTelemetry, then resolves a keyed `"small"` client for ticket triage. |
| 4.7 | `4.7-ProviderSwitching/Ch04.ProviderSwitching` | Runs the same questions against whichever provider is configured and prints answers with latency and token usage. |
| 4.7 | `4.7-ProviderSwitching/Ch04.ProviderSwitching.Tests` | Unit tests that use `FakeChatClient`, so they never call a model. |

## Running

From this folder:

```bash
dotnet run --project 4.3-ChatClient/Ch04.ChatBasics
dotnet run --project 4.3-ChatClient/Ch04.ChatBasics -- path/to/damaged-kettle.jpg   # multimodal input
dotnet run --project 4.4-Streaming/Ch04.StreamingApi                                 # then open the URL shown
dotnet run --project 4.5-Embeddings/Ch04.EmbeddingGenerator
dotnet run --project 4.6-Middleware/Ch04.ClientPipeline
dotnet run --project 4.7-ProviderSwitching/Ch04.ProviderSwitching
dotnet test --project 4.7-ProviderSwitching/Ch04.ProviderSwitching.Tests
```

Multimodal input needs a vision-capable model. Most current cloud models qualify. With Ollama, pull a vision model such as `llama3.2-vision` and set `AI:ChatDeployment` to it for that run.

## Comparing providers

The provider is chosen by configuration only. To compare two providers, run `Ch04.ProviderSwitching` once with each:

```powershell
$env:AI__Provider = "Ollama"; dotnet run --project 4.7-ProviderSwitching/Ch04.ProviderSwitching
$env:AI__Provider = "OpenAI"; dotnet run --project 4.7-ProviderSwitching/Ch04.ProviderSwitching
Remove-Item Env:AI__Provider
```

Environment variables override user secrets, so this changes the provider for a single run without editing your stored configuration. The OpenAI run also needs `AI:ApiKey` to be set.

## What to look for

- In `Ch04.ClientPipeline`, the first request is answered by the model and the second, identical request is served from the cache. The usage tracker logs one model call, while the logging middleware, which sits outside the cache, records both.
- In `Ch04.StreamingApi`, the page shows time to first token for each answer. Compare it with the total time to see why streaming matters for interactive features.
