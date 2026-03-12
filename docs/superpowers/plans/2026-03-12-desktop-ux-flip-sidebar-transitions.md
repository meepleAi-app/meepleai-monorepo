# Desktop UX: Card Flip, Sidebar, View Transitions — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enhance desktop UX with contextual game card back, sidebar styling improvements, and View Transitions API integration.

**Architecture:** Three independent feature chunks that share one file (`SidebarContextNav.tsx`). Chunk 1 rewrites `GameBackContent.tsx` internals + updates consumer. Chunk 2 adds CSS-only styling to sidebar links. Chunk 3 introduces a new `useViewTransition` hook wired into sidebar and main content area.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, Framer Motion, Lucide React, date-fns, View Transitions API (browser-native)

**Spec:** `docs/superpowers/specs/2026-03-12-desktop-ux-flip-cardrack-transitions-design.md`

---

## File Map

```
Modified:
  apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx  ← Chunk 1
  apps/web/src/components/ui/data-display/meeple-card.tsx                           ← Chunk 1
  apps/web/src/components/library/MeepleLibraryGameCard.tsx                          ← Chunk 1
  apps/web/src/components/library/__tests__/MeepleLibraryGameCard.test.tsx           ← Chunk 1
  apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx                       ← Chunk 2 + 3
  apps/web/src/components/layout/Sidebar/__tests__/Sidebar.test.tsx                  ← Chunk 2
  apps/web/src/components/layout/AppShell/AppShellClient.tsx                         ← Chunk 3
  apps/web/src/styles/globals.css                                                    ← Chunk 3

New:
  apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx  ← Chunk 1
  apps/web/src/lib/hooks/useViewTransition.ts                                        ← Chunk 3
  apps/web/src/lib/hooks/__tests__/useViewTransition.test.ts                         ← Chunk 3
  apps/web/src/types/view-transitions.d.ts                                           ← Chunk 3 (if needed)
```

---

## Chunk 1: GameBackContent Enhancement

### Task 1: Update interfaces (GameBackData + GameBackActions)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx:37-76`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx` (NEW)

- [ ] **Step 1: Create test file with interface-level tests**

```tsx
// apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { GameBackContent, type GameBackData, type GameBackActions } from '../GameBackContent';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

const BASE_DATA: GameBackData = {
  complexityRating: 3,
  playingTimeMinutes: 90,
  minPlayers: 3,
  maxPlayers: 4,
  averageRating: 7.2,
  timesPlayed: 12,
  hasKb: true,
  kbCardCount: 2,
  kbDocuments: [{ id: '1', fileName: 'rules.pdf', status: 'Ready' }],
  lastPlayedAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
  winRate: 33,
  entityLinkCount: 3,
  noteCount: 2,
};

const BASE_ACTIONS: GameBackActions = {
  onChatAgent: vi.fn(),
  onToggleFavorite: vi.fn(),
  isFavorite: false,
  onNewSession: vi.fn(),
  onViewLinks: vi.fn(),
};

describe('GameBackContent', () => {
  it('renders with all data', () => {
    render(
      <GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" detailHref="/games/1" />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: PASS (existing component renders, new fields are optional)

- [ ] **Step 3: Update `GameBackData` interface**

In `GameBackContent.tsx`, replace the `GameBackData` interface (lines 37-56) with:

```typescript
export interface GameBackData {
  // Info chips
  complexityRating?: number | null;
  playingTimeMinutes?: number | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  averageRating?: number | null;
  timesPlayed?: number;

  // KB context label
  kbDocuments?: Array<{ id: string; fileName: string; status: string }>;
  hasKb?: boolean;
  kbCardCount?: number;

  // Enriched header
  lastPlayedAt?: string;
  winRate?: number;

  // Contextual actions
  nextGameNight?: string;
  entityLinkCount?: number;

  // Footer
  noteCount?: number;
}
```

- [ ] **Step 4: Update `GameBackActions` interface**

Replace the `GameBackActions` interface (lines 58-64) with:

```typescript
export interface GameBackActions {
  onChatAgent?: () => void;
  onToggleFavorite?: () => void;
  isFavorite?: boolean;
  onNewSession?: () => void;
  onAddToGameNight?: () => void;
  onViewLinks?: () => void;
}
```

- [ ] **Step 5: Run test to verify it still passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-flip): update GameBackData and GameBackActions interfaces"
```

---

