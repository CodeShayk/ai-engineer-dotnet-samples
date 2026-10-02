# Chapter 16: Production Architecture and Operations

Companion code for Chapter 16. The capstone assembles Northwind Assist from the components built throughout the book. Two smaller projects focus on resilience and on cost.

| Section | Project | What it shows |
|---|---|---|
| 16.2, 16.4 | `16.2-ReferenceArchitecture/Ch16.NorthwindAssist.Api` | The capstone: an ASP.NET Core application composed layer by layer (platform, model access, knowledge, tools, safety, orchestration), with a streaming chat endpoint, a supervisor approval queue, conversations persisted per owner, canary assignment and health checks. |
| 16.3 | `16.3-Resilience/Ch16.ResilientChatClient` | `ResilientChatClient` (timeouts, retries with backoff and jitter, circuit breaker) and `FallbackChatClient`, demonstrated against fake deployments that fail on cue. |
| 16.5 | `16.5-CostAndScale/Ch16.CostOptimization` | `ComplexityRoutingChatClient` sending simple requests to a small model, a cost report against an all-large baseline, and `SemanticAnswerCache` with its safeguards. |

## Running

From this folder:

```bash
dotnet run --project 16.3-Resilience/Ch16.ResilientChatClient           # no model needed
dotnet run --project 16.5-CostAndScale/Ch16.CostOptimization
dotnet run --project 16.2-ReferenceArchitecture/Ch16.NorthwindAssist.Api # then open http://localhost:5116
```

## The capstone

### Layout

| Layer | Files |
|---|---|
| Composition | `Program.cs` |
| Platform | `Platform/PlatformExtensions.cs` (authentication, rate limiting, telemetry), `Platform/DemoAuthentication.cs`, `Platform/Sessions.cs` |
| Model access | `ModelAccess/ModelAccessExtensions.cs`: redaction, telemetry, fallback between deployments, a resilience pipeline per deployment |
| Knowledge | `Knowledge/KnowledgeExtensions.cs`: background ingestion at startup, readiness health check |
| Tools | `Tools/ToolsExtensions.cs`: order and refund tools bound to the signed-in customer, optional allowlisted MCP tools |
| Safety | `Safety/SafetyExtensions.cs`: redactor, prompt attack detector, output sanitizer, leak scanner, action limiter |
| Orchestration | `Orchestration/AgentExtensions.cs`: the Chapter 12 handoff team, rebuilt per request with stable agent ids and exposed as one agent |
| Endpoints | `Endpoints/AssistantEndpoints.cs`, `Endpoints/ApprovalEndpoints.cs`, `Endpoints/ConversationRunner.cs` |
| Deployment | `Deployment/CanaryAssignment.cs` |

### Trying it

Open `http://localhost:5116`, choose a customer and chat. Replies stream as Server-Sent Events: `delta` events carry raw text as it is generated, and a `final` event carries the reply as safe HTML, after leak scanning and sanitization, which replaces the streamed text. A `done` event reports the conversation id, the variant that served it (`stable`, or `canary` for about 10% of conversations) and the agent that answered.

To see an approval, ask for a refund on an eligible item, for example as Thomas Hardy: "The jacket from my order NW-10248 leaks at the seams. I'd like a refund." When the refund agent requests it, the conversation pauses. Tick **Supervisor** to see the queue, and approve or decline. The decision resumes the stored session on behalf of the customer who owns the conversation, which may happen long after the customer's request.

`Ch16.NorthwindAssist.Api.http` has the same requests for the REST client, including the checks that matter: no sign-in gives 401, another customer's conversation gives 404, a customer cannot reach the approval queue (403), and a direct prompt attack is refused before it reaches any agent.

### Which model to use

The capstone runs with Ollama's defaults, but a handoff team needs a model that is reliable at tool calling. With the 3-billion-parameter `llama3.2`, the front desk agent sometimes writes a tool call as text instead of handing off, and answers can ignore the policy excerpts entirely; in testing it invented a discount and a restocking fee that no Northwind policy contains. Those are exactly the failures that evaluation (Chapter 14) and observability (Chapter 15) exist to catch. For a realistic experience, configure Azure OpenAI or OpenAI, or a larger local model such as `qwen3`.

### Running against real infrastructure

The capstone runs locally with in-memory stores. Each piece has a production replacement, configured without changing the composition:

| Concern | Local default | Production configuration |
|---|---|---|
| Authentication | Demo headers (`X-Demo-Customer`, `X-Demo-Role`), Development only | Set `Authentication:Authority` and `Authentication:Audience` for Microsoft Entra ID or another OpenID Connect provider. Roles come from the token. |
| Session store | `AddDistributedMemoryCache` | Replace with Redis (`AddStackExchangeRedisCache`) so conversations survive restarts and are shared across instances. |
| Vector store | In-memory, ingested at startup | Use a `CommunityToolkit.VectorData.*` connector such as `AzureAISearch` or `PgVector`, and move ingestion to a separate job (Chapter 9.9). |
| Fallback model | None | Set `AI:Fallback:ChatDeployment`, and `AI:Fallback:Endpoint` for a deployment in another region. |
| Prompt attack screening | Heuristics | Set `ContentSafety:Endpoint` to use Prompt Shields with the application's managed identity. |
| Order tools | In-process | Set `McpServer:Endpoint` and `McpServer:ApiKey` to use the allowlisted tools from the Chapter 11 HTTP server. |
| Telemetry | OTLP to `localhost:4317` | Point `OTEL_EXPORTER_OTLP_ENDPOINT` at your collector, or add the Azure Monitor distribution. |
| Canary share | `Canary:Percent` = 10 | Drive it from configuration or a feature flag service, and compare variants on the Chapter 15 dashboard. |

Host it as any containerized ASP.NET Core service: Azure Container Apps, Azure Kubernetes Service or App Service (Section 16.2).

## What to look for in the other projects

- **Resilience.** Each scenario prints what the pipeline did: retries before success, the circuit opening so that the primary is not called at all, a hung call stopped by the per-attempt timeout, and a streaming fallback that happens only before the first update.
- **Cost.** The router reports each decision and its reason. Short single questions go to the small model, complaints and comparisons go to the large one, and the small model classifies the ambiguous rest. Prices are illustrative and applied per tier, so the saving is meaningful even when both tiers are the same local model.
- **The semantic cache's near miss.** "Can I return unopened earbuds?" scores close to the cached "opened earbuds" question, but has a different answer. If it scores above your threshold, the threshold is too low.
