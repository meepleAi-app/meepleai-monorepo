# Consolidate build*Connections + status-mapping (Issue #2860 / C3) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unify the 3 divergent PDF status-mappers into one shared, enum-exhaustive module (fixing a real gap where Chunking/Embedding/Pending/Uploading rendered as `none`), and disambiguate the two `build*Connections` families by renaming the smaller (`connection-bar`) family to `build*ConnectionPips` + locking every builder's slot order with tests.

**Architecture:** A pure `lib/kb/processing-status.ts` module maps any PDF processing-state string (case-insensitive, alias-aware) to a `KbDisplayStatus`; the 3 existing mappers become thin delegators. The `connection-bar` builders are renamed so no two `build*Connections` functions with incompatible outputs share a name; slot-order snapshot assertions guard both families against silent drift.

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Vitest · Zod (`ProcessingState` enum).

**Spec:** `docs/superpowers/specs/2026-07-13-issue-2860-consolidate-connections-status-design.md`

## Global Constraints

- Work on branch `feature/issue-2860-consolidate-connections-status` (already created from `main-dev`); PR targets `main-dev`.
- Frontend paths under `apps/web/`. Run commands from `apps/web/`. Single-file test: `pnpm exec vitest run <path>`. `pnpm typecheck`, `pnpm lint`, `pnpm build`.
- Canonical `ProcessingState` enum (`apps/web/src/lib/api/schemas/kb-docs.schemas.ts:18-29`): `Pending | Uploading | Extracting | Chunking | Embedding | Indexing | Ready | Failed`.
- **`Pending → 'processing'`** (canonical decision — the 3 mappers currently disagree; this is the resolved value).
- `KbDisplayStatus = 'processing' | 'indexed' | 'failed' | 'none'` (matches `KbDetailData['status']` and both existing mapper return types verbatim).
- `build*Connections` families are NOT merged (incompatible types). Only the `connection-bar` family is renamed; `nav-items` keeps its names.
- No hardcoded color utilities; commit format `type(scope): description` + `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

### Task 1: Shared `processing-status` module

**Files:**
- Create: `apps/web/src/lib/kb/processing-status.ts`
- Test: `apps/web/src/lib/kb/__tests__/processing-status.test.ts`

**Interfaces:**
- Produces: `type KbDisplayStatus = 'processing' | 'indexed' | 'failed' | 'none'` and `mapProcessingStateToDisplayStatus(state: string | null | undefined): KbDisplayStatus`. Consumed by Task 2.

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/kb/__tests__/processing-status.test.ts`:

```ts
import { describe, it, expect } from 'vitest';

import { mapProcessingStateToDisplayStatus } from '../processing-status';

describe('mapProcessingStateToDisplayStatus', () => {
  // Full canonical ProcessingState enum coverage (PascalCase).
  const canonical: Array<[string, 'processing' | 'indexed' | 'failed' | 'none']> = [
    ['Pending', 'processing'], // resolved canonical value (was 'none' in one mapper)
    ['Uploading', 'processing'],
    ['Extracting', 'processing'],
    ['Chunking', 'processing'],
    ['Embedding', 'processing'],
    ['Indexing', 'processing'],
    ['Ready', 'indexed'],
    ['Failed', 'failed'],
  ];

  it.each(canonical)('maps canonical %s -> %s', (state, expected) => {
    expect(mapProcessingStateToDisplayStatus(state)).toBe(expected);
  });

  it.each(canonical)('is case-insensitive: %s lowercased -> %s', (state, expected) => {
    expect(mapProcessingStateToDisplayStatus(state.toLowerCase())).toBe(expected);
  });

  it.each([
    ['completed', 'indexed'] as const,
    ['uploaded', 'processing'] as const,
    ['processing', 'processing'] as const,
  ])('maps legacy alias %s -> %s', (state, expected) => {
    expect(mapProcessingStateToDisplayStatus(state)).toBe(expected);
  });

  // Regression guard: these were dropped to 'none' by use-kb-detail's old statusMap.
  it.each(['Chunking', 'Embedding', 'Pending', 'Uploading'])(
    'previously-dropped state %s now maps to processing',
    state => {
      expect(mapProcessingStateToDisplayStatus(state)).toBe('processing');
    }
  );

  it.each([null, undefined, '', '   ', 'garbage'])('maps %s -> none', state => {
    expect(mapProcessingStateToDisplayStatus(state as string | null | undefined)).toBe('none');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm exec vitest run src/lib/kb/__tests__/processing-status.test.ts`
