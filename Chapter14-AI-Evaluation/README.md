# Chapter 14: AI Evaluation and Testing

Companion code for Chapter 14. The projects evaluate the policy assistant from Chapter 9 with model-graded and deterministic evaluators, then run a golden dataset as xUnit tests with cached responses and stored results.

| Section | Project | What it shows |
|---|---|---|
| 14.3 | `14.3-QualityEvaluators/Ch14.QualityEvaluators` | Relevance, coherence, groundedness and completeness evaluators scoring the policy assistant's answers, with scores, ratings, reasons and diagnostics. |
| 14.5 | `14.5-CustomEvaluators/Ch14.CustomEvaluators` | `CitationValidityEvaluator`, a deterministic evaluator, and `PolicyCommitmentEvaluator`, a model-graded evaluator with a Northwind rubric that flags unsupported promises. |
| 14.4, 14.6 | `14.6-EvaluationTests/Ch14.EvaluationTests` | The golden dataset (`Data/golden-dataset.json`) as an xUnit theory: deterministic checks first, then evaluators through the reporting library, plus dataset validation and a retrieval hit-rate test. |

## Running

From this folder:

```bash
dotnet run --project 14.3-QualityEvaluators/Ch14.QualityEvaluators
dotnet run --project 14.5-CustomEvaluators/Ch14.CustomEvaluators                # add -- --offline for the deterministic part only
dotnet test --project 14.6-EvaluationTests/Ch14.EvaluationTests
```

Without further configuration, `dotnet test` runs the dataset checks and skips every test that needs a model, so it is safe in any build.

## Running the evaluations

Set `RUN_MODEL_TESTS` to run the full suite against your configured models:

```powershell
# PowerShell
$env:RUN_MODEL_TESTS = "true"; dotnet test --project 14.6-EvaluationTests/Ch14.EvaluationTests
```

```bash
# bash
RUN_MODEL_TESTS=true dotnet test --project 14.6-EvaluationTests/Ch14.EvaluationTests
```

| Variable | Default | Purpose |
|---|---|---|
| `RUN_MODEL_TESTS` | not set | `true` runs the evaluations and the retrieval tests. |
| `EVAL_RESULTS_PATH` | `sample-code/eval-results` | Where results and cached responses are stored. Persist it between CI runs. |
| `EVAL_EXECUTION_NAME` | `local-<timestamp>` | The name each run's results are stored under, typically the build number. |

Responses from both the system under test and the judge are cached, so a second run with no changes completes in seconds and produces identical scores.

## The report

The `dotnet aieval` tool is declared in `sample-code/dotnet-tools.json`. From the `sample-code` folder:

```bash
dotnet tool restore
dotnet aieval report --path eval-results --output eval-report.html
```

Open `eval-report.html` to see every scenario, metric, score and reason, with trends across executions. The tool collects anonymous usage data by default; set `DOTNET_AIEVAL_TELEMETRY_OPTOUT=1` to opt out.

## Choosing a judge

The judge is `AI:JudgeChatDeployment`, which defaults to the chat model. **A small local model makes a poor judge**: with `llama3.2` as both the system under test and the judge, expect many failures, some with a reason of little more than "Explanation", because the judge does not follow the evaluators' output format reliably. That run still exercises every part of the pipeline. For meaningful scores, use a capable cloud model as the judge, ideally a different one from the model being evaluated:

```bash
dotnet user-secrets set "AI:JudgeChatDeployment" "<a capable model deployment>" --id northwind-ai-engineer-samples
```

## The golden dataset

`Data/golden-dataset.json` holds twelve cases across returns, refunds, shipping, warranties (including a UK-specific one), pricing, loyalty, out-of-scope and safety. Required facts may list alternative phrasings separated by `|`, such as `"two years|2 years|two-year"`, because exact-substring checks are otherwise brittle. Add a case whenever you fix a reported failure, so it cannot quietly return.
