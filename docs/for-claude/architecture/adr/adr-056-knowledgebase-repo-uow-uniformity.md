# ADR-056 — Repository UnitOfWork Uniformity in KnowledgeBase BC

**Status**: Accepted
**Date**: 2026-05-13
**Deciders**: @badsworm
**Tracking**: Issue [#942](https://github.com/meepleAi-app/meepleai-monorepo/issues/942) (EPIC #906 follow-up)
**Supersedes**: —

## Context

EPIC #906 SG3 (PR #934) uncovered a behavioural asymmetry between two repositories in the `KnowledgeBase` bounded context:

| Repository | `AddAsync` / `UpdateAsync` / `DeleteAsync` |
|------------|--------------------------------------------|
| `AgentDefinitionRepository` | calls `DbContext.SaveChangesAsync` internally (auto-save) |
| `ChatThreadRepository` | mutates the change-tracker only; persistence requires the caller to invoke `IUnitOfWork.SaveChangesAsync` |

The asymmetry caused a real bug during SG3: `SoftDeleteUserAgentCommandHandler` cascade-closed `ChatThread` aggregates via `_chatThreadRepository.UpdateAsync(...)` expecting the save to happen, but `ChatThreadRepository` returned `Task.CompletedTask` without persisting. The cascade test failed; the handler was patched to inject `IUnitOfWork` and call `SaveChangesAsync` explicitly.

The patch fixed the symptom; the asymmetry remained. Every future handler that consumes both repositories must internalise which one auto-saves and which one doesn't — a trap-prone mental-model burden.

## Decision

**Adopt the UnitOfWork pattern uniformly across all `KnowledgeBase` BC repositories.** All `AddAsync` / `UpdateAsync` / `DeleteAsync` methods mutate the change-tracker only; callers MUST invoke `IUnitOfWork.SaveChangesAsync(ct)` to persist.

## Why option A (UoW everywhere), not option B (auto-save everywhere)

| Concern | UoW everywhere (chosen) | Auto-save everywhere (rejected) |
|---------|-------------------------|--------------------------------|
| Cross-aggregate transactions | ✅ Native — handler aggregates writes, commits once | ❌ Each repo call commits separately; multi-write atomicity lost |
| Cognitive load on handler authors | ⚠️ Must remember to call `SaveChangesAsync` | ✅ Lower (but enables silent partial-write bugs) |
| Dominant existing pattern in BC | ✅ 8 of 18 `IAgentDefinitionRepository` consumers already inject `IUnitOfWork`; `ChatThreadRepository` is already UoW; the broader BC trend is UoW | ❌ Counter to dominant pattern |
| Test isolation | ✅ Tests assert on the same DbContext that the handler used | ⚠️ Auto-save makes test side-effects harder to bound |
| DDD orthodoxy | ✅ Aggregate boundary preserved at handler level | ⚠️ Repo becomes a write-through, not a Repository |

The auto-save model also embeds a footgun in the SG3 cascade scenario: if `AgentDefinitionRepository.UpdateAsync` auto-saves but `ChatThreadRepository.UpdateAsync` doesn't, a handler that updates BOTH inside what looks like a single logical operation actually performs two writes with a window in between. UoW everywhere makes this impossible.

## Consequences

### Positive

- One mental model for all `KnowledgeBase` BC repositories
- Future handlers that touch multiple aggregates are atomic by default
- Cross-aggregate transaction semantics become explicit (handlers declare intent via `SaveChangesAsync` placement)
- Aligns with the existing `ChatThreadRepository` and the 8/18 handlers already using UoW

### Negative

- **Migration cost**: 18 handlers consume `IAgentDefinitionRepository`. 10 of them currently do not inject `IUnitOfWork` and must be updated.
- **Risk of regression**: a handler updated incorrectly (forgetting `SaveChangesAsync`) appears to work in unit tests using in-memory DbContext but fails in integration. Integration tests must run green to confirm migration.
- **Pattern enforcement**: nothing prevents a future repo from re-introducing auto-save. Recommendation: a `// dotnet test` analyzer or a code-review checklist item — see follow-up.

### Neutral

- Other BCs (`Administration`, `Authentication`, etc.) are NOT in scope for this ADR. They may follow either pattern, evaluated independently when their own asymmetries surface.

## Migration plan

1. **Repository changes** (this ADR's implementation PR):
   - `AgentDefinitionRepository.AddAsync`: remove `SaveChangesAsync`
   - `AgentDefinitionRepository.UpdateAsync`: remove `SaveChangesAsync`
   - `AgentDefinitionRepository.DeleteAsync`: remove `SaveChangesAsync`
2. **Handler updates** (this ADR's implementation PR):
   - For each handler using the above 3 methods, inject `IUnitOfWork` (if not already) and call `_unitOfWork.SaveChangesAsync(ct)` immediately after the repo call. 18 handlers — 10 need injection, 8 already have `IUnitOfWork`.
3. **Test verification**:
   - Backend unit tests pass (mocked repos already follow the new contract)
   - Integration tests pass (real DbContext persists as expected)
4. **Docs**: update `docs/for-developers/backend/repository-pattern.md` (or create it) to reference this ADR as the canonical pattern.

## Other repositories in the BC

The following `KnowledgeBase` repositories also expose `Add/Update/Delete` methods. Each is evaluated for compliance with this ADR:

| Repository | Compliant? | Notes |
|---|---|---|
| `ChatThreadRepository` | ✅ Already UoW | Pattern reference |
| `AgentDefinitionRepository` | ❌ Auto-saves | Migrated by this ADR's PR |
| Others (see audit in PR) | TBD | Inventoried during migration; non-compliant ones either migrated or flagged for follow-up |

## Refs

- Issue #942 (implementation tracking)
- PR #934 (SG3 — discovered the asymmetry)
- Commit `d9fc73128` (SG3 patch on `SoftDeleteUserAgentCommandHandler`)
- ADR-055 (auto-revert bot — previous ADR number, unrelated)
- EPIC #906 (parent epic, closed)
