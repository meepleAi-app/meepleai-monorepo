# BE: Snippet chunk-level deep-link (#1702) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the BE `Snippet` record with two nullable chunk-identity fields so the `/api/v1/knowledge-base/ask/global` SSE Citations event can carry chunk-level deep-link information, while keeping the JSON wire format **byte-identical** for the 8 other call sites (per-game endpoints, RAG service, etc.).

**Architecture:** Use C# **init-only additional properties** on the positional record (NOT additional constructor parameters), so the 8 existing call sites don't need to change. Apply `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` to guarantee zero JSON drift when fields are null. Wire only `CrossGameStreamQaQueryHandler` to populate the new fields from `MultiGameSearchResultItem.ChunkId` (string) + `ChunkIndex` (int).

**Tech Stack:** C# 12 record types · System.Text.Json · xUnit 3.2.1 · FluentAssertions 8.8.0 · Testcontainers (PostgreSQL) · WebApplicationFactory

---

## Design Decisions

### DD-1: `string?` not `Guid?` for chunkId

The issue body proposed `Guid? chunkId` (mapping to `TextChunkEntity.Id`). However, the retrieval pipeline exposes a **string composite ID** `MultiGameSearchResultItem.ChunkId` (format `"{PdfDocumentId}_{ChunkIndex}"`), NOT the DB `TextChunkEntity.Id` Guid. Wiring the Guid would require an extra DB join per chunk during streaming. Using the existing string composite ID:

- Already unique upstream (`PdfDocumentId` × `ChunkIndex`)
- No extra DB query
- FE deep-link URL becomes `?docId=<pdfDocId>&chunkId=<pdfDocId>_<chunkIndex>` — `chunkId` self-contains `docId`, redundancy is acceptable
- Spec D-2 (FE graceful degrade on unresolvable chunkId) works identically with `string` or `Guid`

**Decision: use `string? chunkId`. The issue body's `Guid?` was a draft proposal not grounded in the upstream type.**

### DD-2: Init-only additional properties + `[JsonIgnore(WhenWritingNull)]`

The `Snippet` record has **9 production call sites** (verified via `grep`). Adding new **positional** parameters would force 8 unrelated handlers (per-game endpoints, RAG service, AiEndpoints) to update their `new Snippet(...)` calls just to pass `null`. To stay **additive**:

```csharp
internal record Snippet(string text, string source, int page, int line, float score)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? chunkId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? chunkPosition { get; init; }
}
```

- The 8 existing call sites compile and serialize **byte-identically** (null fields are omitted from JSON output)
- Only the cross-game call site sets the new fields via object initializer:
  ```csharp
  new Snippet(r.Content, r.PdfDocumentId, r.PageNumber ?? 0, 0, r.HybridScore)
  {
      chunkId = r.ChunkId,
      chunkPosition = r.ChunkIndex,
  }
  ```

