# Chapter 2: LLM Fundamentals Every Developer Must Know

Companion code for Chapter 2. Each project isolates one concept from the chapter so you can see it with your own model and your own prompts.

| Section | Project | What it shows | Needs a model? |
|---|---|---|---|
| 2.2 | `2.2-Tokenization/Ch02.Tokenization` | Token counts for prose, code, JSON and non-English text with the `o200k_base` tokenizer, then each token printed individually. | No |
| 2.4 | `2.4-Embeddings/Ch02.EmbeddingSimilarity` | Embeds three sentences and compares every pair with cosine similarity. | Embedding model |
| 2.5 | `2.5-Inference/Ch02.InferenceTiming` | Measures time to first token and output speed while streaming, then sets a tiny output limit to produce the `Length` finish reason. | Chat model |
| 2.6 | `2.6-Sampling/Ch02.SamplingExplorer` | Sends the same prompt three times at three temperatures and prints the results side by side. | Chat model |
| 2.7 | `2.7-ModelSelection/Ch02.CostEstimator` | Reads a workload and candidate model prices from `costsettings.json` and prints a monthly cost comparison. | No |

## Running

From this folder:

```bash
dotnet run --project 2.2-Tokenization/Ch02.Tokenization
dotnet run --project 2.4-Embeddings/Ch02.EmbeddingSimilarity
dotnet run --project 2.5-Inference/Ch02.InferenceTiming
dotnet run --project 2.6-Sampling/Ch02.SamplingExplorer
dotnet run --project 2.7-ModelSelection/Ch02.CostEstimator
```

The tokenizer and cost estimator run entirely offline. The others use the provider configured as described in the root [README](../README.md#configuration). With Ollama, pull the default models first:

```bash
ollama pull llama3.2
ollama pull nomic-embed-text
```

## Notes

- **Prices are illustrative.** Edit `costsettings.json` with your provider's current price list and your own traffic estimates. The estimator is a template for doing the arithmetic early, not a quotation.
- **Token counts differ between model families.** The tokenizer sample uses the encoding of current OpenAI models. Llama, Mistral and other families use different vocabularies and will produce different counts for the same text.
- **Timings vary.** Time to first token and tokens per second depend on the model, the hardware (for local models) and the load on the service (for cloud models). Run the inference sample a few times before drawing conclusions.
