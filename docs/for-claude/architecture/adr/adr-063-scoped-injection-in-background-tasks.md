# ADR-063: Scoped Service Injection in Background Tasks

**Status**: Accepted
**Date**: 2026-06-13
**Deciders**: Tech Lead (spec-panel facilitated, Fowler + Nygard + Crispin + Wiegers)
**Issue**: #2284 (PR B / item #4 of follow-up to #2244)
**Related**: #2244 (closed by PR #2278), #2243 (#2263)

## Context

PR #2278 introduced `IPdfIndexingPipeline` (Scoped) and migrated 5 PDF ingestion call sites to use it. Two distinct injection patterns emerged across the 4 consumer call sites:

| Consumer | Pattern | Rationale |
|---|---|---|
| `IndexPdfCommandHandler.cs:50` | Constructor injection (`IPdfIndexingPipeline pipeline`) | MediatR request handler runs synchronously within request scope |
| `PdfProcessingPipelineService.cs:73` | Constructor injection (`IPdfIndexingPipeline indexingPipeline`) | Quartz job runs within a per-job scope created by Quartz scheduler |
| `UploadPdfCommandHandler.Processing.cs:587` | `scope.ServiceProvider.GetRequiredService<IPdfIndexingPipeline>()` inside `_scopeFactory.CreateScope()` | Background task (`ProcessPdfAsync`) runs **after** the original MediatR request completes |
| `CompleteChunkedUploadCommandHandler.cs:777` | `scope.ServiceProvider.GetRequiredService<IPdfIndexingPipeline>()` inside `_scopeFactory.CreateScope()` | Same as above — finalization runs as background task post-upload |

Issue #2284 (item #4) initially proposed "force ctor injection in all 4 consumers, eliminate scope-based pattern" to mirror Task 4 BLOCKER discipline from the original #2244 plan. **Spec-panel review (Fowler + Nygard) rejected this approach as architecturally unsound** because the two `_scopeFactory.CreateScope()` consumers are background tasks whose pipeline dependency would become a **captive dependency** under constructor injection:

```
1. HTTP request → request scope created → UploadPdfCommandHandler (Transient) instantiated
2. Handle(cmd) schedules ProcessPdfAsync as background task → returns 202 Accepted
3. Request scope DISPOSES → ctor-injected MeepleAiDbContext disposed
4. Background task wakes up → _pipeline.IndexAsync(...) → ObjectDisposedException (silent corruption)
```

`scope.ServiceProvider.GetRequiredService<T>()` confined inside a `using var scope = _scopeFactory.CreateScope()` block is **NOT the Service Locator anti-pattern** — it is the canonical pattern for background tasks needing Scoped dependencies. It is type-safe (compile-time interface contract), fail-fast (`GetRequiredService` throws on missing registration), and lifecycle-correct (DbContext lives for the duration of the using block, not the original request).

## Decision

We accept the **mixed injection convention** for `IPdfIndexingPipeline` (and by extension any Scoped service): constructor injection for request-scoped consumers, scope-based resolution for `_scopeFactory.CreateScope()` background tasks.

### Convention

**Use constructor injection when the consumer:**
- Is a MediatR request handler called synchronously within the request scope (`IRequestHandler<TCommand, TResponse>` whose work completes before `Handle` returns).
- Is a service registered as `Scoped` itself, where the runtime lifetime aligns with the request.
- Is a Quartz job, hosted background service, or other framework that creates a per-invocation scope and resolves the consumer from it.

**Use `scope.ServiceProvider.GetRequiredService<T>()` inside `using var scope = _scopeFactory.CreateScope()` when the consumer:**
- Schedules a `Task.Run(...)`, `Channel<T>` producer, or other fire-and-forget background work that outlives the original request scope.
- Needs to "renew" Scoped dependencies (e.g., a fresh `MeepleAiDbContext`) inside a long-running loop where reusing a captive Scoped instance would exhaust the connection pool or corrupt change tracking.

**Forbidden:**
- `IPdfIndexingPipeline? pipeline = null` optional constructor parameter with a fallback `new VectorDocumentEntity { ... }` else branch. This was the BLOCKER pattern from the original #2244 plan Task 4 and would re-introduce the domain-event-bypass anti-pattern (P234 memory). The current state (mandatory positional ctor argument OR `GetRequiredService<T>()` which throws on missing) closes this hole.
- Field-level Service Locator: `_serviceProvider.GetRequiredService<T>()` accessed from arbitrary method bodies (not scoped to a `using` block). Indistinguishable from generic Service Locator anti-pattern.

