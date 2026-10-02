# Chapter 6: Tool Calling and Function Invocation

Companion code for Chapter 6. The projects give Northwind Assist the ability to look up orders, check return eligibility and request refunds, and show the safeguards that make those tools safe to hand to a model.

| Section | Project | What it shows |
|---|---|---|
| 6.2 | `6.2-FunctionTools/Ch06.FunctionTools` | `AIFunctionFactory` turning methods into tools (the schema the model sees is printed), automatic invocation with `UseFunctionInvocation`, and a trace of every call and result. |
| 6.3 | `6.3-ServiceTools/Ch06.ServiceTools` | An ASP.NET Core API whose tools depend on scoped services and take the customer's identity from the signed-in user, never from the model. Includes a chat page with a simulated sign-in. |
| 6.4 | `6.4-ManualInvocation/Ch06.ManualInvocation` | The tool-calling loop written by hand with an audit log, then an approval-required refund tool that pauses for a support agent's decision. |
| 6.5 | `6.5-SafeTools/Ch06.SafeTools` | The refund tool's safeguards exercised directly (authorization, validation, policy, idempotency, computed amounts), then a model being asked to refund another customer's order. |

The order and refund tools live in `Shared/Northwind.Shared/Tools`, because later chapters reuse them. The business rules they call live in `Shared/Northwind.Shared/Domain/ReturnPolicyRules.cs`.

## Running

From this folder:

```bash
dotnet run --project 6.2-FunctionTools/Ch06.FunctionTools
dotnet run --project 6.3-ServiceTools/Ch06.ServiceTools          # then open http://localhost:5106
dotnet run --project 6.4-ManualInvocation/Ch06.ManualInvocation
dotnet run --project 6.5-SafeTools/Ch06.SafeTools
```

### Offline mode

Two projects run without a model when you pass `--offline`:

```bash
dotnet run --project 6.4-ManualInvocation/Ch06.ManualInvocation -- --offline
dotnet run --project 6.5-SafeTools/Ch06.SafeTools -- --offline
```

In `Ch06.ManualInvocation`, a scripted stand-in plays the model's part and requests the tool calls a model would make, so you can watch the manual loop, the audit log and the approval pause and resume with no model involved. Answer `y` or `n` at the approval prompt to see both paths. In `Ch06.SafeTools`, offline mode runs only the direct calls, which need no model.

### The service tools sample

`Ch06.ServiceTools` uses a **demo authentication scheme** that signs the caller in as whichever customer is named in the `X-Demo-Customer` header. The chat page sets the header from its "Signed in as" list. This lets you run the sample without an identity provider, and it is only safe on your own machine, so the application refuses to start outside the Development environment. Chapter 16 shows the production equivalent with JWT bearer authentication.

`Ch06.ServiceTools.http` contains ready-made requests for the Visual Studio and VS Code REST clients, including an attempt to read another customer's order and a request with no sign-in.

## Tool calling and local models

Tool calling needs a model trained for it. Current cloud models are reliable. Among local models, `llama3.2` supports tools but is noticeably less reliable at multi-step sequences than larger models. If a local model answers without calling the tools, or calls them with malformed arguments, try a larger tool-capable model such as `qwen3` or `llama3.1:8b` (see Chapter 7).
