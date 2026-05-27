# Library Hybrid Hub — Phase 1 (Foundation) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lay the type-level + pure-function foundations for the `/library` 6-tab hybrid multi-entity hub (issue #1585) — `HybridHubItem` discriminated union, 5 per-entity mappers, the `deriveHybridItems` merge/filter/sort function, and consolidate the duplicated `LibraryEntityKey` to a single SSOT in the domain module.

**Architecture:** Strict **greenfield + non-breaking** scope. Three new files under `apps/web/src/lib/library/` (types, mappers, derive) — all pure, no React, no API calls, no consumers yet. Plus one SSOT cleanup in `LibraryTabs.tsx` (drop a duplicate `export type`). Nothing existing (`LibraryHub`, `LibraryHybridGrid`, `filterByEntity`) changes behaviorally — those are Phase 2's job when the orchestration arrives. `LibraryEntityKey` expansion 3→6 is **deferred to Phase 2** because it requires `LibraryHub` to gain new tabs and the STATO chip in the same atomic change (Phase 2 owns CrossEntityFilters).

**Tech Stack:** TypeScript 5 · Vitest · React 19 (only type imports) · Zod schemas (consumed via inferred types from `lib/api/schemas/*`)

**Scope tightening vs contract §1**: contract listed "`LibraryEntityKey` 3→6 expansion + `LibraryTabs`/`LibraryHybridGrid` prop adaptation" in Phase 1. Those changes only make sense in lockstep with the new tabs + STATO chip — leaving them here would either break `LibraryHub` (lose the `loaned` tab without a chip replacement) or force a temporary 7-key hybrid type. Both worse than moving the expansion to Phase 2.

---

## File Structure