### Sub-decisions

1. **`CompleteChunkedUploadCommandHandler.cs:777`** and **`UploadPdfCommandHandler.Processing.cs:587`** retain `scope.ServiceProvider.GetRequiredService<IPdfIndexingPipeline>()` — no migration to ctor injection.
2. **`IndexPdfCommandHandler.cs:50`** and **`PdfProcessingPipelineService.cs:73`** retain constructor injection.
3. **Test fixtures** must pass a concrete `IPdfIndexingPipeline` argument (typically `Mock.Of<IPdfIndexingPipeline>()` for tests that don't exercise the indexing path, or a fully-configured mock for tests that do). The Roslyn analyzer follow-up below codifies the "no `null` default" rule.
4. **`Mock.Of<IPdfIndexingPipeline>()` without `.Verify(...)`** is acceptable for tests that don't directly assert pipeline invocation (e.g., constructor smoke tests, exception-path tests). Tests asserting "indexing succeeded" MUST add `.Verify(p => p.IndexAsync(...), Times.Once())` to prevent silent test rot — see PR B / this commit for the canonical example in `IndexPdfCommandHandlerTests`.

## Spec-panel consensus

| Expert | Vote | Constraint added |
|---|---|---|
| Martin Fowler | ✅ Accept | "Document the mixed convention in `CLAUDE.md` so future readers don't confuse scope-based resolution here with Service Locator anti-pattern elsewhere" |
| Michael Nygard | ✅ Accept | "Forcing ctor injection would be P0 production bug (captive dependency → DbContext disposed → connection pool exhaustion under load). Reject Option C of original PR B framing." |
| Lisa Crispin | ✅ Accept with addendum | "Tests must `Verify` pipeline invocation on happy paths — `Mock.Of<>()` without setup masks silent regression where handler stops calling pipeline" |
| Karl Wiegers | ✅ Accept with clarification | "Item #4 wording in #2284 was ambiguous ('REQUIRED non-nullable ctor parameter'). This ADR resolves the ambiguity: 'mandatory injection (any pattern), no nullable fallback'." |

## Consequences

**Positive:**
- Captive dependency bug avoided.
- Background task lifecycle correctness preserved.
- The two conventions are clearly delimited; readers don't need to guess which to apply.
- Anti-pattern surface (nullable fallback + EF entity construction outside mapper) remains closed.

**Negative:**
- Two patterns coexist in the codebase, requiring documentation discipline to keep clear.
- New contributors may default to ctor injection for background tasks without realizing the lifecycle impact; addressed via ADR + CLAUDE.md reference + (proposed) Roslyn analyzer.

## Implementation

This ADR is shipped together with the following code changes (PR B):

1. **No production code changes** — existing implementations across the 4 consumers already comply with the convention.
2. **Test tightening**: `IndexPdfCommandHandlerTests.Handle_OnSuccessfulIndexing_SetsIsActiveForRagToTrue` and `Handle_SharedGamePdf_PropagatesSharedGameIdToChunks` add `Verify(p => p.IndexAsync(...), Times.Once())` to assert handler invocation; canonical example for future test additions.
3. **No Roslyn analyzer in this PR** — proposed as follow-up issue if regression to nullable fallback or EF-entity construction outside mapper recurs.

## Follow-ups

- **Proposed**: Roslyn analyzer flagging `IPdfIndexingPipeline?` constructor parameters with `= null` default AND `new VectorDocumentEntity {` outside `KnowledgeBaseMappers`. Effort ~3-4h. Open issue if anti-pattern recurs.
- **PR C** (#2284 item #5): `PdfDocument.TransitionTo(Ready)` via `IPdfDocumentRepository` + drop manual `PdfStateChangedEvent` publish in `FinalizeProcessingAsync`. Independent change, has bridge-save tech debt.

## References

- Memory pattern: `P234 domain-event-bypass-via-ef-entity` (`~/.claude/projects/.../memory/p234-domain-event-bypass-via-ef-entity.md`)
- Original ticket: #2244 (closed by #2278)
- Residual tracker: #2284
- Tactical hotfix: #2243 (#2263)
- ASP.NET Core docs on captive dependencies: <https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines#captive-dependency>