Per D-4 the BC test asserts the byte-identical wire format for at least one per-game endpoint.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `apps/api/src/Api/Models/Contracts.cs` | Modify (line 30) | Extend `Snippet` record with 2 init-only nullable properties + JsonIgnore attrs |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs` | Modify (line 105–106) | Wire `r.ChunkId` + `r.ChunkIndex` into the `Snippet` projection |
| `apps/api/tests/Api.Tests/Unit/Models/SnippetSerializationTests.cs` | **Create** | Unit tests for serialization behavior (omit-when-null, present-when-set) |
| `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GlobalKbAskStreamEndpointTests.cs` | Modify | Add 2 integration assertions: chunkId/chunkPosition present in `/ask/global` SSE Citations |
| `apps/api/tests/Api.Tests/Integration/KnowledgeBase/PerGameSnippetBackCompatTests.cs` | **Create** | BC verification: at least one per-game endpoint serializes Snippet without chunkId/chunkPosition keys (additive only) |

The 8 other call sites (StreamQa, PlaygroundChat, SuggestPlayerMove, StreamDebugQa, RagService×4, AiEndpoints) are **not modified** — they automatically inherit `chunkId = null` + `chunkPosition = null` via the init-only default, and the JsonIgnore attrs keep the wire format unchanged.

---

## Pre-Flight

- [ ] **Step 0a: Confirm clean working tree on `main-dev`**

```bash
git branch --show-current  # MUST print main-dev
git status                 # MUST be clean
git pull --ff-only         # MUST succeed
```

- [ ] **Step 0b: Create feature branch**

```bash
git checkout -b feature/issue-1702-be-snippet-chunk-id
git config branch.feature/issue-1702-be-snippet-chunk-id.parent main-dev
```

- [ ] **Step 0c: Read the 3 source files** to confirm current state matches the exploration report:

```bash
# Confirm line 30 of Contracts.cs is the Snippet record
sed -n '28,34p' apps/api/src/Api/Models/Contracts.cs
# Confirm line 105-106 of CrossGameStreamQa handler is the Snippet projection
sed -n '100,115p' apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs
# Confirm MultiGameSearchResultItem has ChunkId + ChunkIndex
grep -n "ChunkId\|ChunkIndex" apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IMultiGameHybridSearchService.cs
```

Expected output: `Snippet(string text, string source, int page, int line, float score)` at line 30; `new Snippet(r.Content, r.PdfDocumentId, r.PageNumber ?? 0, 0, r.HybridScore)` at line 105–106; `ChunkId` (string) + `ChunkIndex` (int) public on `MultiGameSearchResultItem`.

---

## Task 1: Unit test — Snippet serialization (omit-when-null)

**Files:**
- Create: `apps/api/tests/Api.Tests/Unit/Models/SnippetSerializationTests.cs`

- [ ] **Step 1: Write the failing test file**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Models;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SnippetSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    [Fact]
    public void Snippet_WithoutChunkFields_SerializesWithoutChunkKeys()
    {
        // Arrange: legacy construction (no chunkId / chunkPosition)
        var snippet = new Snippet("text", "source", 1, 0, 0.9f);

        // Act
        var json = JsonSerializer.Serialize(snippet, JsonOptions);

        // Assert: ZERO drift in wire format for legacy call sites
        json.Should().NotContain("chunkId");
        json.Should().NotContain("chunkPosition");
        json.Should().Contain("\"text\":\"text\"");
        json.Should().Contain("\"source\":\"source\"");
    }

    [Fact]
    public void Snippet_WithChunkFields_SerializesWithBothKeys()
    {
        // Arrange: cross-game call site
        var snippet = new Snippet("text", "src", 14, 0, 0.85f)
        {
            chunkId = "abc_3",
            chunkPosition = 3,
        };

        // Act
        var json = JsonSerializer.Serialize(snippet, JsonOptions);

        // Assert
        json.Should().Contain("\"chunkId\":\"abc_3\"");
        json.Should().Contain("\"chunkPosition\":3");
    }

    [Fact]
    public void Snippet_WithOnlyChunkId_SerializesOnlyThatKey()
    {
        // Arrange: defensive — partial set
        var snippet = new Snippet("text", "src", 1, 0, 0.5f)
        {
            chunkId = "abc_0",
            // chunkPosition intentionally not set
        };

        // Act
        var json = JsonSerializer.Serialize(snippet, JsonOptions);

        // Assert
        json.Should().Contain("\"chunkId\":\"abc_0\"");
        json.Should().NotContain("chunkPosition");
    }
}
```

