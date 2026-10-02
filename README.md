# From .NET Developer to AI Engineer: Companion Code

Working code for every chapter of *From .NET Developer to AI Engineer*. The samples follow one running example, Northwind Traders and its customer assistant, Northwind Assist, from a first model call in Chapter 1 to a production-shaped application in Chapter 16.

## Prerequisites

- **.NET 10 SDK** (10.0.100 or later). `global.json` pins the SDK band and opts in to Microsoft.Testing.Platform for `dotnet test`.
- **A model provider**, one of:
  - **Ollama** for local models, with no account or API key. Install it from <https://ollama.com>, then pull the two default models:
    ```bash
    ollama pull llama3.2
    ollama pull nomic-embed-text
    ```
  - **Azure OpenAI**, through Microsoft Foundry, signed in with `az login` (Microsoft Entra ID) or with an API key.
  - **OpenAI**, with an API key.
- **Docker** (optional), for the Aspire dashboard in Chapter 15.
- **An editor**: Visual Studio 2026, Visual Studio Code with the C# Dev Kit, or Rider.

## Quick start

```bash
git clone https://github.com/CodeShayk/ai-engineer-dotnet-samples.git sample-code
cd sample-code
dotnet build NorthwindAI.slnx
dotnet run --project Chapter01-From-DotNet-To-AI-Engineer/1.6-SetupCheck/Ch01.SetupCheck
```

Cloning into a folder named `sample-code` makes your copy match the paths used throughout the book, such as `sample-code/Chapter09-RAG`.

With nothing configured, every sample uses a local Ollama server at `http://localhost:11434` with `llama3.2` and `nomic-embed-text`.

## Configuration

Every project shares one User Secrets store, with the id `northwind-ai-engineer-samples`, so you configure a provider once and every chapter picks it up. Environment variables override User Secrets (use `__` for `:`, for example `AI__Provider`).

```bash
# Azure OpenAI (Microsoft Entra ID; add AI:ApiKey only if you must use a key)
dotnet user-secrets set "AI:Provider" "AzureOpenAI" --id northwind-ai-engineer-samples
dotnet user-secrets set "AI:Endpoint" "https://<your-resource>.openai.azure.com/openai/v1/" --id northwind-ai-engineer-samples
dotnet user-secrets set "AI:ChatDeployment" "gpt-5-mini" --id northwind-ai-engineer-samples
dotnet user-secrets set "AI:EmbeddingDeployment" "text-embedding-3-small" --id northwind-ai-engineer-samples

# OpenAI
dotnet user-secrets set "AI:Provider" "OpenAI" --id northwind-ai-engineer-samples
dotnet user-secrets set "AI:ApiKey" "<your key>" --id northwind-ai-engineer-samples
```

| Key | Ollama default | Azure OpenAI / OpenAI default | Used for |
|---|---|---|---|
| `AI:Provider` | `Ollama` | | `Ollama`, `AzureOpenAI` or `OpenAI` |
| `AI:Endpoint` | `http://localhost:11434` | required for Azure | The service endpoint. For Azure, the v1 endpoint ending in `/openai/v1/`. |
| `AI:ApiKey` | | required for OpenAI | Azure prefers Microsoft Entra ID when no key is set. |
| `AI:ChatDeployment` | `llama3.2` | `gpt-5-mini` | The main chat model. |
| `AI:SmallChatDeployment` | the chat model | the chat model | Rewriting, reranking, triage and routing (Chapters 3, 9, 16). |
| `AI:JudgeChatDeployment` | the chat model | the chat model | The evaluation judge (Chapter 14). Use a capable model. |
| `AI:EmbeddingDeployment` | `nomic-embed-text` | `text-embedding-3-small` | Embeddings (Chapters 2, 4, 8, 9 onward). |
| `AI:EmbeddingDimensions` | `768` | `1536` | Must match the embedding model. |

A few chapters read settings of their own; the chapter READMEs describe them:

