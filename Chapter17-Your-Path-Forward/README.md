# Chapter 17: Your Path Forward

Companion code for Chapter 17: a small, self-contained starting point for the portfolio projects described in Section 17.3, and the briefs for those projects.

| Section | Project | What it contains |
|---|---|---|
| 17.3 | `17.3-PortfolioStarter/Ch17.PortfolioStarter` | An ASP.NET Core API with a configuration-driven `IChatClient` pipeline (logging and OpenTelemetry), health checks that never call a model, a question-answering endpoint and a structured extraction endpoint with validation, source checks and retries. |
| 17.3 | `17.3-PortfolioStarter/Ch17.PortfolioStarter.EvaluationTests` | Unit tests with a fake model that run on every build, and one golden case, with a model-graded relevance evaluation, that runs when `RUN_MODEL_TESTS=true`. |

The starter does **not** reference the Northwind shared projects. Copy the `17.3-PortfolioStarter` folder into a new repository, take `Directory.Build.props` and `Directory.Packages.props` with it (or replace the central package versions with explicit ones), and rename to taste.

## Running

From this folder:

```bash
dotnet run --project 17.3-PortfolioStarter/Ch17.PortfolioStarter        # http://localhost:5117
dotnet test --project 17.3-PortfolioStarter/Ch17.PortfolioStarter.EvaluationTests
```

The starter reads the same `AI` settings as the rest of the companion code (`AI:Provider`, `AI:Endpoint`, `AI:ApiKey`, `AI:ChatDeployment`) and defaults to Ollama with `llama3.2`. `Ch17.PortfolioStarter.http` has example requests. The golden relevance case needs a capable judge model; small local models often fail to follow the evaluator's output format.

## Five portfolio projects

Each project is small enough to finish in a few weeks of evenings and complete enough to be used by someone other than you. Build them on the starter, and write about each one: what you built, the decisions you made, what the evaluation showed and what you would do differently.

### 1. Grounded question answering over real documents

**Build:** Q&A over a document collection you know and others would find useful, such as a team handbook, an open-source project's documentation or a public set of regulations.

**Demonstrate:** structure-aware chunking, retrieval with metadata filters, a grounded prompt and verified citations (Chapters 8 and 9).

**Deliverable that matters most:** an evaluation report on a golden dataset of about thirty questions with reference answers, covering retrieval hit rate, groundedness and relevance (Chapter 14). A RAG demo without measurement proves nothing.

### 2. A structured extraction pipeline

**Build:** extraction of typed records from unstructured input in a real business process, such as invoices, support emails, job descriptions or incident reports.

**Demonstrate:** schemas with descriptions, validation in code and against the source text, retries with feedback, reporting what is missing, and batch processing with bounded concurrency (Chapter 5). The starter's `/api/extract` endpoint is the seed.

**Deliverable that matters most:** field-by-field accuracy against hand-labeled examples.

### 3. A tool-using agent with approvals

**Build:** an agent that does something real against an API you can access, such as a calendar, an issue tracker or a home automation system.

**Demonstrate:** narrow, well-described tools; identity bound from context; idempotent write actions; approvals for consequential actions; iteration and token limits; and a trace of every step (Chapters 6, 10 and 12).

**Deliverable that matters most:** a short write-up of the attacks you tried against it and how the design stopped them (Chapter 13).

### 4. An MCP server for a system you know

**Build:** an MCP server for a system you understand deeply, from your day job or an open-source tool you use.

**Demonstrate:** well-designed tools with appropriate annotations, resources and prompts where they help, and authentication for the HTTP transport (Chapter 11).

**Deliverable that matters most:** a published .NET tool package, if the license allows, so anyone with an MCP-capable editor can use it.

### 5. Production hardening

**Build:** take an existing AI sample, one of your earlier projects or an open-source application, and make it production-grade.

**Demonstrate:** OpenTelemetry with a dashboard covering health, speed, cost and quality (Chapter 15); an evaluation suite that gates pull requests (Chapter 14); resilience with a tested fallback (Chapter 16); redaction and output sanitization (Chapter 13); and a system card (Chapter 16).

**Deliverable that matters most:** before-and-after numbers for latency percentiles, cost per request and evaluation scores. These are the skills employers find hardest to hire for.

## Making the portfolio count

- **Show the numbers.** Evaluation scores, latency percentiles, cost per request and retrieval hit rates separate engineering from demonstration.
- **Be honest about limitations.** Document the failure modes you found. That is a sign of maturity, not weakness.
- **Bring it into your day job.** The strongest piece is an AI feature you shipped at work, with its evaluation results and production metrics. Start small, internal and low-risk, and run it with the discipline of this book.