- [ ] **Step 2: Run test — confirm it fails (chunkId / chunkPosition do not exist yet)**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SnippetSerializationTests" 2>&1 | tail -20
```

Expected: **compilation error** — `chunkId` and `chunkPosition` are not members of `Snippet`.

- [ ] **Step 3: Commit failing test (optional, recommended for TDD audit trail)**

```bash
git add apps/api/tests/Api.Tests/Unit/Models/SnippetSerializationTests.cs
git commit -m "test: failing serialization tests for Snippet chunk fields (#1702)"
```

---

## Task 2: Extend `Snippet` record with init-only chunk properties

**Files:**
- Modify: `apps/api/src/Api/Models/Contracts.cs:30`

- [ ] **Step 1: Verify current record line**

```bash
sed -n '30p' apps/api/src/Api/Models/Contracts.cs
```

Expected: `internal record Snippet(string text, string source, int page, int line, float score);`

- [ ] **Step 2: Apply the record extension**

Replace line 30 with:

```csharp
internal record Snippet(string text, string source, int page, int line, float score)
{
    /// <summary>
    /// Composite chunk identifier "{PdfDocumentId}_{ChunkIndex}" from MultiGameSearchResultItem.
    /// Only populated by cross-game retrieval (/ask/global). Null for per-game endpoints.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? chunkId { get; init; }

    /// <summary>
    /// Zero-based chunk index within the document (from TextChunkEntity.ChunkIndex / MultiGameSearchResultItem.ChunkIndex).
    /// Only populated by cross-game retrieval. Null for per-game endpoints.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? chunkPosition { get; init; }
}
```

> **Naming convention note (review fix NIT-1):** The properties use **camelCase** (`chunkId`, `chunkPosition`) intentionally — this matches the existing `Snippet` record's positional parameters (`text`, `source`, `page`, `line`, `score`, all lowercase) and the JSON wire keys. If the repo's Meziantou analyzers (or a `.editorconfig` rule) flag this as `MA0007` / `IDE1006` / "Naming rule violation: These words must begin with upper case characters", suppress with `#pragma warning disable MA0007, IDE1006` around the two property declarations OR add a file-level `[SuppressMessage]`. Do NOT rename to PascalCase — that would break JSON parity for existing consumers (they expect `"text"`, `"source"`, etc. — adding PascalCase `ChunkId` would create the JSON key `"ChunkId"` which is inconsistent).

- [ ] **Step 3: Ensure required `using` directives at top of `Contracts.cs`**

```bash
head -8 apps/api/src/Api/Models/Contracts.cs
```

Expected: `using System.Text.Json.Serialization;` MUST be present (used by `JsonIgnore` / `JsonIgnoreCondition`). If absent, add it next to the other `using` directives at the top of the file (do NOT add `using` outside the existing block).

- [ ] **Step 4: Run unit tests — confirm all 3 pass**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SnippetSerializationTests" 2>&1 | tail -10
```

Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 5: Run full unit test suite to confirm no regressions**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit" --no-build 2>&1 | tail -5
```

Expected: existing unit test count unchanged + 3 new tests, all green.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Models/Contracts.cs
git commit -m "feat(kb): extend Snippet record with chunkId/chunkPosition init-only props (#1702)"
```

---

## Task 3: Wire `CrossGameStreamQaQueryHandler` to populate chunk fields

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs:105-106`

- [ ] **Step 1: Verify current projection**

```bash
sed -n '103,110p' apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs
```

Expected:
```csharp
var snippets = retrievalResults!
    .Select(r => new Snippet(r.Content, r.PdfDocumentId, r.PageNumber ?? 0, 0, r.HybridScore))
    .ToList();
```

- [ ] **Step 2: Apply the wiring change**

Replace the `.Select(...)` block with:

```csharp
var snippets = retrievalResults!
    .Select(r => new Snippet(r.Content, r.PdfDocumentId, r.PageNumber ?? 0, 0, r.HybridScore)
    {
        chunkId = r.ChunkId,
        chunkPosition = r.ChunkIndex,
    })
    .ToList();
```

(`r` is `MultiGameSearchResultItem` — both `ChunkId: string` and `ChunkIndex: int` are required non-nullable members, confirmed in the exploration report.)

- [ ] **Step 3: Build to confirm no compile errors**

