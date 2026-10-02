# Chapter 5: Structured Outputs and Data Extraction

Companion code for Chapter 5. The projects turn model output into typed C# objects, then make that output trustworthy with validation, source checks and retries.

| Section | Project | What it shows |
|---|---|---|
| 5.2, 5.3 | `5.3-TypedResponses/Ch05.TypedResponses` | Prints the JSON schema generated from a C# record, triages a support message with `GetResponseAsync<TicketTriage>`, handles a truncated response with `TryGetResult`, and requests a `string[]`. |
| 5.4 | `5.4-ValidationAndRetries/Ch05.ValidatedExtraction` | Data annotation and `IValidatableObject` rules, a check that extracted order numbers appear in the source text, and the validate-and-retry loop in `StructuredOutputService`. |
| 5.5 | `5.5-Patterns/Ch05.StructuredPatterns` | One example each of classification (evidence before the label), extraction (with `MissingInformation`) and transformation (supplier text to catalog listing), then a batch of emails processed with `Parallel.ForEachAsync`. |

`StructuredOutputService`, `StructuredResult<T>` and `ModelValidation` live in `Shared/Northwind.Shared/AI/StructuredOutputService.cs`, because Chapters 9 and 14 use them too.

## Running

From this folder:

```bash
dotnet run --project 5.3-TypedResponses/Ch05.TypedResponses
dotnet run --project 5.4-ValidationAndRetries/Ch05.ValidatedExtraction
dotnet run --project 5.5-Patterns/Ch05.StructuredPatterns
```

## What to look for

- **The retry loop at work.** In `Ch05.ValidatedExtraction`, the second message gives an order number without its `NW-` prefix. If the model normalizes it to `NW-10249`, the source check rejects it, and you will see a warning followed by a corrected second attempt. Whether to accept normalized values is a product decision. The check makes it a deliberate one.
- **Honest gaps.** In the extraction example, the email without an order number should produce `null` for `OrderNumber` and an entry in `MissingInformation`, not an invented number.
- **Local models.** Smaller local models follow field descriptions less reliably than large cloud models. Expect more retries, and occasionally a failure, when running these samples with Ollama. The failure path is part of what the samples demonstrate.
