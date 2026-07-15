# SP9 Dashboard Mobile (Screen A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the mobile-first dashboard GameNight surface (Screen A of SP9 #2989): the greenfield `PendingRsvpCard` (invariant #17) and `RecentSection` (invariant #4), and wire them mobile-first into `HomeFeed`.

**Architecture:** Presentational-first. `PendingRsvpCard` is a **pure component** (props in, callbacks out) so it is fully testable without backend changes. `RecentSection` consumes the existing `useCompletedGameNights` hook. `HomeFeed` gets a mobile-first container + the new Recenti section. Wiring the pending-RSVP data source (which requires an `rsvpStatus`/`inviterName` field on the upcoming list) is isolated in the last task behind an explicit backend-dependency check.

**Tech Stack:** Next.js 16 / React 19, Tailwind 4 (semantic + entity tokens), Vitest + @testing-library/react, React Query hooks.

## Global Constraints

- **Touch targets ≥44×44px** on every interactive CTA (Conferma/Declina). Use `min-h-11` (44px) — SP8 gap B-02 was a 24px CTA, never repeat.
- **Zero hardcoded hex/rgba** in product UI. Use semantic/entity tokens only: `entity-event` (rose `--c-event`), `warning`/`--c-warning`, `--c-warning-ink` (AA text on cream), `text-muted-foreground`, `border-border`. No `rgba()` scrim literals (SP8 A-09/B-04 debt).
- **Italian UI copy**, warm/casual ("Da confermare", "Marco ti ha invitato", "Conferma", "Declina", "Vedi tutte le completate").
- **`prefers-reduced-motion`**: any transition must be disabled under it (match existing component patterns).
- **Test stack**: Vitest + `@testing-library/react` (`render`, `screen`, `within`), `data-testid` hooks, `describe/it`, props as a `baseProps` const — mirror `apps/web/src/components/features/game-night-detail/__tests__/GameNightRsvpRow.test.tsx`.
- **File placement**: new game-night presentational components go in `apps/web/src/components/game-night/` (next to `MeepleEventCard.tsx`); dashboard sections in `apps/web/src/components/features/home/`. The `v2/game-night/` dir does NOT exist — do not create it.
- **RsvpStatus** type (from `@/lib/api/schemas/game-nights.schemas`): `'Accepted' | 'Declined' | 'Maybe' | 'Pending'`.
- Run tests from `apps/web/`: `pnpm test <path>`. Quality gate before each commit: `pnpm typecheck && pnpm lint`.

---

### Task 1: `PendingRsvpCard` — pure component (invariant #17)

**Files:**
- Create: `apps/web/src/components/game-night/PendingRsvpCard.tsx`
- Test: `apps/web/src/components/game-night/__tests__/PendingRsvpCard.test.tsx`

