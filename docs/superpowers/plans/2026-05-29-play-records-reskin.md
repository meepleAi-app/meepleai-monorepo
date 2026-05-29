# Play Records — SP4 reskin (5 route) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Model mix**: Haiku per Task 0/1/4/5 (mechanical wiring), Sonnet per Task 2/3 (state complex + variant matrix).

**Goal:** Reskin presentazionale delle 5 route `/play-records/{index,new,[id],edit,stats}` allineato ai mockup SP4 (`admin-mockups/design_files/sp4-play-records-*.jsx`), wiring dei nuovi campi BE #1663 (PR #1666 outcome + PR #1667 stats), e nuovo componente `PlayRecordDetailView` con `HeroPodium`. Le 4 route già esistenti hanno scheletro funzionante; questo plan le adatta al design + estende il detail (assente).

**Architecture:**
- **Primitive condivise**: `OutcomeBadge`, `ChipStrip`, `usePlayRecordPerspective` hook, `formatRelativeDate` helper, `PlayRecordHeroPodium` (riusabile cross-route).
- **5 route reskin**: index riusa `PlayHistory` esistente con nuovi sub-component; detail introduce `PlayRecordDetailView` (HeroPodium + ConnectionBar + KpiGrid + Classifica); new/edit riusano `SessionCreateForm` esistente con wizard mobile via `useMediaQuery`; stats riusa `StatisticsPage` skeleton con nuovi KPI + bar chart.
- **Derive perspective FE-side**: `won|lost|tie|cooperative|spectator` derivata da `outcomeType` + `winnerPlayerIds` + `currentSessionUser.id` matching `players[].userId`.
- **Cover/emoji**: FE batch-fetch `SharedGame` via TanStack Query con cache `staleTime: 1h`; fallback emoji deterministic da hash(gameName) se `gameId === null` o cache miss.
- **K5 edit gate**: form mostra TUTTI i campi readonly + "Modifica" abilita solo `sessionDate/notes/location` (scope BE `UpdatePlayRecordRequest`); banner inline "Per modificare giocatori/punteggi → elimina e ricrea".

**Tech Stack:** Next.js 16 (App Router) · React 19 · TypeScript · Vitest · Playwright · TanStack Query · Zustand · date-fns (`it` locale).

**Depends on:**
- **BE #1663 Phase 1+2 MERGED** (`b52183544` PR #1666 outcome fields, `a2a69f563` PR #1667 stats fields) ✅
- **#1470 `userHue` canonical** (lib/colors) ✅
- **#1469 `Stars` re-export** (riuso solo se necessario, non critico per play-records)
- **Schema Zod** già aggiornati post-#1663 in `apps/web/src/lib/api/schemas/play-records.schemas.ts` (campi opzionali per rollout-safe) ✅

**Spec di riferimento:**
- Issue #1488 (Epic #1475 Phase D, P1 blocking US-32)
- Mockup: `admin-mockups/design_files/sp4-play-records-{index,new,detail,edit,stats}.jsx`
- Memoria sessione: `claudedocs/epic-1475-refresh-2026-05-28.md`
- Spec panel review: vedi sezione "Spec Panel decisions" nel comment di apertura sub-issue

**Mockup target:**
- `sp4-play-records-index.jsx` → RecordsHero + RecordFilters sticky + RecordCardList/Grid (12 stati: 8 mobile + 4 desktop)
- `sp4-play-records-detail.jsx` → HeroPodium (won/tied/inprogress/planned/cooperative) + ConnectionBar + KpiGrid + Classifica (10+ stati)
- `sp4-play-records-new.jsx` + `pr-form-core.jsx` → Wizard 3-step mobile (Gioco/Quando/Punteggi) + split-form desktop (8-col form + 4-col live preview)
- `sp4-play-records-edit.jsx` → riuso `pr-form-core.jsx` con prefill + delete CTA
- `sp4-play-records-stats.jsx` → StatsHero 4-col KPI + MostPlayed bar + WinByGame bar (6 stati)

**Non-goals di questo plan (tracked separately):**
- ❌ **BE `topScores[]` su `PlayRecordSummary`** (per scores inline nelle card index) — K2 deferred a Phase 3 BE follow-up. MVP: scores solo in detail.
- ❌ **BE `UpdatePlayRecordRequest` expansion** (per editare game/players/scores) — K5 tracked come BE issue parallelo; MVP usa gate readonly + 3-field edit.
- ❌ **BE `gameSlug` su `PlayRecordSummary`** — K1 tracked come BE issue follow-up (low-effort BE); MVP usa batch-fetch `GET /games?ids=...`.
- ❌ **Optimistic UI per mutation** (K12) — MVP: invalidate + toast.
- ❌ **Layout side-by-side detail panel** — design future (split list + drawer detail).
- ❌ **Virtualization index list >500 record** — pagination esistente sufficiente.
- ❌ **Step-up auth UI per delete** — esistente pattern global.