### Task 2: Replace StatChip grid with InfoChip row + enriched header

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Add InfoChip + header tests**

Append to the test file:

```tsx
describe('Enriched Header', () => {
  it('shows "Mai giocato" when lastPlayedAt is null', () => {
    render(<GameBackContent data={{ ...BASE_DATA, lastPlayedAt: undefined }} title="Catan" />);
    expect(screen.getByText(/Mai giocato/)).toBeInTheDocument();
  });

  it('shows relative date when lastPlayedAt is set', () => {
    render(<GameBackContent data={BASE_DATA} title="Catan" />);
    // date-fns formatDistanceToNow returns Italian "3 giorni fa" (approx)
    expect(screen.getByText(/giorni? fa/i)).toBeInTheDocument();
  });

  it('shows win rate when defined', () => {
    render(<GameBackContent data={BASE_DATA} title="Catan" />);
    expect(screen.getByText(/Win rate 33%/)).toBeInTheDocument();
  });

  it('omits win rate when undefined', () => {
    render(<GameBackContent data={{ ...BASE_DATA, winRate: undefined }} title="Catan" />);
    expect(screen.queryByText(/Win rate/)).not.toBeInTheDocument();
  });
});

describe('InfoChips', () => {
  it('renders player range chip', () => {
    render(<GameBackContent data={BASE_DATA} title="Catan" />);
    expect(screen.getByText('3-4')).toBeInTheDocument();
  });

  it('renders playing time chip', () => {
    render(<GameBackContent data={BASE_DATA} title="Catan" />);
    expect(screen.getByText(/90min/)).toBeInTheDocument();
  });

  it('renders complexity dots', () => {
    const { container } = render(<GameBackContent data={BASE_DATA} title="Catan" />);
    const dots = container.querySelectorAll('.rounded-full.w-2.h-2');
    expect(dots.length).toBe(5);
  });

  it('hides complexity when null', () => {
    const { container } = render(
      <GameBackContent data={{ ...BASE_DATA, complexityRating: null }} title="Catan" />
    );
    const dots = container.querySelectorAll('.rounded-full.w-2.h-2');
    expect(dots.length).toBe(0);
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL (old StatChip grid still renders)**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: Several FAIL — "Mai giocato" not found, no `.rounded-full.w-2.h-2` dots, etc.

- [ ] **Step 3: Remove old `StatChip` sub-component (lines 82-103)**

Delete the entire `StatChip` function and its type signature.

- [ ] **Step 4: Add `InfoChip` sub-component + `complexityDots` logic**

Replace `StatChip` with:

```tsx
function InfoChip({ children }: { children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1 bg-muted/40 px-2 py-0.5 rounded-md text-xs text-card-foreground">
      {children}
    </span>
  );
}
```

- [ ] **Step 5: Rewrite header section (lines 178-194)**

Replace the current header with enriched version:

```tsx
{/* Entity-colored header */}
<div
  className="relative overflow-hidden px-5 pb-3 pt-5"
  style={{ backgroundColor: `hsl(${entityColor})` }}
>
  <div
    className="pointer-events-none absolute inset-0 opacity-[0.12]"
    style={{
      backgroundImage:
        'repeating-linear-gradient(45deg, transparent, transparent 10px, currentColor 10px, currentColor 11px)',
    }}
    aria-hidden="true"
  />
  <h2 className="relative z-[1] font-quicksand text-lg font-bold text-white line-clamp-1">
    {title || 'Statistiche'}
  </h2>
  {(lastPlayedAt || winRate != null) && (
    <p className="relative z-[1] text-xs text-white/75 mt-0.5">
      {lastPlayedLabel}
      {winRate != null && ` · Win rate ${winRate}%`}
    </p>
  )}
</div>
```

Add imports at the **top of the file** (file scope, not inside the component):

```tsx
import { formatDistanceToNow } from 'date-fns';
import { it as itLocale } from 'date-fns/locale';
```

Add inside the component, above the return statement:

```tsx
const { lastPlayedAt, winRate, entityLinkCount = 0, nextGameNight, noteCount } = data;

const lastPlayedLabel = lastPlayedAt
  ? formatDistanceToNow(new Date(lastPlayedAt), { locale: itLocale, addSuffix: true })
  : 'Mai giocato';