| Keys | Chapters | Purpose |
|---|---|---|
| `Ollama:Endpoint`, `Ollama:ChatModel` | 7 | The local model for the local-inference samples, whatever `AI:Provider` is. |
| `McpServer:ApiKey`, `McpServer:Endpoint` | 11, 16 | The API key for the HTTP MCP server, and where clients find it. |
| `ContentSafety:Endpoint`, `ContentSafety:ApiKey` | 13, 16 | Azure AI Content Safety Prompt Shields. Heuristics are used when unset. |
| `Authentication:Authority`, `Authentication:Audience` | 16 | JWT bearer authentication. A demo scheme is used in Development when unset. |
| `AI:Fallback:ChatDeployment`, `AI:Fallback:Endpoint` | 16 | A secondary deployment for fallback. |
| `Telemetry:ConsoleExporter`, `OTEL_EXPORTER_OTLP_ENDPOINT` | 15 | Where telemetry goes. |

## Layout

```text
sample-code/
├── NorthwindAI.slnx              one solution, with solution folders that mirror these directories
├── Directory.Build.props         .NET 10, C# latest, nullable, implicit usings, the shared User Secrets id
├── Directory.Packages.props      every NuGet version, pinned centrally with transitive pinning
├── global.json                   SDK version and the Microsoft.Testing.Platform test runner
├── dotnet-tools.json             local tools: the aieval evaluation report generator
├── Shared/
│   ├── Northwind.Shared/         provider factory, Northwind domain and seed data, tools, policy library and
│   │                             RAG pipeline, structured output, security components, test doubles
│   └── Northwind.Agents/         agent context providers and the action rate limiter
└── ChapterNN-<Title>/            one folder per chapter
    ├── README.md                 the projects, what they show and how to run them
    └── N.M-<Topic>/              one folder per book section
        └── ChNN.<Project>/       one or more projects
```

When a listing in the book comes from a companion project, the text names the topic folder and project, for example `9.9-PolicyAssistant/Ch09.PolicyAssistant`.

## Chapters

| Chapter | Folder | Highlights |
|---|---|---|
| 1 | `Chapter01-From-DotNet-To-AI-Engineer` | Setup check |
| 2 | `Chapter02-LLM-Fundamentals` | Tokenization, embeddings, inference timing, sampling, cost estimation |
| 3 | `Chapter03-Prompt-Engineering` | Message roles, few-shot classification, context management, prompt templates, prompt tests |
| 4 | `Chapter04-Microsoft-Extensions-AI` | `IChatClient`, streaming API, embeddings, the client pipeline, provider switching, tests with a fake client |
| 5 | `Chapter05-Structured-Outputs` | Typed responses, validation and retries, extraction patterns |
| 6 | `Chapter06-Tool-Calling` | Function tools, service tools in ASP.NET Core, manual invocation and approvals, safe tools |
| 7 | `Chapter07-Local-and-Open-Models` | Ollama from .NET, hybrid routing by sensitivity |
| 8 | `Chapter08-Vector-Databases` | Similarity metrics, hybrid search, `Microsoft.Extensions.VectorData` |
| 9 | `Chapter09-RAG` | Chunking, reranking, the policy assistant |
| 10 | `Chapter10-Microsoft-Agent-Framework` | Agents, sessions, tools and middleware, context providers, workflows |
| 11 | `Chapter11-Model-Context-Protocol` | An MCP server over stdio and HTTP, and an MCP client |
| 12 | `Chapter12-AI-Agents` | Orchestrations, the handoff support team, bounded agents |
| 13 | `Chapter13-AI-Security` | Prompt injection defenses, redaction, output handling, red-team tests, secure retrieval |
| 14 | `Chapter14-AI-Evaluation` | Quality and custom evaluators, the golden dataset as tests, HTML reports |
| 15 | `Chapter15-AI-Observability` | OpenTelemetry, cost and latency metrics, the Aspire dashboard, dashboard queries |
| 16 | `Chapter16-Production-Architecture` | The capstone API, resilience and fallback, cost routing and semantic caching |
| 17 | `Chapter17-Your-Path-Forward` | A self-contained portfolio starter and five project briefs |

## Running tests

The test projects use xUnit v3 on Microsoft.Testing.Platform. Run them with `--project`:

