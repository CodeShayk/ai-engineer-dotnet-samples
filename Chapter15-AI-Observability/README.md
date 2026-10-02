# Chapter 15: AI Observability and Monitoring

Companion code for Chapter 15. One project, Northwind Assist as an ASP.NET Core API, instrumented with OpenTelemetry from end to end. It exports traces, metrics and logs over OTLP to the Aspire dashboard during development, or to any compatible backend in production.

| Section | File | What it shows |
|---|---|---|
| 15.2 | `Telemetry/TimeToFirstTokenChatClient.cs` | Chat client middleware recording the time to the first streamed text as a histogram. |
| 15.3 | `Telemetry/TracedPolicyRetriever.cs`, `ObservablePolicyAssistant.cs` | Spans of our own for query rewriting, retrieval and reranking, recording chunk ids and scores, never content. |
| 15.4 | `Telemetry/CostTrackingChatClient.cs`, `Telemetry/AIFeature.cs` | Estimated cost per call from a configurable price list, tagged with the model and the feature that made the call. |
| 15.5 | `Telemetry/AssistantMetrics.cs`, `Program.cs` | Behavioral metrics: tool call outcomes, retrieval misses and escalations. A health check that never sends a prompt. |
| 15.6 | `Program.cs`, `../docker-compose.yml` | OpenTelemetry registration for every source and meter, instrumented chat client, embedding generator and agent, and the Aspire dashboard. |

## Running

From the `15.6-OpenTelemetry` folder:

```bash
docker compose up -d                                   # starts the Aspire dashboard
docker compose logs aspire-dashboard                   # shows the sign-in link with its token
dotnet run --project Ch15.ObservableAssistant          # listens on http://localhost:5115
```

Then send some traffic. The simplest way is the demo endpoint, which sends a varied batch of requests through every path:

```bash
curl -X POST http://localhost:5115/api/demo/traffic
```

`Ch15.ObservableAssistant.http` contains individual requests for each endpoint. Open `http://localhost:18888` and look at:

- **Traces.** Open a `POST /api/assistant/ask` trace to see the rewrite, retrieve and rerank spans, the model calls inside them and the attempts to produce a valid grounded answer. Open a chat trace to see the agent run, each tool call and each model round trip.
- **Metrics.** Under the `northwind-assist` resource: `gen_ai.client.token.usage`, `gen_ai.client.operation.duration` and, for streamed calls, `gen_ai.client.operation.time_to_first_chunk` and `gen_ai.client.operation.time_per_output_chunk` from the built-in instrumentation, and `northwind.ai.time_to_first_token`, `northwind.ai.estimated_cost`, `northwind.assistant.tool_calls`, `northwind.assistant.retrieval_misses` and `northwind.assistant.escalations` from our own code.
- **Structured logs.** Application logs, correlated with the traces that produced them.

In Development, the chat client and agent capture message content (`EnableSensitiveData`), so the dashboard shows the full prompts and responses for each model call. Outside Development, content capture is off.

### Without Docker

Set `Telemetry:ConsoleExporter` to `true` in `appsettings.json` (or with `--Telemetry:ConsoleExporter=true` on the command line) to print every span to the console. Metrics still go to the OTLP endpoint, which simply drops them if nothing is listening.

### Prices

`appsettings.json` contains illustrative prices per million tokens. Replace them with your provider's current rates. Local models are priced at zero, so with Ollama the cost metric records zero for every call, which still shows the attribution working.

## Example queries for Azure Monitor

When you export to Azure Monitor (Application Insights), the panels described in Section 15.7 are built from queries like these. Model calls are client spans, so they appear in the `dependencies` table. Metrics appear in `customMetrics`, where histograms are stored as sums and counts per interval.

**Request volume and error rate**

```kusto
requests
| where timestamp > ago(24h)
| summarize requests = count(), failed = countif(success == false) by bin(timestamp, 15m)
| extend errorRate = todouble(failed) / requests
| render timechart
```

**End-to-end latency percentiles by endpoint**

```kusto
requests
| where timestamp > ago(24h)
| summarize p50 = percentile(duration, 50), p95 = percentile(duration, 95) by bin(timestamp, 15m), name
| render timechart
```

**Model call latency percentiles by model**

```kusto
dependencies
| where timestamp > ago(24h)
| where tostring(customDimensions["gen_ai.operation.name"]) == "chat"
| extend model = tostring(customDimensions["gen_ai.response.model"])
| summarize p50 = percentile(duration, 50), p95 = percentile(duration, 95), calls = count() by bin(timestamp, 15m), model
| render timechart
```

**Time to first token (average and worst per interval)**

```kusto
customMetrics
| where name == "northwind.ai.time_to_first_token"
| summarize avgSeconds = sum(valueSum) / sum(valueCount), maxSeconds = max(valueMax) by bin(timestamp, 15m)
| render timechart
```

**Token usage by model and token type**

```kusto
customMetrics
| where name == "gen_ai.client.token.usage"
| extend model = tostring(customDimensions["gen_ai.response.model"]),
         tokenType = tostring(customDimensions["gen_ai.token.type"])
| summarize tokens = sum(valueSum) by bin(timestamp, 1h), model, tokenType
| render timechart
```

**Estimated daily cost by feature**

```kusto
customMetrics
| where name == "northwind.ai.estimated_cost"
| extend feature = tostring(customDimensions["northwind.feature"])
| summarize costUsd = sum(valueSum) by bin(timestamp, 1d), feature
| render columnchart
```

**Finish reasons (watch for content filtering and truncation)**

```kusto
dependencies
| where tostring(customDimensions["gen_ai.operation.name"]) == "chat"
| extend finishReasons = tostring(customDimensions["gen_ai.response.finish_reasons"])
| summarize calls = count() by bin(timestamp, 1h), finishReasons
| render timechart
```

**Rate limiting by deployment**

```kusto
dependencies
| where resultCode == "429"
| summarize throttled = count() by bin(timestamp, 5m), target
| render timechart
```

**Tool call outcomes**

```kusto
customMetrics
| where name == "northwind.assistant.tool_calls"
| extend tool = tostring(customDimensions["tool"]), outcome = tostring(customDimensions["outcome"])
| summarize calls = sum(valueSum) by tool, outcome
```

**Retrieval miss rate**

```kusto
let asks = requests | where name has "/api/assistant/ask" | summarize asks = count() by bin(timestamp, 1h);
let misses = customMetrics | where name == "northwind.assistant.retrieval_misses" | summarize misses = sum(valueSum) by bin(timestamp, 1h);
asks
| join kind=leftouter misses on timestamp
| extend missRate = coalesce(misses, 0.0) / asks
| project timestamp, missRate
| render timechart
```

**Escalations by reason**

```kusto
customMetrics
| where name == "northwind.assistant.escalations"
| extend reason = tostring(customDimensions["reason"])
| summarize escalations = sum(valueSum) by bin(timestamp, 1h), reason
| render columnchart
```

**The slowest requests, to open in the transaction view**

```kusto
requests
| where timestamp > ago(1h)
| top 20 by duration desc
| project timestamp, name, duration, resultCode, operation_Id
```

Attribute names follow the OpenTelemetry semantic conventions for generative AI, which are still evolving. If a query returns nothing, check the attribute names on a recent span in the transaction view and adjust.