```

- [ ] **Step 6: Replace stats grid (lines 198-226) with InfoChip row**

```tsx
{/* Info chips */}
<div className="flex gap-1.5 flex-wrap">
  <InfoChip><Users className="w-3 h-3" /> {playerRange}</InfoChip>
  {playingTimeMinutes != null && (
    <InfoChip><Clock className="w-3 h-3" /> {playingTimeMinutes}min</InfoChip>
  )}
  {complexityDots && <InfoChip>{complexityDots}</InfoChip>}
  {averageRating != null && (
    <InfoChip><Star className="w-3 h-3" /> {averageRating.toFixed(1)}</InfoChip>
  )}
</div>
```

Where `complexityDots` is computed before the return:

```tsx
const complexityDots = complexityRating != null
  ? Array.from({ length: 5 }, (_, i) => (
      <span key={i} className={`inline-block w-2 h-2 rounded-full ${i < Math.round(complexityRating) ? 'bg-current' : 'bg-current/20'}`} />
    ))
  : null;
```

- [ ] **Step 7: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-flip): replace StatChip grid with InfoChip row + enriched header"
```

---

### Task 3: Replace ActionButton grid with ContextualAction list + KB condensing

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Add ContextualAction tests**

```tsx
import userEvent from '@testing-library/user-event';

describe('ContextualActions', () => {
  it('fires onNewSession with stopPropagation', async () => {
    const onNewSession = vi.fn();
    const outerClick = vi.fn();
    const { container } = render(
      <div onClick={outerClick}>
        <GameBackContent data={BASE_DATA} actions={{ ...BASE_ACTIONS, onNewSession }} title="Catan" />
      </div>
    );
    await userEvent.click(screen.getByText('Nuova Sessione'));
    expect(onNewSession).toHaveBeenCalledOnce();
    expect(outerClick).not.toHaveBeenCalled();
  });

  it('shows "Nessuna partita" when timesPlayed is 0', () => {
    render(<GameBackContent data={{ ...BASE_DATA, timesPlayed: 0 }} actions={BASE_ACTIONS} title="Catan" />);
    expect(screen.getByText('Nessuna partita')).toBeInTheDocument();
  });

  it('shows play count as context', () => {
    render(<GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" />);
    expect(screen.getByText('12 partite giocate')).toBeInTheDocument();
  });

  it('hides "Espansioni" when entityLinkCount is 0', () => {
    render(<GameBackContent data={{ ...BASE_DATA, entityLinkCount: 0 }} actions={BASE_ACTIONS} title="Catan" />);
    expect(screen.queryByText('Espansioni')).not.toBeInTheDocument();
  });

  it('shows "Espansioni" with count when entityLinkCount > 0', () => {
    render(<GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" />);
    expect(screen.getByText('Espansioni')).toBeInTheDocument();
    expect(screen.getByText('3 collegate')).toBeInTheDocument();
  });

  it('hides "Aggiungi a serata" when onAddToGameNight is null', () => {
    render(<GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" />);
    expect(screen.queryByText('Aggiungi a serata')).not.toBeInTheDocument();
  });

  it('shows "Aggiungi a serata" when onAddToGameNight provided', () => {
    render(
      <GameBackContent
        data={{ ...BASE_DATA, nextGameNight: 'Sab 21:00' }}
        actions={{ ...BASE_ACTIONS, onAddToGameNight: vi.fn() }}
        title="Catan"
      />
    );
    expect(screen.getByText('Aggiungi a serata')).toBeInTheDocument();
    expect(screen.getByText('Sab 21:00')).toBeInTheDocument();
  });
});

describe('KB Context Label', () => {
  it('shows "KB pronta · N doc" when hasKb', () => {
    render(<GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" />);
    expect(screen.getByText('KB pronta · 2 doc')).toBeInTheDocument();
  });

  it('shows "KB in elaborazione" when docs not ready', () => {
    render(
      <GameBackContent
        data={{ ...BASE_DATA, hasKb: false, kbDocuments: [{ id: '1', fileName: 'a.pdf', status: 'Processing' }] }}
        actions={BASE_ACTIONS}
        title="Catan"
      />
    );
    expect(screen.getByText('KB in elaborazione')).toBeInTheDocument();
  });

  it('shows null context when no KB at all', () => {
    render(
      <GameBackContent
        data={{ ...BASE_DATA, hasKb: false, kbDocuments: [], kbCardCount: 0 }}
        actions={BASE_ACTIONS}
        title="Catan"
      />
    );
    // The "Chiedi all'AI" button should still appear but without context
    expect(screen.getByText("Chiedi all'AI")).toBeInTheDocument();
    expect(screen.queryByText(/KB pronta/)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: FAIL — old ActionButton grid renders, no "Nuova Sessione" text

- [ ] **Step 3: Remove `ActionButton` sub-component (lines 105-137)**

Delete the entire `ActionButton` function.

- [ ] **Step 4: Add `ContextualAction` sub-component**

```tsx
import { Play, Bot, Calendar, Link2 } from 'lucide-react';

