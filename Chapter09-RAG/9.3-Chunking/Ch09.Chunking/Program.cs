// Chapter 9, Section 9.3: Chunking strategies.
// Runs the structure-aware Markdown chunker over Northwind's policy library. No model is needed.
// 1. Shows the chunks produced for the returns policy.
// 2. Summarizes chunk counts and sizes for every document.
// 3. Compares two token budgets, the first step in choosing a chunk size.
//
// The chunker lives in Shared/Northwind.Shared/Knowledge/MarkdownSectionChunker.cs because the
// RAG pipeline in later chapters reuses it.

using Microsoft.ML.Tokenizers;
using Northwind.Shared.AI;
using Northwind.Shared.Knowledge;

SampleConsole.Header("Chapter 9.3: Chunking the policy library");

Tokenizer tokenizer = TiktokenTokenizer.CreateForEncoding("o200k_base");
IReadOnlyList<PolicyDocument> documents = PolicyLibrary.LoadAll();

// --- The chunks for one document --------------------------------------------------------------
var chunker = new MarkdownSectionChunker(tokenizer, maxTokens: 350, overlapParagraphs: 1);
PolicyDocument returnsPolicy = documents.Single(d => d.Id == "returns-policy");

SampleConsole.Section("Returns policy, 350-token budget");

foreach (DocumentChunk chunk in chunker.Chunk(returnsPolicy.Id, returnsPolicy.Title, returnsPolicy.Content))
{
    // The first line of each chunk is its header; show it and the start of the body.
    string[] lines = chunk.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    string firstBodyLine = lines.Length > 1 ? lines[1] : "";

    Console.WriteLine($"[{chunk.ChunkId}] {lines[0]}  ({tokenizer.CountTokens(chunk.Text)} tokens)");
    Console.WriteLine($"{(firstBodyLine.Length > 100 ? firstBodyLine[..97] + "..." : firstBodyLine)}");
    Console.WriteLine();
}

// --- Every document --------------------------------------------------------------------------
SampleConsole.Section("The whole library");

Console.WriteLine($"{"Document",-26} {"Status",-9} {"Region",-7} {"Audience",-9} {"Chunks",6} {"Min",5} {"Max",5}");

foreach (PolicyDocument document in documents)
{
    List<int> sizes = chunker.Chunk(document.Id, document.Title, document.Content)
        .Select(c => tokenizer.CountTokens(c.Text))
        .ToList();

    Console.WriteLine($"{document.Id,-26} {document.Status,-9} {document.Region,-7} {document.Audience,-9} " +
                      $"{sizes.Count,6} {sizes.Min(),5} {sizes.Max(),5}");
}

SampleConsole.Note("Archived and internal documents are chunked like any other. The retrieval filter, not ingestion, keeps them away from customers.");

// --- Choosing chunk size ---------------------------------------------------------------------
SampleConsole.Section("Comparing token budgets");

foreach (int budget in new[] { 350, 60 })
{
    var candidate = new MarkdownSectionChunker(tokenizer, maxTokens: budget, overlapParagraphs: 1);
    List<DocumentChunk> chunks = documents
        .SelectMany(d => candidate.Chunk(d.Id, d.Title, d.Content))
        .ToList();

    List<int> sizes = chunks.Select(c => tokenizer.CountTokens(c.Text)).ToList();
    Console.WriteLine($"Budget {budget,3}: {chunks.Count,3} chunks, average {sizes.Average():F0} tokens, largest {sizes.Max()}");
}

SampleConsole.Note("Northwind's policy sections are short, so a 350-token budget keeps every section whole. " +
                   "At 60 tokens, longer sections split at paragraph boundaries. A single paragraph larger than " +
                   "the budget stays whole, because the chunker never splits inside a paragraph.");
SampleConsole.Note("Smaller chunks are more precise but carry less context. Measure retrieval quality on " +
                   "your own questions to choose (Chapter 14 shows how).");