Expected: FAIL — `Cannot find module '../processing-status'`.

- [ ] **Step 3: Implement the module**

Create `apps/web/src/lib/kb/processing-status.ts`:

```ts
import type { ProcessingState } from '@/lib/api/schemas/kb-docs.schemas';

/**
 * Canonical display status for a KB/PDF document across the app (card badges,
 * drawer, library). Consolidates the three previously-divergent mappers
 * (extra-meeple-card/drawer-helpers, library/kb-utils, use-kb-detail). #2860.
 */
export type KbDisplayStatus = 'processing' | 'indexed' | 'failed' | 'none';

/**
 * Exhaustive over the canonical ProcessingState enum (lowercased). The
 * `satisfies` clause makes a newly-added ProcessingState value a compile error
 * here until it is mapped. Pending -> processing is the resolved canonical value.
 */
const CANONICAL = {
  pending: 'processing',
  uploading: 'processing',
  extracting: 'processing',
  chunking: 'processing',
  embedding: 'processing',
  indexing: 'processing',
  ready: 'indexed',
  failed: 'failed',
} satisfies Record<Lowercase<ProcessingState>, KbDisplayStatus>;

/**
 * Legacy / alternate payload spellings. The /api/v1/pdfs/{id}/text endpoint
 * emits lowercase `uploaded`; some list endpoints emit `completed`/`processing`.
 */
const ALIASES: Record<string, KbDisplayStatus> = {
  completed: 'indexed',
  uploaded: 'processing',
  processing: 'processing',
};

/**
 * Map any PDF processing-state string (canonical PascalCase, lowercase, or a
 * known alias) to a KbDisplayStatus. Unknown / empty -> 'none'.
 */
export function mapProcessingStateToDisplayStatus(
  state: string | null | undefined
): KbDisplayStatus {
  const key = String(state ?? '')
    .trim()
    .toLowerCase();
  return (CANONICAL as Record<string, KbDisplayStatus>)[key] ?? ALIASES[key] ?? 'none';
}
```

- [ ] **Step 4: Run the test to verify it passes + typecheck**