**Interfaces:**
- Consumes: nothing (pure component).
- Produces:
  ```typescript
  export interface PendingRsvpCardProps {
    eventId: string;
    title: string;
    inviterName: string;
    onConfirm: () => void;
    onDecline: () => void;
    disabled?: boolean;      // offline: buttons disabled + tooltip
    className?: string;
  }
  export function PendingRsvpCard(props: PendingRsvpCardProps): JSX.Element;
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-night/__tests__/PendingRsvpCard.test.tsx
import { render, screen, within, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PendingRsvpCard } from '../PendingRsvpCard';

const baseProps = {
  eventId: 'gn-sat-marco',
  title: 'Serata da Marco',
  inviterName: 'Marco',
  onConfirm: () => {},
  onDecline: () => {},
};

describe('PendingRsvpCard', () => {
  it('renders the "Da confermare" badge, title and inviter', () => {
    render(<PendingRsvpCard {...baseProps} />);
    const card = screen.getByTestId('pending-rsvp-card');
    expect(card).toHaveAttribute('data-event-id', 'gn-sat-marco');
    expect(within(card).getByText('Da confermare')).toBeInTheDocument();
    expect(within(card).getByText('Serata da Marco')).toBeInTheDocument();
    expect(within(card).getByText(/Marco ti ha invitato/i)).toBeInTheDocument();
  });

  it('Conferma/Declina buttons are ≥44px and fire callbacks', () => {
    const onConfirm = vi.fn();
    const onDecline = vi.fn();
    render(<PendingRsvpCard {...baseProps} onConfirm={onConfirm} onDecline={onDecline} />);
    const confirm = screen.getByRole('button', { name: 'Conferma' });
    const decline = screen.getByRole('button', { name: 'Declina' });
    expect(confirm.className).toContain('min-h-11');
    expect(decline.className).toContain('min-h-11');
    fireEvent.click(confirm);
    fireEvent.click(decline);
    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onDecline).toHaveBeenCalledOnce();
  });

  it('disables buttons and shows offline tooltip when disabled', () => {
    render(<PendingRsvpCard {...baseProps} disabled />);
    expect(screen.getByRole('button', { name: 'Conferma' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Declina' })).toBeDisabled();
    expect(screen.getByTestId('pending-rsvp-card')).toHaveAttribute(
      'title',
      'Offline — RSVP disponibile alla riconnessione'
    );
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test src/components/game-night/__tests__/PendingRsvpCard.test.tsx`
Expected: FAIL — cannot resolve `../PendingRsvpCard`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/game-night/PendingRsvpCard.tsx
import { clsx } from 'clsx';

export interface PendingRsvpCardProps {
  eventId: string;
  title: string;
  inviterName: string;
  onConfirm: () => void;
  onDecline: () => void;
  disabled?: boolean;
  className?: string;
}

