# Chapter 3: Prompt Engineering for Developers

Companion code for Chapter 3. The projects move from a single conversation to versioned prompt files and automated prompt tests.

| Section | Project | What it shows |
|---|---|---|
| 3.1 | `3.1-MessageRoles/Ch03.MessageRoles` | A chat loop in which the application maintains the conversation history. Type `/forget` to clear it and see that the model remembers nothing on its own. Also demonstrates `ChatOptions.Instructions`. |
| 3.3 | `3.3-FewShot/Ch03.FewShotClassification` | Classifies a test set of support messages with and without examples and compares accuracy. Contains the `TicketClassifier` reused in Chapter 4. |
| 3.5 | `3.5-ContextManagement/Ch03.ContextManagement` | Trims a long conversation to a token budget with `TokenBudgetedHistory`, summarizes older turns with `SummarizingChatReducer`, then installs the reducer as middleware with `UseChatReducer`. |
| 3.6 | `3.6-PromptTemplates/Ch03.PromptTemplates` | Loads versioned prompt files with front matter from `Prompts/`, renders the latest or a pinned version, and records which version produced each answer. |
| 3.7 | `3.7-PromptTesting/Ch03.PromptTests` | xUnit tests: deterministic template tests that run on every build, and a golden-set accuracy test that runs against a live model only when you opt in. |

## Running

From this folder:

```bash
dotnet run --project 3.1-MessageRoles/Ch03.MessageRoles
dotnet run --project 3.3-FewShot/Ch03.FewShotClassification
dotnet run --project 3.5-ContextManagement/Ch03.ContextManagement
dotnet run --project 3.6-PromptTemplates/Ch03.PromptTemplates
```

To pin the grounded-answer prompt to version 1 instead of the latest version:

```bash
dotnet user-secrets set "Prompts:grounded-answer" "1" --id northwind-ai-engineer-samples
```

## Running the tests

```bash
dotnet test --project 3.7-PromptTesting/Ch03.PromptTests
```

By default, the model-dependent golden-set test is skipped, so the suite is fast, free and safe to run in any build. To run it against your configured model:

```bash
# PowerShell
$env:RUN_MODEL_TESTS = "true"; dotnet test --project 3.7-PromptTesting/Ch03.PromptTests

# bash
RUN_MODEL_TESTS=true dotnet test --project 3.7-PromptTesting/Ch03.PromptTests
```

The golden-set test requires 90% accuracy over 30 labeled messages. Small local models may fall short of that threshold, which is itself a useful finding: it tells you the task needs a stronger model, better examples or both.
