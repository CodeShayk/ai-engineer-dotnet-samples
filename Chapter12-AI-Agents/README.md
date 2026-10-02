# Chapter 12: AI Agents and Multi-Agent Systems

Companion code for Chapter 12. The projects cover the orchestration patterns, Northwind's handoff support team, and the limits that keep agents bounded.

| Section | Project | What it shows |
|---|---|---|
| 12.2, 12.3, 12.6 | `12.3-Orchestrations/Ch12.Orchestrations` | Sequential (drafter, compliance reviewer, style editor), concurrent (legal, quality and customer care assessments) and group chat (writer and reviewer) orchestrations, plus the agent-as-tool pattern. |
| 12.4, 12.6 | `12.4-Handoffs/Ch12.SupportHandoff` | A triage agent handing off to order, returns and refund specialists, with explicitly declared handoff routes and supervisor approval for refunds. With `--as-agent`, the whole team answers as a single `AIAgent`. |
| 12.7 | `12.7-Guardrails/Ch12.BoundedAgent` | Function invocation limits applied with `UseProvidedChatClientAsIs`, a failing tool stopped by the consecutive error limit, a per-session token budget in agent run middleware, and a timeout on every run. |

## Running

From this folder:

```bash
dotnet run --project 12.3-Orchestrations/Ch12.Orchestrations -- sequential
dotnet run --project 12.3-Orchestrations/Ch12.Orchestrations -- concurrent
dotnet run --project 12.3-Orchestrations/Ch12.Orchestrations -- groupchat
dotnet run --project 12.3-Orchestrations/Ch12.Orchestrations -- astool

dotnet run --project 12.4-Handoffs/Ch12.SupportHandoff
dotnet run --project 12.4-Handoffs/Ch12.SupportHandoff -- --as-agent

dotnet run --project 12.7-Guardrails/Ch12.BoundedAgent
```

### Talking to the support team

You are signed in as Thomas Hardy (C003). A conversation that exercises every agent:

1. `Hi, the jacket from my order NW-10248 leaks at the seams.` The front desk hands you to the returns specialist.
2. `Yes please, I'd like a refund.` The returns specialist hands off to the refund agent, which asks a supervisor (you) to approve the request.
3. Answer `y` or `n` at the approval prompt.
4. `Where is my order NW-10249?` The conversation goes back through the front desk to the order specialist.

Press Enter on an empty line to finish. The program then lists the refund requests that were created.

## What to look for

- **Agent names in the stream.** Each block of streamed text is labeled with the agent that produced it, so you can see each handoff happen. The customer sees one continuous conversation.
- **Routes are enforced.** The front desk cannot hand off directly to the refund agent; refunds are only reachable via the returns specialist. The handoff tools each agent receives (named `handoff_to_1`, `handoff_to_2` and so on, described by the target agent's description) reflect exactly the routes declared in code.
- **Which output event to keep.** Streamed updates are a kind of workflow output event, and after an approval the workflow emits other outputs before the final conversation. The loops keep only the output that carries the `List<ChatMessage>`. Section 12.6 explains why.
- **The budget in action.** `Ch12.BoundedAgent` uses a deliberately small budget of 4,000 tokens, so the session is stopped after a few questions with a graceful message rather than an error.

## Local models

The orchestrations work well with `llama3.2`. The handoff team is more demanding: a small model sometimes describes a handoff in text ("I'll pass you to the refund team") instead of calling the handoff tool, so the conversation stays with the current agent. The routing machinery is working; the model is not using it. For the full experience, including supervisor approvals, use a larger model or a cloud provider.