function ContextualAction({
  icon: Icon,
  label,
  context,
  colorHsl,
  onClick,
}: {
  icon: React.ElementType;
  label: string;
  context?: string | null;
  colorHsl: string;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      className="flex items-center gap-2 w-full rounded-lg px-3 py-2 text-xs font-medium transition-colors"
      style={{
        backgroundColor: `hsla(${colorHsl}, 0.08)`,
        borderColor: `hsla(${colorHsl}, 0.15)`,
        color: `hsl(${colorHsl})`,
      }}
      onClick={(e) => {
        e.stopPropagation();
        onClick?.();
      }}
    >
      <Icon className="w-4 h-4 shrink-0" />
      <span>{label}</span>
      {context && (
        <span className="ml-auto text-[10px] text-muted-foreground">{context}</span>
      )}
    </button>
  );
}
```

- [ ] **Step 5: Replace actions section + KB preview with ContextualAction list**

Remove the entire KB preview section (lines 228-262) and the quick actions grid (lines 264-303). Replace with:

```tsx
const kbContextLabel = hasKb
  ? `KB pronta · ${kbCardCount} doc`
  : kbDocuments?.some(d => d.status !== 'Ready')
    ? 'KB in elaborazione'
    : null;

const timesPlayedContext = timesPlayed
  ? `${timesPlayed} partite giocate`
  : 'Nessuna partita';
```

And in the JSX:

```tsx
{/* Contextual actions */}
{actions && (
  <div className="flex flex-col gap-1.5" data-testid="game-back-actions">
    {actions.onNewSession && (
      <ContextualAction
        icon={Play}
        label="Nuova Sessione"
        context={timesPlayedContext}
        colorHsl="25 95% 45%"
        onClick={actions.onNewSession}
      />
    )}
    <ContextualAction
      icon={Bot}
      label="Chiedi all'AI"
      context={kbContextLabel}
      colorHsl="262 83% 58%"
      onClick={actions.onChatAgent}
    />
    {actions.onAddToGameNight && (
      <ContextualAction
        icon={Calendar}
        label="Aggiungi a serata"
        context={nextGameNight}
        colorHsl="217 91% 60%"
        onClick={actions.onAddToGameNight}
      />
    )}
    {entityLinkCount > 0 && actions.onViewLinks && (
      <ContextualAction
        icon={Link2}
        label="Espansioni"
        context={`${entityLinkCount} collegate`}
        colorHsl="142 70% 45%"
        onClick={actions.onViewLinks}
      />
    )}
  </div>
)}
```

- [ ] **Step 6: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-flip): replace ActionButton grid with ContextualAction list + KB condensing"
```

---

### Task 4: Compact footer + detail link

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Add footer tests**