```bash
dotnet build apps/api/src/Api/Api.csproj --no-restore 2>&1 | tail -10
```

Expected: `Build succeeded.`

- [ ] **Step 4: Run handler-related unit tests (if any exist for this handler)**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CrossGameStreamQa" --no-build 2>&1 | tail -10
```

Expected: all existing tests green (no behavior change for non-Citations event types).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs
git commit -m "feat(kb): wire chunkId/chunkPosition in CrossGameStreamQa Snippet projection (#1702)"
```

---

## Task 4: Integration test — `/ask/global` SSE Citations carries chunk fields (G/W/T Scenario 1)

**Files:**
- Modify: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GlobalKbAskStreamEndpointTests.cs`

- [ ] **Step 1: Locate the existing Citations assertion in the test file**

```bash
grep -n "StreamingEventType.Citations\|Citations event" apps/api/tests/Api.Tests/Integration/KnowledgeBase/GlobalKbAskStreamEndpointTests.cs
```

Expected: at least one location around line 275 where the test asserts a Citations event is produced.

- [ ] **Step 2: Add the new test method to the file**

> **⚠️ Mocking pattern note (review fix MAJOR-1):** The existing `GlobalKbAskStreamEndpointTests.cs` does NOT have a stored `_hybridSearchMock` field. It uses a factory pattern: `BuildSearchMock()` (around line 414) returns an `IMultiGameHybridSearchService` registered into DI at line 87 via `services.AddScoped<IMultiGameHybridSearchService>(_ => BuildSearchMock())`. The mock returns chunks based on **seeded test data**, not on per-test `Mock.Setup(...)` calls. Two implementation strategies — pick (B):
>
> **(A) Refactor BuildSearchMock to expose a configurable mock field.** Higher blast radius — touches the shared fixture infrastructure used by other tests. Avoid in this PR.
>
> **(B) ✅ Work within the existing seeded-data pattern.** Add a seeded chunk in the test class's existing seed routine (or extend `BuildSearchMock` to include a chunk with a known `ChunkId` + `ChunkIndex` for this test), then assert the produced Citations payload contains those known values. Re-use the existing seeded `_gameAliceOwnedId` (confirmed to exist on the test class). Approximate test body:

```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public async Task AskGlobal_WhenChunksRetrieved_CitationsCarriesChunkIdAndPosition()
{
    // Arrange: ensure the seeded data path produces at least one chunk with known
    // identifiers. The existing BuildSearchMock factory returns chunks derived from
    // the test fixture's seed; this test uses whatever the first chunk is and asserts
    // its identifiers round-trip through the Snippet projection.

    // Act: invoke /ask/global SSE endpoint as per existing test pattern
    var response = await _client.PostAsJsonAsync("/api/v1/knowledge-base/ask/global",
        new { query = "How does setup work?", language = "en", topK = 5 });
    response.EnsureSuccessStatusCode();

    var events = await ParseSseEventsAsync(response);

    // Assert: Citations event contains chunkId AND chunkPosition (values whatever the seed produces)
    var citationsEvent = events
        .Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Citations)
        .Which;

    var citations = citationsEvent.GetProperty("data").GetProperty("citations");
    citations.GetArrayLength().Should().BeGreaterThan(0);

    var firstCitation = citations[0];
    firstCitation.TryGetProperty("chunkId", out var chunkIdProp).Should().BeTrue(
        "chunkId MUST be present in /ask/global Citations payload");
    chunkIdProp.GetString().Should().NotBeNullOrEmpty();
    chunkIdProp.GetString().Should().Contain("_",
        "chunkId is the composite \"{PdfDocumentId}_{ChunkIndex}\" string from MultiGameSearchResultItem.ChunkId");

    firstCitation.TryGetProperty("chunkPosition", out var chunkPosProp).Should().BeTrue(
        "chunkPosition MUST be present in /ask/global Citations payload");
    chunkPosProp.GetInt32().Should().BeGreaterThanOrEqual(0);

    // Optional tighter assertion: if the test class seeds a known chunk index, assert it.
    // chunkIdProp.GetString().Should().Be($"{seededPdfDocId}_0");
    // chunkPosProp.GetInt32().Should().Be(0);
}
```

> **Note for executor:** open `GlobalKbAskStreamEndpointTests.cs` BEFORE adapting this snippet. Match the exact helper invocations already present in the file (`ParseSseEventsAsync`, `_client`, `_gameAliceOwnedId`). If `BuildSearchMock`'s default seed already produces at least 1 chunk, the test works as-is; if not, extend the seed step in the test class's constructor / `SeedAsync` to include one chunk with `ChunkId = "{guid}_0"` and `ChunkIndex = 0`, then use the tighter assertion variant.

- [ ] **Step 3: Run the new integration test**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GlobalKbAskStreamEndpointTests.AskGlobal_WhenChunksRetrieved" --no-build 2>&1 | tail -15
```