export function PendingRsvpCard({
  eventId,
  title,
  inviterName,
  onConfirm,
  onDecline,
  disabled = false,
  className,
}: PendingRsvpCardProps): JSX.Element {
  return (
    <div
      data-testid="pending-rsvp-card"
      data-event-id={eventId}
      title={disabled ? 'Offline — RSVP disponibile alla riconnessione' : undefined}
      className={clsx(
        'rounded-lg border border-dashed border-warning/50 bg-warning/[0.06] p-4',
        disabled && 'opacity-70',
        className
      )}
    >
      <span className="inline-flex items-center rounded-pill bg-warning/15 px-2 py-0.5 text-xs font-bold text-warning-ink">
        Da confermare
      </span>
      <h3 className="mt-2 font-quicksand font-bold text-base text-entity-event">{title}</h3>
      <p className="mt-0.5 text-sm text-muted-foreground">{inviterName} ti ha invitato</p>
      <div className="mt-3 flex gap-2">
        <button
          type="button"
          onClick={onConfirm}
          disabled={disabled}
          className="min-h-11 flex-1 rounded-md bg-entity-event font-display font-bold text-sm text-white transition-colors disabled:opacity-60 motion-reduce:transition-none"
        >
          Conferma
        </button>
        <button
          type="button"
          onClick={onDecline}
          disabled={disabled}
          className="min-h-11 flex-1 rounded-md border border-border-strong font-display font-bold text-sm text-muted-foreground transition-colors disabled:opacity-60 motion-reduce:transition-none"
        >
          Declina
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test src/components/game-night/__tests__/PendingRsvpCard.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 5: Quality gate + commit**

```bash
cd apps/web && pnpm typecheck && pnpm lint --file src/components/game-night/PendingRsvpCard.tsx
git add apps/web/src/components/game-night/PendingRsvpCard.tsx apps/web/src/components/game-night/__tests__/PendingRsvpCard.test.tsx
git commit -m "feat(game-night): PendingRsvpCard component (#2989 inv#17)"
```

---

### Task 2: `RecentSection` — dashboard Recenti (invariant #4)

**Files:**
- Create: `apps/web/src/components/features/home/RecentSection.tsx`
- Test: `apps/web/src/components/features/home/__tests__/RecentSection.test.tsx`

**Interfaces:**
- Consumes: `useCompletedGameNights({ limit })` from `@/hooks/queries/useGameNights` → `useQuery` returning `{ data, isLoading, isError }` where `data` is an array of `{ id, title, scheduledAt, location }`. `MeepleEventCard` from `@/components/game-night/MeepleEventCard`.
- Produces:
  ```typescript
  export interface RecentSectionProps {
    onOpenDetail: (id: string) => void;
    onSeeAll: () => void;
  }
  export function RecentSection(props: RecentSectionProps): JSX.Element;
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/features/home/__tests__/RecentSection.test.tsx
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { RecentSection } from '../RecentSection';

const mockUseCompleted = vi.fn();
vi.mock('@/hooks/queries/useGameNights', () => ({
  useCompletedGameNights: (opts: unknown) => mockUseCompleted(opts),
}));

const baseProps = { onOpenDetail: () => {}, onSeeAll: () => {} };

describe('RecentSection', () => {
  it('renders completed game-nights with the "Recenti" heading', () => {
    mockUseCompleted.mockReturnValue({
      data: [{ id: 'gn-thu', title: 'Giovedì Wingspan', scheduledAt: '2026-07-09T20:00:00Z', location: 'Casa Anna' }],
      isLoading: false, isError: false,
    });
    render(<RecentSection {...baseProps} />);
    const section = screen.getByTestId('recent-section');
    expect(within(section).getByRole('heading', { name: 'Recenti' })).toBeInTheDocument();
    expect(within(section).getByText('Giovedì Wingspan')).toBeInTheDocument();
    expect(within(section).getByRole('button', { name: /Vedi tutte le completate/i })).toBeInTheDocument();
  });

  it('renders empty state when there are no completed game-nights', () => {
    mockUseCompleted.mockReturnValue({ data: [], isLoading: false, isError: false });
    render(<RecentSection {...baseProps} />);
    expect(screen.getByText('Nessuna partita ancora')).toBeInTheDocument();
  });

  it('renders a skeleton while loading (no spinner)', () => {
    mockUseCompleted.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    render(<RecentSection {...baseProps} />);
    expect(screen.getByTestId('recent-section-skeleton')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test src/components/features/home/__tests__/RecentSection.test.tsx`
Expected: FAIL — cannot resolve `../RecentSection`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/features/home/RecentSection.tsx
import { useCompletedGameNights } from '@/hooks/queries/useGameNights';
import { MeepleEventCard } from '@/components/game-night/MeepleEventCard';
import { SkeletonCardGrid } from '@/components/ui/feedback/SkeletonCardGrid';

export interface RecentSectionProps {
  onOpenDetail: (id: string) => void;
  onSeeAll: () => void;
}

export function RecentSection({ onOpenDetail, onSeeAll }: RecentSectionProps): JSX.Element {
  const { data, isLoading } = useCompletedGameNights({ limit: 5 });
  const nights = data ?? [];

  return (
    <section data-testid="recent-section">
      <div className="mb-3 flex items-center gap-3">
        <span className="font-mono text-xs font-bold uppercase tracking-wider text-muted-foreground">Recenti</span>
        <span className="h-px flex-1 bg-border" />
      </div>
      {isLoading ? (
        <div data-testid="recent-section-skeleton"><SkeletonCardGrid count={2} /></div>
      ) : nights.length === 0 ? (
        <div className="rounded-lg border border-border bg-card p-6 text-center">
          <p className="text-sm text-muted-foreground">Nessuna partita ancora</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-3">
          {nights.map(night => (
            <MeepleEventCard
              key={night.id}
              event={{ id: night.id, title: night.title, scheduledAt: night.scheduledAt, location: night.location ?? null, participantCount: 0, gameCount: 0 }}
              variant="list"
              onClick={() => onOpenDetail(night.id)}
            />
          ))}
          <button
            type="button"
            onClick={onSeeAll}
            className="min-h-11 rounded-md border border-dashed border-border-strong font-display font-bold text-sm text-muted-foreground transition-colors motion-reduce:transition-none"
          >
            Vedi tutte le completate →
          </button>
        </div>
      )}
    </section>
  );
}
```

> **Step 3 note:** verify the real import path of `SkeletonCardGrid` before writing (grep `SkeletonCardGrid` — `HomeFeed.tsx` already imports it; copy that exact import). If `useCompletedGameNights` return item shape differs (field names), adjust the `MeepleEventCard` mapping to the real fields found in `useGameNights.ts`.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test src/components/features/home/__tests__/RecentSection.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 5: Quality gate + commit**

```bash
cd apps/web && pnpm typecheck && pnpm lint --file src/components/features/home/RecentSection.tsx
git add apps/web/src/components/features/home/RecentSection.tsx apps/web/src/components/features/home/__tests__/RecentSection.test.tsx
git commit -m "feat(home): RecentSection dashboard Recenti (#2989 inv#4)"
```

---

### Task 3: Integrate `RecentSection` + mobile-first container in `HomeFeed`

**Files:**
- Modify: `apps/web/src/components/features/home/HomeFeed.tsx` (container class line ~45; add `RecentSection` after the "Serate di Gioco" section ~line 149)
- Test: extend `apps/web/src/components/features/home/__tests__/HomeFeed.test.tsx` (create if absent)

**Interfaces:**
- Consumes: `RecentSection` (Task 2). Existing `router` (from `useRouter`) and `openDetail(id, entity)` already in `HomeFeed`.

- [ ] **Step 1: Write the failing test** — assert `RecentSection` renders below the "Serate di Gioco" heading.

```tsx
// add to apps/web/src/components/features/home/__tests__/HomeFeed.test.tsx
it('renders the Recenti section after Serate di Gioco', () => {
  // mock hooks per existing HomeFeed test setup, then:
  render(<HomeFeed />);
  expect(screen.getByTestId('recent-section')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test src/components/features/home/__tests__/HomeFeed.test.tsx`
Expected: FAIL — no `recent-section` testid.

- [ ] **Step 3: Implement — mobile-first container + mount RecentSection**

Change the container (line ~45) to mobile-first with bottom-bar safe padding:
```tsx
// from: <div className="p-4 sm:p-6 space-y-8">
<div className="p-3 pb-20 sm:p-6 sm:pb-6 space-y-8">
```
After the "Serate di Gioco" `</section>` (line ~149), mount:
```tsx
<RecentSection
  onOpenDetail={(id) => openDetail(id, 'event')}
  onSeeAll={() => router.push('/game-nights?filter=completed')}
/>
```
Add the import at the top:
```tsx
import { RecentSection } from './RecentSection';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test src/components/features/home/__tests__/HomeFeed.test.tsx`
Expected: PASS.

- [ ] **Step 5: Quality gate + commit**

```bash
cd apps/web && pnpm typecheck && pnpm lint --file src/components/features/home/HomeFeed.tsx
git add apps/web/src/components/features/home/HomeFeed.tsx apps/web/src/components/features/home/__tests__/HomeFeed.test.tsx
git commit -m "feat(home): mount Recenti section + mobile-first container (#2989 inv#4)"
```

---

### Task 4: Wire pending-RSVP into the upcoming list — **BACKEND DEPENDENCY GATE**

**Files (conditional):**
- Investigate: `apps/web/src/lib/api/schemas/game-nights.schemas.*` + the `getUpcoming` DTO (`apps/api/**` GameNight upcoming query).
- Modify: `apps/web/src/components/features/home/HomeFeed.tsx` (Serate di Gioco section — inject `PendingRsvpCard` for pending items).

**Interfaces:**
- Consumes: `PendingRsvpCard` (Task 1), `useRsvpGameNight()` → `mutate({ id, response: 'Accepted' | 'Declined' })`.

- [ ] **Step 1: Dependency check** — does the upcoming game-night item expose `rsvpStatus` and `inviterName`?
  Run: grep the upcoming DTO/schema.
  ```bash
  grep -rn "rsvpStatus\|inviterName\|getUpcoming" apps/web/src/lib/api/schemas/ apps/api/src/Api/BoundedContexts/*/Application/**/GetUpcoming* 2>/dev/null
  ```
  - **If both fields exist** → proceed to Step 2 (FE-only).
  - **If missing** → STOP. This needs a backend task first (extend the upcoming DTO with `rsvpStatus` + `inviterName`, following CQRS: query → DTO → handler → test). Split that into a separate backend plan; do NOT fake the field in the FE.

- [ ] **Step 2 (FE, only if fields exist): Write the failing test** — a pending item renders `PendingRsvpCard`, Conferma calls `useRsvpGameNight().mutate({ id, response: 'Accepted' })`.

- [ ] **Step 3: Implement** — in the Serate di Gioco map, branch: `night.rsvpStatus === 'Pending'` → `<PendingRsvpCard title={night.title} inviterName={night.inviterName} onConfirm={() => rsvp.mutate({ id: night.id, response: 'Accepted' })} onDecline={() => rsvp.mutate({ id: night.id, response: 'Declined' })} disabled={isOffline} />`; else the existing `<MeepleEventCard>`. Order pending after planned per mockup A-01.

- [ ] **Step 4: Run tests** — Expected PASS.

- [ ] **Step 5: Commit** — `feat(home): wire pending-RSVP card in upcoming list (#2989 inv#17)`.

---

## Self-Review

- **Spec coverage**: PendingRsvpCard (#17) → Task 1 + 4; RecentSection (#4) → Task 2 + 3; mobile-first HomeFeed → Task 3. Loading/error/offline states: skeleton (Task 2), offline-disabled (Task 1), error banner for Recenti = **follow-up** (not covered here — add if the completed query can error visibly; noted as gap).
- **Placeholder scan**: Task 4 is intentionally gated on a real backend-field check (not a fake). Tasks 1–3 have complete code. `SkeletonCardGrid` import + `useCompletedGameNights` item shape flagged for verification in Task 2 Step 3 note (real risk, not a placeholder).
- **Type consistency**: `RsvpStatus` values `'Accepted' | 'Declined'` used consistently; `PendingRsvpCardProps` names match between Task 1 and Task 4 usage; `RecentSectionProps` (`onOpenDetail`, `onSeeAll`) consistent between Task 2 and Task 3.

## Execution status (2026-07-15)

- ✅ **Task 1** — `PendingRsvpCard` (commit `4859045a9`, 3 test pass).
- ✅ **Task 2** — `RecentSection` (commit `0dcba69fa`, 3 test pass).
- ✅ **Task 3** — HomeFeed mobile-first + Recenti mounted (commit `89c591eaf`, typecheck pass). Test HomeFeed dedicato saltato (deviazione: RecentSection già testato in isolamento).
- ⛔ **Task 4 — BLOCKED (backend gate)**: il DTO `upcoming` (`api.gameNights.getUpcoming` → `GameNightDto[]`) NON espone `viewerRsvpStatus` né `inviterName`. Servono prima: (1) BE — estendere la query/DTO upcoming con lo stato RSVP del viewer + nome invitante (CQRS: query → DTO → handler → test); (2) FE — schema zod + wire `PendingRsvpCard` nella sezione "Serate di Gioco". Tracciato come sotto-task backend separato. `PendingRsvpCard` è pronto e testato, riusabile appena il campo esiste.

## Scope note

Screens **B (/game-nights index)** and **C (detail-RSVP)** are separate plans — their components mostly exist and need mobile adaptation (e.g. sticky-bottom RSVP bar), not greenfield. This plan (Screen A) produces working, testable software on its own: two new tested components + a mobile-first dashboard integration.