**Out of band:**
- Fixture isolation `sp4-play-records-data.js` (Task 2 nella ripresa sessione) — non blocca questo plan, è housekeeping dei mockup standalone.

---

## File Structure

### Primitive condivise (Task 0)
- **Create** `apps/web/src/components/play-records/primitives/OutcomeBadge.tsx` + test
- **Create** `apps/web/src/components/play-records/primitives/ChipStrip.tsx` + test
- **Create** `apps/web/src/components/play-records/primitives/PlayRecordHeroPodium.tsx` + test (riusabile session-summary)
- **Create** `apps/web/src/lib/play-records/derivePerspective.ts` (pure function) + test
- **Create** `apps/web/src/lib/play-records/formatRelativeDate.ts` (pure function) + test
- **Create** `apps/web/src/lib/play-records/useSharedGames.ts` (batch hook) + test

### Route reskin (Task 1-5)
- **Modify** `apps/web/src/components/play-records/PlayHistory.tsx` — nuovi sub-component (RecordsHero, RecordFilters sticky, RecordCardList/Grid)
- **Create** `apps/web/src/components/play-records/PlayRecordDetailView.tsx` + test
- **Modify** `apps/web/src/app/(authenticated)/play-records/[id]/page.tsx` — wire `PlayRecordDetailView`
- **Modify** `apps/web/src/components/play-records/SessionCreateForm.tsx` — wizard 3-step + responsive
- **Modify** `apps/web/src/components/play-records/NewPlayRecordSheet.tsx` — wire al nuovo form layout
- **Modify** `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx` — gate K5 (readonly + 3-field edit)
- **Modify** `apps/web/src/app/(authenticated)/play-records/stats/page.tsx` — KPI 4-col + bar chart sections

### i18n (cross-task)
- **Modify** `apps/web/src/i18n/messages/it.json` — aggiungi `playRecords.*` keys (status, outcome, empty, CTA, kpi labels)
- **Modify** `apps/web/src/i18n/messages/en.json` — equivalente EN

### Test fixture / MSW
- **Create** `apps/web/src/test/msw/handlers/play-records.handlers.ts` — handlers `GET /play-records`, `/play-records/:id`, `/play-records/statistics`, `POST/PUT/DELETE`
- **Create** `apps/web/src/test/fixtures/play-records.fixtures.ts` — 4 record seed (won/tied/cooperative/inprogress) + stats seed

---

## Acceptance Criteria trasversali (applicabili a TUTTE le route)

**EC = Edge Case (10), KX = Cross-cutting constraint (7)**