Run: `pnpm exec vitest run src/lib/kb/__tests__/processing-status.test.ts`
Expected: PASS.
Run: `pnpm typecheck`
Expected: PASS (the `satisfies` clause compiles — all 8 lowercase enum keys present).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/kb/processing-status.ts apps/web/src/lib/kb/__tests__/processing-status.test.ts
git commit -m "$(cat <<'EOF'
feat(kb): shared processing-status mapper with enum coverage (#2860)

Case-insensitive, alias-aware map from PDF ProcessingState to KbDisplayStatus.
Pending -> processing (resolved). satisfies Record<Lowercase<ProcessingState>,...>
makes a new enum value a compile error until mapped.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Delegate the 3 mappers to the shared module

**Files:**
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/drawer-helpers.ts:8-25`
- Modify: `apps/web/src/components/library/kb-utils.ts:55-78`
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/hooks/use-kb-detail.ts:54-64`
- Test: update any existing tests for these mappers to the new canonical behavior.

**Interfaces:**
- Consumes: `mapProcessingStateToDisplayStatus` (Task 1).
- Produces: no new exports; the three existing functions keep their signatures and now delegate. Behavior deltas: `drawer-helpers.mapProcessingStateToStatus('Pending')` `'none'→'processing'`; `use-kb-detail` now maps `Chunking/Embedding/Pending/Uploading` `'none'→'processing'`.

- [ ] **Step 1: Delegate `drawer-helpers.ts`**

In `apps/web/src/components/ui/data-display/extra-meeple-card/drawer-helpers.ts`, add the import at the top and replace the `mapProcessingStateToStatus` function body (lines 8-25). Result:

```ts
import { mapProcessingStateToDisplayStatus } from '@/lib/kb/processing-status';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// PDF Document Helpers (Issue #5195)
// ============================================================================

/** Map PdfProcessingState string -> KbDocumentPreview status (#2860: delegates to shared module). */
export function mapProcessingStateToStatus(
  state: string
): 'processing' | 'indexed' | 'failed' | 'none' {
  return mapProcessingStateToDisplayStatus(state);
}
```

(Leave `mapRawToPdfDocumentDto` below it untouched.)

- [ ] **Step 2: Delegate `kb-utils.ts`**

In `apps/web/src/components/library/kb-utils.ts`, add the import and replace the `mapToIndexingStatus` body (lines 55-78):

```ts
import { mapProcessingStateToDisplayStatus } from '@/lib/kb/processing-status';

/**
 * Map processingState (PascalCase) or processingStatus to KbDisplayStatus.
 * #2860: delegates to the shared module.
 */
export function mapToIndexingStatus(doc: {
  processingState?: string;
  processingStatus?: string;
}): 'processing' | 'indexed' | 'failed' | 'none' {
  return mapProcessingStateToDisplayStatus(doc.processingState || doc.processingStatus);
}
```

(Leave `getDocumentStatus` and `isDocumentReady` untouched. Place the import with the other imports at the top of the file.)

- [ ] **Step 3: Delegate `use-kb-detail.ts`**

In `apps/web/src/components/ui/data-display/extra-meeple-card/hooks/use-kb-detail.ts`, add the import at the top, then replace the inline `statusMap` block (lines 55-64) so the `status` const uses the shared module:

Replace:

```ts
  const rawStatus = String(doc.processingStatus ?? 'none').toLowerCase();
  const statusMap: Record<string, KbDetailData['status']> = {
    uploaded: 'processing',
    extracting: 'processing',
    indexing: 'processing',
    indexed: 'indexed',
    failed: 'failed',
    none: 'none',
  };
  const status = statusMap[rawStatus] ?? 'none';
```

with:

```ts
  const status = mapProcessingStateToDisplayStatus(
    doc.processingStatus as string | null | undefined
  );
```

And add to the imports at the top of the file:

```ts
import { mapProcessingStateToDisplayStatus } from '@/lib/kb/processing-status';
```

- [ ] **Step 4: Run the mapper + KB test suites and reconcile behavior deltas**

Run: `pnpm exec vitest run src/components/ui/data-display/extra-meeple-card src/components/library`
Expected: mostly PASS. If any existing test asserted an OLD behavior that this change intentionally corrects, update that assertion to the new canonical value:
- `drawer-helpers` / game-detail: `Pending` now yields `'processing'` (was `'none'`).
- `use-kb-detail`: `Chunking`/`Embedding`/`Pending`/`Uploading` now yield `'processing'` (was `'none'`).
Do NOT revert the delegation to keep an old assertion — the old behavior was the defect being fixed. `kb-utils` had no behavior change (it already mapped Pending -> processing).

- [ ] **Step 5: Typecheck**

Run: `pnpm typecheck`
Expected: PASS. (`mapProcessingStateToDisplayStatus` returns `KbDisplayStatus`, assignable to each mapper's return type and to `KbDetailData['status']` — all are the same 4 literals.)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/extra-meeple-card/drawer-helpers.ts \
        apps/web/src/components/library/kb-utils.ts \
        apps/web/src/components/ui/data-display/extra-meeple-card/hooks/use-kb-detail.ts
# plus any test files you updated in Step 4
git commit -m "$(cat <<'EOF'
refactor(kb): 3 status-mappers delegate to shared processing-status (#2860)

drawer-helpers, kb-utils, and use-kb-detail now call
mapProcessingStateToDisplayStatus. Fixes use-kb-detail dropping
Chunking/Embedding/Pending/Uploading to 'none'; unifies Pending -> processing.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Rename `connection-bar` builders to `build*ConnectionPips`

**Files:**
- Modify: `apps/web/src/components/ui/data-display/connection-bar/build-connections.ts` (rename 9 functions)
- Modify: `apps/web/src/components/ui/data-display/connection-bar/index.ts` (barrel)
- Modify: `apps/web/src/components/agent/AgentCharacterSheet.tsx` (import + call: `buildAgentConnections` -> `buildAgentConnectionPips`)
- Modify: `apps/web/src/components/game-detail/GameDetailDesktop.tsx` (import + call: `buildGameConnections` -> `buildGameConnectionPips`)
- Modify: `apps/web/src/components/ui/data-display/connection-bar/__tests__/build-connections.test.ts` (rename + extend to all 9)

**Interfaces:**
- Produces: `connection-bar` exports become `buildGameConnectionPips`, `buildPlayerConnectionPips`, `buildSessionConnectionPips`, `buildAgentConnectionPips`, `buildKbConnectionPips`, `buildChatConnectionPips`, `buildEventConnectionPips`, `buildToolkitConnectionPips`, `buildToolConnectionPips` (all `→ ConnectionPip[]`). The `nav-items` `build*Connections` names are unchanged.

- [ ] **Step 1: Rename the 9 functions in `build-connections.ts`**

In `apps/web/src/components/ui/data-display/connection-bar/build-connections.ts`, rename each `export function build<Entity>Connections` to `export function build<Entity>ConnectionPips` (bodies unchanged). The 9 names:
`buildGameConnectionPips`, `buildPlayerConnectionPips`, `buildSessionConnectionPips`, `buildAgentConnectionPips`, `buildKbConnectionPips`, `buildChatConnectionPips`, `buildEventConnectionPips`, `buildToolkitConnectionPips`, `buildToolConnectionPips`.

- [ ] **Step 2: Update the barrel `index.ts`**

Replace the export block in `apps/web/src/components/ui/data-display/connection-bar/index.ts`:

```ts
export { ConnectionBar } from './ConnectionBar';
export type { ConnectionPip, ConnectionBarProps } from './types';
export {
  buildGameConnectionPips,
  buildPlayerConnectionPips,
  buildSessionConnectionPips,
  buildAgentConnectionPips,
  buildKbConnectionPips,
  buildChatConnectionPips,
  buildEventConnectionPips,
  buildToolkitConnectionPips,
  buildToolConnectionPips,
} from './build-connections';
```

- [ ] **Step 3: Update the 2 production consumers**

- `apps/web/src/components/agent/AgentCharacterSheet.tsx:29` — change the named import `buildAgentConnections` to `buildAgentConnectionPips`, and rename its call-site(s) in the file (search the file for `buildAgentConnections`).
- `apps/web/src/components/game-detail/GameDetailDesktop.tsx:8` — change the named import `buildGameConnections` to `buildGameConnectionPips`, and rename its call-site(s) in the file (search the file for `buildGameConnections`).

- [ ] **Step 4: Typecheck to catch any missed rename**

Run: `pnpm typecheck`
Expected: PASS. If it reports `buildGameConnections`/`buildAgentConnections` is not exported from `connection-bar`, fix the remaining call-site it names. (The `nav-items` builders keep the old names, so only `connection-bar` importers should be affected.)

- [ ] **Step 5: Rewrite the connection-bar test to cover all 9 with slot-order assertions**

Replace `apps/web/src/components/ui/data-display/connection-bar/__tests__/build-connections.test.ts` with:

```ts
import { describe, it, expect } from 'vitest';

import {
  buildGameConnectionPips,
  buildPlayerConnectionPips,
  buildSessionConnectionPips,
  buildAgentConnectionPips,
  buildKbConnectionPips,
  buildChatConnectionPips,
  buildEventConnectionPips,
  buildToolkitConnectionPips,
  buildToolConnectionPips,
} from '../build-connections';

// Locks each builder's slot order (entityType sequence) against silent drift (#2860).
describe('connection-bar build*ConnectionPips slot order', () => {
  it('game -> agent, kb, chat, session', () => {
    const pips = buildGameConnectionPips({ agentCount: 1, kbCount: 3, chatCount: 5, sessionCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['agent', 'kb', 'chat', 'session']);
  });
  it('player -> session, game', () => {
    const pips = buildPlayerConnectionPips({ sessionCount: 4, favoriteGameCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['session', 'game']);
  });
  it('session -> game, player, tool, agent', () => {
    const pips = buildSessionConnectionPips({ gameCount: 1, playerCount: 4, toolCount: 3, agentCount: 1 });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'player', 'tool', 'agent']);
  });
  it('agent -> game, kb, chat', () => {
    const pips = buildAgentConnectionPips({ gameCount: 1, kbCount: 2, chatCount: 3 });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'kb', 'chat']);
  });
  it('kb -> game, agent', () => {
    const pips = buildKbConnectionPips({ gameCount: 1, agentCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'agent']);
  });
  it('chat -> agent, game', () => {
    const pips = buildChatConnectionPips({ agentCount: 1, gameCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['agent', 'game']);
  });
  it('event -> player, game, session', () => {
    const pips = buildEventConnectionPips({ participantCount: 5, gameCount: 2, sessionCount: 1 });
    expect(pips.map(p => p.entityType)).toEqual(['player', 'game', 'session']);
  });
  it('toolkit -> game, tool, session', () => {
    const pips = buildToolkitConnectionPips({ gameCount: 1, toolCount: 4, sessionCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'tool', 'session']);
  });
  it('tool -> toolkit', () => {
    const pips = buildToolConnectionPips({ toolkitCount: 3 });
    expect(pips.map(p => p.entityType)).toEqual(['toolkit']);
  });
  it('sets isEmpty when count is 0', () => {
    const pips = buildGameConnectionPips({ agentCount: 0, kbCount: 2, chatCount: 0, sessionCount: 1 });
    expect(pips.map(p => p.isEmpty)).toEqual([true, false, true, false]);
  });
});
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `pnpm exec vitest run src/components/ui/data-display/connection-bar`
Expected: PASS (10 tests).

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/connection-bar/build-connections.ts \
        apps/web/src/components/ui/data-display/connection-bar/index.ts \
        apps/web/src/components/agent/AgentCharacterSheet.tsx \
        apps/web/src/components/game-detail/GameDetailDesktop.tsx \
        apps/web/src/components/ui/data-display/connection-bar/__tests__/build-connections.test.ts
git commit -m "$(cat <<'EOF'
refactor(connection-bar): rename builders to build*ConnectionPips + lock slot order (#2860)

Removes the name collision with meeple-card/nav-items build*Connections (which
return the incompatible ConnectionChipProps[]). Snapshot-locks all 9 pip
builders' entityType slot order.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Lock nav-items slot order + document the two families

**Files:**
- Modify (as needed): the 9 `apps/web/src/components/ui/data-display/meeple-card/nav-items/__tests__/build*Connections.test.ts`
- Modify: `docs/for-developers/frontend/card-decision-table.md`

**Interfaces:** none (tests + doc only).

- [ ] **Step 1: Ensure every nav-items builder test asserts its entityType slot order**

For each of the 9 files in `apps/web/src/components/ui/data-display/meeple-card/nav-items/__tests__/`, confirm there is an assertion of the form `expect(items.map(i => i.entityType)).toEqual([...])`. `buildAgentConnections.test.ts:19-22` already has one. For any file lacking it, add a test using the canonical order below (call the builder with all counts > 0 and empty handlers `{}` — handlers are optional):

| Builder | entityType order |
|---|---|
| buildGameConnections | `['kb', 'agent', 'chat', 'session']` |
| buildAgentConnections | `['chat', 'kb', 'agent', 'agent']` (present) |
| buildKbConnections | `['kb', 'kb', 'kb', 'kb']` |
| buildSessionConnections | `['player', 'session', 'tool', 'session']` |
| buildPlayerConnections | `['player', 'session', 'game', 'player']` |
| buildToolConnections | `['tool', 'tool', 'tool', 'tool']` |
| buildToolkitConnections | `['tool', 'toolkit', 'toolkit', 'session']` |
| buildChatConnections | `['chat', 'kb', 'agent', 'chat']` |
| buildEventConnections | `['player', 'event', 'game', 'event']` |

Example assertion to add to a file missing one (adapt the builder call to that builder's `counts`/`handlers` params — read the builder's signature; counts all > 0):

```ts
  it('emits the canonical entityType slot order', () => {
    const items = buildGameConnections(
      { kbCount: 1, agentCount: 1, chatCount: 1, sessionCount: 1 },
      {}
    );
    expect(items.map(i => i.entityType)).toEqual(['kb', 'agent', 'chat', 'session']);
  });
```

- [ ] **Step 2: Run all nav-items builder tests**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/nav-items`
Expected: PASS (all 9 files green, each asserting entityType order).

- [ ] **Step 3: Document the two connection families in the decision-table**

Append a section to `docs/for-developers/frontend/card-decision-table.md`:

```markdown
## Connection builders — two families (#2860)

Two intentionally-separate connection families; do NOT merge them (incompatible
output types). Pick by render surface:

| Family | Builder | Output | Rendered by | Surface |
|---|---|---|---|---|
| Pips | `build*ConnectionPips` (`ui/data-display/connection-bar/`) | `ConnectionPip[]` (icon in payload, count required, `isEmpty`) | `ConnectionBar` | detail pages — read-only cascade-nav strip |
| Chips | `build*Connections` (`ui/data-display/meeple-card/nav-items/`) | `ConnectionChipProps[]` (entity-derived icon, optional count, `items`/`onCreate`/`onClick`/`href`) | `ConnectionChipStrip` → `ConnectionChip` | card footers — nav + actions |

Each builder's slot order is locked by tests in the respective `__tests__/` dir.
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/nav-items/__tests__/ \
        docs/for-developers/frontend/card-decision-table.md
git commit -m "$(cat <<'EOF'
test(meeple-card): lock nav-items connection slot order + document families (#2860)

Ensures every nav-items build*Connections test asserts its entityType slot
order; documents the Pips-vs-Chips family split in the card decision-table.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Final verification + PR

**Files:** none (verification only).

- [ ] **Step 1: Full quality gates**

Run each and confirm PASS:
- `cd apps/web && pnpm exec vitest run src/lib/kb src/components/ui/data-display/connection-bar src/components/ui/data-display/meeple-card/nav-items src/components/ui/data-display/extra-meeple-card src/components/library`
- `cd apps/web && pnpm typecheck`
- `cd apps/web && pnpm lint`
- `cd apps/web && pnpm build`

- [ ] **Step 2: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2860-consolidate-connections-status
gh pr create --base main-dev --title "refactor(meeple-card): consolidate status-mapping + disambiguate connection builders (#2860)" --body "$(cat <<'EOF'
Closes #2860 (C3 / ST6 of umbrella #2863).

## What
- `lib/kb/processing-status.ts`: one case-insensitive, alias-aware mapper (`ProcessingState` -> `KbDisplayStatus`) with compile-time enum coverage (`satisfies`). The 3 divergent mappers (drawer-helpers, kb-utils, use-kb-detail) now delegate. Fixes use-kb-detail dropping Chunking/Embedding/Pending/Uploading to `none`; resolves Pending -> processing.
- `connection-bar` builders renamed `build*ConnectionPips` (removes the name collision with `nav-items` `build*Connections`, which return the incompatible `ConnectionChipProps[]`). Both families' slot orders are locked by tests. Families documented in the card decision-table.

## Design
`docs/superpowers/specs/2026-07-13-issue-2860-consolidate-connections-status-design.md`. The two connection families are NOT merged (incompatible types — verified); only disambiguated + locked.

## Visible delta
The game-detail drawer now shows an "in elaborazione" badge for PDFs in Pending/Chunking/Embedding (previously no badge) — more accurate.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: After CI green + merge — close-out**

Update issue #2860 (state + DoD) and tick its box in umbrella #2863's Phase C checklist.

---

## Self-Review notes (author)

- **Spec coverage:** §3 Part A → Tasks 3-4; §4 Part B → Tasks 1-2; §5 testing → per-task + Task 5; §9 acceptance → Task 5 gates. All mapped.
- **Type consistency:** `KbDisplayStatus` (Task 1) == the 4 literals returned by all 3 mappers and `KbDetailData['status']` (verified types.ts:440) — delegation type-checks. `build*ConnectionPips` names consistent across build-connections.ts, index.ts, consumers, and test (Task 3).
- **Ordering:** Part B (Tasks 1-2) and Part A (Tasks 3-4) are independent; either order works. Task 2 depends on Task 1; Task 3 is self-contained.
- **No merge:** the plan renames only `connection-bar` and never touches `ConnectionPip`/`ConnectionChipProps` types (per spec §7).
