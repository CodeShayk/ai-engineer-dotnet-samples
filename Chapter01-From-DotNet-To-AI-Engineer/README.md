# Chapter 1: From .NET Developer to AI Engineer

Companion code for Chapter 1. There is one project, and its job is to prove that your machine and your model provider are configured correctly before you start Chapter 2.

| Section | Project | What it shows |
|---|---|---|
| 1.6 | `1.6-SetupCheck/Ch01.SetupCheck` | Sends one question to the configured provider (Ollama, Azure OpenAI or OpenAI) and prints the answer and token usage. |

## Running

From this folder:

```bash
dotnet run --project 1.6-SetupCheck/Ch01.SetupCheck
```

With no configuration, the sample uses a local Ollama server at `http://localhost:11434` with the `llama3.2` model. To use Azure OpenAI instead, set the values described in Section 1.6. Note the `/openai/v1/` suffix on the endpoint:

```bash
dotnet user-secrets set "AI:Provider" "AzureOpenAI" --id northwind-ai-engineer-samples
dotnet user-secrets set "AI:Endpoint" "https://<your-resource>.openai.azure.com/openai/v1/" --id northwind-ai-engineer-samples
dotnet user-secrets set "AI:ChatDeployment" "gpt-5-mini" --id northwind-ai-engineer-samples
```

This project is deliberately self-contained. It creates its client inline, exactly as the listing in Section 1.6 does, so it has no dependency on the shared project and supports only Ollama and Azure OpenAI. From Chapter 2 onward, the samples use the shared `AIClientFactory` from `Shared/Northwind.Shared`, which also supports OpenAI and is explained in Chapter 4. See the root [README](../README.md#configuration) for every setting.

## If it fails

| Symptom | Likely cause |
|---|---|
| `Connection refused` on port 11434 | Ollama is not running. Start it, then run `ollama pull llama3.2`. |
| `model "llama3.2" not found` | Run `ollama pull llama3.2`. |
| `401` or `403` from Azure OpenAI | Run `az login`, and check that your account has the *Cognitive Services OpenAI User* role on the resource. |
| `404` from Azure OpenAI | `AI:ChatDeployment` must be your deployment name, not the model name, if the two differ. |
