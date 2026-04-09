# Decision Doc: AgentStatsDisplay Integration (Gate G0.3 — #294)

**Date:** 2026-04-09
**Decision Owner:** @user
**Status:** 🟡 DRAFT — awaiting user decision
**GitHub Issue:** meepleAi-app/meepleai-monorepo#294
**Component:** `apps/web/src/components/ui/agent/AgentStatsDisplay.tsx` (95 lines)

## Context

`AgentStatsDisplay` is a horizontal flex layout showing agent metadata (status badge + invocation count + avg response time + capabilities badges + model info). Bound to `AgentMetadata` type from `types/agent.ts`. Has NO consumer today because `AgentCharacterSheet.tsx` uses a custom RPG-style layout (2×2 StatPip grid + mana pips) bound to `AgentDetailData` (different type).

## Current State Analysis

**Admin agent listing page:** `apps/web/src/app/admin/(dashboard)/agents/definitions/page.tsx`
**Current row display:** (TBD — needs verification) likely renders `AgentDefinition` data in a table

**Type mismatch:**
- `AgentStatsDisplay` expects `AgentMetadata { status, model, invocationCount, lastExecuted?, avgResponseTime?, capabilities? }`
- Admin list fetches `AgentDefinition` (different shape)
- A **mapper function** is required: `mapAgentDefinitionToMetadata(def: AgentDefinition): AgentMetadata`

## Options

### Option A — Integrate in admin agent list table rows
**Target:** `apps/web/src/app/admin/(dashboard)/agents/definitions/page.tsx`

**Pros:**
- Admin sees capabilities badges + model at a glance (currently requires opening each agent)
- Compact horizontal layout fits table rows
- Reuses existing component (eliminates orphan status)

**Cons:**
- Table row height increases (design change)
- Requires mapper `AgentDefinition → AgentMetadata`
- Capabilities badges may overflow narrow columns

**Effort:** ~4h (mapper + integration + tests)

### Option B — Integrate as dashboard widget
**Target:** new/existing admin agent dashboard (`admin/(dashboard)/agents/page.tsx` if exists)

**Pros:**
- Can show top-N agents with stats on a single page
- Flexible layout (card grid, not table row)
- Higher visibility

**Cons:**
- Requires a dashboard that may not exist yet
- Larger scope than #294 intended

**Effort:** ~8h (design + integration + mapper + tests)

### Option C — Integrate in admin agent detail page sidebar
**Target:** `apps/web/src/app/admin/(dashboard)/agents/definitions/[id]/page.tsx`

**Pros:**
- Agent detail has space for a stats panel
- Low visual disruption

**Cons:**
- `AgentCharacterSheet.tsx` already shows detailed stats (custom RPG design)
- Would create visual inconsistency between admin and user-facing agent detail
- Potential duplication with existing StatPip grid

**Effort:** ~5h

### Option D — Decline (close #294)
**Pros:**
- Zero risk
- Removes orphan from backlog

**Cons:**
- Loses the at-a-glance capabilities display feature
- Component wasted

**Effort:** 0h

## Recommendation

**Option A — Integrate in admin agent list table rows** — with mapper + column width adjustment.

**Rationale:**
- Scoped to admin only (no user-facing risk)
- Admin benefits most from compact multi-agent comparison
- `AgentCharacterSheet` stays untouched (user detail page, RPG design preserved)
- Mapper is straightforward (most fields map 1:1)
- Smallest effort with clearest value

**Implementation plan:**
1. Create `apps/web/src/lib/mappers/agent.ts` with `mapAgentDefinitionToMetadata()`
2. Verify columns in `admin/(dashboard)/agents/definitions/page.tsx` — add or widen for stats column
3. Replace inline name/status rendering with `<AgentStatsDisplay metadata={map(def)} />` in the chosen column
4. Unit tests: mapper with all field combinations
5. Integration: admin list renders ≥1 row with stats

## Decision

**Choice:** [x] A  [ ] B  [ ] C  [ ] D
**Signed:** @user (autonomous execution delegation)
**Date:** 2026-04-09
**Rationale:** Follow spec-panel recommendation — integrate in admin agent list table rows via mapper `AgentDefinition → AgentMetadata`. Admin-scoped (no user-facing risk), AgentCharacterSheet RPG design preserved.

## Mapper Specification (if A/B/C chosen)

```typescript
// apps/web/src/lib/mappers/agent.ts
import type { AgentDefinition } from '@/types/agent-definition';
import type { AgentMetadata } from '@/types/agent';

export function mapAgentDefinitionToMetadata(def: AgentDefinition): AgentMetadata {
  return {
    status: def.isActive ? 'active' : 'idle', // or derive from def.status
    model: {
      name: def.modelName,
      temperature: def.temperature,
      maxTokens: def.maxTokens,
    },
    invocationCount: def.totalInvocations ?? 0,
    lastExecuted: def.lastInvokedAt ?? undefined,
    avgResponseTime: def.avgResponseTimeMs ?? undefined,
    capabilities: def.capabilities ?? undefined,
  };
}
```

**Actual shape:** verify against current `AgentDefinition` type at execution time. Field names may differ.

## Go/No-Go Criteria

- [ ] Mapper handles all `AgentDefinition` field variations
- [ ] Table column width adjusted (no horizontal scroll)
- [ ] Mobile responsive check (if admin is mobile-usable)
- [ ] Existing admin list tests pass
- [ ] New unit test for mapper
- [ ] Typecheck + lint clean

## If Decision Is D (Decline)

Actions:
1. Close GitHub issue #294 with rationale
2. Mark `AgentStatsDisplay.tsx` for deletion in follow-up cleanup
3. Update `orphan-components-integration-plan.md` to remove #294
