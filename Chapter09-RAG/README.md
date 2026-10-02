# Chapter 9: Retrieval-Augmented Generation (RAG)

Companion code for Chapter 9. The first two projects isolate one stage of the pipeline each. The third puts every stage together as an interactive policy assistant.

| Section | Project | What it shows |
|---|---|---|
| 9.3 | `9.3-Chunking/Ch09.Chunking` | The structure-aware Markdown chunker run over the policy library: the chunks for one document, sizes for every document and a comparison of two token budgets. Needs no model. |
| 9.8 | `9.8-Reranking/Ch09.Reranking` | Filtered vector search for twelve candidates, then a language model grading each one. Prints both orderings side by side. |
| 9.9 | `9.9-PolicyAssistant/Ch09.PolicyAssistant` | The complete pipeline in a console chat: ingestion at startup, then query rewriting, filtered retrieval, reranking, a grounded prompt, structured generation and citation validation for every question. |

The pipeline components live in `Shared/Northwind.Shared/Knowledge`, because the agents in Chapters 10 and 12, the evaluations in Chapter 14 and the capstone in Chapter 16 reuse them:

| Component | File |
|---|---|
| `PolicyLibrary`, `PolicyDocument` | `PolicyLibrary.cs` (the Markdown policies are embedded resources in `Policies/`) |
| `MarkdownSectionChunker`, `DocumentChunk` | `MarkdownSectionChunker.cs` |
| `PolicyChunkRecord`, `PolicyIngestor` | `PolicyChunkRecord.cs`, `PolicyIngestor.cs` |
| `PolicyRetriever`, `QueryRewriter`, `RetrievedChunk`, `CustomerContext` | `Retrieval.cs` |
| `LlmReranker` | `LlmReranker.cs` |
| `GroundedPrompt`, `GroundedAnswer`, `Citation`, `CitationValidator` | `GroundedAnswers.cs` |
| `PolicyAssistant`, `AssistantAnswer` | `PolicyAssistant.cs` |

## Running

From this folder:

```bash
dotnet run --project 9.3-Chunking/Ch09.Chunking
dotnet run --project 9.8-Reranking/Ch09.Reranking
dotnet run --project 9.9-PolicyAssistant/Ch09.PolicyAssistant
```

In the policy assistant:

| Command | Effect |
|---|---|
| `/region uk`, `/region us`, `/region global` | Changes the customer's region, which changes which regional policies can be retrieved (try asking about warranties). |
| `/debug` | Shows the rewritten search query, the candidates and the reranked sources for each question. |
| `/new` | Starts a new conversation. |
| Enter on an empty line | Exits. |

## Things to try

- **The filter at work.** Ask "How long do I have to return something?" The archived 2024 policy offered 60 days; the current one offers 30. Only the current policy can be retrieved.
- **Internal content stays internal.** Ask "What's the maximum goodwill credit an agent can give?" The internal goodwill guidelines are indexed, but the audience filter keeps them out of customer answers, so the assistant should say it is not sure.
- **Regional policies.** Ask "How long is the warranty?" with `/region uk` and then `/region global`.
- **A follow-up question.** Ask about opened earbuds, then "What about the speaker I bought last month?" and use `/debug` to see the rewritten query.

## Local models

The pipeline makes three kinds of model call: embedding, rewriting and reranking with the small model, and composing the answer with the main model. With Ollama's defaults, all of them run locally. The 3-billion-parameter `llama3.2` is serviceable but noticeably weaker as a reranker and at following the grounding rules than larger models. It sometimes drops relevant passages, which leads to more "I'm not sure" answers. That is the pipeline failing safely, as designed. For better results, configure a stronger chat model, or a cloud provider, with `AI:ChatDeployment` and `AI:SmallChatDeployment`.
