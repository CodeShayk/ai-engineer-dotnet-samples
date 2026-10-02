# Chapter 7: Local and Open Models

Companion code for Chapter 7. The first project connects to a local Ollama server through the same abstractions as every other chapter. The second routes sensitive requests to a local model and everything else to the cloud.

| Section | Project | What it shows |
|---|---|---|
| 7.4 | `7.4-OllamaChat/Ch07.OllamaChat` | Checks the Ollama server, lists local models, pulls the chat model if missing, streams a response, sets the context length per request, runs the Chapter 5 triage type and the Chapter 6 order tools against the local model, and calls the same model through the OpenAI-compatible endpoint. |
| 7.5 | `7.5-HybridRouting/Ch07.HybridRouting` | `SensitiveDataRoutingChatClient`, an `IChatClient` that sends emails containing card numbers, bank details, phone numbers or email addresses to the local model and everything else to the configured cloud model. |

## Prerequisites

Install [Ollama](https://ollama.com), start it, and pull the default models:

```bash
ollama pull llama3.2
ollama pull nomic-embed-text
```

`Ch07.OllamaChat` pulls the chat model itself if it is missing, which takes a few minutes the first time.

## Running

From this folder:

```bash
dotnet run --project 7.4-OllamaChat/Ch07.OllamaChat
dotnet run --project 7.5-HybridRouting/Ch07.HybridRouting
dotnet run --project 7.5-HybridRouting/Ch07.HybridRouting -- --offline   # routing decisions only, no models
```

## Configuration

These projects talk to Ollama **whatever `AI:Provider` is set to**, because local inference is the subject of the chapter. Two optional settings override the defaults:

| Key | Default | Purpose |
|---|---|---|
| `Ollama:Endpoint` | `http://localhost:11434` | The Ollama server, for example a shared server or a container on another host. |
| `Ollama:ChatModel` | `llama3.2` | The local chat model. Try `qwen3` or `llama3.1:8b` for more reliable tool calling. |

```bash
dotnet user-secrets set "Ollama:ChatModel" "qwen3" --id northwind-ai-engineer-samples
```

For `Ch07.HybridRouting`, the "cloud" side uses the provider configured with `AI:Provider`. If that is also Ollama, both sides of the router are local; the sample says so when it starts. Configure Azure OpenAI or OpenAI to see the routing make a real difference, or use `--offline` to watch the decisions alone.

## What to look for

- **First-request latency.** The first request after Ollama has been idle includes loading the model into memory, so it is much slower than the ones that follow. Run `Ch07.OllamaChat` twice and compare.
- **Reliability gaps.** With small local models, structured output occasionally fails to parse and tool calls are sometimes skipped. The sample reports both rather than hiding them, because measuring the gap on your own tasks is the point of Section 7.5.
- **Routing errs on the side of caution.** The detector treats anything that looks like a card number or a sort code as sensitive, with no checksum test. A false positive costs a slightly less capable answer; a false negative sends data outside the boundary.