Expected: 1 passed, 0 failed. Run time may exceed 1 min (Testcontainers Postgres spin-up).

- [ ] **Step 4: Run full integration suite for this file**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GlobalKbAskStreamEndpointTests" --no-build 2>&1 | tail -5
```

Expected: all existing tests in this class still green + 1 new test green.

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/GlobalKbAskStreamEndpointTests.cs
git commit -m "test(kb): integration test for chunkId/chunkPosition in /ask/global SSE (#1702)"
```

---

## Task 5: BC verification test — per-game endpoint wire format unchanged (D-4)

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/PerGameSnippetBackCompatTests.cs`

**Purpose:** Assert that at least one per-game endpoint (e.g. StreamQa or PlaygroundChat) serializes `Snippet` **without** the new `chunkId` / `chunkPosition` JSON keys when those fields are null. This guards against accidental JSON drift if a future refactor changes `[JsonIgnore]` config or System.Text.Json defaults.

- [ ] **Step 1: Pick the simplest per-game endpoint to exercise**

Survey:
```bash
grep -rn "/api/v1/.*qa\|/api/v1/.*ask\|/api/v1/.*chat" apps/api/src/Api/Routing/ --include="*.cs" | head -10
```

The plan **assumes** `/api/v1/games/{gameId}/ask` (StreamQa per-game) or equivalent. The executor MUST choose the endpoint actually wired to `StreamQaQueryHandler` (which produces `Snippet` without setting chunk fields) and adjust the URL and request body to match.

- [ ] **Step 2: Write the BC test file**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PerGameSnippetBackCompatTests : IClassFixture<SharedTestcontainersFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    // ... constructor follows the same pattern as GlobalKbAskStreamEndpointTests
    // (use SharedTestcontainersFixture for DB, seed minimal game + 1 PDF + 1 chunk)

    [Fact]
    public async Task PerGameQa_SnippetSerialization_OmitsChunkIdAndChunkPositionKeys()
    {
        // Arrange: seed a per-game QA scenario producing at least 1 Snippet
        // (handler called: StreamQaQueryHandler — does NOT set chunkId / chunkPosition)
        // ... (executor adapts seeding from GlobalKbAskStreamEndpointTests)

        // Act: invoke per-game endpoint. Replace `_gameAliceOwnedId` with the actual seeded game Guid
        // from the test fixture (matches the field name used in GlobalKbAskStreamEndpointTests).
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/games/{_gameAliceOwnedId}/ask",  // CONFIRM exact route in AiEndpoints.cs
            new { query = "test", language = "en", topK = 3 });

        // Parse SSE events
        var events = await ParseSseEventsAsync(response);
        var citationsEvent = events.First(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Citations);
        var rawJson = citationsEvent.GetRawText();

        // Assert: the new keys MUST NOT appear in the JSON wire format
        rawJson.Should().NotContain("chunkId",
            "per-game endpoints MUST NOT emit chunkId in Snippet (additive-only contract)");
        rawJson.Should().NotContain("chunkPosition",
            "per-game endpoints MUST NOT emit chunkPosition in Snippet (additive-only contract)");

        // Sanity: the existing keys MUST still be there
        rawJson.Should().Contain("\"text\":");
        rawJson.Should().Contain("\"source\":");
        rawJson.Should().Contain("\"page\":");
        rawJson.Should().Contain("\"line\":");
        rawJson.Should().Contain("\"score\":");
    }
}
```