| File | Responsibility | Status |
|---|---|---|
| `apps/web/src/lib/library/hybrid-hub.types.ts` | `HybridHubItem` discriminated union + 5 sub-types + `HybridHubEntity` literal | Create |
| `apps/web/src/lib/library/hybrid-hub.mappers.ts` | 5 pure mapper functions (DTO → HubItem) + `KbDoc` FE interface (greenfield, replaced by Zod schema when BE-1 #1588 lands) | Create |
| `apps/web/src/lib/library/hybrid-hub.derive.ts` | `deriveHybridItems(sources, tab, query, sort)` + `HybridHubTab` literal | Create |
| `apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts` | Per-mapper test (input DTO → asserted HubItem shape) | Create |
| `apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts` | Merge / tab filter / query match / sort tests | Create |
| `apps/web/src/components/features/library/LibraryTabs.tsx` | Drop duplicate `export type LibraryEntityKey`; re-export from domain via barrel | Modify (line 33) |
| `apps/web/src/components/features/library/index.ts` | Update barrel re-export of `LibraryEntityKey` to source from `lib/library/library-filters` | Modify (line 41) |

**No other file changes.** `library-filters.ts` (`LibraryEntityKey` source-of-truth at line 24) stays at `'all' | 'kb' | 'loaned'`. `LibraryHub`, `LibraryHybridGrid`, `filterByEntity`, all existing tests untouched.

---

### Task 1: Create `hybrid-hub.types.ts` (discriminated union)

**Files:**
- Create: `apps/web/src/lib/library/hybrid-hub.types.ts`

- [ ] **Step 1: Write the file**

```ts
/**
 * Hybrid hub item types — Phase 1 (Issue #1591) foundation for the
 * `/library` multi-entity hub (parent issue #1585).
 *
 * `HybridHubItem` is a discriminated union over 5 entity kinds (game / agent /
 * kb / session / chat). Each variant carries the common `HybridHubItemBase`
 * fields plus its own entity-specific extras.
 *
 * The discriminant is `entity`, which matches `MeepleEntityType` in
 * `components/ui/data-display/meeple-card/types.ts` (a `MeepleCard` accepts
 * all 5). Phase 2 will render `<MeepleCard entity={item.entity} ... />` in
 * the grid for every item, regardless of kind.
 *
 * `updatedAt` is an ISO timestamp string — it drives the cross-entity
 * "recent" sort in `deriveHybridItems`. Each mapper resolves it from the
 * most meaningful timestamp on the source DTO (e.g. `lastInvokedAt` for an
 * agent, falling back to `createdAt`).
 *
 * `href` is the canonical navigation target rendered by the grid card click.
 * Final URL shapes are still subject to routing decisions in Phase 2; the
 * mappers stick to current public routes (`/library/{gameId}`,
 * `/agents/{id}`, `/knowledge-base/{id}`, `/sessions/{id}`, `/chats/{id}`)
 * and any redirect can be done at the routing layer later.
 */

import type { GameStateType } from '@/lib/api/schemas/library.schemas';

export type HybridHubEntity = 'game' | 'agent' | 'kb' | 'session' | 'chat';

export interface HybridHubItemBase {
  readonly id: string;
  readonly entity: HybridHubEntity;
  readonly title: string;
  readonly subtitle?: string;
  readonly updatedAt: string;
  readonly href: string;
}

export interface GameHubItem extends HybridHubItemBase {
  readonly entity: 'game';
  readonly gameId: string;
  readonly rating?: number;
  readonly state?: GameStateType;
  readonly imageUrl?: string;
}

export interface AgentHubItem extends HybridHubItemBase {
  readonly entity: 'agent';
  readonly gameName?: string;
  readonly agentType: string;
  readonly isActive: boolean;
}

export interface KbHubItem extends HybridHubItemBase {
  readonly entity: 'kb';
  readonly gameName?: string;
  readonly processingState: string;
  readonly pageCount?: number;
}

export interface SessionHubItem extends HybridHubItemBase {
  readonly entity: 'session';
  readonly gameName?: string;
  readonly status: string;
  readonly playerCount: number;
}

export interface ChatHubItem extends HybridHubItemBase {
  readonly entity: 'chat';
  readonly gameName?: string;
  readonly messageCount?: number;
}

export type HybridHubItem =
  | GameHubItem
  | AgentHubItem
  | KbHubItem
  | SessionHubItem
  | ChatHubItem;
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `pnpm --filter @meepleai/web typecheck`
Expected: PASS (no errors). This is a pure type-declaration file.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/library/hybrid-hub.types.ts
git commit -m "feat(library): #1591 add HybridHubItem discriminated union (Phase 1)"
```

---

### Task 2: TDD `libraryEntryToHubItem` mapper

**Files:**
- Create: `apps/web/src/lib/library/hybrid-hub.mappers.ts`
- Create: `apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts`

- [ ] **Step 1: Write the failing test**

`apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import { libraryEntryToHubItem } from '../hybrid-hub.mappers';

const baseEntry: UserLibraryEntry = {
  id: '00000000-0000-0000-0000-000000000001',
  userId: '00000000-0000-0000-0000-000000000099',
  gameId: '00000000-0000-0000-0000-0000000000aa',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: 'https://example.test/catan.jpg',
  addedAt: '2026-01-10T12:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: '2026-02-01T08:00:00Z',
  stateNotes: null,
  hasKb: true,
  kbCardCount: 1,
  kbIndexedCount: 1,
  kbProcessingCount: 0,
  ownershipDeclaredAt: null,
  hasRagAccess: true,
  agentIsOwned: true,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.3,
  averageRating: 7.2,
  privateGameId: null,
  isPrivateGame: false,
  canProposeToCatalog: false,
};

describe('libraryEntryToHubItem', () => {
  it('maps a UserLibraryEntry to a GameHubItem with entity="game"', () => {
    const result = libraryEntryToHubItem(baseEntry);
    expect(result.entity).toBe('game');
    expect(result.id).toBe(baseEntry.id);
    expect(result.gameId).toBe(baseEntry.gameId);
    expect(result.title).toBe('Catan');
    expect(result.subtitle).toBe('Kosmos');
    expect(result.rating).toBe(7.2);
    expect(result.state).toBe('Owned');
    expect(result.imageUrl).toBe('https://example.test/catan.jpg');
    expect(result.href).toBe(`/library/${baseEntry.gameId}`);
  });

  it('prefers stateChangedAt over addedAt for updatedAt', () => {
    const result = libraryEntryToHubItem(baseEntry);
    expect(result.updatedAt).toBe('2026-02-01T08:00:00Z');
  });

  it('falls back to addedAt when stateChangedAt is null', () => {
    const result = libraryEntryToHubItem({ ...baseEntry, stateChangedAt: null });
    expect(result.updatedAt).toBe('2026-01-10T12:00:00Z');
  });

  it('falls back to gameIconUrl when gameImageUrl is null', () => {
    const result = libraryEntryToHubItem({
      ...baseEntry,
      gameImageUrl: null,
      gameIconUrl: 'https://example.test/catan-icon.png',
    });
    expect(result.imageUrl).toBe('https://example.test/catan-icon.png');
  });

  it('returns undefined imageUrl when both image and icon are null', () => {
    const result = libraryEntryToHubItem({
      ...baseEntry,
      gameImageUrl: null,
      gameIconUrl: null,
    });
    expect(result.imageUrl).toBeUndefined();
  });

  it('returns undefined subtitle when gamePublisher is null', () => {
    const result = libraryEntryToHubItem({ ...baseEntry, gamePublisher: null });
    expect(result.subtitle).toBeUndefined();
  });

  it('returns undefined rating when averageRating is null', () => {
    const result = libraryEntryToHubItem({ ...baseEntry, averageRating: null });
    expect(result.rating).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: FAIL — "Cannot find module '../hybrid-hub.mappers'" (file does not exist yet).

- [ ] **Step 3: Write minimal implementation**

`apps/web/src/lib/library/hybrid-hub.mappers.ts`:

```ts
/**
 * Pure mappers DTO → HybridHubItem — Phase 1 (Issue #1591).
 *
 * Each function is a deterministic transform over a single source DTO; no IO,
 * no React, no global state — so unit-testable in isolation. Phase 2's
 * orchestrator will call these inside `useMemo` after the per-entity hooks
 * resolve.
 *
 * Conventions:
 *   - `subtitle`, `gameName`, optional fields: convert `null` from the DTO to
 *     `undefined` (the FE convention for "absent"); the union types only model
 *     `undefined`.
 *   - `updatedAt`: pick the most meaningful recency signal on the source.
 *   - `href`: stick to current public routes; final routing in Phase 2.
 */

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import type {
  AgentHubItem,
  ChatHubItem,
  GameHubItem,
  KbHubItem,
  SessionHubItem,
} from './hybrid-hub.types';

export function libraryEntryToHubItem(entry: UserLibraryEntry): GameHubItem {
  return {
    id: entry.id,
    entity: 'game',
    title: entry.gameTitle,
    subtitle: entry.gamePublisher ?? undefined,
    updatedAt: entry.stateChangedAt ?? entry.addedAt,
    href: `/library/${entry.gameId}`,
    gameId: entry.gameId,
    rating: entry.averageRating ?? undefined,
    state: entry.currentState,
    imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: PASS — all 7 `libraryEntryToHubItem` tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/library/hybrid-hub.mappers.ts apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts
git commit -m "feat(library): #1591 add libraryEntryToHubItem mapper (Phase 1)"
```

---

### Task 3: TDD `agentToHubItem` mapper

**Files:**
- Modify: `apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts` (add tests)
- Modify: `apps/web/src/lib/library/hybrid-hub.mappers.ts` (add function)

- [ ] **Step 1: Write the failing test (append to the existing file)**

Append to `hybrid-hub.mappers.test.ts`:

```ts
import { agentToHubItem } from '../hybrid-hub.mappers';

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

const baseAgent: AgentDto = {
  id: '00000000-0000-0000-0000-0000000000b1',
  name: 'Catan Tutor',
  type: 'Tutor',
  strategyName: 'HybridSearch',
  strategyParameters: {},
  isActive: true,
  createdAt: '2026-01-15T09:00:00Z',
  lastInvokedAt: '2026-03-12T18:30:00Z',
  invocationCount: 47,
  isRecentlyUsed: true,
  isIdle: false,
  gameId: '00000000-0000-0000-0000-0000000000aa',
  gameName: 'Catan',
  createdByUserId: '00000000-0000-0000-0000-000000000099',
};

describe('agentToHubItem', () => {
  it('maps an AgentDto to an AgentHubItem with entity="agent"', () => {
    const result = agentToHubItem(baseAgent);
    expect(result.entity).toBe('agent');
    expect(result.id).toBe(baseAgent.id);
    expect(result.title).toBe('Catan Tutor');
    expect(result.subtitle).toBe('Catan');
    expect(result.gameName).toBe('Catan');
    expect(result.agentType).toBe('Tutor');
    expect(result.isActive).toBe(true);
    expect(result.href).toBe(`/agents/${baseAgent.id}`);
  });

  it('prefers lastInvokedAt over createdAt for updatedAt', () => {
    const result = agentToHubItem(baseAgent);
    expect(result.updatedAt).toBe('2026-03-12T18:30:00Z');
  });

  it('falls back to createdAt when lastInvokedAt is null', () => {
    const result = agentToHubItem({ ...baseAgent, lastInvokedAt: null });
    expect(result.updatedAt).toBe('2026-01-15T09:00:00Z');
  });

  it('returns undefined gameName/subtitle when the agent is not game-bound', () => {
    const result = agentToHubItem({ ...baseAgent, gameName: null, gameId: null });
    expect(result.gameName).toBeUndefined();
    expect(result.subtitle).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: FAIL — "agentToHubItem is not a function" or "no exported member 'agentToHubItem'".

- [ ] **Step 3: Add the implementation to `hybrid-hub.mappers.ts`**

Append the import and function:

```ts
// add to the existing imports block:
// import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

export function agentToHubItem(agent: AgentDto): AgentHubItem {
  return {
    id: agent.id,
    entity: 'agent',
    title: agent.name,
    subtitle: agent.gameName ?? undefined,
    updatedAt: agent.lastInvokedAt ?? agent.createdAt,
    href: `/agents/${agent.id}`,
    gameName: agent.gameName ?? undefined,
    agentType: agent.type,
    isActive: agent.isActive,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: PASS — all `libraryEntryToHubItem` + 4 new `agentToHubItem` tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/library/hybrid-hub.mappers.ts apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts
git commit -m "feat(library): #1591 add agentToHubItem mapper (Phase 1)"
```

---

### Task 4: TDD `kbDocToHubItem` mapper (with greenfield `KbDoc` FE interface)

**Files:**
- Modify: `apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts` (add tests)
- Modify: `apps/web/src/lib/library/hybrid-hub.mappers.ts` (add `KbDoc` interface + function)

**Note:** `KbDoc` is a **greenfield FE interface** in this file because BE-1 #1588 (the cross-game `GET /kb-docs?userId` endpoint) is not delivered yet. When it lands, the interface here will be replaced by an inferred Zod schema in `lib/api/schemas/`; the mapper signature stays the same.

- [ ] **Step 1: Write the failing test (append to existing file)**

```ts
import { kbDocToHubItem, type KbDoc } from '../hybrid-hub.mappers';

const baseKbDoc: KbDoc = {
  id: '00000000-0000-0000-0000-0000000000c1',
  gameId: '00000000-0000-0000-0000-0000000000aa',
  gameName: 'Catan',
  fileName: 'catan-rulebook-en.pdf',
  processingState: 'Ready',
  pageCount: 24,
  processedAt: '2026-02-15T11:20:00Z',
  updatedAt: '2026-02-15T11:20:00Z',
};

describe('kbDocToHubItem', () => {
  it('maps a KbDoc to a KbHubItem with entity="kb"', () => {
    const result = kbDocToHubItem(baseKbDoc);
    expect(result.entity).toBe('kb');
    expect(result.id).toBe(baseKbDoc.id);
    expect(result.title).toBe('catan-rulebook-en.pdf');
    expect(result.subtitle).toBe('Catan');
    expect(result.gameName).toBe('Catan');
    expect(result.processingState).toBe('Ready');
    expect(result.pageCount).toBe(24);
    expect(result.updatedAt).toBe('2026-02-15T11:20:00Z');
    expect(result.href).toBe(`/knowledge-base/${baseKbDoc.id}`);
  });

  it('returns undefined gameName/subtitle when the doc is not game-attached', () => {
    const result = kbDocToHubItem({ ...baseKbDoc, gameName: null, gameId: null });
    expect(result.gameName).toBeUndefined();
    expect(result.subtitle).toBeUndefined();
  });

  it('returns undefined pageCount when the field is absent', () => {
    const result = kbDocToHubItem({ ...baseKbDoc, pageCount: null });
    expect(result.pageCount).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: FAIL — "no exported member 'KbDoc'" / "kbDocToHubItem is not a function".

- [ ] **Step 3: Add the interface + implementation**

Append to `hybrid-hub.mappers.ts`:

```ts
/**
 * KbDoc — greenfield FE interface for the cross-game user KB listing.
 *
 * This shape will be replaced by an inferred Zod schema in
 * `lib/api/schemas/kb-docs.schemas.ts` when BE-1 #1588 ships the
 * `GET /kb-docs?userId` endpoint. The fields here mirror what we expect from
 * `PdfDocumentEntity` filtered by `UploadedByUserId` (see issue #1588 body).
 * Keep this stable: replacing the type later should be a zero-touch swap.
 */
export interface KbDoc {
  readonly id: string;
  readonly gameId: string | null;
  readonly gameName: string | null;
  readonly fileName: string;
  readonly processingState: string;
  readonly pageCount?: number | null;
  readonly processedAt: string | null;
  readonly updatedAt: string;
}

export function kbDocToHubItem(doc: KbDoc): KbHubItem {
  return {
    id: doc.id,
    entity: 'kb',
    title: doc.fileName,
    subtitle: doc.gameName ?? undefined,
    updatedAt: doc.updatedAt,
    href: `/knowledge-base/${doc.id}`,
    gameName: doc.gameName ?? undefined,
    processingState: doc.processingState,
    pageCount: doc.pageCount ?? undefined,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: PASS — all previous + 3 new `kbDocToHubItem` tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/library/hybrid-hub.mappers.ts apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts
git commit -m "feat(library): #1591 add kbDocToHubItem mapper + KbDoc FE interface (Phase 1)"
```

---

### Task 5: TDD `sessionToHubItem` mapper

**Files:**
- Modify: `apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts` (add tests)
- Modify: `apps/web/src/lib/library/hybrid-hub.mappers.ts` (add function)

**Note**: `GameSessionDto` does **not** carry a `gameName` field (see `games.schemas.ts:95-108`). The mapper leaves `gameName` undefined; Phase 2's orchestrator will enrich the items via a client-side `gameId → title` lookup against the user's library cache. The `title` field uses a short-id fallback so the grid card always shows something even before enrichment.

- [ ] **Step 1: Write the failing test**

```ts
import { sessionToHubItem } from '../hybrid-hub.mappers';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

const baseSession: GameSessionDto = {
  id: '00000000-0000-0000-0000-0000000000d1',
  gameId: '00000000-0000-0000-0000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-04-20T19:00:00Z',
  completedAt: '2026-04-20T21:30:00Z',
  playerCount: 4,
  players: [],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 150,
};

describe('sessionToHubItem', () => {
  it('maps a GameSessionDto to a SessionHubItem with entity="session"', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.entity).toBe('session');
    expect(result.id).toBe(baseSession.id);
    expect(result.status).toBe('Completed');
    expect(result.playerCount).toBe(4);
    expect(result.subtitle).toBe('Alice');
    expect(result.href).toBe(`/sessions/${baseSession.id}`);
  });

  it('leaves gameName undefined (DTO does not include it; Phase 2 enrich)', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.gameName).toBeUndefined();
  });

  it('prefers completedAt over startedAt for updatedAt', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.updatedAt).toBe('2026-04-20T21:30:00Z');
  });

  it('falls back to startedAt when completedAt is null', () => {
    const result = sessionToHubItem({ ...baseSession, completedAt: null });
    expect(result.updatedAt).toBe('2026-04-20T19:00:00Z');
  });

  it('uses a short-id fallback title (Phase 2 will enrich with game name)', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.title).toBe('Session 00000000');
  });

  it('returns undefined subtitle when there is no winner', () => {
    const result = sessionToHubItem({ ...baseSession, winnerName: null });
    expect(result.subtitle).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: FAIL — "sessionToHubItem is not a function".

- [ ] **Step 3: Add implementation**

```ts
// add to imports:
// import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

export function sessionToHubItem(session: GameSessionDto): SessionHubItem {
  return {
    id: session.id,
    entity: 'session',
    title: `Session ${session.id.slice(0, 8)}`,
    subtitle: session.winnerName ?? undefined,
    updatedAt: session.completedAt ?? session.startedAt,
    href: `/sessions/${session.id}`,
    gameName: undefined,
    status: session.status,
    playerCount: session.playerCount,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: PASS — all previous + 6 new tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/library/hybrid-hub.mappers.ts apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts
git commit -m "feat(library): #1591 add sessionToHubItem mapper (Phase 1)"
```

---

### Task 6: TDD `chatToHubItem` mapper

**Files:**
- Modify: `apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts` (add tests)
- Modify: `apps/web/src/lib/library/hybrid-hub.mappers.ts` (add function)

- [ ] **Step 1: Write the failing test**

```ts
import { chatToHubItem } from '../hybrid-hub.mappers';

import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

const baseChat: ChatSessionSummaryDto = {
  id: '00000000-0000-0000-0000-0000000000e1',
  userId: '00000000-0000-0000-0000-000000000099',
  gameId: '00000000-0000-0000-0000-0000000000aa',
  gameTitle: 'Catan',
  agentId: '00000000-0000-0000-0000-0000000000b1',
  agentType: 'Tutor',
  agentName: 'Catan Tutor',
  title: 'How does the longest road work?',
  messageCount: 8,
  lastMessagePreview: 'The road must consist of...',
  createdAt: '2026-04-22T10:00:00Z',
  lastMessageAt: '2026-04-22T10:15:00Z',
  isArchived: false,
};

describe('chatToHubItem', () => {
  it('maps a ChatSessionSummaryDto to a ChatHubItem with entity="chat"', () => {
    const result = chatToHubItem(baseChat);
    expect(result.entity).toBe('chat');
    expect(result.id).toBe(baseChat.id);
    expect(result.title).toBe('How does the longest road work?');
    expect(result.subtitle).toBe('Catan');
    expect(result.gameName).toBe('Catan');
    expect(result.messageCount).toBe(8);
    expect(result.href).toBe(`/chats/${baseChat.id}`);
  });

  it('prefers lastMessageAt over createdAt for updatedAt', () => {
    const result = chatToHubItem(baseChat);
    expect(result.updatedAt).toBe('2026-04-22T10:15:00Z');
  });

  it('falls back to createdAt when lastMessageAt is null', () => {
    const result = chatToHubItem({ ...baseChat, lastMessageAt: null });
    expect(result.updatedAt).toBe('2026-04-22T10:00:00Z');
  });

  it('uses agentName as subtitle when gameTitle is null', () => {
    const result = chatToHubItem({ ...baseChat, gameTitle: null });
    expect(result.subtitle).toBe('Catan Tutor');
  });

  it('uses a short-id fallback title when title is null', () => {
    const result = chatToHubItem({ ...baseChat, title: null });
    expect(result.title).toBe('Chat 00000000');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: FAIL — "chatToHubItem is not a function".

- [ ] **Step 3: Add implementation**

```ts
// add to imports:
// import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

export function chatToHubItem(chat: ChatSessionSummaryDto): ChatHubItem {
  return {
    id: chat.id,
    entity: 'chat',
    title: chat.title ?? `Chat ${chat.id.slice(0, 8)}`,
    subtitle: chat.gameTitle ?? chat.agentName ?? undefined,
    updatedAt: chat.lastMessageAt ?? chat.createdAt,
    href: `/chats/${chat.id}`,
    gameName: chat.gameTitle ?? undefined,
    messageCount: chat.messageCount,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.mappers`
Expected: PASS — all mappers (~25 tests total) green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/library/hybrid-hub.mappers.ts apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts
git commit -m "feat(library): #1591 add chatToHubItem mapper (Phase 1)"
```

---

### Task 7: TDD `deriveHybridItems` (merge / tab filter / query / sort)

**Files:**
- Create: `apps/web/src/lib/library/hybrid-hub.derive.ts`
- Create: `apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts`

This is the **multi-source orchestration shape** that Phase 2's `LibraryHub` will plug per-entity hook outputs into. It's a pure function — no React, no hooks — so it's testable in isolation. The `HybridHubTab` literal here is **not** the same as `LibraryEntityKey` (which still uses 3 keys until Phase 2 expands it); it's a Phase-1-internal type that ships alongside the derivation.

- [ ] **Step 1: Write the failing test**

`apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { deriveHybridItems, type HybridHubSources, type HybridHubTab } from '../hybrid-hub.derive';
import type {
  AgentHubItem,
  ChatHubItem,
  GameHubItem,
  KbHubItem,
  SessionHubItem,
} from '../hybrid-hub.types';

function gameItem(overrides: Partial<GameHubItem>): GameHubItem {
  return {
    id: 'g1',
    entity: 'game',
    title: 'Catan',
    subtitle: 'Kosmos',
    updatedAt: '2026-04-01T00:00:00Z',
    href: '/library/g1',
    gameId: 'g1',
    rating: 7.2,
    state: 'Owned',
    ...overrides,
  };
}
function agentItem(overrides: Partial<AgentHubItem>): AgentHubItem {
  return {
    id: 'a1',
    entity: 'agent',
    title: 'Tutor',
    updatedAt: '2026-04-02T00:00:00Z',
    href: '/agents/a1',
    agentType: 'Tutor',
    isActive: true,
    ...overrides,
  };
}
function kbItem(overrides: Partial<KbHubItem>): KbHubItem {
  return {
    id: 'k1',
    entity: 'kb',
    title: 'rules.pdf',
    updatedAt: '2026-04-03T00:00:00Z',
    href: '/knowledge-base/k1',
    processingState: 'Ready',
    ...overrides,
  };
}
function sessionItem(overrides: Partial<SessionHubItem>): SessionHubItem {
  return {
    id: 's1',
    entity: 'session',
    title: 'Session s1',
    updatedAt: '2026-04-04T00:00:00Z',
    href: '/sessions/s1',
    status: 'Completed',
    playerCount: 4,
    ...overrides,
  };
}
function chatItem(overrides: Partial<ChatHubItem>): ChatHubItem {
  return {
    id: 'c1',
    entity: 'chat',
    title: 'Question',
    updatedAt: '2026-04-05T00:00:00Z',
    href: '/chats/c1',
    messageCount: 4,
    ...overrides,
  };
}

function makeSources(overrides: Partial<HybridHubSources> = {}): HybridHubSources {
  return {
    games: [gameItem({})],
    agents: [agentItem({})],
    kb: [kbItem({})],
    sessions: [sessionItem({})],
    chat: [chatItem({})],
    ...overrides,
  };
}

describe('deriveHybridItems — tab filtering', () => {
  it('"all" tab returns the merged union of every source', () => {
    const result = deriveHybridItems(makeSources(), 'all', '', 'recent');
    expect(result).toHaveLength(5);
    expect(new Set(result.map(it => it.entity))).toEqual(
      new Set(['game', 'agent', 'kb', 'session', 'chat'])
    );
  });

  const cases: ReadonlyArray<readonly [HybridHubTab, GameHubItem['entity']]> = [
    ['games', 'game'],
    ['agents', 'agent'],
    ['kb', 'kb'],
    ['sessions', 'session'],
    ['chat', 'chat'],
  ];
  it.each(cases)('tab "%s" returns only %s items', (tab, entity) => {
    const result = deriveHybridItems(makeSources(), tab, '', 'recent');
    expect(result).toHaveLength(1);
    expect(result[0]?.entity).toBe(entity);
  });
});

describe('deriveHybridItems — query matching', () => {
  it('empty query returns every item in the tab', () => {
    const result = deriveHybridItems(makeSources(), 'all', '', 'recent');
    expect(result).toHaveLength(5);
  });

  it('matches title (case-insensitive)', () => {
    const sources = makeSources({
      games: [gameItem({ title: 'Catan' }), gameItem({ id: 'g2', title: 'Carcassonne' })],
    });
    const result = deriveHybridItems(sources, 'games', 'cat', 'recent');
    expect(result.map(it => it.id)).toEqual(['g1']);
  });

  it('matches subtitle (case-insensitive)', () => {
    const sources = makeSources({
      games: [gameItem({ id: 'g1', subtitle: 'Kosmos' }), gameItem({ id: 'g2', subtitle: 'Z-Man' })],
    });
    const result = deriveHybridItems(sources, 'games', 'z-MAN', 'recent');
    expect(result.map(it => it.id)).toEqual(['g2']);
  });

  it('trims whitespace before matching', () => {
    const result = deriveHybridItems(makeSources(), 'games', '   ', 'recent');
    expect(result).toHaveLength(1);
  });
});

describe('deriveHybridItems — sort', () => {
  it('"recent" sorts by updatedAt descending across entities', () => {
    const sources = makeSources({
      games: [gameItem({ updatedAt: '2026-04-01T00:00:00Z' })],
      agents: [agentItem({ updatedAt: '2026-04-05T00:00:00Z' })],
      kb: [kbItem({ updatedAt: '2026-04-03T00:00:00Z' })],
      sessions: [sessionItem({ updatedAt: '2026-04-02T00:00:00Z' })],
      chat: [chatItem({ updatedAt: '2026-04-04T00:00:00Z' })],
    });
    const result = deriveHybridItems(sources, 'all', '', 'recent');
    expect(result.map(it => it.entity)).toEqual(['agent', 'chat', 'kb', 'session', 'game']);
  });

  it('"title" sorts alphabetically, case-insensitive', () => {
    const sources = makeSources({
      games: [
        gameItem({ id: 'g1', title: 'Carcassonne' }),
        gameItem({ id: 'g2', title: 'azul' }),
        gameItem({ id: 'g3', title: 'Brass' }),
      ],
      agents: [],
      kb: [],
      sessions: [],
      chat: [],
    });
    const result = deriveHybridItems(sources, 'all', '', 'title');
    expect(result.map(it => it.id)).toEqual(['g2', 'g3', 'g1']);
  });

  it('"rating" places game items by rating desc; non-games sink to the bottom', () => {
    const sources = makeSources({
      games: [
        gameItem({ id: 'g1', rating: 7.0 }),
        gameItem({ id: 'g2', rating: 9.0 }),
        gameItem({ id: 'g3', rating: undefined }),
      ],
      agents: [agentItem({})],
      kb: [],
      sessions: [],
      chat: [],
    });
    const result = deriveHybridItems(sources, 'all', '', 'rating');
    expect(result.map(it => it.id)).toEqual(['g2', 'g1', 'g3', 'a1']);
  });

  it('"state" places game items first ordered by state; non-games sink', () => {
    const sources = makeSources({
      games: [
        gameItem({ id: 'g1', state: 'Wishlist' }),
        gameItem({ id: 'g2', state: 'Owned' }),
        gameItem({ id: 'g3', state: 'InPrestito' }),
      ],
      agents: [agentItem({})],
      kb: [],
      sessions: [],
      chat: [],
    });
    const result = deriveHybridItems(sources, 'all', '', 'state');
    expect(result.map(it => it.id)).toEqual(['g2', 'g3', 'g1', 'a1']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.derive`
Expected: FAIL — "Cannot find module '../hybrid-hub.derive'".

- [ ] **Step 3: Write the implementation**

`apps/web/src/lib/library/hybrid-hub.derive.ts`:

```ts
/**
 * deriveHybridItems — pure merge/filter/sort function for the hybrid hub.
 * Phase 1 (Issue #1591) foundation; Phase 2's `LibraryHub` will call this
 * with the 5 mapped per-entity arrays once the orchestration hooks are wired.
 *
 * `HybridHubTab` is a Phase-1-internal literal; the top-level `LibraryEntityKey`
 * stays at `'all' | 'kb' | 'loaned'` until Phase 2's tab expansion + STATO
 * chip ship together (the two changes only make sense in one atomic step).
 */

import type { LibrarySortKey } from './library-filters';
import type { HybridHubEntity, HybridHubItem } from './hybrid-hub.types';

export type HybridHubTab = 'all' | 'games' | 'agents' | 'kb' | 'sessions' | 'chat';

export interface HybridHubSources {
  readonly games: readonly HybridHubItem[];
  readonly agents: readonly HybridHubItem[];
  readonly kb: readonly HybridHubItem[];
  readonly sessions: readonly HybridHubItem[];
  readonly chat: readonly HybridHubItem[];
}

const TAB_TO_ENTITY: Record<Exclude<HybridHubTab, 'all'>, HybridHubEntity> = {
  games: 'game',
  agents: 'agent',
  kb: 'kb',
  sessions: 'session',
  chat: 'chat',
};

const STATE_ORDER: Record<string, number> = {
  Owned: 0,
  Nuovo: 1,
  InPrestito: 2,
  Wishlist: 3,
};

export function deriveHybridItems(
  sources: HybridHubSources,
  tab: HybridHubTab,
  query: string,
  sort: LibrarySortKey
): HybridHubItem[] {
  const merged: HybridHubItem[] = [
    ...sources.games,
    ...sources.agents,
    ...sources.kb,
    ...sources.sessions,
    ...sources.chat,
  ];
  const tabFiltered =
    tab === 'all' ? merged : merged.filter(it => it.entity === TAB_TO_ENTITY[tab]);
  const queryFiltered = matchHybridQuery(tabFiltered, query);
  return sortHybridItems(queryFiltered, sort);
}

function matchHybridQuery(
  items: readonly HybridHubItem[],
  query: string
): HybridHubItem[] {
  const trimmed = query.trim().toLowerCase();
  if (trimmed.length === 0) return [...items];
  return items.filter(
    it =>
      it.title.toLowerCase().includes(trimmed) ||
      (it.subtitle?.toLowerCase().includes(trimmed) ?? false)
  );
}

function sortHybridItems(
  items: readonly HybridHubItem[],
  sort: LibrarySortKey
): HybridHubItem[] {
  const copy = [...items];
  switch (sort) {
    case 'title':
      return copy.sort((a, b) =>
        a.title.localeCompare(b.title, undefined, { sensitivity: 'base' })
      );
    case 'rating':
      return copy.sort(compareRating);
    case 'state':
      return copy.sort(compareState);
    case 'recent':
    default:
      return copy.sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt));
  }
}

function compareRating(a: HybridHubItem, b: HybridHubItem): number {
  const aRating = a.entity === 'game' ? a.rating : undefined;
  const bRating = b.entity === 'game' ? b.rating : undefined;
  if (aRating == null && bRating == null) return 0;
  if (aRating == null) return 1;
  if (bRating == null) return -1;
  return bRating - aRating;
}

function compareState(a: HybridHubItem, b: HybridHubItem): number {
  const aIsGame = a.entity === 'game';
  const bIsGame = b.entity === 'game';
  if (aIsGame && !bIsGame) return -1;
  if (!aIsGame && bIsGame) return 1;
  if (!aIsGame && !bIsGame) return 0;
  // both games
  const aOrder = STATE_ORDER[(a as { state?: string }).state ?? ''] ?? 99;
  const bOrder = STATE_ORDER[(b as { state?: string }).state ?? ''] ?? 99;
  return aOrder - bOrder;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- hybrid-hub.derive`
Expected: PASS — all ~14 derive tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/library/hybrid-hub.derive.ts apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts
git commit -m "feat(library): #1591 add deriveHybridItems pure function (Phase 1)"
```

---

### Task 8: SSOT consolidation — drop duplicate `LibraryEntityKey` in `LibraryTabs.tsx`

**Files:**
- Modify: `apps/web/src/components/features/library/LibraryTabs.tsx` (drop line 33 + import)
- Modify: `apps/web/src/components/features/library/index.ts` (re-export from domain)

The value is identical (`'all' | 'kb' | 'loaned'`), so this is **not a breaking change** — it just collapses two independent `export type` declarations into one source. After this task, the only definition of `LibraryEntityKey` lives in `lib/library/library-filters.ts`, where `filterByEntity` already consumes it.

- [ ] **Step 1: Confirm the existing tests still pass before any change**

Run: `pnpm --filter @meepleai/web test -- LibraryTabs library-filters`
Expected: PASS (baseline — no test changes needed for this task).

- [ ] **Step 2: Modify `LibraryTabs.tsx`**

Replace line 33:

```ts
// Before
export type LibraryEntityKey = 'all' | 'kb' | 'loaned';
```

With an import + re-export:

```ts
// After (replace line 33)
import type { LibraryEntityKey } from '@/lib/library/library-filters';

export type { LibraryEntityKey };
```

The `export type { LibraryEntityKey };` preserves the public re-export — any code currently importing `LibraryEntityKey` from `@/components/features/library/LibraryTabs` (or the barrel) keeps working unchanged.

- [ ] **Step 3: Update the barrel (`index.ts`) to source from the domain**

In `apps/web/src/components/features/library/index.ts`, replace the `LibraryEntityKey` re-export with a domain re-export:

```ts
// Before (around line 41-44, inside the LibraryTabs export block)
export { LibraryTabs } from '@/components/features/library/LibraryTabs';
export type {
  LibraryEntityKey,
  LibraryTabConfig,
  LibraryTabsProps,
} from '@/components/features/library/LibraryTabs';

// After
export { LibraryTabs } from '@/components/features/library/LibraryTabs';
export type {
  LibraryTabConfig,
  LibraryTabsProps,
} from '@/components/features/library/LibraryTabs';
export type { LibraryEntityKey } from '@/lib/library/library-filters';
```

This makes the barrel re-export `LibraryEntityKey` directly from the canonical source, leaving zero indirection through `LibraryTabs`.

- [ ] **Step 4: Run typecheck and tests to verify nothing broke**

Run: `pnpm --filter @meepleai/web typecheck && pnpm --filter @meepleai/web test -- LibraryTabs library-filters LibraryHub`
Expected: PASS — typecheck clean, all existing `LibraryTabs.test.tsx`, `library-filters.test.ts`, and `LibraryHub.test.tsx` tests green (no behavior change).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/LibraryTabs.tsx apps/web/src/components/features/library/index.ts
git commit -m "refactor(library): #1591 consolidate LibraryEntityKey SSOT in domain module (Phase 1)"
```

---

## Acceptance check for Phase 1

After all 8 tasks are committed, verify the AC subset that Phase 1 owns per contract §9:

- **AC1** (6 entity tabs with live counts) — *deferred to Phase 2*; the type union is ready in `hybrid-hub.types.ts` but the UI expansion is Phase 2's job.
- **AC2** (grid renders 5 entities via `MeepleCard`) — *deferred to Phase 2*; the `HybridHubItem` shape is ready and validated by mapper tests.
- **AC9** (bulk-selection game-scoped) — *deferred to Phase 2*; involves `LibraryHub` selection-mode override which doesn't exist standalone.

What Phase 1 actually delivers:
1. `HybridHubItem` discriminated union + 5 sub-types (types.ts)
2. 5 pure mappers from existing DTO shapes (mappers.ts) — ~25 unit tests
3. `KbDoc` greenfield FE interface (mappers.ts) — to be Zod-schema-replaced when BE-1 #1588 ships
4. `deriveHybridItems` pure merge/filter/sort function (derive.ts) — ~14 unit tests
5. `LibraryEntityKey` single SSOT (no more duplicate `export type`)

Run the final full check before opening the PR:

```bash
pnpm --filter @meepleai/web typecheck
pnpm --filter @meepleai/web test -- hybrid-hub library-filters LibraryTabs
pnpm --filter @meepleai/web lint
```

All three must pass.

---

## Out of scope (explicit deferrals to Phase 2 / #1592)

- `LibraryEntityKey` value expansion `'all' | 'kb' | 'loaned'` → `'all' | 'games' | 'agents' | 'kb' | 'sessions' | 'chat'`
- `LibraryTabs` adapt to render 6 tabs
- `LibraryHybridGrid` accept `HybridHubItem[]` instead of `UserLibraryEntry[]`
- `LibraryHub` multi-query orchestration (calls hooks + `deriveHybridItems`)
- `LibraryHub` selection-mode override (force `browse` outside `games` tab — AC9)
- STATO chip carrying `loaned` / `with-KB` filters
- `useUserKbDocs` greenfield hook (calls BE-1 #1588)
- Hero badge / 4 stat chips / Importa BGG / Esporta
- `CrossEntityFilters` chip row

These belong to **#1592** (Phase 2 Surface).
