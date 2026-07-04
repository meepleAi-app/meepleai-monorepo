# ME-M1.3 Mechanic Extractor Guardrails (#525) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add validation guardrails (T1–T4 + cost cap + retry loop + observability) to the Mechanic Extractor pipeline so IP-violating, hallucinated, or runaway-cost LLM outputs are blocked before reaching the admin review queue, with auditable rejection reasons.

**Architecture:** Refactor the context-free `IMechanicOutputValidator.Validate(section, rawJson)` stub into an async **chain of injectable `IMechanicGuardrail`** evaluated cheapest-first / fail-fast. Each guardrail receives a `MechanicGuardrailContext` carrying the parsed JSON, the source chunk pool (text + page metadata), the PDF page count, and the options. The pipeline builds the context, runs the chain, and drives a configurable re-prompt retry loop. Cost-cap projection becomes retry-inclusive. New Prometheus counters + structured logging.

**Tech Stack:** .NET 9, xUnit v3, FluentAssertions 8.8.0, Moq 4.20.72, System.Diagnostics.Metrics (Prometheus via OTel), `IEmbeddingService.EmbedAsync` (e5-base).

---

## Design Decisions (locked)

1. **USD not EUR.** The body says `MaxAnalysisCostEur` / €2.00, but the entire codebase uses USD (`CostCapUsd`, `EstimatedCostUsd`, `InputCostPerMillionTokens`). Use `MaxAnalysisCostUsd` (default `2.00m`) for consistency. Document the deviation in the PR.

2. **Guardrail naming reconciliation.** The M1.2 stub labels citation-presence as `T4_citation_required`. The ADR-051/#525 spec uses: T1=quote length, T2=long-verbatim, T3=citation present+grounded, T4=page+substring. We adopt the **spec naming**: rename the existing presence check rule to `T3_citation_required` and reserve `T4_*` for page/substring. (No existing tests reference the old rule string — verified: no Mechanic validator tests exist.)

3. **Source pool = retrieved chunks.** `LoadRetrievalContextAsync` already loads `TextChunks` (with `PageNumber`) then concatenates to a string. We add a parallel structured list `IReadOnlyList<MechanicSourceChunk>` passed through the pipeline so T2 (n-gram), T3 (grounding), T4 (substring) can operate per-chunk. No schema change, no `chunk_version_id` column — the in-memory retrieval snapshot IS the pinned pool for the run.

4. **Async chain, fail-fast, cheapest-first order:** T1 (word count) → T4 (substring/page, string ops) → T2 (rolling hash) → T3 (embedding, network). Within a section, on first guardrail producing violations the chain stops (fail-fast) and the retry loop kicks in.

5. **Fail-closed on embedding outage (T3).** If `IEmbeddingService.EmbedAsync` throws, the section fails validation (IP > latency in M1), surfaced as `Rule="T3_grounding_unavailable"`.

6. **Cost in USD; grounding similarity 0.65; consecutive source words 10; max quote words 25; max retries 2** — all in `MechanicGuardrailOptions` with these defaults.

---

## File Structure

**Create:**
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Configuration/MechanicGuardrailOptions.cs` — options record (single responsibility: tunables).
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/Guardrails/IMechanicGuardrail.cs` — guardrail interface + `MechanicGuardrailContext` + `MechanicSourceChunk`.
- `.../Guardrails/QuoteCapGuardrail.cs` (T1)
- `.../Guardrails/PageSubstringGuardrail.cs` (T4)
- `.../Guardrails/RejectionSamplingGuardrail.cs` (T2)
- `.../Guardrails/GroundingGuardrail.cs` (T3)
- `.../Guardrails/CitationPresenceGuardrail.cs` (T3a — extracted from stub)
- `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.MechanicGuardrails.cs` — counters.
- Tests under `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/MechanicExtractor/Guardrails/` (one file per guardrail) + fixtures `apps/api/tests/Api.Tests/Fixtures/MechanicValidator/T1/`.

**Modify:**
- `.../MechanicExtractor/IMechanicOutputValidator.cs` — new async signature `ValidateAsync(MechanicGuardrailContext, CancellationToken)`.
- `.../MechanicExtractor/MechanicOutputValidator.cs` — becomes the chain orchestrator.
- `.../MechanicExtractor/MechanicAnalysisExecutor.cs` — build structured chunk list; pass through.
- `.../MechanicExtractor/IMechanicAnalysisPipeline.cs` — add `SourceChunksBySection` + `PdfPageCount` to request.
- `.../MechanicExtractor/MechanicAnalysisPipeline.cs` — build context, run async chain, configurable retry loop + augmented re-prompt + stable-output detection; retry-inclusive cost cap.
- `.../Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs` — register options + guardrails.
- `apps/api/src/Api/appsettings.json` — `MechanicGuardrails` section.

---

## Task 0: MechanicGuardrailOptions + DI registration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Configuration/MechanicGuardrailOptions.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs`
- Modify: `apps/api/src/Api/appsettings.json`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/MechanicExtractor/MechanicGuardrailOptionsTests.cs`

- [ ] **Step 1: Write the failing test** — defaults are correct.

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.MechanicExtractor;

public sealed class MechanicGuardrailOptionsTests
{
    [Fact]
    public void Defaults_MatchAdr051()
    {
        var o = new MechanicGuardrailOptions();
        o.MaxQuoteWords.Should().Be(25);
        o.MaxConsecutiveSourceWords.Should().Be(10);
        o.MinClaimGroundingSimilarity.Should().Be(0.65);
        o.MaxAnalysisCostUsd.Should().Be(2.00m);
        o.MaxRetriesPerSection.Should().Be(2);
        MechanicGuardrailOptions.SectionName.Should().Be("MechanicGuardrails");
    }
}
```