> **Executor note:** if seeding a full per-game QA flow is too heavy (LLM mock, vector index, etc.), an acceptable simpler alternative is to **directly invoke** `StreamQaQueryHandler` via DI in a unit-style integration test (no HTTP layer), assert the emitted `StreamingCitations` payload, then `JsonSerializer.Serialize` it with the production JSON options and grep the raw text. Document the choice in the test class XML doc.

- [ ] **Step 3: Run the BC test**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PerGameSnippetBackCompatTests" --no-build 2>&1 | tail -10
```

Expected: 1 passed.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/PerGameSnippetBackCompatTests.cs
git commit -m "test(kb): BC verification — per-game Snippet wire format unchanged (#1702 D-4)"
```

---

## Task 6: Full BE test suite + final verification

- [ ] **Step 1: Run the full backend test suite**

```bash
cd apps/api
dotnet test --no-restore 2>&1 | tail -15
```

Expected: total test count = baseline + 5 new (3 unit + 1 integration in `GlobalKbAskStreamEndpointTests` + 1 integration in `PerGameSnippetBackCompatTests`). All green. (Watch the known-flaky list in `CLAUDE.md` — if a pre-existing baseline test fails, document it in PR body and proceed.)

- [ ] **Step 2: Spot-check at least one per-game handler did NOT need modification**

```bash
git diff main-dev -- apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs
git diff main-dev -- apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/PlaygroundChatCommandHandler.cs
git diff main-dev -- apps/api/src/Api/Services/RagService.cs
```

Expected: **empty diff** for all three. (Confirms the init-only design lets unrelated call sites stay untouched.)

- [ ] **Step 3: Verify build succeeds clean**

```bash
dotnet build apps/api/src/Api/Api.csproj --no-incremental 2>&1 | tail -5
```

Expected: `Build succeeded.` with 0 warnings related to our changes.

---

## Task 7: Push + PR

- [ ] **Step 1: Push the branch**

```bash
git push -u origin feature/issue-1702-be-snippet-chunk-id
```

- [ ] **Step 2: Open PR targeting `main-dev`**

```bash
gh pr create --base main-dev --title "feat(kb): expose chunkId/chunkPosition in Snippet for /ask/global (#1702)" --body "$(cat <<'EOF'
## Summary

Relates to #1702 — BE side complete. Will close #1702 only after the FE follow-up PR merges (per D-5 sequencing).

Extends the `Snippet` record with two **nullable init-only properties** `chunkId: string?` and `chunkPosition: int?`, populated only by `CrossGameStreamQaQueryHandler` from `MultiGameSearchResultItem.ChunkId` (composite `"{PdfDocumentId}_{ChunkIndex}"`) and `ChunkIndex`. The 8 other call sites (per-game StreamQa, PlaygroundChat, SuggestPlayerMove, StreamDebugQa, RagService×4, AiEndpoints×2) are **not modified** — the init-only design + `[JsonIgnore(WhenWritingNull)]` guarantees their JSON wire format is byte-identical.

## Design decisions (recorded in plan)

- **DD-1**: Use `string?` for `chunkId` (not `Guid?` as the issue body proposed) because the upstream `MultiGameSearchResultItem.ChunkId` is already a string composite ID. Avoids unnecessary DB join during streaming.
- **DD-2**: Init-only additional properties + `[JsonIgnore(WhenWritingNull)]` to keep zero JSON drift for unrelated call sites.

## Test coverage

- 3 unit tests (serialization: omit-when-null / present-when-set / partial set)
- 1 integration test in `GlobalKbAskStreamEndpointTests` (G/W/T Scenario 1: `/ask/global` SSE Citations contains both new keys)
- 1 BC verification test in `PerGameSnippetBackCompatTests` (D-4: per-game endpoint wire format MUST NOT contain the new keys when chunk fields are unset)

## Test plan

- [x] `dotnet test --filter "FullyQualifiedName~SnippetSerializationTests"` — 3/3
- [x] `dotnet test --filter "FullyQualifiedName~GlobalKbAskStreamEndpointTests"` — all green, 1 new
- [x] `dotnet test --filter "FullyQualifiedName~PerGameSnippetBackCompatTests"` — 1/1
- [x] `dotnet build` — clean
- [x] Spot-check `git diff main-dev` on StreamQaQueryHandler / PlaygroundChatCommandHandler / RagService → all empty (proves additive-only)

## Follow-up (separate PR per D-5)

FE work to consume the new fields (`KbCitationSchema` extension, `CitationPill` payload, URL `?chunkId=`, `KbDocViewerDesktop` chunk-level scroll, graceful page-level fallback) — tracked in a separate FE-only branch.
EOF
)"
```