- **EC-1** Cooperative game (`outcomeType === 'none'`) → no OutcomeBadge, hero variant `cooperative`, podium "classifica per dimension scelta" senza corona.
- **EC-2** Freeform game (`gameId === null`) → cover emoji fallback deterministic da `hash(gameName)`; no link gioco da ChipStrip.
- **EC-3** `winnerPlayerIds.length === 0` + `status === 'Completed'` → cooperative variant (invariante BE da verificare in test).
- **EC-4** Spectator: `currentUser.id` NON in `players[].userId` → variant `spectator`, badge neutrale "Vittoria di X" o "Pareggio a X punti".
- **EC-5** Tutti i player guest (`userId === null` su tutti) → no perspective derive, solo classifica.
- **EC-6** `duration === null` (InProgress senza endTime) → omit "⏱ Xh Ym" dalle meta; mostra "In corso · turno N" se disponibile.
- **EC-7** `sessionDate` futura (Planned) → format "Tra 2 giorni" via `formatDistanceToNowStrict` con locale `it`.
- **EC-8** `Archived` status → filter da default view list; opzione toggle "Mostra archiviate" su filter dropdown.
- **EC-9** Stats vuoto (`totalSessions === 0`) → `StatsHero` mostra "—" + empty section con CTA "Registra prima partita".
- **EC-10** Multi-dim scoring (`players[].scores[]` len > 1) → `totalScore` (post-#1663) è dim `points`; altre dim → accordion expandable solo in detail.

**K11 Cache invalidation**: TanStack Query keys `['play-records','history',filters]`, `['play-records','detail',id]`, `['play-records','stats']`. Invalidate **tutti i 3** post-mutation (create/update/delete). Stats `staleTime: 5min`.

**K13 Empty state strategy**: ogni route ha empty state distinto (index empty-first-run vs empty-filter; stats empty-zero-plays; detail/edit 404 redirect; new sempre disponibile).

**K14 Test fixture strategy**: MSW handlers seed 4 record + stats. Vitest unit per derive functions, RTL component test per sub-component, Playwright e2e per flow index→detail→edit.

**K15 i18n**: tutte le label esposte all'utente via `useTranslations('playRecords')`. NO hardcoded IT strings nei nuovi component. Verifica catalog completo prima del merge (`pnpm test:i18n` se esiste, altrimenti grep `tutte|registra|partita` nei file `.tsx`).

**K16 Accessibility**: aria-label su filter chips, aria-pressed su view toggle, aria-live="polite" su empty-results post-filter, focus management nel wizard 3-step (Tab/Shift-Tab + auto-focus su step enter).

**K17 Loading skeleton parity**: layout-matching shimmer (es. 92px row per index, 4 KPI box per stats hero). NO generic spinner.

---

## Task 0: Primitive condivise (foundation)

**Owner**: Haiku subagent · **Effort**: ~3-4h

**Files:**
- Create: `apps/web/src/lib/play-records/derivePerspective.ts` + test
- Create: `apps/web/src/lib/play-records/formatRelativeDate.ts` + test
- Create: `apps/web/src/lib/play-records/useSharedGames.ts` + test
- Create: `apps/web/src/components/play-records/primitives/OutcomeBadge.tsx` + test
- Create: `apps/web/src/components/play-records/primitives/ChipStrip.tsx` + test
- Create: `apps/web/src/components/play-records/primitives/PlayRecordHeroPodium.tsx` + test

### Step 1: `derivePerspective.ts` (pure function)

- [ ] **Step 1.1: Test (TDD)**

```ts
// apps/web/src/lib/play-records/__tests__/derivePerspective.test.ts
import { describe, it, expect } from 'vitest';
import { derivePerspective } from '../derivePerspective';

describe('derivePerspective', () => {
  it('returns "won" when currentUser is in winnerPlayerIds', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [{ id: 'p-1', userId: 'u-1', displayName: 'Me' }],
      winnerPlayerIds: ['p-1'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'won', currentUserPlayerId: 'p-1' });
  });

  it('returns "lost" when currentUser is player but NOT winner', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [
        { id: 'p-1', userId: 'u-1', displayName: 'Me' },
        { id: 'p-2', userId: 'u-2', displayName: 'Other' },
      ],
      winnerPlayerIds: ['p-2'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'lost', currentUserPlayerId: 'p-1' });
  });

  it('returns "tie" when 2+ winners include currentUser', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [
        { id: 'p-1', userId: 'u-1', displayName: 'Me' },
        { id: 'p-2', userId: 'u-2', displayName: 'Other' },
      ],
      winnerPlayerIds: ['p-1', 'p-2'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'tie', currentUserPlayerId: 'p-1' });
  });

  it('returns "cooperative" when outcomeType is none (EC-1)', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [{ id: 'p-1', userId: 'u-1', displayName: 'Me' }],
      winnerPlayerIds: [],
      outcomeType: 'none',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'cooperative', currentUserPlayerId: 'p-1' });
  });

  it('returns "spectator" when currentUser NOT in players (EC-4)', () => {
    const result = derivePerspective({
      currentUserId: 'u-99',
      players: [{ id: 'p-1', userId: 'u-1', displayName: 'Other' }],
      winnerPlayerIds: ['p-1'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'spectator', currentUserPlayerId: null });
  });

  it('returns "spectator" when all players are guests (EC-5)', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [{ id: 'p-1', userId: null, displayName: 'Guest' }],
      winnerPlayerIds: ['p-1'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'spectator', currentUserPlayerId: null });
  });

  it('returns "pending" for InProgress/Planned', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [{ id: 'p-1', userId: 'u-1', displayName: 'Me' }],
      winnerPlayerIds: [],
      outcomeType: 'competitive',
      status: 'InProgress',
    });
    expect(result).toEqual({ kind: 'pending', currentUserPlayerId: 'p-1' });
  });
});
```

- [ ] **Step 1.2: Run, confirm FAIL** (module missing).
- [ ] **Step 1.3: Implementa `derivePerspective.ts`**

```ts
// apps/web/src/lib/play-records/derivePerspective.ts
import type {
  PlayRecordStatus,
  PlayRecordOutcomeType,
  SessionPlayer,
} from '@/lib/api/schemas/play-records.schemas';

export type PerspectiveKind = 'won' | 'lost' | 'tie' | 'cooperative' | 'spectator' | 'pending';

export interface Perspective {
  kind: PerspectiveKind;
  currentUserPlayerId: string | null;
}

export interface DerivePerspectiveInput {
  currentUserId: string | null;
  players: Pick<SessionPlayer, 'id' | 'userId' | 'displayName'>[];
  winnerPlayerIds: string[];
  outcomeType: PlayRecordOutcomeType | undefined;
  status: PlayRecordStatus;
}

export function derivePerspective(input: DerivePerspectiveInput): Perspective {
  const { currentUserId, players, winnerPlayerIds, outcomeType, status } = input;

  // EC-6: InProgress/Planned → no winner yet
  if (status === 'InProgress' || status === 'Planned') {
    const currentPlayer = currentUserId
      ? players.find(p => p.userId === currentUserId) ?? null
      : null;
    return { kind: 'pending', currentUserPlayerId: currentPlayer?.id ?? null };
  }

  // EC-1, EC-3: Cooperative
  if (outcomeType === 'none' || winnerPlayerIds.length === 0) {
    const currentPlayer = currentUserId
      ? players.find(p => p.userId === currentUserId) ?? null
      : null;
    return { kind: 'cooperative', currentUserPlayerId: currentPlayer?.id ?? null };
  }

  // EC-4, EC-5: Spectator
  const currentPlayer = currentUserId
    ? players.find(p => p.userId === currentUserId)
    : undefined;
  if (!currentPlayer) {
    return { kind: 'spectator', currentUserPlayerId: null };
  }

  // Competitive: derive from winnerPlayerIds membership
  const userIsWinner = winnerPlayerIds.includes(currentPlayer.id);
  if (!userIsWinner) {
    return { kind: 'lost', currentUserPlayerId: currentPlayer.id };
  }
  if (winnerPlayerIds.length > 1) {
    return { kind: 'tie', currentUserPlayerId: currentPlayer.id };
  }
  return { kind: 'won', currentUserPlayerId: currentPlayer.id };
}
```

- [ ] **Step 1.4: Run, confirm PASS** (7 test).

### Step 2: `formatRelativeDate.ts`

- [ ] **Step 2.1: Test (TDD)**

```ts
// apps/web/src/lib/play-records/__tests__/formatRelativeDate.test.ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { formatRelativeDate } from '../formatRelativeDate';

describe('formatRelativeDate', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-05-29T14:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('formats today (within 24h) as "oggi"', () => {
    expect(formatRelativeDate('2026-05-29T10:00:00Z')).toBe('oggi');
  });

  it('formats yesterday as "ieri"', () => {
    expect(formatRelativeDate('2026-05-28T14:00:00Z')).toBe('ieri');
  });

  it('formats N days ago as "N giorni fa"', () => {
    expect(formatRelativeDate('2026-05-25T14:00:00Z')).toMatch(/4 giorni fa/);
  });

  it('formats future as "tra N giorni" (EC-7)', () => {
    expect(formatRelativeDate('2026-06-01T14:00:00Z')).toMatch(/tra 3 giorni/);
  });

  it('formats >30 days ago as short Italian date "5 mag"', () => {
    expect(formatRelativeDate('2026-04-15T14:00:00Z')).toBe('15 apr');
  });

  it('returns "—" for null input', () => {
    expect(formatRelativeDate(null)).toBe('—');
  });
});
```

- [ ] **Step 2.2: Implementa** (usa `date-fns/formatDistanceToNowStrict` + `format` con locale `it`).
- [ ] **Step 2.3: Run, confirm PASS** (6 test).

### Step 3: `useSharedGames.ts` (batch hook con TanStack Query)

- [ ] **Step 3.1: Test (TDD)** — mock `gameApi.batchGet` con MSW, assert single network call per batch.
- [ ] **Step 3.2: Implementa**

```ts
// apps/web/src/lib/play-records/useSharedGames.ts
import { useQuery } from '@tanstack/react-query';
import { gamesApi } from '@/lib/api/games.api';

export function useSharedGames(gameIds: (string | null)[]) {
  const uniqueIds = [...new Set(gameIds.filter((id): id is string => !!id))];
  return useQuery({
    queryKey: ['shared-games', 'batch', uniqueIds.sort()],
    queryFn: () => gamesApi.batchGet(uniqueIds),
    staleTime: 60 * 60 * 1000, // 1h
    enabled: uniqueIds.length > 0,
  });
}
```

**NB**: se `gamesApi.batchGet` non esiste, fallback a `Promise.all(uniqueIds.map(id => gamesApi.getById(id)))` (acceptable per MVP, BE issue follow-up per batch endpoint).

- [ ] **Step 3.3: Run, confirm PASS**.

### Step 4-6: `OutcomeBadge`, `ChipStrip`, `PlayRecordHeroPodium` components

Per ciascun component:
- [ ] Test RTL: render con tutte le variant + a11y aria-label
- [ ] Impl: usa CSS vars `--c-session`, `--c-event`, `--c-toolkit` (entity-tinted, mockup style)
- [ ] Run, confirm PASS

`PlayRecordHeroPodium` accetta props:
```ts
interface PlayRecordHeroPodiumProps {
  variant: 'won' | 'tied' | 'cooperative' | 'inprogress' | 'planned';
  game: { id: string | null; name: string; coverUrl?: string; coverEmoji?: string };
  rankedScores: Array<{ playerId: string; name: string; score: number | null; isWinner: boolean }>;
  metadata: { date: string; duration: string | null; playerCount: number; turn?: number };
  perspective: Perspective; // from derivePerspective
}
```

Riusabile da `session-summary` esistente (parlare con owner di session-summary se serve allineamento, ma può anche essere standalone se non emerge bisogno cross-route).

### Step 7: Commit

- [ ] `git commit -m "feat(play-records): foundation primitives (derive, format, podium, badge)"`

---

## Task 1: `/play-records` (index) reskin

**Owner**: Haiku subagent · **Effort**: ~3h · **BlockedBy**: Task 0

**Files:**
- Modify: `apps/web/src/components/play-records/PlayHistory.tsx`
- Optional create: `apps/web/src/components/play-records/index/{RecordsHero,RecordFilters,RecordCardList,RecordCardGrid}.tsx`

**AC:**
- [ ] **AC-1.1**: `RecordsHero` mostra 4 KPI inline (`partite`, `vittorie`, `giochi`, `ore totali`) consumendo `usePlayerStatistics` (deferred load — non blocca render del hero).
- [ ] **AC-1.2**: `RecordFilters` sticky top (`position: sticky; top: 0`) con: search input + 4 status chips + 4 dropdown (gioco/data/esito/sort) + view toggle list/grid.
- [ ] **AC-1.3**: `RecordCardList` mostra cover gioco (via `useSharedGames`, fallback emoji), titolo, data relativa, OutcomeBadge, meta (durata+player count+vincitore), ChipStrip (game/player/chat). No scoring inline (K2).
- [ ] **AC-1.4**: `RecordCardGrid` 3-col desktop, 1-col mobile; cover prominente, OutcomeBadge top-right, no scoring inline.
- [ ] **AC-1.5**: Empty states distinti: first-run (no record) vs filter-no-results.
- [ ] **AC-1.6**: Loading skeleton: 5x 92px shimmer rows (NO spinner generic).
- [ ] **AC-1.7**: Error state con CTA Riprova (danger color).
- [ ] **AC-1.8**: EC-8: `Archived` records filtrati da default; toggle "Mostra archiviate" sotto status chip "Tutte" (collapsed by default).
- [ ] **AC-1.9**: Cache invalidation K11: rispetta query keys; mutation downstream triggera re-render automatico.
- [ ] **AC-1.10**: K15 i18n: tutte le label via `useTranslations('playRecords.index')`.
- [ ] **AC-1.11**: K16 a11y: aria-pressed su chip, role="search" su input, aria-live="polite" su results count.
- [ ] **AC-1.12**: Playwright e2e: filter → search → toggle view → tap card → detail (smoke flow).

**Step 1: Test (TDD)** — RTL component test per ogni sub-component + integration `PlayHistory.test.tsx` con MSW handlers seed.

**Step 2: Implementa** — Sostituisce JSX esistente con nuovi sub-component; rispetta token canonici (`bg-card`, `text-foreground`, `border-border`, entity utilities); CSS vars per gradient (cover, hero radial).

**Step 3: Run** — `cd apps/web && pnpm vitest run src/components/play-records/` + `pnpm test:e2e play-records-index` (se test e2e esistono, altrimenti crea uno smoke `apps/web/e2e/play-records.spec.ts`).

**Step 4: Commit** — `git commit -m "feat(play-records): #1488 index reskin (RecordsHero + filters sticky + cards)"`

---

## Task 2: `/play-records/[id]` (detail) — nuovo `PlayRecordDetailView`

**Owner**: Sonnet subagent (variant matrix complexity) · **Effort**: ~5h · **BlockedBy**: Task 0

**Files:**
- Create: `apps/web/src/components/play-records/PlayRecordDetailView.tsx` + test (variant matrix)
- Create: `apps/web/src/components/play-records/detail/{ConnectionBar,KpiGrid,Classifica,ScoreBreakdown}.tsx`
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/page.tsx` — wire `PlayRecordDetailView`

**AC:**
- [ ] **AC-2.1**: `PlayRecordDetailView` consuma `usePlayRecord(id)` (fetch full `PlayRecordDto`) + `derivePerspective` con `currentSessionUser`.
- [ ] **AC-2.2**: `PlayRecordHeroPodium` rendered con variant da perspective.kind: `won` (confetti + corona + podium top-3) | `tied` (banner pareggio + 2+ corone) | `cooperative` (no corona, classifica per dimension) | `inprogress` (badge pulsante + turno) | `planned` (badge calendar + CTA Avvia).
- [ ] **AC-2.3**: `ConnectionBar` con chip entity-tinted (game, player count, chat presence, event date).
- [ ] **AC-2.4**: `KpiGrid` 4 KPI: durata, top score, media, distacco max-min.
- [ ] **AC-2.5**: `Classifica` ranked by `totalScore` (post-#1663) con avatar entity-tinted (`userHue(userId)`), barra progresso relativa al top.
- [ ] **AC-2.6**: `ScoreBreakdown` accordion expandable se `scores[].length > 1` (EC-10) — mostra multi-dim per player.
- [ ] **AC-2.7**: EC-1 cooperative: hero no corona, classifica neutrale.
- [ ] **AC-2.8**: EC-2 freeform (`gameId === null`): cover emoji fallback, no link `/games/[id]`.
- [ ] **AC-2.9**: EC-4/5 spectator: banner neutrale "Vittoria di X" / "Pareggio".
- [ ] **AC-2.10**: EC-6/7 InProgress/Planned: omit duration o show future date relative.
- [ ] **AC-2.11**: Variant matrix test Vitest: render con fixture per ogni variant + a11y assertions.
- [ ] **AC-2.12**: 404 redirect a `/play-records` se record non trovato.

**Step 1: Test (TDD)** — Variant matrix con 6 scenari (won/tied/cooperative/spectator/inprogress/planned) + 4 edge case (freeform/multi-dim/all-guests/InProgress-no-duration).

**Step 2: Implementa** — Component composition; reusa primitive da Task 0.

**Step 3: Playwright e2e** — flow index → tap card → detail rendering (smoke).

**Step 4: Commit** — `git commit -m "feat(play-records): #1488 PlayRecordDetailView with podium + variants"`

---

## Task 3: `/play-records/new` reskin (wizard + split-form)

**Owner**: Sonnet subagent (state complex form) · **Effort**: ~4h · **BlockedBy**: Task 0

**Files:**
- Modify: `apps/web/src/components/play-records/SessionCreateForm.tsx` — wizard 3-step responsive
- Modify: `apps/web/src/components/play-records/NewPlayRecordSheet.tsx` — wire layout

**AC:**
- [ ] **AC-3.1**: Mobile (`viewport <= 768px`): wizard 3-step (Step1 Gioco / Step2 Quando / Step3 Punteggi) con StepIndicator sticky top.
- [ ] **AC-3.2**: Desktop (`viewport > 768px`): split form 8-col (form fields) + 4-col (live preview record-card che si aggiorna in tempo reale).
- [ ] **AC-3.3**: Step 1 Gioco: ricerca + selezione da `LIBRARY` cached via `useSharedGames`. Fallback "Gioco freeform" → input testo.
- [ ] **AC-3.4**: Step 2 Quando: date picker (`sessionDate`) + time + duration estimate + location optional.
- [ ] **AC-3.5**: Step 3 Punteggi: PlayerManager (aggiungi/rimuovi giocatori — user da rubrica O guest) + ScoringInterface per ogni dim configurata.
- [ ] **AC-3.6**: Submit → `POST /api/v1/play-records` con `CreatePlayRecordRequest` schema; success → toast + redirect `/play-records/[id]` (newly created).
- [ ] **AC-3.7**: Validation Zod inline per ogni step; "Avanti" disabilitato se step invalid.
- [ ] **AC-3.8**: K16 a11y: focus management su step change; aria-current="step" su StepIndicator.
- [ ] **AC-3.9**: K11 cache invalidation post-create: invalidate history + stats keys.
- [ ] **AC-3.10**: Empty library state: fallback a freeform game obbligatorio.

**Step 1: Test (TDD)** — RTL: navigate wizard steps, validation gate, submit flow with MSW.

**Step 2: Implementa** — Refactor `SessionCreateForm` con `useMediaQuery('(max-width: 768px)')` hook (se non esiste, crea `apps/web/src/lib/hooks/useMediaQuery.ts`).

**Step 3: Playwright e2e** — happy path mobile (wizard 3-step submit).

**Step 4: Commit** — `git commit -m "feat(play-records): #1488 new wizard mobile + split desktop"`

---

## Task 4: `/play-records/[id]/edit` reskin con K5 gate

**Owner**: Haiku subagent · **Effort**: ~2h · **BlockedBy**: Task 0, Task 3

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx`

**AC:**
- [ ] **AC-4.1**: Layout identico a `new` (riusa `SessionCreateForm` con prop `mode='edit'`).
- [ ] **AC-4.2**: Pre-fill: caricamento via `usePlayRecord(id)` + `setValues` su form.
- [ ] **AC-4.3**: K5 gate readonly: tutti i campi sono **readonly** (visualizzati ma non editabili) EXCEPT `sessionDate`, `notes`, `location` che restano editable.
- [ ] **AC-4.4**: Banner inline sopra form: "Per modificare giocatori o punteggi, elimina e ricrea la partita" + link "Cancella partita" (apre confirmation dialog).
- [ ] **AC-4.5**: Submit → `PUT /api/v1/play-records/{id}` con `UpdatePlayRecordRequest` (solo sessionDate/notes/location).
- [ ] **AC-4.6**: Delete CTA: `AdminConfirmationDialog` (esistente pattern globale) → `DELETE /api/v1/play-records/{id}` → redirect `/play-records`.
- [ ] **AC-4.7**: K11 cache: invalidate detail + history + stats post-update/delete.
- [ ] **AC-4.8**: K16 a11y: aria-readonly sui campi non editabili; focus iniziale su `sessionDate`.

**Step 1: Test (TDD)** — RTL: render pre-filled, edit only allowed fields, attempt edit readonly field is no-op, delete flow.

**Step 2: Implementa** — Estende `SessionCreateForm` con prop `mode` + `editableFields` whitelist.

**Step 3: Commit** — `git commit -m "feat(play-records): #1488 edit with K5 gate (3-field scope)"`

---

## Task 5: `/play-records/stats` reskin

**Owner**: Haiku subagent · **Effort**: ~2.5h · **BlockedBy**: Task 0

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/stats/page.tsx`
- Create: `apps/web/src/components/play-records/stats/{StatsHero,MostPlayedBar,WinByGameBar}.tsx`

**AC:**
- [ ] **AC-5.1**: `StatsHero` 4-col KPI: `Partite` (`totalSessions`), `Giochi` (`mostPlayedGames.length`), `Win rate` (`totalWins/totalSessions * 100`), `Preferito` (`mostPlayedGames[0]` con cover/emoji da `useSharedGames`).
- [ ] **AC-5.2**: Sub-label `${hoursPlayed}h totali` (da `totalDurationMinutes / 60`).
- [ ] **AC-5.3**: `MostPlayedBar` section: top 5 giochi da `mostPlayedGames`, ordered desc, barre proporzionali al play count.
- [ ] **AC-5.4**: `WinByGameBar` section: ordered by win-rate desc (`won/played`), barre proporzionali alla win-rate %.
- [ ] **AC-5.5**: EC-9 empty: `totalSessions === 0` → KPI mostrano `—`, sections mostrano `EmptySection` con CTA "Registra partita".
- [ ] **AC-5.6**: Loading: `SectionSkeleton` shimmer 200px per ogni section.
- [ ] **AC-5.7**: Error: layout-matching error state con CTA Riprova.
- [ ] **AC-5.8**: Grid responsive: mobile 1-col (stacked), desktop 2-col side-by-side (MostPlayed | WinByGame).
- [ ] **AC-5.9**: K11 cache: `staleTime: 5min` per stats query.
- [ ] **AC-5.10**: K15 i18n: tutte le label via `useTranslations('playRecords.stats')`.

**Step 1: Test (TDD)** — RTL: render empty + loading + error + happy path con fixture stats.

**Step 2: Implementa** — Component composition + bar visualization (riusa pattern admin dashboard se simile esiste).

**Step 3: Commit** — `git commit -m "feat(play-records): #1488 stats reskin (KPI + MostPlayed + WinByGame)"`

---

## Self-Review

**1. Spec coverage:**
- ✅ K1-K10 decisions integrate in AC + non-goals
- ✅ EC-1..EC-10 distribute per route (cooperative, freeform, spectator, multi-dim, etc.)
- ✅ K11-K17 cross-cutting constraint applicate trasversalmente
- ⚠️ K2 `topScores[]` BE follow-up tracked in non-goals (NON questo PR)
- ⚠️ K5 `UpdatePlayRecordRequest` expansion tracked in non-goals (NON questo PR)
- ⚠️ K1 `gameSlug` BE follow-up tracked in non-goals; MVP usa batch `?ids=` fallback `Promise.all`

**2. Placeholder scan:**
- ✅ Ogni task ha file path concreti + AC numeriche
- ⚠️ Task 0 Step 3 (`useSharedGames`): se `gameApi.batchGet` NON esiste, fallback a `Promise.all(getById)` esplicitato (acceptable per MVP).
- ⚠️ Task 3 Step 2: `useMediaQuery` potenzialmente da creare se non esistente — verifica `apps/web/src/lib/hooks/`.

**3. Type consistency:**
- ✅ Tutti i tipi da `play-records.schemas.ts` (canonical post-#1663)
- ✅ `Perspective` discriminated union exportata da `derivePerspective.ts`
- ✅ Component props typed (no `any`)

**4. Test strategy:**
- ✅ Unit (Vitest) per derive functions + format helpers
- ✅ Component (RTL + MSW) per ogni sub-component
- ✅ Integration `PlayHistory.test.tsx` con seed fixture
- ✅ Playwright e2e smoke per Task 1 + Task 3 (happy path)
- ⚠️ Visual regression NON in scope (gate rimosso 2026-05-20)

**5. Note di rischio:**
- 🔴 **R1 — `useSharedGames` N+1**: se BE non espone batch endpoint e cache è cold, 50 record = 50 fetch. Mitigazione: `Promise.all` parallel + `staleTime: 1h` + BE follow-up issue.
- 🟡 **R2 — K5 UX limited edit**: utente si aspetta "modifica tutto"; banner spiega trade-off ma è degraded. Tracked BE follow-up.
- 🟡 **R3 — `PlayRecordHeroPodium` riuso session-summary**: se session-summary ha già un component simile, evitare duplicazione. Verifica `apps/web/src/components/sessions/` durante Task 0.6.
- 🟡 **R4 — i18n catalog completeness**: nuove keys vanno aggiunte a it.json + en.json; potenziale "Invalid string length" react-intl error se MESSAGES test fixture incompleta (P106 pattern).
- 🟢 **R5 — Wizard responsive switch**: se `useMediaQuery` cambia mid-form, state preserve necessario; testare scenario explicitly.

---

## Execution Handoff

### Ordine consigliato di esecuzione

1. **Task 0** (primitives, ~3-4h) — Haiku subagent → unblock Task 1-5.
2. **Task 1** (index reskin, ~3h) — Haiku subagent → primo PR ready.
3. **Task 2** (detail view, ~5h) — Sonnet subagent (variant matrix) → parallelizzabile con Task 1.
4. **Task 3** (new wizard, ~4h) — Sonnet subagent (state complex) → parallelizzabile.
5. **Task 4** (edit con K5 gate, ~2h) — Haiku subagent (after Task 3).
6. **Task 5** (stats reskin, ~2.5h) — Haiku subagent → parallelizzabile.

**Effort totale stimato**: ~20h subagent (mix Haiku + Sonnet, model-mix on complexity per P120).

**PR strategy** (opzione A: single PR):
- 1 PR feature/issue-1488-play-records-reskin con tutti 6 task in commit separati. Vantaggio: review olistica. Svantaggio: PR grosso, conflict risk.

**PR strategy** (opzione B: split per task):
- 6 PR sequenziali, base main-dev. Vantaggio: piccoli + reviewable. Svantaggio: blocking review chain.

**Raccomandazione**: opzione **A** single PR — il reskin è coeso, conviene merge atomico per evitare design drift fra route durante le review intermedie. Se PR grow >1500 LOC, split a B.

### Post-merge checklist

- [ ] Aggiornare `v2-migration-matrix.md`: 5 righe play-records `pending → done`
- [ ] Aggiornare `epic-1475-refresh-2026-05-28.md` (Phase D #1488 → DONE)
- [ ] Commento conclusivo su #1488 con link PR + screenshot 5 route
- [ ] Verificare `pnpm test:i18n` (se esiste) o grep per hardcoded IT strings
- [ ] Lighthouse a11y score >= 90 su tutte le 5 route
- [ ] Update epic #1475 body (Phase D 0/5 → 1/5)
- [ ] BE follow-up issues aperte: (a) `topScores[]` su PlayRecordSummary, (b) `UpdatePlayRecordRequest` expansion, (c) `gameSlug` su summary, (d) batch `GET /games?ids=` endpoint

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
