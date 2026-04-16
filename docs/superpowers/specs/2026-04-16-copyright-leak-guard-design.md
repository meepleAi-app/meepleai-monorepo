# Copyright Leak Guard — Design Spec

**Date**: 2026-04-16
**Branch**: `feature/issue-446-447-copyright-guard` (parent: `main-dev`)
**Closes**: #446 (refactor), #447 (hardening)
**Status**: Approved by user · Pending implementation plan
**Superseded issue**: #444 (split via spec-panel review)

---

## 1. Summary

Add a three-layer defense against verbatim leak of copyright-protected content in RAG responses. The first two layers exist; this spec introduces the third (response-body scan) plus cleanup of dead code in the first.

- **Layer 1** (prompt): conditional `## Copyright Notice` block in system prompt when Protected citations exist — *already implemented, refactor required to wire agent language*
- **Layer 2** (DTO): `SnippetPreview` nullified in `CitationDto` for Protected tier — *already implemented, no change*
- **Layer 3** (response body): post-stream n-gram scan against Protected chunk full text, fallback canned message on leak detection — *NEW*

## 2. Context

The app is in **alpha** and not distributed. No legal contracts or compliance SLAs are active. Design decisions optimize for UX (fail-open) over strict compliance; a future split (#448) escalates guards if telemetry shows high false-positive rate or if legal posture changes.

Spec-panel review of the original issue #444 (experts: Wiegers, Adzic, Fowler, Nygard, Crispin) produced a split into #446 (refactor), #447 (hardening), #448 (future).

## 3. Scope

### In scope
- Wire `AgentDefinition.ChatLanguage` to `BuildSystemPrompt`, remove hardcoded `"it"` (closes #446)
- New service `ICopyrightLeakGuard` with `NgramCopyrightLeakGuard` implementation (closes #447)
- New service `ICopyrightFallbackMessageProvider` (localization seam)
- New SSE event `StreamingEventType.CopyrightSanitized` (id 27)
- `ChunkCitation.FullText [JsonIgnore]` field for in-memory-only full text
- Observability: 3 counters + 1 histogram in `MeepleAiMetrics.Rag`
- Configuration: `Copyright:*` section in `appsettings.json`
- Architectural documentation: `docs/architecture/copyright-tier-rag.md`
- Tests: 5 conditional prompt tests, 8 BDD scan scenarios, 4 E2E integration scenarios, 3 provider tests

### Out of scope (tracked in #448)
- Frontend handler for `CopyrightSanitized` event (separate follow-up issue)
- Game glossary whitelist (false-positive mitigation)
- Streaming-aware buffer/sliding-window scan
- LLM re-prompt recovery
- Grafana dashboard for copyright metrics
- Legal sign-off workflow
- Retroactive scan on historical messages

## 4. Architecture

### Component diagram

```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
├── Application/
│   ├── Models/
│   │   └── AssembledPrompt.cs              [MODIFIED]
│   ├── Services/
│   │   ├── RagPromptAssemblyService.cs     [MODIFIED]
│   │   ├── ICopyrightLeakGuard.cs          [NEW]
│   │   ├── NgramCopyrightLeakGuard.cs      [NEW]
│   │   ├── ICopyrightFallbackMessageProvider.cs  [NEW]
│   │   ├── DefaultCopyrightFallbackMessageProvider.cs  [NEW]
│   │   └── CopyrightLeakGuardOptions.cs    [NEW]
│   └── Commands/
│       └── ChatWithSessionAgentCommandHandler.cs  [MODIFIED]
└── Infrastructure/
    └── DependencyInjection/
        └── KnowledgeBaseServiceExtensions.cs  [MODIFIED]

apps/api/src/Api/Observability/Metrics/
└── MeepleAiMetrics.Rag.cs  [MODIFIED]

apps/api/src/Api/appsettings.json  [MODIFIED]

apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/
├── Application/Services/
│   ├── RagPromptAssemblyServiceCopyrightTests.cs    [MODIFIED]
│   ├── NgramCopyrightLeakGuardScenarios.cs          [NEW]
│   └── CopyrightFallbackMessageProviderTests.cs     [NEW]
└── Integration/
    └── CopyrightLeakGuardE2EScenarios.cs            [NEW]

docs/architecture/
└── copyright-tier-rag.md  [NEW]
```

### Data flow

```
User query
  ↓
ChatWithSessionAgentCommandHandler
  ↓  agentLanguage = NormalizeLanguage(agent.ChatLanguage)
RagPromptAssemblyService.AssemblePromptAsync(..., agentLanguage)
  ↓  each ChunkCitation populated with FullText = chunk.Text (in-memory)
Citations[] with FullText
  ↓
CopyrightTierResolver.ResolveAsync
  ↓  tiers assigned (Full/Protected)
LLM stream → accumulate fullResponse
  ↓
[IF any Protected citation exists]
ICopyrightLeakGuard.ScanAsync(fullResponse, protectedCitations, ct)
  ↓
  └─ HasLeak=true
       ↓ emit StreamingEventType.CopyrightSanitized(fallbackMessage)
       ↓ responseText = fallbackMessage
       ↓ metrics: copyright.verbatim_run.detected++
  └─ HasLeak=false → continue
  └─ Exception/Timeout → fail-open (log + metrics, allow response)
  ↓
ParaphraseExtractor.Extract (existing, unchanged)
  ↓
Persist ChatMessage (CitationsJson = JSON WITHOUT FullText due to [JsonIgnore])
  ↓
SSE StreamingComplete → CitationDto[] (existing sanitization)
```

## 5. Key design decisions

| ID | Decision | Rationale |
|---|---|---|
| DD1 | **Compliance framework**: internal policy only (no legal framework) | App is alpha, no contracts active |
| DD2 | **Failure mode**: fail-open (log + allow) | UX priority in alpha; compliance risk minimal pre-distribution |
| DD3 | **Streaming strategy**: scan post-full-response (no streaming buffer) | Simple, no streaming refactor; client-side swap via new SSE event |
| DD4 | **Recovery action**: fallback canned message (no LLM re-prompt) | No +latency/+cost in alpha; degraded UX acceptable for leak case |
| DD5 | **Threshold N**: 12 consecutive words, configurable | Mid-range vs plagiarism detection norms (6-10); buffer against game-term FPs |
| F1 | **FullText persistence**: `[JsonIgnore]` on `ChunkCitation.FullText` | Preserves architectural cleanliness (single source of truth) without DB bloat (~20MB/mo avoided) |
| F2 | **Language normalization**: `"auto"`/`""`/`null` → `"it"` | Alpha is IT-first; per-query language detection deferred to post-alpha |
| F3 | **Service extraction**: `ICopyrightFallbackMessageProvider` separated | Localization seam without handler coupling; future resource-based impl drop-in |

## 6. Interfaces

### `ICopyrightLeakGuard`

```csharp
internal interface ICopyrightLeakGuard
{
    Task<CopyrightLeakResult> ScanAsync(
        string responseBody,
        IReadOnlyList<ChunkCitation> protectedCitations,
        CancellationToken ct);
}

internal sealed record CopyrightLeakResult(
    bool HasLeak,
    IReadOnlyList<LeakMatch> Matches);

internal sealed record LeakMatch(
    string DocumentId,
    int PageNumber,
    int RunLength,
    int BodyStartIndex,
    string MatchedText);
```

### `ICopyrightFallbackMessageProvider`

```csharp
internal interface ICopyrightFallbackMessageProvider
{
    string GetMessage(string language);
}
```

### `CopyrightLeakGuardOptions`

```csharp
internal sealed class CopyrightLeakGuardOptions
{
    public int VerbatimRunThreshold { get; set; } = 12;
    public int ScanTimeoutMs { get; set; } = 500;
    public string FailureMode { get; set; } = "FailOpen";        // reserved for #448
    public string RecoveryAction { get; set; } = "FallbackCanned"; // reserved for #448
}
```

### `ChunkCitation` modification

```csharp
internal sealed record ChunkCitation(
    string DocumentId,
    int PageNumber,
    float RelevanceScore,
    string SnippetPreview,
    CopyrightTier CopyrightTier = CopyrightTier.Protected,
    string? ParaphrasedSnippet = null,
    bool IsPublic = false)
{
    /// <summary>
    /// Full chunk text for copyright leak guard scan.
    /// In-memory only during request lifecycle. NOT persisted ([JsonIgnore]).
    /// Null when re-hydrated from storage (older messages).
    /// </summary>
    [JsonIgnore]
    public string? FullText { get; init; }
}
```

### `StreamingEventType` addition

```csharp
// In apps/api/src/Api/Models/Contracts.cs
internal enum StreamingEventType
{
    // ... existing values including DebugContextWindow = 26 ...
    CopyrightSanitized = 27  // NEW (next free after 26)
}

internal record StreamingCopyrightSanitized(
    string SanitizedBody,
    int MatchCount);
```

Client semantics: receiver should replace previously accumulated token body with `SanitizedBody`. No further `Token` events are expected.

## 7. Algorithm — n-gram consecutive match

### Normalization (applied to body and chunk text)

1. Lowercase (invariant culture)
2. Split on `[\s\.,;:!?()\[\]"']+` regex
3. Filter empty tokens → `string[]`

### Matching per citation

```
source = citation.FullText ?? citation.SnippetPreview
if source.IsNullOrWhiteSpace: skip citation, log warn
sourceTokens = normalize(source)
bodyTokens = normalize(responseBody)
N = options.VerbatimRunThreshold (default 12)

if bodyTokens.Count < N: no match possible
for i in 0..(bodyTokens.Count - N):
    window = bodyTokens[i..i+N]
    if sourceTokens.ContainsSubsequence(window):
        record LeakMatch(documentId, pageNumber, N, bodyStartIndex, matchedText)
        break  # first match per citation
```

**Complexity**: O(|body| × |chunks| × N) — for body=500 tokens, 5 chunks × 1500 tokens, N=12 → ~3.75M ops → <10ms measured.

### Edge cases

- `responseBody` empty → `HasLeak=false`, no scan
- `protectedCitations` empty → skip entirely
- Source empty (FullText and Preview both null/whitespace) → skip citation, log warn
- Timeout → `OperationCanceledException` → caught by handler → fail-open

## 8. Configuration

`apps/api/src/Api/appsettings.json`:

```json
{
  "Copyright": {
    "VerbatimRunThreshold": 12,
    "ScanTimeoutMs": 500,
    "FailureMode": "FailOpen",
    "RecoveryAction": "FallbackCanned"
  }
}
```

Startup validation (DI extension):
- `VerbatimRunThreshold >= 3`
- `ScanTimeoutMs > 0`

## 9. Observability

Additions to `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Rag.cs`:

| Metric | Type | Tags |
|---|---|---|
| `copyright.paraphrase_instruction.injected` | Counter<long> | `has_protected`, `agent_language` |
| `copyright.verbatim_run.detected` | Counter<long> | `run_length`, `document_id` |
| `copyright.guard.scan_errors` | Counter<long> | `error_type` |
| `copyright.guard.scan_duration_ms` | Histogram<long> | _(no tags)_ |

## 10. Handler integration

Insertion in `ChatWithSessionAgentCommandHandler.HandleAsync` (after line 378 `var responseText = fullResponse.ToString();`):

```csharp
var responseText = fullResponse.ToString();
var totalTokens = finalUsage?.TotalTokens ?? 0;

// #447: Copyright leak guard (fail-open)
var protectedCitations = resolvedCitations
    .Where(c => c.CopyrightTier == CopyrightTier.Protected)
    .ToList();

// Note: yield return cannot be inside try-catch (C# compiler restriction,
// see handler line 312). Pattern: collect leakResult in try-catch, then
// emit SSE event outside the try block.
CopyrightLeakResult? leakResult = null;

if (protectedCitations.Count > 0)
{
    try
    {
        using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        scanCts.CancelAfter(TimeSpan.FromMilliseconds(_copyrightOptions.Value.ScanTimeoutMs));

        var scanSw = Stopwatch.StartNew();
        leakResult = await _copyrightLeakGuard
            .ScanAsync(responseText, protectedCitations, scanCts.Token)
            .ConfigureAwait(false);
        scanSw.Stop();

        MeepleAiMetrics.CopyrightScanDurationMs.Record(scanSw.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        MeepleAiMetrics.CopyrightScanErrors.Add(1,
            new KeyValuePair<string, object?>("error_type", ex.GetType().Name));
        _logger.LogError(ex,
            "Copyright leak guard failed for session {SessionId}, allowing response (fail-open)",
            command.AgentSessionId);
        leakResult = null;  // fail-open: treat as no leak
    }
}

// Emit SSE event and apply recovery OUTSIDE try-catch (yield return restriction)
if (leakResult?.HasLeak == true)
{
    foreach (var match in leakResult.Matches)
    {
        MeepleAiMetrics.CopyrightVerbatimDetected.Add(1,
            new KeyValuePair<string, object?>("run_length", match.RunLength),
            new KeyValuePair<string, object?>("document_id", match.DocumentId));
    }

    _logger.LogWarning(
        "Copyright leak detected in session {SessionId}: {MatchCount} matches, sanitizing response",
        command.AgentSessionId, leakResult.Matches.Count);

    var fallbackMessage = _fallbackMessageProvider.GetMessage(agentLanguage);

    yield return CreateEvent(
        StreamingEventType.CopyrightSanitized,
        new StreamingCopyrightSanitized(fallbackMessage, leakResult.Matches.Count));

    responseText = fallbackMessage;
}

// Existing paraphrase extraction + persistence continues...
```

## 11. Testing strategy

### Unit tests

**`RagPromptAssemblyServiceCopyrightTests.cs`** (5 new tests):
- `BuildSystemPrompt_AllFull_OmitsCopyrightNotice`
- `BuildSystemPrompt_AtLeastOneProtected_IncludesNotice_Italian`
- `BuildSystemPrompt_AtLeastOneProtected_IncludesNotice_English`
- `BuildSystemPrompt_ZeroCitations_OmitsCopyrightNotice`
- `BuildSystemPrompt_ProtectedCitations_NoticeBeforeGameRules`

**`NgramCopyrightLeakGuardScenarios.cs`** (8 BDD scenarios, Given/When/Then xUnit):
1. `Given_15VerbatimWords_WhenScanned_ThenLeakDetected`
2. `Given_10ConsecutiveWords_WhenScanned_ThenNoLeakDetected`
3. `Given_WordsInReorderedSentence_WhenScanned_ThenNoLeakDetected`
4. `Given_CaseAndPunctuationDifferent_WhenScanned_ThenLeakDetected`
5. `Given_FullTextAvailable_WhenScanned_ThenScansFullText`
6. `Given_FullTextNull_WhenScanned_ThenFallsBackToPreview`
7. `Given_ScanTimeoutExceeded_WhenScanned_ThenThrowsCancellation`
8. `Given_EmptyBody_WhenScanned_ThenReturnsNoLeak`

**`CopyrightFallbackMessageProviderTests.cs`** (3 tests):
- `GetMessage_It_ReturnsItalianMessage`
- `GetMessage_En_ReturnsEnglishMessage`
- `GetMessage_Unknown_ReturnsEnglishFallback`

### Integration tests

**`CopyrightLeakGuardE2EScenarios.cs`** (4 BDD scenarios, Testcontainers):
9. `Given_NonOwnerUser_WhenLlmLeaks15Words_ThenFallbackMessageEmitted_AndMetricsRecorded`
10. `Given_NonOwnerUser_WhenLlmParaphrases_ThenResponsePassesUnchanged`
11. `Given_OwnerUser_WhenAllCitationsFull_ThenGuardSkipped`
12. `Given_GuardThrowsException_WhenLlmResponds_ThenFailOpen_AndErrorMetricRecorded`

### Coverage targets

| File | Target |
|---|---|
| `NgramCopyrightLeakGuard.cs` | ≥ 95% |
| `DefaultCopyrightFallbackMessageProvider.cs` | 100% |
| `RagPromptAssemblyService.BuildSystemPrompt` (modified) | ≥ 90% both branches |
| `ChatWithSessionAgentCommandHandler` (modified section) | ≥ 85% |
| Project overall | ≥ 90% (no regression) |

### Performance test

- 5 chunks × 1500 tokens, body 500 tokens, 100 runs, p99 < 50ms
- Added as `[Fact]` in scenarios file (no separate benchmark project)

## 12. Commit plan

6 atomic commits on branch `feature/issue-446-447-copyright-guard` (parent `main-dev`):

| # | Commit | Closes/Refs |
|---|---|---|
| 1 | `docs(kb): add copyright tier RAG architecture doc` | refs #446 |
| 2 | `refactor(kb): wire agent language to system prompt assembly` | refs #446 |
| 3 | `test(kb): cover copyright conditional prompt branch` | **closes #446** |
| 4 | `feat(kb): add copyright leak guard infrastructure` | refs #447 |
| 5 | `feat(kb): integrate copyright leak guard in session agent chat` | refs #447 |
| 6 | `test(kb): add BDD scenarios for copyright leak guard` | **closes #447** |

Each commit must leave the codebase in a green state (`dotnet test` passes).

## 13. Known limits (acknowledged, deferred)

1. **Client token-stream exposure**: streaming tokens are emitted to client *before* the post-stream scan. Mitigation: `CopyrightSanitized` SSE event signals client to swap rendered body. Frontend handler is a follow-up issue (out of scope).
2. **Game canonical terms**: no glossary whitelist; canonical phrases (e.g., "Terraforming Rating") may generate false positives. Tracked in #448 C1, trigger: FP rate > 15%.
3. **No retroactive scan**: `FullText` is in-memory only. Historical messages in DB cannot be re-scanned. Acceptable in alpha.
4. **Single-language fallback**: `ICopyrightFallbackMessageProvider` default impl has hardcoded IT/EN. Future resource-based impl planned.

## 14. Rollback plan

Each commit is independently revertable. Full feature rollback:
- `git revert <commit-6>..<commit-4>` → removes guard only, leaves refactor #446 intact
- `git revert <commit-6>..<commit-1>` → removes entire PR

At runtime, `Copyright:VerbatimRunThreshold` can be set to a very high number (e.g., 9999) to effectively disable guard without deploy.

## 15. References

- Original issue: #444 (closed, superseded)
- Refactor issue: #446
- Hardening issue: #447
- Future enhancements: #448
- Spec-panel review: conducted 2026-04-16, panel Wiegers/Adzic/Fowler/Nygard/Crispin