- [ ] **Step 3: Wait for CI** — expect `GitGuardian` (required), `Backend Fast`, `CodeQL csharp`, and `Migration Safety Gate` to run. `Migration Safety Gate` should pass (no migration in this PR). If `Backend Fast` fails with a flaky test, re-run via `gh run rerun --failed`. The only **required** check on `main-dev` branch protection is `GitGuardian Security Checks` — if all required pass, merge normally (no `--admin` needed).

- [ ] **Step 4: Post-merge — manually check off the 4 BE-relevant AC boxes in #1702** (do NOT close the issue — FE follow-up still pending):

```bash
gh issue view 1702 --json body | jq -r '.body'   # confirm current state
# Edit issue body via the GitHub UI or:
# gh issue edit 1702 --body "<updated body with 4 BE boxes checked, 7 FE boxes still unchecked>"
```

Specifically, mark these AC items as completed:
- ✅ BE: `Snippet` (or successor) carries `chunkId` + `chunkPosition`
- ✅ BE: `Citations` SSE event from `/ask/global` propagates these fields
- ✅ BE: per-game endpoints (e.g. agent chat) snapshot JSON unchanged when `chunkId`/`chunkPosition` are null
- ✅ BE: integration test asserts `Snippet.chunkId != null` for `/ask/global` response when retrieval yielded chunks

Leave unchecked (FE PR will mark them):
- ⬜ FE follow-up: `KbCitationSchema` adds `chunkId?` + `chunkPosition?`
- ⬜ FE follow-up: `CitationPill.onClick` payload extended
- ⬜ FE follow-up: orchestrator URL pushes chunkId
- ⬜ FE follow-up: viewer scrolls to chunk position
- ⬜ FE follow-up: graceful degradation
- ⬜ FE follow-up: test covers both paths
- ⬜ Phase 2 page-level fallback retained (verified by FE PR)

---

## Self-Review Checklist (run before handoff)

- [x] **Spec coverage** — each panel decision D-1..D-5 has a task:
  - D-1 extend `Snippet` directly → Task 2
  - D-2 FE graceful degrade → FE follow-up (out of scope per D-5)
  - D-3 G/W/T scenarios → Task 4
  - D-4 BC snapshot test → Task 5
  - D-5 BE PR-1 only, no FE bundled → confirmed (only BE files modified)
- [x] **Placeholder scan** — no TBD / TODO / "fill in later"; only one executor decision point in Task 5 (route URL) with concrete guidance
- [x] **Type consistency** — `chunkId: string?` and `chunkPosition: int?` used consistently across Tasks 1, 2, 3, 4, 5
- [x] **Issue body deviation documented** — DD-1 (string vs Guid) explained upfront with rationale