- [ ] **Step 2: Run test, verify FAIL** — `dotnet test --filter MechanicGuardrailOptionsTests` → FAIL (type missing).

- [ ] **Step 3: Implement** (mirror `BackgroundAnalysisOptions` style — sealed, init props, const SectionName):

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Application.Configuration;

/// <summary>ME-M1.3 (#525) tunable guardrail thresholds (ADR-051).</summary>
public sealed class MechanicGuardrailOptions
{
    public const string SectionName = "MechanicGuardrails";

    /// <summary>T1: max words per citation quote.</summary>
    public int MaxQuoteWords { get; init; } = 25;

    /// <summary>T2: max contiguous normalized words from source allowed outside citation quotes.</summary>
    public int MaxConsecutiveSourceWords { get; init; } = 10;

    /// <summary>T3: minimum cosine similarity between a claim and its cited chunk.</summary>
    public double MinClaimGroundingSimilarity { get; init; } = 0.65;

    /// <summary>T8: hard cost cap (USD) for one analysis run, retry-inclusive.</summary>
    public decimal MaxAnalysisCostUsd { get; init; } = 2.00m;

    /// <summary>Max re-prompt retries per section (total attempts = value + 1).</summary>
    public int MaxRetriesPerSection { get; init; } = 2;

    /// <summary>Typical retry inflation factor for cost projection (1.3 = +30%).</summary>
    public decimal RetryCostInflationFactor { get; init; } = 1.3m;
}
```

- [ ] **Step 4: Register DI** — in `SharedGameCatalogServiceExtensions.cs`, next to the existing `services.Configure<BackgroundAnalysisOptions>(...)` line:

```csharp
services.Configure<MechanicGuardrailOptions>(
    configuration.GetSection(MechanicGuardrailOptions.SectionName));
```

- [ ] **Step 5: appsettings.json** — add section (place near other SharedGameCatalog config):

```json
"MechanicGuardrails": {
  "MaxQuoteWords": 25,
  "MaxConsecutiveSourceWords": 10,
  "MinClaimGroundingSimilarity": 0.65,
  "MaxAnalysisCostUsd": 2.00,
  "MaxRetriesPerSection": 2,
  "RetryCostInflationFactor": 1.3
}
```

- [ ] **Step 6: Run test, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 MechanicGuardrailOptions + DI"`

---

## Task 1: MechanicSourceChunk + MechanicGuardrailContext + IMechanicGuardrail

**Files:**
- Create: `.../MechanicExtractor/Guardrails/IMechanicGuardrail.cs`
- Test: none yet (pure type definitions; exercised by guardrail tasks).

- [ ] **Step 1: Define the contracts** (no test — these are data carriers verified transitively):