```bash
dotnet test --project Chapter03-Prompt-Engineering/3.7-PromptTesting/Ch03.PromptTests
dotnet test --project Chapter04-Microsoft-Extensions-AI/4.7-ProviderSwitching/Ch04.ProviderSwitching.Tests
dotnet test --project Chapter13-AI-Security/13.5-ExcessiveAgency/Ch13.ExcessiveAgency
dotnet test --project Chapter14-AI-Evaluation/14.6-EvaluationTests/Ch14.EvaluationTests
dotnet test --project Chapter17-Your-Path-Forward/17.3-PortfolioStarter/Ch17.PortfolioStarter.EvaluationTests
```

Tests that call a live model are skipped unless `RUN_MODEL_TESTS=true`, so every test project is safe to run in any build:

```powershell
$env:RUN_MODEL_TESTS = "true"; dotnet test --project Chapter14-AI-Evaluation/14.6-EvaluationTests/Ch14.EvaluationTests
```

## Samples that run without a model

These run entirely offline, which makes them a good place to start reading code:

| Project | Command |
|---|---|
| Tokenization and cost estimation | `dotnet run --project Chapter02-LLM-Fundamentals/2.2-Tokenization/Ch02.Tokenization` (and `2.7-ModelSelection/Ch02.CostEstimator`) |
| Manual invocation and approvals with a scripted model | `dotnet run --project Chapter06-Tool-Calling/6.4-ManualInvocation/Ch06.ManualInvocation -- --offline` |
| Refund tool safeguards | `dotnet run --project Chapter06-Tool-Calling/6.5-SafeTools/Ch06.SafeTools -- --offline` |
| Hybrid routing decisions | `dotnet run --project Chapter07-Local-and-Open-Models/7.5-HybridRouting/Ch07.HybridRouting -- --offline` |
| Brute-force search benchmark | `dotnet run --project Chapter08-Vector-Databases/8.2-SimilaritySearch/Ch08.SimilarityMetrics -- --offline` |
| Chunking | `dotnet run --project Chapter09-RAG/9.3-Chunking/Ch09.Chunking` |
| Security samples | `--offline` on the 13.2, 13.3 and 13.6 projects; 13.4 needs no flag |
| Citation evaluator | `dotnet run --project Chapter14-AI-Evaluation/14.5-CustomEvaluators/Ch14.CustomEvaluators -- --offline` |
| Resilience and fallback | `dotnet run --project Chapter16-Production-Architecture/16.3-Resilience/Ch16.ResilientChatClient` |

## About local models

Everything runs on Ollama's defaults, and the samples were verified with `llama3.2` and `nomic-embed-text`. A 3-billion-parameter model is, however, much weaker than current cloud models at tool calling, following grounding rules and judging other answers. You will see it skip tools, describe a handoff instead of performing one, answer "I'm not sure" more often, or invent a detail its sources do not contain. The samples report these failures rather than hiding them, and several chapters are about catching exactly these failures. For the multi-agent samples (Chapters 10 to 12 and 16) and the evaluation judge (Chapter 14), configure a cloud provider or a larger tool-capable local model such as `qwen3`.

## Notes

- **Prices and model names are illustrative.** Replace them with your provider's current models and rates; `Chapter02-LLM-Fundamentals/2.7-ModelSelection/Ch02.CostEstimator/costsettings.json` and `Chapter15-AI-Observability/15.6-OpenTelemetry/Ch15.ObservableAssistant/appsettings.json` hold the price lists.
- **Vector store connectors** come from the .NET Community Toolkit (`CommunityToolkit.VectorData.*`). Keep the connector and `Microsoft.Extensions.VectorData.Abstractions` versions in step; Chapter 8 explains why.
- **Demo authentication.** The ASP.NET Core samples in Chapters 6 and 16 sign callers in from request headers so that they run without an identity provider. That scheme is only enabled in the Development environment.
- **Generated output.** Evaluation results are written to `eval-results`, and agent sessions from Chapter 10 to a `sessions` folder next to the executable. Neither belongs in source control.
