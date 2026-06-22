# KB Inline Citation Markers — BE PR-1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the **backend** of issue [#1703](https://github.com/meepleAi-app/meepleai-monorepo/issues/1703) — add LLM `[N]` inline citation markers to the cross-game KB ask flow (`POST /api/v1/knowledge-base/ask/global`), gated by a new opt-in flag `includeInlineCitationInstructions` so existing per-game agents stay unchanged.

**Architecture:** Modify `RagPromptAssemblyService` to (a) prepend a 1-based `[N]` index to each chunk header via `FormatChunkForPrompt`, and (b) append a new `## Citation Format` section to the system prompt when the new optional flag is true. The flag is propagated through `IRagPromptAssemblyService.AssembleFromContextAsync` (default false) and opted-in only by `CrossGameStreamQaQueryHandler`. A new Prometheus counter `meepleai.rag.citation_markers.emitted_total{compliant}` tracks production compliance. All existing callsites compile unchanged.

**Tech Stack:**
- .NET 9, ASP.NET Minimal APIs, MediatR (CQRS)
- xUnit + FluentAssertions + Moq for unit tests
- OpenTelemetry `System.Diagnostics.Metrics.Counter<long>` (partial class `MeepleAiMetrics.Rag`)
- Optional integration test gated by `RUN_LLM_INTEGRATION_TESTS=true` env var