```tsx
describe('Compact Footer', () => {
  it('shows filled heart when isFavorite', () => {
    render(
      <GameBackContent
        data={BASE_DATA}
        actions={{ ...BASE_ACTIONS, isFavorite: true }}
        title="Catan"
        detailHref="/games/1"
      />
    );
    expect(screen.getByText('Pref')).toBeInTheDocument();
  });

  it('shows noteCount when > 0', () => {
    render(<GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" />);
    expect(screen.getByText('2')).toBeInTheDocument(); // noteCount=2
  });

  it('hides noteCount when 0', () => {
    render(
      <GameBackContent data={{ ...BASE_DATA, noteCount: 0 }} actions={BASE_ACTIONS} title="Catan" />
    );
    // noteCount=0 → no sticky note icon rendered
    expect(screen.queryByText('0')).not.toBeInTheDocument();
  });

  it('renders detail link', () => {
    render(<GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" detailHref="/games/1" />);
    const link = screen.getByText('Dettaglio →');
    expect(link.closest('a')).toHaveAttribute('href', '/games/1');
  });

  it('rejects unsafe hrefs', () => {
    render(<GameBackContent data={BASE_DATA} actions={BASE_ACTIONS} title="Catan" detailHref="//evil.com" />);
    expect(screen.queryByText('Dettaglio →')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Expected: FAIL — old "Vai alla pagina gioco" link exists, not "Dettaglio →"

- [ ] **Step 3: Replace detail link section with compact footer**

Remove the old detail link div (lines 305-319). Replace with:

```tsx
{/* Compact footer */}
<div className="mt-auto border-t border-border/10 pt-2 flex items-center justify-between text-xs">
  <div className="flex gap-3 text-muted-foreground">
    {actions?.onToggleFavorite && (
      <button
        type="button"
        className="inline-flex items-center gap-1 hover:text-card-foreground transition-colors"
        onClick={(e) => { e.stopPropagation(); actions.onToggleFavorite?.(); }}
      >
        {actions.isFavorite
          ? <Heart className="w-3 h-3 fill-current" />
          : <Heart className="w-3 h-3" />}
        Pref
      </button>
    )}
    {noteCount != null && noteCount > 0 && (
      <span className="inline-flex items-center gap-1">
        <StickyNote className="w-3 h-3" /> {noteCount}
      </span>
    )}
  </div>
  {detailHref && isSafeHref(detailHref) && (
    <Link
      href={detailHref}
      className="font-medium font-nunito"
      style={{ color: `hsl(${entityColor})` }}
      onClick={(e) => e.stopPropagation()}
    >
      Dettaglio →
    </Link>
  )}
</div>
```

- [ ] **Step 4: Clean up unused imports**

Remove imports no longer used: `ExternalLink`, `BookOpen`, `FileText`, `Weight`, `Gamepad2`. Add new imports: `Play`, `Bot`, `Calendar`, `Link2`, `formatDistanceToNow`, `it as itLocale`.

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-flip): compact footer with favorite toggle, noteCount, and detail link"
```

---

### Task 5: Update consumer — MeepleLibraryGameCard

**Files:**
- Modify: `apps/web/src/components/library/MeepleLibraryGameCard.tsx:330-380`
- Modify: `apps/web/src/components/library/__tests__/MeepleLibraryGameCard.test.tsx`

- [ ] **Step 1: Update `gameBackData` useMemo in MeepleLibraryGameCard.tsx**

At line ~333, add new fields to the returned object:

```typescript
const gameBackData: GameBackData | undefined = useMemo(() => {
  if (!flippable) return undefined;
  return {
    complexityRating: game.complexityRating,
    playingTimeMinutes: game.playingTimeMinutes,
    minPlayers: game.minPlayers,
    maxPlayers: game.maxPlayers,
    averageRating: game.averageRating,
    timesPlayed: game.playCount ?? 0,  // wire from real data (was hardcoded 0)
    hasKb: game.hasKb,
    kbCardCount: game.kbCardCount,
    kbDocuments: kbDocuments?.map(d => ({
      id: d.id,
      fileName: d.fileName,
      status: getDocumentStatus(d),
    })),
    lastPlayedAt: game.lastPlayed ?? undefined,  // NEW: mapped from lastPlayed
  };
}, [
  flippable,
  game.complexityRating,
  game.playingTimeMinutes,
  game.minPlayers,
  game.maxPlayers,
  game.averageRating,
  game.hasKb,
  game.kbCardCount,
  game.playCount,       // NEW
  game.lastPlayed,      // NEW
  kbDocuments,
]);
```

- [ ] **Step 2: Update `gameBackActions` useMemo**

At line ~362, remove `onViewKb` and `onEditNotes`, add new actions:

```typescript
const gameBackActions: GameBackActions | undefined = useMemo(() => {
  if (!flippable) return undefined;
  return {
    onChatAgent: game.hasKb
      ? () => { window.location.href = `/chat/new?game=${game.gameId}`; }
      : undefined,
    onToggleFavorite: handleToggleFavorite,
    isFavorite: game.isFavorite,
    onNewSession: () => { window.location.href = `/sessions/new?game=${game.gameId}`; },
    onViewLinks: () => { window.location.href = `/library/games/${game.gameId}#links`; },
  };
}, [
  flippable,
  game.hasKb,
  game.gameId,
  game.isFavorite,
  handleToggleFavorite,
]);
```

- [ ] **Step 3: Update test file to match new interfaces**

In `MeepleLibraryGameCard.test.tsx`, any test that references `onViewKb` or `onEditNotes` in assertions must be updated. Search for these strings and remove/replace assertions.

- [ ] **Step 4: Run full test suite for library**

Run: `cd apps/web && pnpm vitest run src/components/library/__tests__/MeepleLibraryGameCard.test.tsx`
Expected: PASS

- [ ] **Step 5: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS (no type errors from removed interface fields)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/MeepleLibraryGameCard.tsx apps/web/src/components/library/__tests__/MeepleLibraryGameCard.test.tsx
git commit -m "feat(card-flip): update MeepleLibraryGameCard consumer for new GameBack interfaces"
```