```csharp
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>A retrieved source chunk pinned for the analysis run (T2/T3/T4 source pool).</summary>
public sealed record MechanicSourceChunk(int ChunkIndex, int? PageNumber, Guid? ChunkId, string Content);

/// <summary>Everything a guardrail needs to evaluate one section output.</summary>
public sealed record MechanicGuardrailContext(
    MechanicSection Section,
    JsonElement Root,
    IReadOnlyList<MechanicSourceChunk> SourceChunks,
    int? PdfPageCount,
    MechanicGuardrailOptions Options);

/// <summary>One ADR-051 guardrail. Returns empty list when the output passes.</summary>
public interface IMechanicGuardrail
{
    /// <summary>Stable rule family prefix, e.g. "T1". Used for fail-fast ordering + metrics.</summary>
    string RuleFamily { get; }

    /// <summary>Lower runs first (cheapest-first). T1=10, T4=20, T2=30, T3=40.</summary>
    int Order { get; }

    Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Build** — `dotnet build apps/api/src/Api` → success. Commit: `git commit -m "feat(mechanic): #525 guardrail contracts (context + source chunk + interface)"`

---

## Task 2: T1 QuoteCapGuardrail (hardened Unicode tokenizer)

**Files:**
- Create: `.../Guardrails/QuoteCapGuardrail.cs`
- Test: `apps/api/tests/.../Guardrails/QuoteCapGuardrailTests.cs`
- Fixtures: `apps/api/tests/Api.Tests/Fixtures/MechanicValidator/T1/*.json` (≥10, incl. Unicode)

- [ ] **Step 1: Write failing tests** — boundary 24/25 pass, 26 fail; Unicode whitespace (` `, ` `) and em-dash counted as separators; pure-punctuation tokens excluded.

```csharp
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.MechanicExtractor.Guardrails;

public sealed class QuoteCapGuardrailTests
{
    private static MechanicGuardrailContext Ctx(string json) => new(
        MechanicSection.Mechanics,
        JsonDocument.Parse(json).RootElement,
        Array.Empty<MechanicSourceChunk>(),
        PdfPageCount: 50,
        new MechanicGuardrailOptions());

    private static string QuoteOfWords(int n) =>
        "{\"citations\":[{\"quote\":\"" + string.Join(' ', Enumerable.Range(1, n).Select(i => "w" + i)) + "\"}]}";

    [Theory]
    [InlineData(24, true)]
    [InlineData(25, true)]
    [InlineData(26, false)]
    public async Task WordCountBoundary(int words, bool ok)
    {
        var result = await new QuoteCapGuardrail().EvaluateAsync(Ctx(QuoteOfWords(words)), default);
        result.Any().Should().Be(!ok);
        if (!ok) result[0].Rule.Should().Be("T1_quote_cap");
    }

    [Fact]
    public async Task UnicodeWhitespaceAndEmDash_CountAsSeparators()
    {
        // 26 words separated by NBSP, thin-space, em-dash → must fail
        var quote = "a b c—d e f g h i j k l m n o p q r s t u v w x y z";
        var json = "{\"citations\":[{\"quote\":\"" + quote + "\"}]}";
        var result = await new QuoteCapGuardrail().EvaluateAsync(Ctx(json), default);
        result.Should().ContainSingle().Which.Rule.Should().Be("T1_quote_cap");
    }

    [Fact]
    public async Task PurePunctuationTokens_AreExcluded()
    {
        // 25 real words + standalone "-" and "—" punctuation tokens → still 25 → pass
        var quote = string.Join(' ', Enumerable.Range(1, 25).Select(i => "w" + i)) + " - —";
        var json = "{\"citations\":[{\"quote\":\"" + quote + "\"}]}";
        var result = await new QuoteCapGuardrail().EvaluateAsync(Ctx(json), default);
        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run, verify FAIL** (type missing).

- [ ] **Step 3: Implement** — walk JSON for `quote` strings, tokenize with a hardened counter:

```csharp
using System.Globalization;
using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

internal sealed class QuoteCapGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T1";
    public int Order => 10;

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.TryGetProperty("quote", out var q) && q.ValueKind == JsonValueKind.String)
            {
                var text = q.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var count = CountWords(text!);
                    if (count > context.Options.MaxQuoteWords)
                    {
                        violations.Add(new MechanicValidationViolation(
                            "T1_quote_cap",
                            $"quote has {count} words, max is {context.Options.MaxQuoteWords}",
                            $"{path}.quote"));
                    }
                }
            }
        });
        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    /// <summary>Counts word tokens: any maximal run of non-whitespace chars containing ≥1 letter or digit.
    /// Unicode whitespace (incl. NBSP  , thin space  ) and em-dash split words; pure-punctuation
    /// runs (e.g. "-", "—") are NOT counted.</summary>
    internal static int CountWords(string text)
    {
        var count = 0;
        var i = 0;
        var n = text.Length;
        while (i < n)
        {
            while (i < n && IsSeparator(text[i])) i++;
            if (i >= n) break;
            var hasAlnum = false;
            while (i < n && !IsSeparator(text[i]))
            {
                if (char.IsLetterOrDigit(text[i])) hasAlnum = true;
                i++;
            }
            if (hasAlnum) count++;
        }
        return count;
    }

    private static bool IsSeparator(char c) =>
        char.IsWhiteSpace(c) || c == '—' || c == '–'; // em-dash, en-dash
}
```

> NOTE: `MechanicJsonWalker` is a small static helper extracted from the existing recursive walk in `MechanicOutputValidator.WalkAndValidate`. Define it in `Guardrails/MechanicJsonWalker.cs` in this task (Step 3b): a static `ForEachObject(JsonElement, string path, Action<JsonElement,string>)` doing the same Object/Array recursion as the current stub (lines 59–80).

```csharp
// Guardrails/MechanicJsonWalker.cs
using System.Text.Json;
namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
internal static class MechanicJsonWalker
{
    public static void ForEachObject(JsonElement el, string path, Action<JsonElement, string> visit)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                visit(el, path);
                foreach (var p in el.EnumerateObject())
                    ForEachObject(p.Value, $"{path}.{p.Name}", visit);
                break;
            case JsonValueKind.Array:
                var idx = 0;
                foreach (var item in el.EnumerateArray())
                    ForEachObject(item, $"{path}[{idx++}]", visit);
                break;
        }
    }
}
```

- [ ] **Step 4: Create ≥10 T1 fixtures** in `Fixtures/MechanicValidator/T1/` — `pass_24_words.json`, `pass_25_words.json`, `fail_26_words.json`, `pass_nbsp.json`, `fail_nbsp_26.json`, `pass_emdash.json`, `pass_punctuation_only_extra.json`, `pass_empty_citations.json`, `fail_multiple_quotes.json`, `pass_unicode_naive.json`. Add a `[Theory]` loading each via `File.ReadAllText` with expected violation count (use `Directory.GetCurrentDirectory()` + relative path; mirror how other Api.Tests load fixtures — check an existing fixture-loading test first).

- [ ] **Step 5: Run, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 T1 quote-cap guardrail + Unicode tokenizer + fixtures"`

---

## Task 3: T4 PageSubstringGuardrail

**Files:**
- Create: `.../Guardrails/PageSubstringGuardrail.cs`
- Test: `.../Guardrails/PageSubstringGuardrailTests.cs`

- [ ] **Step 1: Write failing tests** — page in range + quote is normalized substring of some chunk → pass; page out of range → `T4_page_out_of_range`; quote not substring → `T4_quote_not_substring`.

```csharp
[Fact]
public async Task QuoteIsNormalizedSubstringOfChunk_Passes()
{
    var chunks = new[] { new MechanicSourceChunk(0, 3, null, "Players take turns drawing cards from the deck.") };
    var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":3,\"quote\":\"drawing cards from the deck\"}]}";
    var r = await Eval(json, chunks, pageCount: 50);
    r.Should().BeEmpty();
}

[Fact]
public async Task PageOutOfRange_Fails()
{
    var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":51,\"quote\":\"anything\"}]}";
    var r = await Eval(json, Array.Empty<MechanicSourceChunk>(), pageCount: 50);
    r.Should().ContainSingle().Which.Rule.Should().Be("T4_page_out_of_range");
}

[Fact]
public async Task QuoteNotSubstring_Fails()
{
    var chunks = new[] { new MechanicSourceChunk(0, 3, null, "The board has nineteen spaces.") };
    var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":3,\"quote\":\"dragons breathe fire\"}]}";
    var r = await Eval(json, chunks, pageCount: 50);
    r.Should().ContainSingle().Which.Rule.Should().Be("T4_quote_not_substring");
}
```

(Helper `Eval(json, chunks, pageCount)` builds the context and calls `new PageSubstringGuardrail().EvaluateAsync`.)

- [ ] **Step 2: Run, verify FAIL.**

- [ ] **Step 3: Implement** — normalize (lowercase, strip punctuation, collapse whitespace) both quote and candidate chunks (those whose `PageNumber == pdf_page`, or all chunks if no page metadata), then `Contains`:

```csharp
using System.Text;
using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

internal sealed class PageSubstringGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T4";
    public int Order => 20;

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        var normalizedChunksByPage = context.SourceChunks
            .GroupBy(c => c.PageNumber)
            .ToDictionary(g => g.Key, g => g.Select(c => Normalize(c.Content)).ToList());
        var allNormalized = context.SourceChunks.Select(c => Normalize(c.Content)).ToList();

        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.ValueKind != JsonValueKind.Object) return;
            if (!obj.TryGetProperty("pdf_page", out var pageEl) || pageEl.ValueKind != JsonValueKind.Number) return;
            if (!pageEl.TryGetInt32(out var page)) return;

            if (page < 1 || (context.PdfPageCount is int pc && page > pc))
            {
                violations.Add(new MechanicValidationViolation(
                    "T4_page_out_of_range",
                    $"expected 1..{context.PdfPageCount?.ToString() ?? "?"}, got {page}",
                    $"{path}.pdf_page"));
                return;
            }

            if (!obj.TryGetProperty("quote", out var qEl) || qEl.ValueKind != JsonValueKind.String) return;
            var quote = Normalize(qEl.GetString() ?? string.Empty);
            if (quote.Length == 0) return;

            var candidates = normalizedChunksByPage.TryGetValue(page, out var byPage) && byPage.Count > 0
                ? byPage
                : allNormalized;
            var found = candidates.Any(c => c.Contains(quote, StringComparison.Ordinal));
            if (!found)
            {
                var expected = candidates.Count > 0 ? candidates[0] : "(no source)";
                violations.Add(new MechanicValidationViolation(
                    "T4_quote_not_substring",
                    $"quote not found on page {page}. expected≈\"{Truncate(expected, 80)}\" actual=\"{Truncate(quote, 80)}\"",
                    $"{path}.quote"));
            }
        });
        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    internal static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        var lastSpace = false;
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(char.ToLowerInvariant(ch)); lastSpace = false; }
            else if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                if (!lastSpace && sb.Length > 0) { sb.Append(' '); lastSpace = true; }
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
```

- [ ] **Step 4: Run, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 T4 page+substring guardrail"`

---

## Task 4: T2 RejectionSamplingGuardrail (Rabin-Karp)

**Files:**
- Create: `.../Guardrails/RejectionSamplingGuardrail.cs`
- Test: `.../Guardrails/RejectionSamplingGuardrailTests.cs`

- [ ] **Step 1: Write failing tests** — a ≥10-word contiguous run from a non-citation field that exact-matches source → `T2_long_verbatim`; citation `quote` fields are excluded; 9-word overlap passes; diacritics preserved (`naïve` ≠ `naive`).

```csharp
[Fact]
public async Task TenWordVerbatimFromClaim_Fails()
{
    var src = "players take turns drawing cards from the top of the deck each round";
    var chunks = new[] { new MechanicSourceChunk(0, 1, null, src) };
    var json = "{\"claim\":\"players take turns drawing cards from the top of the deck\"}"; // 11 words verbatim
    var r = await Eval(json, chunks);
    r.Should().ContainSingle().Which.Rule.Should().Be("T2_long_verbatim");
}

[Fact]
public async Task NineWordOverlap_Passes()
{
    var chunks = new[] { new MechanicSourceChunk(0, 1, null, "players take turns drawing cards from the top deck") };
    var json = "{\"claim\":\"players take turns drawing cards from the top\"}"; // 8 words
    var r = await Eval(json, chunks);
    r.Should().BeEmpty();
}

[Fact]
public async Task CitationQuote_IsExcluded()
{
    var src = "players take turns drawing cards from the top of the deck each round";
    var chunks = new[] { new MechanicSourceChunk(0, 1, null, src) };
    var json = "{\"citations\":[{\"quote\":\"players take turns drawing cards from the top of the deck\"}]}";
    var r = await Eval(json, chunks);
    r.Should().BeEmpty();
}
```

- [ ] **Step 2: Run, verify FAIL.**

- [ ] **Step 3: Implement** — normalize source pool to a token list; build a set of all N-gram rolling hashes (N = `MaxConsecutiveSourceWords`); for each non-`quote` string field, tokenize and slide an N-window, flag on hash hit confirmed by token equality (guard against hash collision):

```csharp
using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

internal sealed class RejectionSamplingGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T2";
    public int Order => 30;

    private static readonly string[] ScannedFields = { "claim", "description", "text", "answer", "primary" };

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var n = context.Options.MaxConsecutiveSourceWords;
        var violations = new List<MechanicValidationViolation>();

        // Build source n-gram index (page-tagged for the message).
        var sourceTokensByChunk = context.SourceChunks
            .Select(c => (c.ChunkIndex, Tokens: Tokenize(c.Content)))
            .ToList();
        var sourceNgrams = new Dictionary<long, List<(int chunk, int offset, string[] gram)>>();
        foreach (var (chunkIdx, tokens) in sourceTokensByChunk)
            IndexNgrams(tokens, n, chunkIdx, sourceNgrams);

        if (sourceNgrams.Count == 0)
            return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);

        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            foreach (var field in ScannedFields)
            {
                if (!obj.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.String) continue;
                var candidate = Tokenize(el.GetString() ?? string.Empty);
                if (candidate.Length < n) continue;

                for (var i = 0; i + n <= candidate.Length; i++)
                {
                    var hash = HashWindow(candidate, i, n);
                    if (!sourceNgrams.TryGetValue(hash, out var bucket)) continue;
                    var window = candidate.AsSpan(i, n);
                    var match = bucket.FirstOrDefault(b => window.SequenceEqual(b.gram));
                    if (match.gram != null)
                    {
                        var seq = string.Join(' ', window.ToArray());
                        violations.Add(new MechanicValidationViolation(
                            "T2_long_verbatim",
                            $"{n}-word sequence matches chunk #{match.chunk} at offset {match.offset}: '{seq}'",
                            $"{path}.{field}"));
                        return; // one per object is enough (fail-fast intra-object)
                    }
                }
            }
        });
        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    private static void IndexNgrams(string[] tokens, int n, int chunk,
        Dictionary<long, List<(int, int, string[])>> index)
    {
        for (var i = 0; i + n <= tokens.Length; i++)
        {
            var hash = HashWindow(tokens, i, n);
            var gram = tokens.AsSpan(i, n).ToArray();
            if (!index.TryGetValue(hash, out var list)) { list = new(); index[hash] = list; }
            list.Add((chunk, i, gram));
        }
    }

    // FNV-1a over the joined normalized tokens of the window (deterministic, no Math.random/Date).
    private static long HashWindow(string[] tokens, int start, int n)
    {
        unchecked
        {
            const long prime = 1099511628211L;
            var hash = 1469598103934665603L;
            for (var k = 0; k < n; k++)
            {
                foreach (var ch in tokens[start + k]) { hash ^= ch; hash *= prime; }
                hash ^= ' '; hash *= prime;
            }
            return hash;
        }
    }

    // Normalize: lowercase, strip punctuation, collapse whitespace, NO stemming, NO diacritic folding.
    internal static string[] Tokenize(string s)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) current.Append(char.ToLowerInvariant(ch));
            else { if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); } }
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result.ToArray();
    }
}
```

- [ ] **Step 4: Run, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 T2 rejection-sampling guardrail (Rabin-Karp/FNV)"`

---

## Task 5: T3a CitationPresenceGuardrail + T3b GroundingGuardrail

**Files:**
- Create: `.../Guardrails/CitationPresenceGuardrail.cs` (T3a, sync, extracted from stub)
- Create: `.../Guardrails/GroundingGuardrail.cs` (T3b, embedding)
- Test: `.../Guardrails/CitationPresenceGuardrailTests.cs`, `.../Guardrails/GroundingGuardrailTests.cs`

- [ ] **Step 1 (T3a): Write failing test** — claim-bearing object without non-empty `citations` → `T3_citation_required` (renamed from stub's `T4_citation_required`).

```csharp
[Fact]
public async Task ClaimWithoutCitations_Fails()
{
    var json = "{\"claim\":\"x\"}";
    var r = await new CitationPresenceGuardrail().EvaluateAsync(Ctx(json), default);
    r.Should().ContainSingle().Which.Rule.Should().Be("T3_citation_required");
}
```

- [ ] **Step 2 (T3a): Implement** — lift the stub's claim-field/`HasNonEmptyCitations` logic into a guardrail (`RuleFamily="T3"`, `Order=15`, sync wrapped in `Task.FromResult`). Rule string `T3_citation_required`.

- [ ] **Step 3 (T3b): Write failing test with mocked `IEmbeddingService`** — claim cosine below threshold → `T3_grounding`; fail-closed on outage → `T3_grounding_unavailable`.

```csharp
[Fact]
public async Task LowCosine_Fails()
{
    var embed = new Mock<IEmbeddingService>(); // Domain.Services.IEmbeddingService
    embed.Setup(e => e.EmbedAsync("cards have suits", It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[] { 1f, 0f });
    embed.Setup(e => e.EmbedAsync(It.Is<string>(s => s != "cards have suits"), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[] { 0f, 1f }); // orthogonal → cosine 0 < 0.65
    var chunks = new[] { new MechanicSourceChunk(7, 12, null, "the board is hexagonal") };
    var json = "{\"claim\":\"cards have suits\",\"citations\":[{\"pdf_page\":12,\"quote\":\"q\",\"chunk_id\":null}]}";
    var r = await new GroundingGuardrail(embed.Object).EvaluateAsync(Ctx(json, chunks), default);
    r.Should().ContainSingle().Which.Rule.Should().Be("T3_grounding");
}

[Fact]
public async Task EmbeddingOutage_FailsClosed()
{
    var embed = new Mock<IEmbeddingService>();
    embed.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(new InvalidOperationException("down"));
    var chunks = new[] { new MechanicSourceChunk(7, 12, null, "x") };
    var json = "{\"claim\":\"c\",\"citations\":[{\"pdf_page\":12,\"quote\":\"q\"}]}";
    var r = await new GroundingGuardrail(embed.Object).EvaluateAsync(Ctx(json, chunks), default);
    r.Should().ContainSingle().Which.Rule.Should().Be("T3_grounding_unavailable");
}
```

- [ ] **Step 4 (T3b): Implement** — for each claim-bearing object with citations, embed claim text + cited chunk (resolve by `chunk_id` then `pdf_page`), cosine; fail-closed on throw:

```csharp
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

internal sealed class GroundingGuardrail : IMechanicGuardrail
{
    private readonly IEmbeddingService _embeddings;
    public GroundingGuardrail(IEmbeddingService embeddings) => _embeddings = embeddings;

    public string RuleFamily => "T3";
    public int Order => 40;

    private static readonly string[] ClaimFields = { "claim", "description", "text", "answer", "primary" };

    public async Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        var targets = new List<(string claim, string path, MechanicSourceChunk chunk)>();

        MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
        {
            if (obj.ValueKind != JsonValueKind.Object) return;
            var claim = ClaimFields
                .Where(f => obj.TryGetProperty(f, out var e) && e.ValueKind == JsonValueKind.String)
                .Select(f => obj.GetProperty(f).GetString())
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            if (claim is null) return;
            if (!obj.TryGetProperty("citations", out var cits) || cits.ValueKind != JsonValueKind.Array) return;
            foreach (var c in cits.EnumerateArray())
            {
                var chunk = ResolveChunk(c, context.SourceChunks);
                if (chunk != null) targets.Add((claim!, path, chunk));
            }
        });

        foreach (var (claim, path, chunk) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var a = await _embeddings.EmbedAsync(claim, cancellationToken);
                var b = await _embeddings.EmbedAsync(chunk.Content, cancellationToken);
                var cos = Cosine(a, b);
                if (cos < context.Options.MinClaimGroundingSimilarity)
                    violations.Add(new MechanicValidationViolation(
                        "T3_grounding",
                        $"claim '{Trunc(claim)}' has cosine {cos:0.00} with cited chunk #{chunk.ChunkIndex}" +
                        $" (page {chunk.PageNumber}), below threshold {context.Options.MinClaimGroundingSimilarity}",
                        $"{path}"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                violations.Add(new MechanicValidationViolation(
                    "T3_grounding_unavailable",
                    $"embedding service unavailable, failing closed: {ex.Message}", path));
                break; // outage is global; no point retrying each claim
            }
        }
        return violations;
    }

    private static MechanicSourceChunk? ResolveChunk(JsonElement citation, IReadOnlyList<MechanicSourceChunk> pool)
    {
        if (citation.ValueKind != JsonValueKind.Object) return null;
        if (citation.TryGetProperty("chunk_id", out var idEl) && idEl.ValueKind == JsonValueKind.String
            && Guid.TryParse(idEl.GetString(), out var cid))
        {
            var byId = pool.FirstOrDefault(p => p.ChunkId == cid);
            if (byId != null) return byId;
        }
        if (citation.TryGetProperty("pdf_page", out var pEl) && pEl.ValueKind == JsonValueKind.Number
            && pEl.TryGetInt32(out var page))
            return pool.FirstOrDefault(p => p.PageNumber == page);
        return null;
    }

    internal static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static string Trunc(string s) => s.Length <= 50 ? s : s[..50];
}
```

- [ ] **Step 5: Run all guardrail tests, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 T3 citation-presence + grounding guardrails"`

---

## Task 6: MechanicOutputValidator → async chain orchestrator

**Files:**
- Modify: `.../MechanicExtractor/IMechanicOutputValidator.cs` (new async signature)
- Modify: `.../MechanicExtractor/MechanicOutputValidator.cs` (orchestrator)
- Modify: `.../Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs` (register 5 guardrails)
- Test: `.../MechanicExtractor/MechanicOutputValidatorChainTests.cs`

- [ ] **Step 1: Write failing test** — chain runs in `Order`, fail-fast stops at first family with violations; well-formedness handled before chain.

```csharp
[Fact]
public async Task FailFast_StopsAtFirstFailingFamily()
{
    // T1 fails (26-word quote) AND T4 would fail (bad page) → only T1 reported (Order 10 < 20)
    var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":999,\"quote\":\"" +
               string.Join(' ', Enumerable.Range(1,26).Select(i=>"w"+i)) + "\"}]}";
    var sut = new MechanicOutputValidator(new IMechanicGuardrail[]
        { new QuoteCapGuardrail(), new PageSubstringGuardrail() });
    var result = await sut.ValidateAsync(Ctx(json, pageCount:50), default);
    result.IsValid.Should().BeFalse();
    result.Violations.Should().OnlyContain(v => v.Rule.StartsWith("T1"));
}
```

- [ ] **Step 2: New interface signature** (replace `Validate`):

```csharp
public interface IMechanicOutputValidator
{
    Task<MechanicValidationResult> ValidateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken);
}
```

(Keep `MechanicValidationResult` / `MechanicValidationViolation` records unchanged.)

- [ ] **Step 3: Orchestrator implementation** — ordered, fail-fast:

```csharp
internal sealed class MechanicOutputValidator : IMechanicOutputValidator
{
    private readonly IReadOnlyList<IMechanicGuardrail> _guardrails;
    public MechanicOutputValidator(IEnumerable<IMechanicGuardrail> guardrails)
        => _guardrails = guardrails.OrderBy(g => g.Order).ToList();

    public async Task<MechanicValidationResult> ValidateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        foreach (var guardrail in _guardrails)
        {
            var violations = await guardrail.EvaluateAsync(context, cancellationToken);
            if (violations.Count > 0)
                return MechanicValidationResult.Invalid(violations); // fail-fast
        }
        return MechanicValidationResult.Valid();
    }
}
```

> Well-formedness: the pipeline parses raw JSON into `JsonElement` BEFORE building the context (Task 7). A JSON parse failure short-circuits to a `well_formed` violation there — guardrails always receive valid JSON.

- [ ] **Step 4: DI registration** — replace the single `services.AddScoped<IMechanicOutputValidator, MechanicOutputValidator>();` with guardrail registrations + validator:

```csharp
services.AddScoped<IMechanicGuardrail, QuoteCapGuardrail>();
services.AddScoped<IMechanicGuardrail, CitationPresenceGuardrail>();
services.AddScoped<IMechanicGuardrail, PageSubstringGuardrail>();
services.AddScoped<IMechanicGuardrail, RejectionSamplingGuardrail>();
services.AddScoped<IMechanicGuardrail, GroundingGuardrail>();
services.AddScoped<IMechanicOutputValidator, MechanicOutputValidator>();
```

- [ ] **Step 5: Run, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 async guardrail chain orchestrator + DI"`

---

## Task 7: Pipeline wiring — context build + configurable retry re-prompt loop (AC-6)

**Files:**
- Modify: `.../MechanicExtractor/IMechanicAnalysisPipeline.cs` (request: + `SourceChunksBySection`, `PdfPageCount`)
- Modify: `.../MechanicExtractor/MechanicAnalysisExecutor.cs` (load structured chunks + page count)
- Modify: `.../MechanicExtractor/MechanicAnalysisPipeline.cs` (build context, async validate, retry loop)
- Test: `.../MechanicExtractor/MechanicAnalysisPipelineRetryTests.cs`

- [ ] **Step 1: Extend request record** — add `IReadOnlyDictionary<MechanicSection, IReadOnlyList<MechanicSourceChunk>> SourceChunksBySection` and `int? PdfPageCount`.

- [ ] **Step 2: Executor builds structured chunks** — in `MechanicAnalysisExecutor.LoadRetrievalContextAsync`, additionally project the loaded chunks into `MechanicSourceChunk` (already selecting `ChunkIndex`, `PageNumber`, `Content`; add `Id` for `ChunkId`). Return both the concatenated string (LLM context) and the list. Load `PdfDocument.PageCount` via the existing DbContext.

- [ ] **Step 3: Write failing retry test** — first attempt invalid (T1), re-prompt augments system message with violations JSON, second attempt valid → succeeds with `RetryCount=1`; identical normalized output twice → break early (`RegenerationDivergent=false`).

```csharp
[Fact]
public async Task InvalidThenValid_RetriesOnceAndSucceeds()
{
    // llm returns 26-word-quote JSON first, clean JSON second
    var llm = new Mock<ILlmService>();
    llm.SetupSequence(s => s.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(LlmCompletionResult.CreateSuccess(BadJson))
       .ReturnsAsync(LlmCompletionResult.CreateSuccess(GoodJson));
    // ... build pipeline with real validator (QuoteCap only) + options MaxRetriesPerSection=2 ...
    var result = await pipeline.RunAsync(request, default);
    result.Outcome.Should().Be(MechanicPipelineOutcome.Succeeded);
    // assert second system prompt contained the T1 violation
    llm.Verify(s => s.GenerateCompletionAsync(
        It.Is<string>(p => p.Contains("T1_quote_cap")), It.IsAny<string>(),
        It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 4: Implement retry loop** in `RunSectionAsync` — replace hardcoded `MaxValidationRetries=1` with `options.MaxRetriesPerSection`; on validation failure, build augmented system prompt appending `"PREVIOUS_ATTEMPT_VIOLATIONS": [...]` JSON; compute SHA256 of normalized output to detect stable/divergent-free regeneration and break early; accumulate retry tokens into the run cost.

```csharp
// pseudocode shape — real edit integrates into existing RunSectionAsync
var attempt = 0;
string? lastHash = null;
while (true)
{
    var completion = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, RequestSource.Manual, ct);
    // ... cost accounting, strip fences ...
    using var parsed = JsonDocument.Parse(cleanJson); // catch JsonException -> well_formed failure
    var ctx = new MechanicGuardrailContext(section, parsed.RootElement, sourceChunks, pageCount, _options);
    var validation = await _validator.ValidateAsync(ctx, ct);
    if (validation.IsValid) return Success(...);

    var hash = Sha256Normalized(cleanJson);
    if (hash == lastHash) return Failed(reason: "RegenerationDivergent=false", validation.Violations);
    lastHash = hash;

    if (attempt >= _options.MaxRetriesPerSection)
        return Failed(reason: AggregateViolations(validation.Violations), validation.Violations);
    attempt++;
    systemPrompt = AugmentWithViolations(baseSystemPrompt, validation.Violations); // JSON, not NL
}
```

- [ ] **Step 5: Run, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 AC-6 configurable re-prompt retry loop + stable-output detection"`

---

## Task 8: Retry-inclusive cost cap + mid-stream overrun (AC-5)

**Files:**
- Modify: `.../MechanicExtractor/AnalysisCostEstimator.cs` (retry-inclusive projection)
- Modify: `.../MechanicExtractor/MechanicAnalysisPipeline.cs` (mid-stream overrun → PartiallyExtracted)
- Test: `.../MechanicExtractor/CostCapRetryInclusiveTests.cs`

- [ ] **Step 1: Write failing test** — projected cost uses `baseEstimate × RetryCostInflationFactor`; if `base × (1 + maxRetries)` exceeds cap, the inflation projection is used; mid-stream overrun stops next section and persists `PartiallyExtracted`.

- [ ] **Step 2: Implement** — extend estimator to expose a retry-inflated projection; in the pipeline's per-section cost check (existing lines ~95–105), when accumulated cost crosses `MaxAnalysisCostUsd` mid-run, complete the current section, raise `MechanicAnalysisCostCapOverriddenEvent` with `Reason=MidStreamOverrun` (extend event or add reason field), stop the loop, set outcome so the executor persists `Status=PartiallyExtracted`.

- [ ] **Step 3: Run, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 AC-5 retry-inclusive cost cap + mid-stream overrun policy"`

---

## Task 9: Observability — metrics + structured logging (AC-7)

**Files:**
- Create: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.MechanicGuardrails.cs`
- Modify: `.../MechanicExtractor/MechanicOutputValidator.cs` (emit per-guardrail metrics + logs)
- Test: `.../MechanicExtractor/MechanicGuardrailMetricsTests.cs` (assert counters increment via a MeterListener)

- [ ] **Step 1: Write failing test** — running the chain on an invalid output increments `mechanic_validator_invocations_total{validator,outcome}` and `mechanic_validator_violations_total{rule}` (use `System.Diagnostics.Metrics.MeterListener` to capture).

- [ ] **Step 2: Implement metrics partial** (mirror `MeepleAiMetrics.MechanicValidation.cs`):

```csharp
namespace Api.Observability;
internal static partial class MeepleAiMetrics
{
    public static readonly Counter<long> MechanicValidatorInvocations =
        Meter.CreateCounter<long>("mechanic_validator_invocations_total",
            description: "Guardrail evaluations by validator and outcome.");
    public static readonly Counter<long> MechanicValidatorViolations =
        Meter.CreateCounter<long>("mechanic_validator_violations_total",
            description: "Guardrail violations by rule.");
}
```

- [ ] **Step 3: Emit in orchestrator** — wrap each guardrail eval with a stopwatch; `MechanicValidatorInvocations.Add(1, new TagList{{"validator",g.RuleFamily},{"outcome",ok?"pass":"fail"}})`; per violation `MechanicValidatorViolations.Add(1, new TagList{{"rule",v.Rule}})`; `LogInformation` with structured fields `analysis_id`, `section`, `validator`, `outcome`, `retry_count`, `violation_rule?`, `latency_ms`. (Pass `analysis_id`/`retry_count` into the context or an overload; add `AnalysisId` + `RetryCount` to `MechanicGuardrailContext`.)

- [ ] **Step 4: Run, verify PASS.** Commit: `git commit -m "feat(mechanic): #525 AC-7 guardrail metrics + structured logging"`

---

## Task 10: End-to-end pipeline integration test

**Files:**
- Test: `.../MechanicExtractor/MechanicGuardrailPipelineIntegrationTests.cs`

- [ ] **Step 1: Write test** — a fabricated LLM output that violates T2 (long verbatim) against a known source chunk pool → pipeline retries → second clean output → `Succeeded`, section run persisted with `RetryCount=1`, metrics emitted, no IP-violating output surfaced.

- [ ] **Step 2: Run, verify PASS.** Commit: `git commit -m "test(mechanic): #525 end-to-end guardrail pipeline integration"`

---

## Self-Review Checklist (run before execution)

- **Spec coverage:** AC-1→Task 2; AC-2→Task 4; AC-3→Task 5; AC-4→Task 3; AC-5→Task 8; AC-6→Task 7; AC-7→Task 9. ✓
- **Type consistency:** `MechanicGuardrailContext`, `MechanicSourceChunk`, `IMechanicGuardrail.EvaluateAsync`, `ValidateAsync` used identically across Tasks 1–10. ✓
- **No placeholders:** algorithm bodies (T1 tokenizer, T2 FNV n-gram, T3 cosine, T4 normalize) are complete; Task 7/8 integration steps reference exact existing lines to edit.
- **Open risks:** (a) `MechanicAnalysisCostCapOverriddenEvent` may need a `Reason` field for mid-stream (Task 8) — verify event shape at execution; (b) fixture-loading convention must match an existing Api.Tests fixture test — verify in Task 2 Step 4; (c) `MechanicValidationResult.Invalid` currently takes `IReadOnlyList` — confirmed compatible.
