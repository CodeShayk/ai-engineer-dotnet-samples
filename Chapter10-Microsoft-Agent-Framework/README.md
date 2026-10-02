# Chapter 10: Microsoft Agent Framework

Companion code for Chapter 10. The projects build Northwind Assist as an agent, step by step, then model two business processes as workflows.

| Section | Project | What it shows |
|---|---|---|
| 10.3 | `10.3-FirstAgent/Ch10.FirstAgent` | `ChatClientAgent` and `AsAIAgent`, `RunAsync` and `RunStreamingAsync`. |
| 10.4 | `10.4-Sessions/Ch10.AgentSessions` | A chat loop whose `AgentSession` is serialized to disk after every turn and restored at startup, so the conversation survives a restart. |
| 10.5 | `10.5-ToolsAndMiddleware/Ch10.ToolsAndMiddleware` | The order tools and an approval-required refund tool; an approval that pauses one run and resumes in the next with the same session; agent run middleware that redacts card numbers, function calling middleware that audits tool calls, and chat client middleware that shows what each model call receives. |
| 10.6 | `10.6-ContextProviders/Ch10.ContextProviders` | Context providers for the customer profile, policy retrieval and remembered preferences. A second, separate conversation shows what the memory provider retained. |
| 10.8 | `10.8-Workflows/Ch10.Workflows` | A function executor; the support triage workflow with conditional edges to an order agent, the RAG policy assistant or escalation; and a refund workflow with a request port for supervisor approval and checkpoints. |

The customer profile and policy knowledge context providers live in `Shared/Northwind.Agents/ContextProviders`, because Chapters 12 and 16 reuse them. The preference memory provider is specific to this chapter and lives in the `Ch10.ContextProviders` project.

## Running

From this folder:

```bash
dotnet run --project 10.3-FirstAgent/Ch10.FirstAgent
dotnet run --project 10.4-Sessions/Ch10.AgentSessions                  # optional: a conversation id
dotnet run --project 10.5-ToolsAndMiddleware/Ch10.ToolsAndMiddleware
dotnet run --project 10.6-ContextProviders/Ch10.ContextProviders
dotnet run --project 10.8-Workflows/Ch10.Workflows                     # optional: triage or refund
```

### Sessions across restarts

Run `Ch10.AgentSessions`, introduce yourself, and press Enter on an empty line to exit. Run it again and ask what you said. The session is stored as JSON in the `sessions` folder next to the executable. Open the file to see what an `AgentSession` contains: for a `ChatClientAgent`, the chat history lives in the session's state. Type `/reset` to start over, or pass a different conversation id as an argument.

### Approvals

`Ch10.ToolsAndMiddleware` and `Ch10.Workflows` pause for a decision at the console. Answer `y` to approve or anything else to decline, and compare the outcomes. In both cases the refund request is created in code, with the amount taken from the order, and nothing is refunded without a decision.

## What to look for

- **Middleware ordering in the logs.** In `Ch10.ToolsAndMiddleware`, each `ModelCall` line comes from chat client middleware and appears once per model call. `Audit` lines come from function calling middleware and appear once per tool call, including calls that fail. When you type a card number, the `ModelCall` line shows `[CARD NUMBER]`, because the agent run middleware removed it before the agent saw the message.
- **Declared protocols.** The workflow executors declare the messages they send and the outputs they yield with `[SendsMessage]` and `[YieldsOutput]`. Remove one of those attributes and run the workflow: the runtime stops with an error naming the undeclared type.
- **Checkpoints.** The refund workflow reports how many checkpoints it saved. They are held in memory here; a durable `CheckpointManager` would let a workflow waiting days for approval survive a restart.

## Local models

Everything in this chapter runs with Ollama's default `llama3.2`, but small models are inconsistent at multi-step tool use. Sometimes the model asks for more information instead of calling the refund tool, or calls it with a missing product ID, which the tool then rejects. The safeguards are working as designed, not failing. For more consistent tool use, configure a larger model or a cloud provider.