---

## Chunk 2: Sidebar Styling

### Task 6: SidebarLink left border + tooltip + micro-interactions

**Files:**
- Modify: `apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx:51-82`
- Modify: `apps/web/src/components/layout/Sidebar/__tests__/Sidebar.test.tsx`

- [ ] **Step 1: Add sidebar styling tests**

Append to `Sidebar.test.tsx`:

```tsx
describe('SidebarLink styling', () => {
  it('renders left border on active link', () => {
    (usePathname as Mock).mockReturnValue('/dashboard');
    (useCurrentUser as Mock).mockReturnValue({ data: { id: '1' } });
    render(<Sidebar isCollapsed={false} onToggle={vi.fn()} />);
    const activeLink = screen.getByText('Dashboard').closest('a');
    expect(activeLink?.className).toContain('border-l-[3px]');
    expect(activeLink?.className).toContain('border-[hsl(25_95%_45%)]');
  });

  it('shows tooltip when collapsed', () => {
    (usePathname as Mock).mockReturnValue('/dashboard');
    (useCurrentUser as Mock).mockReturnValue({ data: { id: '1' } });
    render(<Sidebar isCollapsed={true} onToggle={vi.fn()} />);
    // Tooltip spans should exist with label text (opacity-0 by default, group-hover:opacity-100)
    const tooltips = screen.getAllByText('Dashboard');
    // One is the tooltip (hidden by opacity)
    expect(tooltips.length).toBeGreaterThanOrEqual(1);
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `cd apps/web && pnpm vitest run src/components/layout/Sidebar/__tests__/Sidebar.test.tsx`
Expected: FAIL — no `border-l-[3px]` class in current code

- [ ] **Step 3: Update `SidebarLink` in `SidebarContextNav.tsx`**

Replace the `SidebarLink` function (lines 51-82):

```tsx
function SidebarLink({
  href,
  icon: Icon,
  label,
  isActive,
  isCollapsed,
}: {
  href: string;
  icon: React.ElementType;
  label: string;
  isActive?: boolean;
  isCollapsed: boolean;
}) {
  return (
    <Link
      href={href}
      className={cn(
        'group relative flex items-center gap-3 rounded-lg text-sm font-medium',
        'min-h-[44px] px-3 py-2',
        'transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-1',
        'border-l-[3px]',
        isActive
          ? 'bg-[hsl(25_95%_45%/0.08)] text-[hsl(25_95%_42%)] font-semibold border-[hsl(25_95%_45%)]'
          : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground border-transparent',
        isCollapsed && 'justify-center px-2'
      )}
    >
      <Icon
        className="h-4 w-4 shrink-0 group-hover:scale-105 transition-transform duration-150"
        aria-hidden="true"
      />
      {!isCollapsed && <span className="truncate">{label}</span>}

      {/* Tooltip — collapsed only */}
      {isCollapsed && (
        <span
          className={cn(
            'absolute left-full ml-2 top-1/2 -translate-y-1/2',
            'px-2 py-1 rounded-md',
            'bg-popover text-popover-foreground text-xs font-medium',
            'shadow-md border border-border/50',
            'opacity-0 group-hover:opacity-100 pointer-events-none',
            'transition-opacity duration-150',
            'whitespace-nowrap z-50'
          )}
        >
          {label}
        </span>
      )}
    </Link>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/Sidebar/__tests__/Sidebar.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx apps/web/src/components/layout/Sidebar/__tests__/Sidebar.test.tsx
git commit -m "feat(sidebar): left border active indicator, collapsed tooltip, icon hover scale"
```

---

## Chunk 3: View Transitions

### Task 7: useViewTransition hook + type declarations

**Files:**
- Create: `apps/web/src/lib/hooks/useViewTransition.ts`
- Create: `apps/web/src/lib/hooks/__tests__/useViewTransition.test.ts`
- Create: `apps/web/src/types/view-transitions.d.ts` (if TS errors)

- [ ] **Step 1: Write hook test**

```tsx
// apps/web/src/lib/hooks/__tests__/useViewTransition.test.ts
import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

import { useViewTransition } from '../useViewTransition';

describe('useViewTransition', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    mockPush.mockClear();
  });

  it('falls back to router.push when startViewTransition not available', () => {
    // @ts-expect-error - testing absence
    document.startViewTransition = undefined;
    const { result } = renderHook(() => useViewTransition());
    result.current.navigateWithTransition('/dashboard');
    expect(mockPush).toHaveBeenCalledWith('/dashboard');
  });

  it('wraps navigation in startViewTransition when available', () => {
    const mockTransition = vi.fn((cb: () => void) => { cb(); return { finished: Promise.resolve(), ready: Promise.resolve(), updateCallbackDone: Promise.resolve() }; });
    (document as any).startViewTransition = mockTransition;
    const { result } = renderHook(() => useViewTransition());
    result.current.navigateWithTransition('/games');
    expect(mockTransition).toHaveBeenCalledOnce();
    expect(mockPush).toHaveBeenCalledWith('/games');
    delete (document as any).startViewTransition;
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm vitest run src/lib/hooks/__tests__/useViewTransition.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Create the hook**

```typescript
// apps/web/src/lib/hooks/useViewTransition.ts
'use client';

import { useRouter } from 'next/navigation';
import { useCallback } from 'react';

export function useViewTransition() {
  const router = useRouter();

  const navigateWithTransition = useCallback(
    (href: string) => {
      if (!document.startViewTransition) {
        router.push(href);
        return;
      }
      document.startViewTransition(() => {
        router.push(href);
      });
    },
    [router]
  );

  return { navigateWithTransition };
}
```

- [ ] **Step 4: Add type declarations if needed**

If `pnpm typecheck` fails on `document.startViewTransition`, create:

```typescript
// apps/web/src/types/view-transitions.d.ts
interface ViewTransition {
  finished: Promise<void>;
  ready: Promise<void>;
  updateCallbackDone: Promise<void>;
}

interface Document {
  startViewTransition?(callback: () => void | Promise<void>): ViewTransition;
}
```

- [ ] **Step 5: Run tests + typecheck**

Run: `cd apps/web && pnpm vitest run src/lib/hooks/__tests__/useViewTransition.test.ts && pnpm typecheck`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/hooks/useViewTransition.ts apps/web/src/lib/hooks/__tests__/useViewTransition.test.ts apps/web/src/types/view-transitions.d.ts
git commit -m "feat(transitions): add useViewTransition hook with graceful fallback"
```

---

### Task 8: CSS transition rules in globals.css

**Files:**
- Modify: `apps/web/src/styles/globals.css`

- [ ] **Step 1: Append view transition CSS at end of `globals.css`**

```css
/* ─── SPA View Transitions (triggered by useViewTransition hook) ─── */

::view-transition-old(root) {
  animation: vt-fade-out 150ms ease-out;
}

::view-transition-new(root) {
  animation: vt-fade-in 150ms ease-in;
}

@keyframes vt-fade-out {
  from { opacity: 1; }
  to { opacity: 0; }
}

@keyframes vt-fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

::view-transition-old(page-content) {
  animation: vt-slide-out 200ms ease-out;
}

::view-transition-new(page-content) {
  animation: vt-slide-in 200ms ease-in;
}

@keyframes vt-slide-out {
  from { transform: translateX(0); opacity: 1; }
  to { transform: translateX(-20px); opacity: 0; }
}

@keyframes vt-slide-in {
  from { transform: translateX(20px); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
}

@media (prefers-reduced-motion: reduce) {
  ::view-transition-old(root),
  ::view-transition-new(root),
  ::view-transition-old(page-content),
  ::view-transition-new(page-content) {
    animation: none;
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/styles/globals.css
git commit -m "feat(transitions): add view transition CSS keyframes with reduced-motion support"
```

---

### Task 9: Wire viewTransitionName to AppShellClient + MeepleCard + SidebarLink

**Files:**
- Modify: `apps/web/src/components/layout/AppShell/AppShellClient.tsx:100-112`
- Modify: `apps/web/src/components/ui/data-display/meeple-card.tsx`
- Modify: `apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx:51-82`

- [ ] **Step 1: Add `viewTransitionName` to `<main>` in `AppShellClient.tsx`**

At line 100, add `style` prop:

```tsx
<main
  id="main-content"
  tabIndex={-1}
  style={{ viewTransitionName: 'page-content' }}
  className={cn(
    'flex-1',
    !fullWidth && 'px-4 sm:px-6 lg:px-8',
    bottomPadding,
    'pt-4',
    className
  )}
>
```

- [ ] **Step 2: Wire `entityId` to `viewTransitionName` on MeepleCard**

In `meeple-card.tsx`, find the `<Component>` element's `style` prop at lines 643-651. Add `viewTransitionName` to the existing style object:

```tsx
style={
  {
    '--mc-entity-color': `hsl(${color})`,
    outlineColor: `hsla(${color}, 0.4)`,
    willChange: 'transform, box-shadow, outline',
    viewTransitionName: entityId ? `meeple-card-${entityId}` : undefined,
  } as React.CSSProperties
}
```

- [ ] **Step 3: Wire `useViewTransition` into `SidebarLink`**

In `SidebarContextNav.tsx`, add the import at file scope and update `SidebarLink` to use the hook. Show the **complete** function (incorporating Task 6 styling + hook):

```tsx
import { useViewTransition } from '@/lib/hooks/useViewTransition';

function SidebarLink({
  href,
  icon: Icon,
  label,
  isActive,
  isCollapsed,
}: {
  href: string;
  icon: React.ElementType;
  label: string;
  isActive?: boolean;
  isCollapsed: boolean;
}) {
  const { navigateWithTransition } = useViewTransition();

  return (
    <Link
      href={href}
      onClick={(e) => {
        e.preventDefault();
        navigateWithTransition(href);
      }}
      className={cn(
        'group relative flex items-center gap-3 rounded-lg text-sm font-medium',
        'min-h-[44px] px-3 py-2',
        'transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-1',
        'border-l-[3px]',
        isActive
          ? 'bg-[hsl(25_95%_45%/0.08)] text-[hsl(25_95%_42%)] font-semibold border-[hsl(25_95%_45%)]'
          : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground border-transparent',
        isCollapsed && 'justify-center px-2'
      )}
    >
      <Icon
        className="h-4 w-4 shrink-0 group-hover:scale-105 transition-transform duration-150"
        aria-hidden="true"
      />
      {!isCollapsed && <span className="truncate">{label}</span>}

      {/* Tooltip — collapsed only */}
      {isCollapsed && (
        <span
          className={cn(
            'absolute left-full ml-2 top-1/2 -translate-y-1/2',
            'px-2 py-1 rounded-md',
            'bg-popover text-popover-foreground text-xs font-medium',
            'shadow-md border border-border/50',
            'opacity-0 group-hover:opacity-100 pointer-events-none',
            'transition-opacity duration-150',
            'whitespace-nowrap z-50'
          )}
        >
          {label}
        </span>
      )}
    </Link>
  );
}
```

- [ ] **Step 4: Add `viewTransitionName` to game detail page header**

Find the game detail client component under `apps/web/src/app/(authenticated)/games/[id]/` (e.g., `page.tsx` or a client component rendering the header). Add `viewTransitionName` to the game header wrapper:

```tsx
<div style={{ viewTransitionName: `meeple-card-${gameId}` }}>
  {/* game header: image + title + metadata */}
</div>
```

If the game detail page does not exist yet, skip this step and note it for future work.

- [ ] **Step 5: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 6: Run all affected tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/Sidebar/__tests__/Sidebar.test.tsx src/lib/hooks/__tests__/useViewTransition.test.ts`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/layout/AppShell/AppShellClient.tsx apps/web/src/components/ui/data-display/meeple-card.tsx apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx
git commit -m "feat(transitions): wire viewTransitionName to main, MeepleCard, and SidebarLink"
```

---

## Final Validation

### Task 10: Full test suite + typecheck

- [ ] **Step 1: Run full frontend typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 2: Run all affected test files**

```bash
cd apps/web && pnpm vitest run \
  src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx \
  src/components/library/__tests__/MeepleLibraryGameCard.test.tsx \
  src/components/layout/Sidebar/__tests__/Sidebar.test.tsx \
  src/lib/hooks/__tests__/useViewTransition.test.ts
```

Expected: All PASS

- [ ] **Step 3: Run full vitest suite (smoke check)**

Run: `cd apps/web && pnpm test`
Expected: No regressions

- [ ] **Step 4: Final commit if any lint/format changes**

```bash
git add -A && git commit -m "chore: lint and format fixes from desktop UX implementation"
```
