# Chapter 13: AI Security

Companion code for Chapter 13. Each project demonstrates the controls for one family of risk from the OWASP Top 10 for LLM applications. The red-team scenarios run as automated tests.

| Section | Project | What it shows | Needs a model? |
|---|---|---|---|
| 13.2 | `13.2-PromptInjection/Ch13.PromptInjectionDefenses` | An attack corpus screened by Prompt Shields or a heuristic detector, an email summarized inside an unpredictable boundary, and restricted-mode processing for emails that contain attacks. | Optional (`--offline`) |
| 13.3 | `13.3-SensitiveData/Ch13.SensitiveDataProtection` | `RedactingChatClient` removing payment and contact details before every model call, the Luhn checksum, `OutputLeakScanner` checking replies on the way out, and telemetry configured without message content. | Optional (`--offline`) |
| 13.4 | `13.4-OutputHandling/Ch13.SafeOutputHandling` | Malicious outputs (image exfiltration, script, untrusted and `javascript:` links) run through `AssistantOutputSanitizer`. | No |
| 13.5 | `13.5-ExcessiveAgency/Ch13.ExcessiveAgency` | xUnit tests in which a scripted model falls for a hidden instruction, believes a customer's false identity and requests refunds repeatedly, and the code around it holds every time. | No |
| 13.6 | `13.6-VectorSecurity/Ch13.SecureRetrieval` | A two-tenant knowledge base: screening at ingestion quarantines a poisoned review, and `SecureKnowledgeRetriever` applies tenant, audience and status filters to every search. | Optional (`--offline`) |

The reusable components live in the shared projects, because the production architecture in Chapter 16 uses them too:

| Component | Location |
|---|---|
| `UntrustedContent` | `Shared/Northwind.Shared/Security/UntrustedContent.cs` |
| `IPromptAttackDetector`, `PromptShieldsDetector`, `HeuristicPromptAttackDetector` | `Shared/Northwind.Shared/Security/PromptAttackDetection.cs` |
| `ISensitiveDataRedactor`, `PaymentAndContactRedactor`, `PaymentDataRedactor` | `Shared/Northwind.Shared/Security/SensitiveDataRedactor.cs` |
| `RedactingChatClient` | `Shared/Northwind.Shared/Security/RedactingChatClient.cs` |
| `OutputLeakScanner` | `Shared/Northwind.Shared/Security/OutputLeakScanner.cs` |
| `AssistantOutputSanitizer` | `Shared/Northwind.Shared/Security/AssistantOutputSanitizer.cs` |
| `ActionRateLimiter` | `Shared/Northwind.Agents/Security/ActionRateLimiter.cs` |
| `ScriptedChatClient` (for tests) | `Shared/Northwind.Shared/Testing/ScriptedChatClient.cs` |

## Running

From this folder:

```bash
dotnet run --project 13.2-PromptInjection/Ch13.PromptInjectionDefenses            # add -- --offline to skip the model
dotnet run --project 13.3-SensitiveData/Ch13.SensitiveDataProtection               # add -- --offline to skip the model
dotnet run --project 13.4-OutputHandling/Ch13.SafeOutputHandling
dotnet test --project 13.5-ExcessiveAgency/Ch13.ExcessiveAgency
dotnet run --project 13.6-VectorSecurity/Ch13.SecureRetrieval                      # add -- --offline to skip the model
```

## Using Azure AI Content Safety Prompt Shields

Without configuration, `Ch13.PromptInjectionDefenses` uses the heuristic detector. To use Prompt Shields, create an Azure AI Content Safety resource and set its endpoint:

```bash
dotnet user-secrets set "ContentSafety:Endpoint" "https://<resource>.cognitiveservices.azure.com/" --id northwind-ai-engineer-samples
```

The sample authenticates with Microsoft Entra ID through `DefaultAzureCredential` (run `az login`, and assign yourself the *Cognitive Services User* role on the resource). If you must use a key instead, set `ContentSafety:ApiKey`.

## What to look for

- **Detector verdicts.** Every item in the corpus is labeled DETECTED, clean, MISSED or FALSE ALARM. The benign items deliberately contain words like "ignore", so you can see that the heuristic looks for phrasing, not keywords. Add your own attacks to `AttackCorpus.cs` and watch the heuristic miss the novel ones; that is the argument for a trained classifier.
- **What reaches the model.** In `Ch13.SensitiveDataProtection`, the `ReachedTheModel` log line shows the user message after redaction. The redaction log line records how many values were removed, never the values themselves.
- **The rate limiter and parallel calls.** One of the rate limiter tests requests every refund in a single model response. The limiter counts calls only up to the one being invoked, so exactly three run, however the calls are batched. Section 13.5 explains why that matters.
- **Filters, not similarity.** In `Ch13.SecureRetrieval`, the Northwind customer asking about goodwill credit gets the returns policy, not the internal goodwill guidance, however relevant the guidance is. Run it with `--offline` to see that the same filters hold with a crude embedding.