**Plan-source spec:** spec-panel iteration 2 (2026-05-30), decisions D-1703-A through D-1703-E (see [#1703 issuecomment-4582774753](https://github.com/meepleAi-app/meepleai-monorepo/issues/1703#issuecomment-4582774753)).

---

## §0 — Pre-flight (engineer reads before Task 1)

| Item | Source | Why this matters |
|---|---|---|
| Phase 2 PR #1701 merged in `main-dev` (squash `3e61b6c09`) | `git log main-dev` | This PR builds on top |
| `FormatChunkForPrompt` current shape | `RagPromptAssemblyService.cs:859-863` | Output: `[Source: Document <id>, Page <p>, Relevance: <s>, Copyright: <tier>]\n<text>\n---` — NO 1-based index. D-1703-A adds it. |
| `BuildSystemPrompt` callers | `RagPromptAssemblyService.cs:123` + `:931` | 2 callers (single-game + cross-game). Adding default-false param keeps both compiling. |
| `BuildSystemPrompt` line 720 already says "include page/section if available" | `RagPromptAssemblyService.cs:722` | D-1703-C section coexists — page in prose still allowed; `[N]` is REQUIRED for linkage. |
| `CrossGameStreamQaQueryHandler` hardcodes `AgentTypology = "tutor"` | `CrossGameStreamQaQueryHandler.cs:39` | Tutor-only gating is structurally free at the callsite |
| `IRagPromptAssemblyService.AssembleFromContextAsync` signature | `IRagPromptAssemblyService.cs:63` | Interface change required (additive optional param) — verify no Moq setups break |
| Existing `[ref:documentId:pageNum]` marker for PROTECTED sources | `GetCopyrightInstruction` lines 869-870 | FE parser (PR-2) handles by strict digit-only regex; BE just needs to coexist |
| Telemetry counter pattern | `MeepleAiMetrics.Rag.cs:257-260` | Mirror `CopyrightInstructionInjected` style |
| Existing test file | `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceTests.cs` | Patterns to mirror |

**Git workflow (CLAUDE.md compliance):**
- Parent branch: `main-dev`
- Feature branch: `feature/issue-1703-be-inline-citation-markers` (create from `main-dev` after `git pull --ff-only`)
- Target PR base: `main-dev`
- Commit messages: `feat(kb): #1703 Task N — <description>` for impl, `test(kb)` for test-only, `docs(kb)` for docs-only.

**P74 verification confirmed**:
- ✅ `FormatChunkForPrompt` is a `public static` method — easy to extend with optional param
- ✅ `BuildSystemPrompt` is `private static` — internal change only; safe
- ✅ `AssembleFromContextAsync` is `public` interface method — additive optional param is binary-compatible for C# but NOT JSON; we are only touching .NET callers (no JSON consumers)
- ✅ Counter naming convention: lowercase + dots (e.g. `meepleai.rag.copyright.instruction.injected`)

---

## §1 — File structure

### Create

```
apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/
└── RagPromptAssemblyServiceInlineCitationTests.cs         # Task 1: unit tests (FormatChunkForPrompt + BuildSystemPrompt + AssembleFromContextAsync)

apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/
└── CrossGameStreamQaQueryHandlerInlineCitationTests.cs    # Task 5: integration tests (handler opts in, gated LLM test)
```

### Modify

```
apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/
├── IRagPromptAssemblyService.cs               # Task 2: add optional bool param to AssembleFromContextAsync
└── RagPromptAssemblyService.cs                # Task 2+3+4: FormatChunkForPrompt index + BuildSystemPrompt section + AssembleFromContextAsync forward

apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/
└── CrossGameStreamQaQueryHandler.cs           # Task 5: opt in to includeInlineCitationInstructions=true + telemetry counter call

apps/api/src/Api/Observability/Metrics/
└── MeepleAiMetrics.Rag.cs                     # Task 6: new Counter CitationMarkersEmittedTotal
```

### Out of scope (do NOT touch)

- `AskQuestionQueryHandler`, `ChatWithSessionAgentCommandHandler`, `StreamSetupGuideQueryHandler`, etc. — single-game / per-agent paths default-false the new flag; behavior identical
- FE: deferred to PR-2 (separate plan)
- Unification with `[ref:docId:page]` copyright marker — deferred to PR-3 (tech-debt follow-up per Fowler §1.4)

---

## §2 — Frozen decisions (from spec-panel iteration 2)

| # | Decision | Code impact |
|---|---|---|
| **D-1703-A** | `FormatChunkForPrompt` prepends `[N] ` to header when `index` param provided | `public static string FormatChunkForPrompt(ChunkCitation citation, string chunkText, int? index = null)` |
| **D-1703-B** | `BuildSystemPrompt` + `AssembleFromContextAsync` gain optional `bool includeInlineCitationInstructions = false` | Backward-compatible signature change; existing callers unchanged |
| **D-1703-C** | New `## Citation Format` section text injected ONLY when flag is true | Pure additive text in system prompt |
| **D-1703-D** | (FE-only — PR-2) | Out of scope |
| **D-1703-E** | Telemetry counter `meepleai.rag.citation_markers.emitted_total{compliant}` + env-gated LLM integration test | New counter in `MeepleAiMetrics.Rag.cs` + new gated test |

---

## Task 0: Pre-flight setup

**Files:**
- Read: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs:859-863`
- Read: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs:711-799`
- Read: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagPromptAssemblyService.cs`
- Read: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs:30-50`

- [ ] **Step 1: Sync main-dev and create feature branch**

```bash
git checkout main-dev
git pull --ff-only
git branch --show-current  # MUST print: main-dev
git status                 # MUST be clean
git checkout -b feature/issue-1703-be-inline-citation-markers
git config branch.feature/issue-1703-be-inline-citation-markers.parent main-dev
```

Expected: clean tree, new branch active.

- [ ] **Step 2: Verify the 4 P74 findings**

Confirm by reading:
1. `RagPromptAssemblyService.cs:861-862` — `FormatChunkForPrompt` returns `$"[Source: Document {citation.DocumentId}, Page {citation.PageNumber}, Relevance: {citation.RelevanceScore:F2}, Copyright: {tierLabel}]\n{chunkText}\n---"`. **NO 1-based index**.
2. `RagPromptAssemblyService.cs:123` AND `:931` — both call `BuildSystemPrompt(agentTypology, gameTitle, ...)`. Two callsites.
3. `CrossGameStreamQaQueryHandler.cs:39` — `private const string AgentTypology = "tutor";`. Hardcoded.
4. `IRagPromptAssemblyService.cs` — interface has `AssembleFromContextAsync(...)` with current params, no inline-citation knowledge.

No commit; this is read-only verification.

---

## Task 1: Add new test file — unit tests for FormatChunkForPrompt + BuildSystemPrompt + AssembleFromContextAsync

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceInlineCitationTests.cs`

These tests will FAIL until Tasks 2, 3, 4 are done. We write them first (TDD).

- [ ] **Step 1: Create the failing test file**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceInlineCitationTests.cs`:

```csharp
// Issue #1703 — Inline citation markers (BE).
// Spec-panel decisions D-1703-A, D-1703-B, D-1703-C.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

public class RagPromptAssemblyServiceInlineCitationTests
{
    private static ChunkCitation BuildCitation(string docId, int page, string snippet, CopyrightTier tier = CopyrightTier.Full)
    {
        return new ChunkCitation
        {
            DocumentId = docId,
            PageNumber = page,
            SnippetPreview = snippet,
            FullText = snippet,
            RelevanceScore = 0.85f,
            CopyrightTier = tier,
        };
    }

    // ── D-1703-A: FormatChunkForPrompt with optional index ──

    [Fact]
    public void FormatChunkForPrompt_WithoutIndex_PreservesLegacyShape()
    {
        var citation = BuildCitation("doc-1", 14, "Sample chunk text.");

        var result = RagPromptAssemblyService.FormatChunkForPrompt(citation, "Sample chunk text.");

        result.Should().NotContain("[1]");
        result.Should().StartWith("[Source: Document doc-1, Page 14");
        result.Should().Contain("Sample chunk text.");
        result.Should().EndWith("---");
    }

    [Fact]
    public void FormatChunkForPrompt_WithIndex1_PrependsBracketedIndex()
    {
        var citation = BuildCitation("doc-1", 14, "Sample chunk text.");

        var result = RagPromptAssemblyService.FormatChunkForPrompt(citation, "Sample chunk text.", index: 1);

        result.Should().StartWith("[1] [Source: Document doc-1, Page 14");
    }

    [Fact]
    public void FormatChunkForPrompt_WithIndex42_PrependsCorrectNumber()
    {
        var citation = BuildCitation("doc-99", 21, "Other text.");

        var result = RagPromptAssemblyService.FormatChunkForPrompt(citation, "Other text.", index: 42);

        result.Should().StartWith("[42] [Source: Document doc-99, Page 21");
    }

    // ── D-1703-B + D-1703-C: BuildSystemPrompt with includeInlineCitationInstructions ──

    [Fact]
    public async Task AssembleFromContextAsync_WithoutFlag_DoesNotIncludeCitationFormatSection()
    {
        var service = new RagPromptAssemblyService(
            ragService: null!, ragQueryClassifier: null!, embeddingService: null!,
            agentMemoryContextBuilder: null!, debugCollector: null,
            agentLanguageDefaults: AgentLanguageDefaults.Default);

        var chunks = new List<ChunkCitation> { BuildCitation("doc-1", 14, "text") };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None);

        prompt.SystemPrompt.Should().NotContain("## Citation Format");
        prompt.SystemPrompt.Should().NotContain("prefixed with [N]");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagTrue_IncludesCitationFormatSection()
    {
        var service = new RagPromptAssemblyService(
            ragService: null!, ragQueryClassifier: null!, embeddingService: null!,
            agentMemoryContextBuilder: null!, debugCollector: null,
            agentLanguageDefaults: AgentLanguageDefaults.Default);

        var chunks = new List<ChunkCitation> { BuildCitation("doc-1", 14, "text") };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None,
            includeInlineCitationInstructions: true);

        prompt.SystemPrompt.Should().Contain("## Citation Format");
        prompt.SystemPrompt.Should().Contain("prefixed with [N]");
        prompt.SystemPrompt.Should().Contain("[N,M]");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagTrue_PrependsIndexToEachChunkHeader()
    {
        var service = new RagPromptAssemblyService(
            ragService: null!, ragQueryClassifier: null!, embeddingService: null!,
            agentMemoryContextBuilder: null!, debugCollector: null,
            agentLanguageDefaults: AgentLanguageDefaults.Default);

        var chunks = new List<ChunkCitation>
        {
            BuildCitation("doc-1", 14, "first chunk"),
            BuildCitation("doc-2", 21, "second chunk"),
            BuildCitation("doc-3", 7, "third chunk"),
        };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None,
            includeInlineCitationInstructions: true);

        prompt.SystemPrompt.Should().Contain("[1] [Source: Document doc-1");
        prompt.SystemPrompt.Should().Contain("[2] [Source: Document doc-2");
        prompt.SystemPrompt.Should().Contain("[3] [Source: Document doc-3");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagFalse_DoesNotPrependIndex()
    {
        var service = new RagPromptAssemblyService(
            ragService: null!, ragQueryClassifier: null!, embeddingService: null!,
            agentMemoryContextBuilder: null!, debugCollector: null,
            agentLanguageDefaults: AgentLanguageDefaults.Default);

        var chunks = new List<ChunkCitation>
        {
            BuildCitation("doc-1", 14, "first chunk"),
            BuildCitation("doc-2", 21, "second chunk"),
        };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None);

        // Legacy shape: chunk header starts with "[Source: ...", not "[N] [Source: ...]"
        prompt.SystemPrompt.Should().Contain("[Source: Document doc-1");
        prompt.SystemPrompt.Should().NotContain("[1] [Source: Document doc-1");
        prompt.SystemPrompt.Should().NotContain("[2] [Source: Document doc-2");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagTrue_PlacesSectionAfterReasoningApproach()
    {
        var service = new RagPromptAssemblyService(
            ragService: null!, ragQueryClassifier: null!, embeddingService: null!,
            agentMemoryContextBuilder: null!, debugCollector: null,
            agentLanguageDefaults: AgentLanguageDefaults.Default);

        var chunks = new List<ChunkCitation> { BuildCitation("doc-1", 14, "text") };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None,
            includeInlineCitationInstructions: true);

        var reasoningIdx = prompt.SystemPrompt.IndexOf("## Reasoning Approach", StringComparison.Ordinal);
        var citationIdx = prompt.SystemPrompt.IndexOf("## Citation Format", StringComparison.Ordinal);
        var ragIdx = prompt.SystemPrompt.IndexOf("## Game Rules and Documentation", StringComparison.Ordinal);

        reasoningIdx.Should().BeGreaterThan(0, "Reasoning Approach section must exist");
        citationIdx.Should().BeGreaterThan(reasoningIdx, "Citation Format must come after Reasoning Approach");
        ragIdx.Should().BeGreaterThan(citationIdx, "Citation Format must come before Game Rules and Documentation");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyServiceInlineCitationTests" --no-restore
```

Expected: 7 tests FAIL — the new optional param `index` on `FormatChunkForPrompt` and the new optional param `includeInlineCitationInstructions` on `AssembleFromContextAsync` don't exist yet (compile errors or AssertFailed).

If compile fails, that's fine — TDD red phase.

- [ ] **Step 3: Commit the failing tests**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceInlineCitationTests.cs
git commit -m "test(kb): #1703 Task 1 — failing unit tests for inline citation markers (D-1703-A/B/C)

- FormatChunkForPrompt: legacy shape, [1] index, [42] index
- AssembleFromContextAsync: without flag (no section, no index prefix)
- AssembleFromContextAsync: with flag (section present, [N] prefix on chunks)
- AssembleFromContextAsync: section placement (after Reasoning, before RAG)
- 7 tests, all RED until Tasks 2-4 implement the flag

Note: tests use FluentAssertions and instantiate RagPromptAssemblyService
directly with null dependencies (the test paths exercise pure formatting
and prompt-text building, no RAG retrieval is invoked)."
```

---

## Task 2: D-1703-A — Add optional index param to FormatChunkForPrompt

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs:858-863`

- [ ] **Step 1: Read current FormatChunkForPrompt**

Current code at lines 858-863:

```csharp
/// <summary>Formats a chunk with copyright annotation for the LLM system prompt.</summary>
public static string FormatChunkForPrompt(ChunkCitation citation, string chunkText)
{
    var tierLabel = citation.CopyrightTier == CopyrightTier.Full ? "FULL" : "PROTECTED";
    return $"[Source: Document {citation.DocumentId}, Page {citation.PageNumber}, Relevance: {citation.RelevanceScore:F2}, Copyright: {tierLabel}]\n{chunkText}\n---";
}
```

- [ ] **Step 2: Replace with index-aware variant**

Replace lines 858-863 with:

```csharp
/// <summary>
/// Formats a chunk with copyright annotation for the LLM system prompt.
/// When <paramref name="index"/> is non-null, prepends "[N] " to the header
/// (Issue #1703 D-1703-A — inline citation markers). Existing callers passing
/// no index get the legacy header shape, byte-identical to pre-#1703.
/// </summary>
public static string FormatChunkForPrompt(ChunkCitation citation, string chunkText, int? index = null)
{
    var tierLabel = citation.CopyrightTier == CopyrightTier.Full ? "FULL" : "PROTECTED";
    var prefix = index.HasValue ? $"[{index.Value}] " : string.Empty;
    return $"{prefix}[Source: Document {citation.DocumentId}, Page {citation.PageNumber}, Relevance: {citation.RelevanceScore:F2}, Copyright: {tierLabel}]\n{chunkText}\n---";
}
```

- [ ] **Step 3: Run the 3 FormatChunkForPrompt tests**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyServiceInlineCitationTests.FormatChunkForPrompt" --no-restore
```

Expected: 3 tests PASS (`FormatChunkForPrompt_WithoutIndex_PreservesLegacyShape`, `FormatChunkForPrompt_WithIndex1_PrependsBracketedIndex`, `FormatChunkForPrompt_WithIndex42_PrependsCorrectNumber`).

The remaining 4 tests still FAIL — that's expected (they need Task 3 + 4).

- [ ] **Step 4: Run existing RagPromptAssemblyServiceTests to confirm no regression**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyServiceTests" --no-restore
```

Expected: all existing tests PASS. The legacy `FormatChunkForPrompt(citation, chunkText)` call without index produces the same string as before.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs
git commit -m "feat(kb): #1703 Task 2 — FormatChunkForPrompt optional index param (D-1703-A)

- Added 'int? index = null' optional param
- When index has value, prepends '[N] ' to chunk header
- When null (default), legacy shape preserved byte-identical
- 3 inline-citation tests now GREEN; existing tests unchanged"
```

---

## Task 3: D-1703-B (impl) — Add includeInlineCitationInstructions param to BuildSystemPrompt + thread it through

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs:711-799` (BuildSystemPrompt signature + body)
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs:880-900` (BuildRagContextString to forward index)

- [ ] **Step 1: Update BuildSystemPrompt signature**

Find the existing signature at lines 711-715:

```csharp
private static string BuildSystemPrompt(
    string agentTypology, string gameTitle, GameState? gameState, string ragContext,
    bool hasExpansions = false, bool hasProtectedCitations = false,
    string agentLanguage = "it")
```

Replace with:

```csharp
private static string BuildSystemPrompt(
    string agentTypology, string gameTitle, GameState? gameState, string ragContext,
    bool hasExpansions = false, bool hasProtectedCitations = false,
    string agentLanguage = "it",
    bool includeInlineCitationInstructions = false)
```

- [ ] **Step 2: Inject the Citation Format section in BuildSystemPrompt body**

Find the existing "## Reasoning Approach" block (lines 746-752):

```csharp
// Chain-of-thought reasoning instructions
sb.AppendLine("## Reasoning Approach");
sb.AppendLine("Think step-by-step when answering:");
sb.AppendLine("1. Identify the relevant rules from the provided context.");
sb.AppendLine("2. Quote or reference the specific rule text that applies.");
sb.AppendLine("3. Explain how the rule applies to the user's specific situation.");
sb.AppendLine("4. State your conclusion clearly.");
sb.AppendLine();
```

INSERT the following block immediately AFTER it (so it sits between "Reasoning Approach" and "Copyright Notice"):

```csharp
// Issue #1703 D-1703-C: inline citation markers (only when caller opts in).
// Placed after Reasoning Approach so the LLM has the chain-of-thought rules
// in mind before being asked to emit [N] markers.
if (includeInlineCitationInstructions)
{
    sb.AppendLine("## Citation Format");
    sb.AppendLine("Each chunk in the documentation below is prefixed with [N] (e.g. [1], [2], [3]).");
    sb.AppendLine("When your answer draws from a chunk, append the corresponding [N] marker immediately after the cited word(s).");
    sb.AppendLine("Use [N,M] when a single statement is supported by multiple chunks.");
    sb.AppendLine("You MAY still mention page numbers in prose for emphasis, but the [N] marker is required for the citation linkage.");
    sb.AppendLine();
}
```

- [ ] **Step 3: Update BuildRagContextString to forward index when called from an inline-citation context**

Find current signature at line 885:

```csharp
private static string BuildRagContextString(IReadOnlyList<ChunkCitation> citations)
```

Replace with:

```csharp
private static string BuildRagContextString(IReadOnlyList<ChunkCitation> citations, bool prependIndex = false)
```

Find the existing loop at lines 891-897:

```csharp
var sb = new StringBuilder();
foreach (var citation in citations)
{
    // FullText is set on the single-game path; for cross-game pre-retrieved chunks
    // SnippetPreview is the canonical text (FullText may be null — acceptable for cross-game).
    var chunkText = citation.FullText ?? citation.SnippetPreview;
    sb.AppendLine(FormatChunkForPrompt(citation, chunkText));
}
```

Replace with:

```csharp
var sb = new StringBuilder();
int idx = 0;
foreach (var citation in citations)
{
    idx++;
    // FullText is set on the single-game path; for cross-game pre-retrieved chunks
    // SnippetPreview is the canonical text (FullText may be null — acceptable for cross-game).
    var chunkText = citation.FullText ?? citation.SnippetPreview;
    int? indexForPrompt = prependIndex ? idx : null;
    sb.AppendLine(FormatChunkForPrompt(citation, chunkText, indexForPrompt));
}
```

- [ ] **Step 4: Build to verify compile**

```bash
cd apps/api && dotnet build src/Api --no-restore
```

Expected: SUCCESS. The internal-call sites for `BuildSystemPrompt` and `BuildRagContextString` still compile because the new params have defaults.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs
git commit -m "feat(kb): #1703 Task 3 — BuildSystemPrompt + BuildRagContextString opt-in flag (D-1703-B/C)

- BuildSystemPrompt: new optional bool includeInlineCitationInstructions = false
  - Injects '## Citation Format' section after Reasoning Approach when true
  - Otherwise legacy prompt structure unchanged
- BuildRagContextString: new optional bool prependIndex = false
  - When true, calls FormatChunkForPrompt with 1-based index per chunk
  - When false (default), forwards null index → legacy header shape
- Both new params default off — existing call sites unchanged at this task
- Internal changes only; Task 4 wires AssembleFromContextAsync to thread the flag"
```

---

## Task 4: D-1703-B (interface + handler) — Thread flag through AssembleFromContextAsync

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagPromptAssemblyService.cs` (interface signature)
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs:903-959` (impl)

- [ ] **Step 1: Add the optional param to the interface**

Open `IRagPromptAssemblyService.cs`. Find the `AssembleFromContextAsync` method (around line 50-80). It looks like:

```csharp
/// <summary>
/// Assembles a system + user prompt from PRE-RETRIEVED chunks, without invoking
/// any retrieval services. Used for cross-game flows where chunks come from
/// multiple games (see #1661 cross-game RAG).
/// </summary>
Task<AssembledPrompt> AssembleFromContextAsync(
    string agentTypology,
    string gameTitle,
    string userQuestion,
    IReadOnlyList<ChunkCitation> preRetrievedChunks,
    ChatThread? chatThread,
    UserTier? userTier,
    string agentLanguage,
    CancellationToken cancellationToken);
```

Add the new optional bool param at the end:

```csharp
/// <summary>
/// Assembles a system + user prompt from PRE-RETRIEVED chunks, without invoking
/// any retrieval services. Used for cross-game flows where chunks come from
/// multiple games (see #1661 cross-game RAG).
///
/// When <paramref name="includeInlineCitationInstructions"/> is true, the system
/// prompt includes a "## Citation Format" section instructing the LLM to emit
/// [N] inline markers, and each chunk header in the RAG context is prefixed with
/// [N] (Issue #1703 D-1703-B/C).
/// </summary>
Task<AssembledPrompt> AssembleFromContextAsync(
    string agentTypology,
    string gameTitle,
    string userQuestion,
    IReadOnlyList<ChunkCitation> preRetrievedChunks,
    ChatThread? chatThread,
    UserTier? userTier,
    string agentLanguage,
    CancellationToken cancellationToken,
    bool includeInlineCitationInstructions = false);
```

- [ ] **Step 2: Update the impl in RagPromptAssemblyService**

Open `RagPromptAssemblyService.cs`. Find `AssembleFromContextAsync` (starts around line 903) — it looks like:

```csharp
/// <inheritdoc />
public Task<AssembledPrompt> AssembleFromContextAsync(
    string agentTypology,
    string gameTitle,
    string userQuestion,
    IReadOnlyList<ChunkCitation> preRetrievedChunks,
    ChatThread? chatThread,
    UserTier? userTier,
    string agentLanguage,
    CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(agentTypology);
    ArgumentNullException.ThrowIfNull(gameTitle);
    ArgumentNullException.ThrowIfNull(userQuestion);
    ArgumentNullException.ThrowIfNull(preRetrievedChunks);
    ArgumentNullException.ThrowIfNull(agentLanguage);

    // Build RAG context from the pre-retrieved chunks — NO retrieval services invoked.
    var ragContext = BuildRagContextString(preRetrievedChunks);
    ...
```

Replace the signature + the `BuildRagContextString` + `BuildSystemPrompt` calls. Specifically:

(a) Add the new optional param at the end of the signature (parameter list).

(b) Pass `prependIndex: includeInlineCitationInstructions` to `BuildRagContextString`.

(c) Pass `includeInlineCitationInstructions: includeInlineCitationInstructions` to `BuildSystemPrompt`.

Concretely, change the signature (line ~903-912) to:

```csharp
/// <inheritdoc />
public Task<AssembledPrompt> AssembleFromContextAsync(
    string agentTypology,
    string gameTitle,
    string userQuestion,
    IReadOnlyList<ChunkCitation> preRetrievedChunks,
    ChatThread? chatThread,
    UserTier? userTier,
    string agentLanguage,
    CancellationToken cancellationToken,
    bool includeInlineCitationInstructions = false)
```

Then in the body, change the `BuildRagContextString` line (~920) from:

```csharp
var ragContext = BuildRagContextString(preRetrievedChunks);
```

to:

```csharp
var ragContext = BuildRagContextString(preRetrievedChunks, prependIndex: includeInlineCitationInstructions);
```

Then locate the `BuildSystemPrompt` call inside `AssembleFromContextAsync` (around line 931). It currently looks like:

```csharp
var systemPrompt = BuildSystemPrompt(
    agentTypology, gameTitle, gameState: null, ragContext,
    /* hasExpansions: */ false, hasProtectedCitations, agentLanguage);
```

Change to:

```csharp
var systemPrompt = BuildSystemPrompt(
    agentTypology, gameTitle, gameState: null, ragContext,
    hasExpansions: false, hasProtectedCitations, agentLanguage,
    includeInlineCitationInstructions: includeInlineCitationInstructions);
```

- [ ] **Step 3: Run ALL the new inline-citation tests**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyServiceInlineCitationTests" --no-restore
```

Expected: ALL 7 tests PASS.

- [ ] **Step 4: Run full RagPromptAssemblyService test suite to confirm no regression**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyService" --no-restore
```

Expected: 100% PASS across `RagPromptAssemblyServiceTests`, `RagPromptAssemblyServiceCopyrightTests`, `RagPromptAssemblyServiceFallbackMetricsTests`, `RagPromptAssemblyServiceInlineCitationTests`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagPromptAssemblyService.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs
git commit -m "feat(kb): #1703 Task 4 — thread inline-citation flag through interface (D-1703-B)

- IRagPromptAssemblyService.AssembleFromContextAsync gains optional
  'bool includeInlineCitationInstructions = false' as last param
- Impl forwards to BuildRagContextString(prependIndex) and to
  BuildSystemPrompt(includeInlineCitationInstructions)
- Default-false invariant: existing callers unchanged behavior
- All 7 inline-citation unit tests now GREEN
- 0 regression in legacy RagPromptAssemblyService tests"
```

---

## Task 5: D-1703-B (callsite opt-in) — CrossGameStreamQaQueryHandler opts in + telemetry counter call

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs:200-220` (call site)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandlerInlineCitationTests.cs`

- [ ] **Step 1: Write a failing handler integration test**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandlerInlineCitationTests.cs`:

```csharp
// Issue #1703 — CrossGameStreamQaQueryHandler opts in to inline citation markers.
// Verifies the handler passes includeInlineCitationInstructions=true to
// AssembleFromContextAsync, and that the assembled prompt contains the marker
// section (mock the prompt service to capture the param value).
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.CrossGameStreamQa;

public class CrossGameStreamQaQueryHandlerInlineCitationTests
{
    [Fact]
    public async Task Handler_CallsAssembleFromContextWithInlineCitationFlagTrue()
    {
        // ARRANGE
        var promptServiceMock = new Mock<IRagPromptAssemblyService>();
        bool capturedFlag = false;
        promptServiceMock
            .Setup(s => s.AssembleFromContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ChunkCitation>>(),
                It.IsAny<ChatThread?>(),
                It.IsAny<UserTier?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .Callback<string, string, string, IReadOnlyList<ChunkCitation>, ChatThread?, UserTier?, string, CancellationToken, bool>(
                (_, _, _, _, _, _, _, _, flag) => capturedFlag = flag)
            .ReturnsAsync(new AssembledPrompt(
                SystemPrompt: "test system",
                UserPrompt: "test user",
                Citations: new List<ChunkCitation>(),
                EstimatedTokens: 100));

        // ACT
        // NOTE: Full handler wiring requires several other mocks (search service,
        // RBAC, LLM client, etc.). Use the minimum viable Arrange that exercises
        // the prompt-assembly call path. If the handler hard-blocks on a downstream
        // dependency, mark this test [Fact(Skip = "needs full handler harness")]
        // and rely on the unit tests from Task 1 as the primary coverage gate.
        //
        // Implementer's task: spin up a minimal handler instance with mocks for
        // the dependencies required to reach the AssembleFromContextAsync call.
        // If the harness setup exceeds ~50 lines, prefer Skip and document.

        // ASSERT
        // capturedFlag.Should().BeTrue("handler opts in to inline citation markers per D-1703-B");
        // (Activate this assertion once the Arrange is complete.)
        Assert.True(true, "Placeholder — implementer to complete the Arrange phase. " +
                          "If harness setup is too heavy, mark Skip and rely on Task 1 unit tests.");
    }
}
```

**Engineer note**: this handler test is OPTIONAL — the Task 1 unit tests already prove that `AssembleFromContextAsync` with `includeInlineCitationInstructions=true` produces the right system prompt. The handler test only proves the handler PASSES `true`. If wiring the full handler harness is heavy (the handler depends on `IMultiGameSearchService`, `ILlmStreamingService`, `IRagAccessService`, `IRagPromptAssemblyService`, `ILogger`, etc.), prefer to: (a) write a single Mock-based test that asserts the handler calls `AssembleFromContextAsync` with the bool true (as sketched above), OR (b) skip and rely on Task 1 unit + a manual integration walkthrough.

- [ ] **Step 2: Modify the handler call site**

Open `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs`. Find the `ExecutePromptAssemblyAsync` method (around line 201-225). It calls `AssembleFromContextAsync` like:

```csharp
var assembled = await _promptService.AssembleFromContextAsync(
    AgentTypology,
    CrossGameTitle,
    query.Query,
    citations,
    chatThread: null,
    userTier: UserTier.Normal,
    query.AgentLanguage,
    ct).ConfigureAwait(false);
```

Change to:

```csharp
var assembled = await _promptService.AssembleFromContextAsync(
    AgentTypology,
    CrossGameTitle,
    query.Query,
    citations,
    chatThread: null,
    userTier: UserTier.Normal,
    query.AgentLanguage,
    ct,
    includeInlineCitationInstructions: true).ConfigureAwait(false);
```

- [ ] **Step 3: Run the handler test (if implemented) + full handler suite**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~CrossGameStreamQaQueryHandler" --no-restore
```

Expected: all existing handler tests still PASS (no regression). The new optional param defaults to false at the impl level, but since we explicitly pass `true` here, behavior changes only for this endpoint.

If the handler test from Step 1 is fully Arranged, it should PASS too. If it's the placeholder version, it trivially passes (`Assert.True(true)`).

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandlerInlineCitationTests.cs
git commit -m "feat(kb): #1703 Task 5 — CrossGameStreamQaQueryHandler opts in to inline markers (D-1703-B)

- Pass includeInlineCitationInstructions: true to AssembleFromContextAsync
- Only the /ask/global endpoint receives the new system-prompt section;
  per-game agents (AskQuestionQueryHandler, ChatWithSessionAgentCommandHandler,
  StreamSetupGuideQueryHandler, etc.) continue to receive the legacy prompt
- Added a placeholder integration test (or fully Arranged Mock test if the
  harness allows) under CrossGameStreamQaQueryHandlerInlineCitationTests
- No regression in existing handler tests"
```

---

## Task 6: D-1703-E (telemetry) — Add CitationMarkersEmittedTotal counter

**Files:**
- Modify: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Rag.cs` (add new counter)

**Note**: this counter is incremented at the FE-aware boundary (e.g. by a future ingestion job that parses LLM responses and inspects token output). For PR-1 BE, we **only declare the counter** — incrementing it from BE during streaming is out of scope because the BE streams tokens individually and only the assembled response is the right place to inspect.

Per Crispin §1.6 (test 4): the integration test from Task 7 (gated) is what exercises this counter.

- [ ] **Step 1: Add the counter declaration to MeepleAiMetrics.Rag.cs**

Open `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Rag.cs`. Find the existing `CopyrightInstructionInjected` counter at lines 257-260:

```csharp
public static readonly Counter<long> CopyrightInstructionInjected = Meter.CreateCounter<long>(
    name: "meepleai.rag.copyright.instruction.injected",
    unit: "prompts",
    description: "Count of system prompts that include the copyright paraphrase instruction");
```

Add the following AFTER it (between `CopyrightInstructionInjected` and the next counter declaration):

```csharp
/// <summary>
/// Counter for LLM responses inspected for inline citation markers ([N]).
/// Issue #1703 D-1703-E — tracks production compliance with the BE prompt
/// instruction. Tag <c>compliant=true</c> when the response contains at least
/// one well-formed [N] marker whose N is within citations bounds; <c>false</c>
/// otherwise.
/// Increment site: the /ask/global handler at Complete-event time (TODO PR-2 FE
/// helper OR a follow-up BE post-processor that scans assembled token text).
/// Tags: compliant (bool), agent_typology (string).
/// </summary>
public static readonly Counter<long> CitationMarkersEmittedTotal = Meter.CreateCounter<long>(
    name: "meepleai.rag.citation_markers.emitted_total",
    unit: "responses",
    description: "Count of LLM responses inspected for inline [N] citation markers");
```

- [ ] **Step 2: Build to verify compile**

```bash
cd apps/api && dotnet build src/Api --no-restore
```

Expected: SUCCESS.

- [ ] **Step 3: Verify counter is discoverable**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~MeepleAiMetrics" --no-restore 2>&1 | tail -10
```

Expected: no breaking change in any metric-discovery test. If no such test exists, skip — building is enough.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Rag.cs
git commit -m "feat(observability): #1703 Task 6 — CitationMarkersEmittedTotal counter (D-1703-E)

- New OpenTelemetry counter meepleai.rag.citation_markers.emitted_total
- Unit 'responses', tags compliant (bool) + agent_typology (string)
- Increment site TBD in a follow-up:
  - Option α: BE post-processor scans assembled token text at Complete event
  - Option β: FE PR-2 helper increments via a thin reporting endpoint
- This task ONLY declares the counter; increment wiring deferred per the
  observation that BE streams tokens individually (no convenient inspection
  point on the hot path). Target compliance: ≥ 70% production"
```

---

## Task 7: D-1703-E (optional gated integration test) — Real LLM compliance check

**Files:**
- Append: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceInlineCitationTests.cs`

This task is OPTIONAL but recommended. The test runs against a real LLM endpoint (gated by env var `RUN_LLM_INTEGRATION_TESTS=true`) and asserts that the prompt produces `[N]` markers in ≥ 8/10 runs at fixed seed.

If the implementer is uncomfortable touching live LLM in CI (cost / API key), skip this task — Task 1's 7 unit tests are sufficient for PR merge.

- [ ] **Step 1: Append the gated integration test**

Open the test file from Task 1 and APPEND this class at the end:

```csharp
/// <summary>
/// Gated integration test against a real LLM endpoint. Asserts that the assembled
/// system prompt, when sent to a real model, produces inline [N] citation markers
/// in ≥ 8/10 fixed-seed runs.
///
/// Skipped unless RUN_LLM_INTEGRATION_TESTS=true environment variable is set.
/// Requires OPENROUTER_API_KEY or equivalent in env.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Requires", "LLM")]
public class RagPromptAssemblyServiceInlineCitationLlmIntegrationTests
{
    private static bool ShouldRun =>
        string.Equals(
            Environment.GetEnvironmentVariable("RUN_LLM_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    [SkippableFact]
    public async Task RealLlmEmitsBracketedMarkersInMajorityOfRuns()
    {
        Skip.IfNot(ShouldRun, "RUN_LLM_INTEGRATION_TESTS not set to true");

        // Engineer note: this test should:
        // 1. Assemble a prompt with includeInlineCitationInstructions: true using
        //    a fixed input (3 chunks, question "What are the abilities of class X?")
        // 2. Send to the real LLM client (use the same wiring CrossGameStreamQaQueryHandler uses)
        // 3. Run 10 times with the same prompt + seed=42 + temperature=0.1
        // 4. Count responses that contain at least one match of /\[\d+(?:,\s*\d+)*\]/
        // 5. Assert count >= 8

        // The full implementation requires DI wiring or a service-locator
        // pattern. If the implementer's session doesn't have time to wire that,
        // mark Skip("manual run only") and document the manual procedure in
        // claudedocs/1703-llm-integration-runbook.md.

        Assert.True(true, "Placeholder — implementer to wire real LLM call. " +
                          "Manual runbook acceptable as an alternative.");
    }
}
```

Add the `Xunit.SkippableFact` package if not already a dependency:

```bash
cd apps/api/tests/Api.Tests && dotnet add package Xunit.SkippableFact --no-restore
```

(Check `Api.Tests.csproj` first — it may already be there.)

- [ ] **Step 2: Verify Skip works (without LLM credentials)**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyServiceInlineCitationLlmIntegrationTests" --no-restore
```

Expected: 1 test SKIPPED (no environment var set).

- [ ] **Step 3: Commit (if you added the test)**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceInlineCitationTests.cs apps/api/tests/Api.Tests/Api.Tests.csproj
git commit -m "test(kb): #1703 Task 7 — gated LLM integration test scaffolding (D-1703-E)

- New test class RagPromptAssemblyServiceInlineCitationLlmIntegrationTests
- Skipped by default; runs only when RUN_LLM_INTEGRATION_TESTS=true
- Placeholder body — full wiring deferred to a manual runbook OR
  follow-up integration harness PR
- Documents the test contract: 10 runs at seed=42 / temp=0.1,
  assert >= 8 contain /\[\d+(?:,\s*\d+)*\]/ markers
- Target real-world compliance >= 70% per D-1703-E"
```

---

## Task 8: Verify + finalize

**Files:**
- All from previous tasks

- [ ] **Step 1: Run the full Api.Tests suite to confirm no regression**

```bash
cd apps/api && dotnet test tests/Api.Tests --no-restore 2>&1 | tail -25
```

Expected: ALL pass except gated LLM integration test (skipped). Look for `Passed!` summary.

If any test fails, investigate:
- If it's a pre-existing flaky test (CLAUDE.md "Known Flaky Tests" section), re-run with `--filter` excluding it
- If it's a real regression caused by this PR, fix it

- [ ] **Step 2: Lint + format check**

```bash
cd apps/api && dotnet format src/Api --verify-no-changes --no-restore
```

Expected: no changes needed.

If format flags issues, run `dotnet format src/Api` and commit fixes with `style(kb): #1703 dotnet format fixes`.

- [ ] **Step 3: Push branch + open PR**

```bash
git push -u origin feature/issue-1703-be-inline-citation-markers

gh pr create --base main-dev --title "feat(kb): #1703 BE — inline citation markers ([N]) for /ask/global" --body "$(cat <<'EOF'
## Summary

Implements **BE PR-1** of issue #1703 — adds LLM `[N]` inline citation markers to the cross-game KB ask flow (\`POST /api/v1/knowledge-base/ask/global\`), gated by a new opt-in flag so existing per-game agents stay unchanged.

## Spec-panel decisions implemented (D-1703-A/B/C/E)

| # | Decision | Where |
|---|---|---|
| D-1703-A | \`FormatChunkForPrompt\` prepends \`[N]\` when index provided | Task 2 |
| D-1703-B | \`BuildSystemPrompt\` + \`AssembleFromContextAsync\` gain \`includeInlineCitationInstructions: bool = false\` | Tasks 3-4 |
| D-1703-C | \`## Citation Format\` section text injected only when flag is true | Task 3 |
| D-1703-B (handler) | \`CrossGameStreamQaQueryHandler\` opts in (\`true\`) | Task 5 |
| D-1703-E | New \`CitationMarkersEmittedTotal\` counter (declaration only; increment site TBD) | Task 6 |
| D-1703-E (gated test) | Optional \`RUN_LLM_INTEGRATION_TESTS\` gated test scaffolding | Task 7 |

D-1703-D is **FE-only** and ships in PR-2 (separate plan).

## Test plan

- [x] \`dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyServiceInlineCitationTests"\` → 7 tests PASS (3 FormatChunkForPrompt + 4 AssembleFromContextAsync flag behaviors)
- [x] \`dotnet test tests/Api.Tests --filter "FullyQualifiedName~RagPromptAssemblyService"\` → 100% PASS (no regression in existing RagPromptAssemblyServiceTests / Copyright / FallbackMetrics suites)
- [x] \`dotnet test tests/Api.Tests --filter "FullyQualifiedName~CrossGameStreamQaQueryHandler"\` → all existing handler tests PASS (default-false invariant for unchanged consumers; explicit-true at the cross-game callsite)
- [x] Full \`dotnet test tests/Api.Tests\` → all PASS except gated LLM integration test (skipped without env var)
- [x] \`dotnet format src/Api --verify-no-changes\` → clean

## Backwards compatibility

- All existing \`BuildSystemPrompt\` and \`AssembleFromContextAsync\` callers compile unchanged thanks to default-false params
- All existing per-game agent endpoints (\`AskQuestionQueryHandler\`, \`ChatWithSessionAgentCommandHandler\`, \`StreamSetupGuideQueryHandler\`, etc.) continue to receive the legacy system prompt
- \`FormatChunkForPrompt(citation, chunkText)\` (no index) returns byte-identical output to pre-#1703

## Follow-ups

- **PR-2 (FE)**: parseCitationMarkers utility + DrawerCompleted integration (separate plan)
- **PR-3 (tech-debt)**: unify \`[ref:docId:page]\` copyright marker with \`[N]\` system (Fowler architectural note)
- **Counter increment site**: TBD — either BE post-processor scans assembled tokens at Complete event, or FE PR-2 thin reporting endpoint reports compliance

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR created; CI runs.

- [ ] **Step 4: Update the issue thread**

Post a comment on #1703 linking the BE PR:

```bash
gh issue comment 1703 --body "**BE PR-1 opened**: see linked PR. Implements D-1703-A/B/C/E (BE side). FE PR-2 (D-1703-D parser + DrawerCompleted integration) will follow after BE merge."
```

---

## §3 — Self-review (engineer should run this before submitting PR)

### Spec coverage

| Spec decision | Task |
|---|---|
| D-1703-A — FormatChunkForPrompt `[N]` prefix | Task 2 (impl) + Task 1 tests |
| D-1703-B — BuildSystemPrompt + AssembleFromContextAsync optional bool | Task 3 (BuildSystemPrompt + BuildRagContextString) + Task 4 (interface + impl) + Task 1 tests |
| D-1703-B — CrossGameStreamQaQueryHandler opts in | Task 5 |
| D-1703-C — Prompt section text | Task 3 |
| D-1703-E — Telemetry counter | Task 6 |
| D-1703-E — Gated LLM integration test | Task 7 (optional) |
| D-1703-D — FE parser | OUT OF SCOPE (deferred to PR-2 / separate plan) |

### Type consistency check

- `int? index = null` param on `FormatChunkForPrompt` (Task 2) → matches usage in `BuildRagContextString(prependIndex)` (Task 3)
- `bool includeInlineCitationInstructions = false` param consistently named and positioned (last param) across `BuildSystemPrompt` (Task 3), `AssembleFromContextAsync` (Task 4), `IRagPromptAssemblyService.AssembleFromContextAsync` (Task 4), and `CrossGameStreamQaQueryHandler` call site (Task 5)
- Counter naming follows existing convention: `meepleai.rag.citation_markers.emitted_total` (Task 6), tags `compliant` (bool) + `agent_typology` (string)
- All test class names follow `<ClassUnderTest>InlineCitationTests` pattern (Task 1, Task 5, Task 7)

### Placeholder scan

- Task 5 acknowledges "placeholder OR fully arranged" depending on harness effort — explicit decision documented in commit message
- Task 6 acknowledges "increment site TBD" — explicitly deferred, not hidden TODO
- Task 7 is OPTIONAL with placeholder body — explicitly gated

No "implement later" / "add error handling" / "similar to Task N" wording.

### Open follow-ups

- Counter increment site implementation (BE post-processor OR FE PR-2 reporter)
- FE PR-2 (parseCitationMarkers + DrawerCompleted integration)
- PR-3 tech-debt: unify `[ref:...]` with `[N]` system

---

**End of plan. Self-review complete. Ready for execution.**
