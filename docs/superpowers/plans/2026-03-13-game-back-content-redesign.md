# GameBackContent Redesign Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Game MeepleCard back face with enriched header, stats row, KB summary, tag pills, navigation links, and compact footer.

**Architecture:** Update interfaces to add optional fields (all `| null` safe). Replace ContextualAction grid with NavLinks list + CompactFooter. All sub-components private to `GameBackContent.tsx`. Consumer passes `subtitle` via props, removes `onNewSession`/`onAddToGameNight`.

**Tech Stack:** React 19, TypeScript, Tailwind CSS, Vitest + React Testing Library, Next.js Link

**Spec:** `docs/superpowers/specs/2026-03-13-game-back-content-redesign.md`

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx` | Modify | Interfaces + 6-section layout + private sub-components |
| `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx` | Modify | Update existing + add ~20 new tests |
| `apps/web/src/components/library/MeepleLibraryGameCard.tsx` | Modify | Update data/actions mapping |

---

## Chunk 1: Interface Updates + Enriched Header

### Task 1: Update interfaces

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx:24-74`

- [ ] **Step 1: Update GameBackData interface**

Add new optional fields. Keep all existing fields exactly as they are (same types, same names).

```typescript
export interface GameBackData {
  /** Complexity rating (1-5 scale, e.g. BGG weight) */
  complexityRating?: number | null;
  /** Playing time in minutes */
  playingTimeMinutes?: number | null;
  /** Min players */
  minPlayers?: number | null;
  /** Max players */
  maxPlayers?: number | null;
  /** Average rating (0-10) */
  averageRating?: number | null;
  /** Times played by user */
  timesPlayed?: number | null;
  /** KB document previews */
  kbDocuments?: Array<{ id: string; fileName: string; status: string }>;
  /** Whether game has KB */
  hasKb?: boolean;
  /** Number of KB cards */
  kbCardCount?: number;
  /** Pre-formatted "last played" label (consumer handles locale) */
  lastPlayedLabel?: string;
  /** Win rate percentage (0-100) */
  winRate?: number | null;
  /** Number of entity links (expansions, etc.) */
  entityLinkCount?: number;
  /** Number of user notes */
  noteCount?: number;

  // --- New fields (Issue #336) ---
  /** Total play time in minutes (aggregate across sessions) */
  totalPlayTimeMinutes?: number | null;
  /** Category tags (e.g. "Strategia", "Famiglia") */
  categories?: string[];
  /** Mechanic tags (e.g. "Worker Placement", "Deck Building") */
  mechanics?: string[];
  /** BGG weight (1-5 scale) */
  bggWeight?: number | null;
  /** Best player count */
  bestPlayerCount?: number | null;
  /** Number of sessions played */
  sessionCount?: number;
}
```

**Note:** `nextGameNight` is intentionally removed (orphaned after action migration per spec). `timesPlayed` and `winRate` changed to `number | null` per spec (enables `!= null` guard for stats row visibility).

**Regression note:** `onChatAgent` remains in the interface but has no rendering path on the card back after the ContextualAction removal. The "Chiedi all'AI" button is accessible from the card's quick actions overlay and the detail page — this is intentional per spec.

- [ ] **Step 2: Update GameBackActions interface**

Remove `onNewSession` and `onAddToGameNight`. Add navigation handlers.

```typescript
export interface GameBackActions {
  onChatAgent?: () => void;
  onToggleFavorite?: () => void;
  isFavorite?: boolean;
  // --- Navigation (Issue #336) ---
  onViewSessions?: () => void;
  onViewNotes?: () => void;
  onViewLinks?: () => void;
}
```

- [ ] **Step 3: Update GameBackContentProps interface**

Add `subtitle` prop.

```typescript
export interface GameBackContentProps {
  data: GameBackData;
  actions?: GameBackActions;
  /** Entity color HSL string (default game-orange) */
  entityColor?: string;
  /** Game title */
  title?: string;
  /** Publisher or subtitle, shown under title in header */
  subtitle?: string;
  /** Link to game detail page */
  detailHref?: string;
  className?: string;
}
```

- [ ] **Step 4: Run existing tests to verify no compile errors**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: Some tests fail (tests reference `onNewSession`, `onAddToGameNight`, `nextGameNight` which are removed). This is expected — we'll fix tests in Task 3.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx
git commit -m "refactor(card-back): update interfaces for Issue #336 redesign

Add subtitle, totalPlayTimeMinutes, categories, mechanics, bggWeight,
bestPlayerCount, sessionCount to GameBackData. Add onViewSessions,
onViewNotes to GameBackActions. Remove onNewSession, onAddToGameNight,
nextGameNight."
```

### Task 2: Implement enriched header with subtitle

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx:141-207`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Write failing test for subtitle rendering**

Add to test file in the `Enriched Header` describe block:

```typescript
it('shows subtitle when provided', () => {
  renderComponent({}, {}, { title: 'Agricola', subtitle: 'Lookout Games' });
  expect(screen.getByText('Lookout Games')).toBeInTheDocument();
});

it('omits subtitle when not provided', () => {
  renderComponent({}, {}, { title: 'Agricola' });
  // Only title should be in header, no empty subtitle element
  const header = screen.getByText('Agricola').closest('div');
  expect(header?.querySelectorAll('p')).toHaveLength(1); // only lastPlayed line
});
```

**Note:** The `renderComponent` helper needs updating first (Step 2) to pass `subtitle`.

- [ ] **Step 2: Update test helper to support subtitle**

In the test file, update the `renderComponent` function's `props` parameter and the `<GameBackContent>` render:

```typescript
function renderComponent(
  dataOverrides?: Partial<GameBackData>,
  actionsOverrides?: Partial<GameBackActions> | null,
  props?: { title?: string; subtitle?: string; detailHref?: string; entityColor?: string }
) {
  const data = { ...defaultData, ...dataOverrides };
  const actions =
    actionsOverrides === null ? undefined : { ...defaultActions, ...actionsOverrides };
  return render(
    <GameBackContent
      data={data}
      actions={actions}
      title={props?.title ?? 'Catan'}
      subtitle={props?.subtitle}
      detailHref={props?.detailHref ?? '/games/catan'}
      entityColor={props?.entityColor}
    />
    {/* NOTE: Removed {...props} spread that was in the old helper — it caused double-assignment of title/detailHref/entityColor */}
  );
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx -t "shows subtitle"`
Expected: FAIL — `subtitle` prop not used in component yet.

- [ ] **Step 4: Implement subtitle in header**

Update the header section in `GameBackContent.tsx`. Replace the current header (lines 188-207) with:

```tsx
{/* Enriched header (Issue #336) */}
<div
  className="relative overflow-hidden px-5 pb-3 pt-5"
  style={{
    background: `linear-gradient(135deg, hsl(${entityColor}), hsl(${entityColor} / 0.85))`,
  }}
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
  {subtitle && (
    <p className="relative z-[1] text-sm text-white/70 mt-0.5 line-clamp-1">
      {subtitle}
    </p>
  )}
</div>
```

**Note:** The old header showed `lastPlayedLabel` and `winRate` inline. These move to the Stats Row (section ②). The header now shows only title + subtitle.

- [ ] **Step 5: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: Subtitle tests PASS. Some old header tests will fail (they look for `lastPlayedLabel`/`winRate` in the header — those moved to stats row). We'll fix these in Task 3.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx \
       apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-back): enriched header with subtitle (#336)

Header now shows title + publisher subtitle on entity gradient.
lastPlayedLabel and winRate move to stats row (next task)."
```

### Task 3: Fix broken tests from interface + header changes

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Remove nextGameNight from defaultData**

```typescript
const defaultData: GameBackData = {
  complexityRating: 3.2,
  playingTimeMinutes: 90,
  minPlayers: 2,
  maxPlayers: 4,
  averageRating: 7.8,
  timesPlayed: 12,
  kbDocuments: [
    { id: '1', fileName: 'rules.pdf', status: 'Ready' },
    { id: '2', fileName: 'faq.pdf', status: 'processing' },
  ],
  hasKb: true,
  kbCardCount: 5,
  lastPlayedLabel: '3 giorni fa',
  winRate: 65,
  entityLinkCount: 3,
  noteCount: 2,
};
```

- [ ] **Step 2: Remove onNewSession and onAddToGameNight from defaultActions**

```typescript
const defaultActions: GameBackActions = {
  onChatAgent: vi.fn(),
  onToggleFavorite: vi.fn(),
  isFavorite: false,
  onViewLinks: vi.fn(),
};
```

- [ ] **Step 3: Remove or update tests that reference removed actions**

Delete these tests entirely:
- `'fires onNewSession with stopPropagation'` — action removed
- `'shows "Nessuna partita" for timesPlayed=0'` — context text was on onNewSession action
- `'shows play count context'` — context text was on onNewSession action
- `'shows Aggiungi a serata with nextGameNight context'` — action removed
- `'hides Aggiungi a serata when onAddToGameNight is undefined'` — action removed

Update the header tests:
- `'shows "Mai giocato" when lastPlayedLabel is undefined'` — lastPlayedLabel now in stats row, not header. Move to new stats row test section (Task 5).
- `'shows lastPlayedLabel when provided'` — same, move to stats row tests.
- `'shows win rate when provided'` — same, move to stats row tests.
- `'omits win rate when not provided'` — same, move to stats row tests.

- [ ] **Step 4: Run all tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All remaining tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "test(card-back): fix tests after interface + header changes (#336)

Remove tests for removed actions (onNewSession, onAddToGameNight).
Move lastPlayedLabel/winRate tests to stats row section (next task)."
```

---

## Chunk 2: Stats Row + KB Summary + Tags

### Task 4: Implement StatsRow sub-component

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Write failing tests for StatsRow**

Add new describe block in the test file:

```typescript
// ============================================================================
// Stats Row (Issue #336)
// ============================================================================

describe('Stats Row', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders stats row when timesPlayed is provided', () => {
    renderComponent({ timesPlayed: 12 });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Partite')).toBeInTheDocument();
  });

  it('renders stats row with timesPlayed=0 (falsy but not null)', () => {
    renderComponent({ timesPlayed: 0 });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('renders win rate formatted as percentage', () => {
    renderComponent({ winRate: 67 });
    expect(screen.getByText('67%')).toBeInTheDocument();
    expect(screen.getByText('Vittorie')).toBeInTheDocument();
  });

  it('renders total play time as hours when >= 60 minutes', () => {
    renderComponent({ totalPlayTimeMinutes: 180 });
    expect(screen.getByText('3h')).toBeInTheDocument();
    expect(screen.getByText('Tempo')).toBeInTheDocument();
  });

  it('renders total play time as minutes when < 60', () => {
    renderComponent({ totalPlayTimeMinutes: 45 });
    expect(screen.getByText('45m')).toBeInTheDocument();
  });

  it('renders winRate 0 as "0%"', () => {
    renderComponent({ winRate: 0 });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('0%')).toBeInTheDocument();
  });

  it('renders lastPlayedLabel in stats row', () => {
    renderComponent({ lastPlayedLabel: '2 giorni fa', timesPlayed: 5 });
    expect(screen.getByText('2 giorni fa')).toBeInTheDocument();
  });

  it('hides stats row when all stats are undefined', () => {
    renderComponent({
      timesPlayed: undefined,
      winRate: undefined,
      totalPlayTimeMinutes: undefined,
      lastPlayedLabel: undefined,
    });
    expect(screen.queryByTestId('stats-row')).not.toBeInTheDocument();
  });

  it('shows separators between multiple stats', () => {
    renderComponent({ timesPlayed: 5, winRate: 60 });
    const statsRow = screen.getByTestId('stats-row');
    expect(statsRow.querySelectorAll('[data-separator]')).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx -t "Stats Row"`
Expected: FAIL — `stats-row` testid not found.

- [ ] **Step 3: Implement StatsRow**

Add the `StatsRow` private component in `GameBackContent.tsx` after the `InfoChip` component:

```tsx
const MAX_VISIBLE_TAGS = 6;

function StatItem({ value, label }: { value: string; label: string }) {
  return (
    <span className="inline-flex items-baseline gap-1">
      <strong className="text-sm font-bold text-card-foreground">{value}</strong>
      <span className="text-xs text-muted-foreground">{label}</span>
    </span>
  );
}

function StatsRow({
  timesPlayed,
  winRate,
  totalPlayTimeMinutes,
  lastPlayedLabel,
}: Pick<GameBackData, 'timesPlayed' | 'winRate' | 'totalPlayTimeMinutes' | 'lastPlayedLabel'>) {
  const items: Array<{ value: string; label: string }> = [];

  if (timesPlayed != null) {
    items.push({ value: String(timesPlayed), label: 'Partite' });
  }
  if (winRate != null) {
    items.push({ value: `${winRate}%`, label: 'Vittorie' });
  }
  if (totalPlayTimeMinutes != null) {
    // Show minutes if < 60, otherwise hours
    const formatted = totalPlayTimeMinutes < 60
      ? `${totalPlayTimeMinutes}m`
      : `${Math.round(totalPlayTimeMinutes / 60)}h`;
    items.push({ value: formatted, label: 'Tempo' });
  }

  if (items.length === 0 && !lastPlayedLabel) return null;

  return (
    <div
      className="flex items-center justify-center gap-3 py-1.5"
      data-testid="stats-row"
    >
      {items.map((item, i) => (
        <React.Fragment key={item.label}>
          {i > 0 && (
            <span className="text-muted-foreground/50 text-xs" data-separator aria-hidden="true">|</span>
          )}
          <StatItem value={item.value} label={item.label} />
        </React.Fragment>
      ))}
      {lastPlayedLabel && (
        <>
          {items.length > 0 && (
            <span className="text-muted-foreground/50 text-xs" data-separator aria-hidden="true">|</span>
          )}
          <span className="text-xs text-muted-foreground italic">{lastPlayedLabel}</span>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Add StatsRow to the main component layout**

In the main component, after the enriched header div, add the StatsRow. Place it as the first element inside the content area div (`<div className="flex flex-1 flex-col overflow-y-auto px-4 py-3 gap-3">`):

```tsx
{/* Stats Row (Issue #336) */}
<StatsRow
  timesPlayed={timesPlayed}
  winRate={winRate}
  totalPlayTimeMinutes={data.totalPlayTimeMinutes}
  lastPlayedLabel={lastPlayedLabel !== 'Mai giocato' ? lastPlayedLabel : undefined}
/>
```

**IMPORTANT:** Update the destructuring at the top of the component to remove BOTH defaults:
```typescript
const {
  // ... existing ...
  timesPlayed,      // REMOVE = 0 default (null/undefined must hide stats row)
  // ...
  lastPlayedLabel,  // REMOVE = 'Mai giocato' default (no longer in header)
  winRate,
  entityLinkCount = 0,
  noteCount = 0,
} = data;
```
Without removing `timesPlayed = 0`, the stats row would always render "0 Partite" even when no play data exists.

- [ ] **Step 5: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All Stats Row tests PASS. Existing tests still pass.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx \
       apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-back): add StatsRow with partite/vittorie/tempo (#336)

Horizontal stats row with | separators. Shows timesPlayed, winRate,
totalPlayTimeMinutes, lastPlayedLabel. Hidden when all absent.
Uses != null check so timesPlayed:0 renders correctly."
```

### Task 5: Implement KbSummary sub-component

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Write failing tests for KbSummary**

Replace the existing `'KB Context in ContextualAction'` describe block (those tests checked KB text in the old ContextualAction — which is being removed) with:

```typescript
// ============================================================================
// KB Summary (Issue #336)
// ============================================================================

describe('KB Summary', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders condensed KB summary when hasKb is true', () => {
    renderComponent({ hasKb: true, kbCardCount: 5, kbDocuments: [
      { id: '1', fileName: 'rules.pdf', status: 'Ready' },
      { id: '2', fileName: 'faq.pdf', status: 'Ready' },
      { id: '3', fileName: 'errata.pdf', status: 'Ready' },
    ]});
    expect(screen.getByTestId('kb-summary')).toBeInTheDocument();
    expect(screen.getByText(/3 documenti/)).toBeInTheDocument();
  });

  it('shows green status when all KB docs are ready', () => {
    renderComponent({ hasKb: true, kbDocuments: [
      { id: '1', fileName: 'rules.pdf', status: 'Ready' },
    ]});
    const badge = screen.getByTestId('kb-status-badge');
    expect(badge).toHaveTextContent('Pronta');
  });

  it('shows amber status when some KB docs are processing', () => {
    renderComponent({ hasKb: true, kbDocuments: [
      { id: '1', fileName: 'rules.pdf', status: 'Ready' },
      { id: '2', fileName: 'faq.pdf', status: 'Processing' },
    ]});
    const badge = screen.getByTestId('kb-status-badge');
    expect(badge).toHaveTextContent('In elaborazione');
  });

  it('hides KB summary when hasKb is false', () => {
    renderComponent({ hasKb: false, kbDocuments: [] });
    expect(screen.queryByTestId('kb-summary')).not.toBeInTheDocument();
  });

  it('hides KB summary when kbDocuments is empty', () => {
    renderComponent({ hasKb: true, kbDocuments: [] });
    expect(screen.queryByTestId('kb-summary')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx -t "KB Summary"`
Expected: FAIL — `kb-summary` testid not found.

- [ ] **Step 3: Implement KbSummary**

Add after `StatsRow` in `GameBackContent.tsx`:

```tsx
function KbSummary({
  hasKb,
  kbDocuments,
}: Pick<GameBackData, 'hasKb' | 'kbDocuments'>) {
  if (!hasKb || !kbDocuments?.length) return null;

  const allReady = kbDocuments.every(d => d.status === 'Ready');
  const docCount = kbDocuments.length;

  return (
    <div
      className="flex items-center justify-between bg-muted/30 rounded-lg px-3 py-2"
      data-testid="kb-summary"
    >
      <span className="text-xs text-card-foreground">
        {docCount} {docCount === 1 ? 'documento' : 'documenti'} KB
      </span>
      <span
        className={cn(
          'text-[10px] font-semibold px-1.5 py-0.5 rounded',
          allReady
            ? 'bg-emerald-500/10 text-emerald-600'
            : 'bg-amber-500/10 text-amber-600'
        )}
        data-testid="kb-status-badge"
      >
        {allReady ? 'Pronta' : 'In elaborazione'}
      </span>
    </div>
  );
}
```

- [ ] **Step 4: Add KbSummary to the main component layout**

After the `StatsRow` in the content area, before the InfoChip row:

```tsx
{/* KB Summary (Issue #336) */}
<KbSummary hasKb={hasKb} kbDocuments={data.kbDocuments} />
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All KB Summary tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx \
       apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-back): add condensed KbSummary with status badge (#336)

Shows doc count + aggregate status (green=ready, amber=processing).
Hidden when hasKb=false or no documents."
```

### Task 6: Implement TagPills sub-component

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Write failing tests for TagPills**

```typescript
// ============================================================================
// Tag Pills (Issue #336)
// ============================================================================

describe('Tag Pills', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders category and mechanic tags as pills', () => {
    renderComponent({
      categories: ['Strategia', 'Famiglia'],
      mechanics: ['Worker Placement'],
    });
    expect(screen.getByTestId('tag-pills')).toBeInTheDocument();
    expect(screen.getByText('Strategia')).toBeInTheDocument();
    expect(screen.getByText('Famiglia')).toBeInTheDocument();
    expect(screen.getByText('Worker Placement')).toBeInTheDocument();
  });

  it('shows max 6 tags with +N overflow pill', () => {
    renderComponent({
      categories: ['Cat1', 'Cat2', 'Cat3', 'Cat4'],
      mechanics: ['Mech1', 'Mech2', 'Mech3'],
    });
    // 7 total, should show 6 + "+1"
    expect(screen.getByText('+1')).toBeInTheDocument();
  });

  it('shows all tags when 6 or fewer', () => {
    renderComponent({
      categories: ['Cat1', 'Cat2'],
      mechanics: ['Mech1'],
    });
    expect(screen.queryByText(/^\+\d+$/)).not.toBeInTheDocument();
  });

  it('renders with only categories (no mechanics)', () => {
    renderComponent({ categories: ['Strategia'], mechanics: undefined });
    expect(screen.getByTestId('tag-pills')).toBeInTheDocument();
    expect(screen.getByText('Strategia')).toBeInTheDocument();
  });

  it('hides when both categories and mechanics are empty', () => {
    renderComponent({ categories: [], mechanics: [] });
    expect(screen.queryByTestId('tag-pills')).not.toBeInTheDocument();
  });

  it('hides when both categories and mechanics are undefined', () => {
    renderComponent({ categories: undefined, mechanics: undefined });
    expect(screen.queryByTestId('tag-pills')).not.toBeInTheDocument();
  });

  it('overflow pill has singular aria-label for +1', () => {
    renderComponent({
      categories: ['C1', 'C2', 'C3', 'C4', 'C5', 'C6', 'C7'],
    });
    const overflow = screen.getByText('+1');
    expect(overflow).toHaveAttribute('aria-label', '1 altro tag');
  });

  it('overflow pill has plural aria-label for +2 or more', () => {
    renderComponent({
      categories: ['C1', 'C2', 'C3', 'C4', 'C5', 'C6', 'C7', 'C8'],
    });
    const overflow = screen.getByText('+2');
    expect(overflow).toHaveAttribute('aria-label', '2 altri tag');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx -t "Tag Pills"`
Expected: FAIL — `tag-pills` testid not found.

- [ ] **Step 3: Implement TagPills**

```tsx
function TagPills({
  categories,
  mechanics,
}: Pick<GameBackData, 'categories' | 'mechanics'>) {
  const allTags = [...(categories ?? []), ...(mechanics ?? [])];
  if (allTags.length === 0) return null;

  const visible = allTags.slice(0, MAX_VISIBLE_TAGS);
  const overflowCount = allTags.length - MAX_VISIBLE_TAGS;

  return (
    <div className="flex flex-wrap gap-1.5" data-testid="tag-pills">
      {visible.map(tag => (
        <span
          key={tag}
          className="bg-muted text-muted-foreground text-[10px] px-2 py-0.5 rounded-full"
        >
          {tag}
        </span>
      ))}
      {overflowCount > 0 && (
        <span
          className="bg-muted text-muted-foreground text-[10px] px-2 py-0.5 rounded-full font-semibold"
          aria-label={`${overflowCount} ${overflowCount === 1 ? 'altro tag' : 'altri tag'}`}
        >
          +{overflowCount}
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Add TagPills to the main component layout**

After KbSummary, before the contextual actions section:

```tsx
{/* Tag Pills (Issue #336) */}
<TagPills categories={data.categories} mechanics={data.mechanics} />
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All Tag Pills tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx \
       apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-back): add TagPills with overflow +N (#336)

Categories + mechanics as rounded pills. Max 6 visible, overflow shows
+N with aria-label. Hidden when no tags."
```

---

## Chunk 3: Navigation Links + Compact Footer + Remove Old Sections

### Task 7: Implement NavLinks sub-component

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Write failing tests for NavLinks**

```typescript
// ============================================================================
// Navigation Links (Issue #336)
// ============================================================================

describe('Navigation Links', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders session link when handler and count provided', () => {
    const onViewSessions = vi.fn();
    renderComponent({ sessionCount: 3 }, { onViewSessions });
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders note link when handler and count provided', () => {
    const onViewNotes = vi.fn();
    renderComponent({ noteCount: 2 }, { onViewNotes });
    expect(screen.getByText('Note')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('renders entity link when handler and count provided', () => {
    const onViewLinks = vi.fn();
    renderComponent({ entityLinkCount: 5 }, { onViewLinks });
    expect(screen.getByText('Collegamenti')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('calls handler on click with stopPropagation', () => {
    const onViewSessions = vi.fn();
    renderComponent({ sessionCount: 3 }, { onViewSessions });
    const btn = screen.getByText('Sessioni').closest('button')!;
    const event = new MouseEvent('click', { bubbles: true });
    const stopProp = vi.spyOn(event, 'stopPropagation');
    fireEvent(btn, event);
    expect(onViewSessions).toHaveBeenCalledTimes(1);
    expect(stopProp).toHaveBeenCalled();
  });

  it('hides nav links when no handler has a count > 0', () => {
    renderComponent(
      { sessionCount: 0, noteCount: 0, entityLinkCount: 0 },
      { onViewSessions: vi.fn(), onViewNotes: vi.fn(), onViewLinks: vi.fn() }
    );
    expect(screen.queryByTestId('nav-links')).not.toBeInTheDocument();
  });

  it('hides nav links when no handlers provided', () => {
    renderComponent(
      { sessionCount: 5, noteCount: 2 },
      { onViewSessions: undefined, onViewNotes: undefined, onViewLinks: undefined }
    );
    expect(screen.queryByTestId('nav-links')).not.toBeInTheDocument();
  });

  it('nav link buttons are keyboard accessible', () => {
    const onViewSessions = vi.fn();
    renderComponent({ sessionCount: 1 }, { onViewSessions });
    const btn = screen.getByText('Sessioni').closest('button')!;
    fireEvent.keyDown(btn, { key: 'Enter' });
    // Button elements handle Enter natively, just verify it's a button
    expect(btn.tagName).toBe('BUTTON');
    expect(btn).not.toBeDisabled();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx -t "Navigation Links"`
Expected: FAIL — `nav-links` testid not found.

- [ ] **Step 3: Implement NavLinks**

Add new import at top of file:

```typescript
import { Bot, ChevronRight, Clock, FileText, Heart, Link2, Notebook, Play, Star, Users } from 'lucide-react';
```

(Add `ChevronRight`, `FileText`, `Notebook` to existing imports, remove `Calendar`, `StickyNote`)

```tsx
function NavLinks({
  actions,
  sessionCount,
  noteCount,
  entityLinkCount,
}: {
  actions?: GameBackActions;
  sessionCount?: number;
  noteCount?: number;
  entityLinkCount?: number;
}) {
  const links = [
    { icon: Play, label: 'Sessioni', count: sessionCount, onClick: actions?.onViewSessions },
    { icon: Notebook, label: 'Note', count: noteCount, onClick: actions?.onViewNotes },
    { icon: Link2, label: 'Collegamenti', count: entityLinkCount, onClick: actions?.onViewLinks },
  ].filter(l => l.onClick && (l.count ?? 0) > 0);

  if (links.length === 0) return null;

  return (
    <div className="flex flex-col gap-0.5" data-testid="nav-links">
      {links.map(({ icon: Icon, label, count, onClick }) => (
        <button
          key={label}
          type="button"
          className="flex items-center gap-2 w-full rounded-lg px-2 py-1.5 text-xs font-medium transition-colors hover:bg-muted/50"
          onClick={e => {
            e.stopPropagation();
            onClick?.();
          }}
        >
          <Icon className="w-3.5 h-3.5 text-muted-foreground" />
          <span className="text-card-foreground">{label}</span>
          <span className="ml-auto text-muted-foreground">{count}</span>
          <ChevronRight className="w-3 h-3 text-muted-foreground/50" />
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Add NavLinks to the main component layout**

Replace the old contextual actions section (`{/* Contextual actions */}` block, lines ~251-290) with:

```tsx
{/* Navigation Links (Issue #336) */}
<NavLinks
  actions={actions}
  sessionCount={data.sessionCount}
  noteCount={noteCount}
  entityLinkCount={entityLinkCount}
/>
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: NavLinks tests PASS. Old ContextualAction tests (Espansioni, etc.) should already be removed or will fail — fix in next step.

- [ ] **Step 6: Remove remaining old ContextualAction tests**

Delete the entire `ContextualActions` describe block from the test file. The `'shows Espansioni with...'` and `'hides Espansioni when...'` tests are covered by the NavLinks tests now.

- [ ] **Step 7: Remove old ContextualAction component**

Delete the `ContextualAction` function component from `GameBackContent.tsx` (lines ~88-132). It's no longer used.

- [ ] **Step 8: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All tests PASS.

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx \
       apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-back): replace ContextualActions with NavLinks (#336)

Navigation links with counters + chevron. Shows sessions, notes,
entity links. Hidden when no handler+count pair available.
Remove old ContextualAction component."
```

### Task 8: Redesign CompactFooter

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Write failing tests for new footer**

Update the existing `Compact Footer` describe block. Keep working tests, add new ones:

```typescript
// ============================================================================
// Compact Footer (Issue #336)
// ============================================================================

describe('Compact Footer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders filled heart when isFavorite is true', () => {
    renderComponent({}, { isFavorite: true });
    const favBtn = screen.getByTestId('favorite-toggle');
    expect(favBtn.querySelector('[data-filled="true"]')).toBeInTheDocument();
  });

  it('renders outline heart when isFavorite is false', () => {
    renderComponent({}, { isFavorite: false });
    const favBtn = screen.getByTestId('favorite-toggle');
    expect(favBtn.querySelector('[data-filled="false"]')).toBeInTheDocument();
  });

  it('heart toggle has aria-pressed attribute', () => {
    renderComponent({}, { isFavorite: true });
    const favBtn = screen.getByTestId('favorite-toggle');
    expect(favBtn).toHaveAttribute('aria-pressed', 'true');
  });

  it('heart toggle responds to click', () => {
    const onToggleFavorite = vi.fn();
    renderComponent({}, { onToggleFavorite, isFavorite: false });
    fireEvent.click(screen.getByTestId('favorite-toggle'));
    expect(onToggleFavorite).toHaveBeenCalledTimes(1);
  });

  it('shows BGG weight when provided', () => {
    renderComponent({ bggWeight: 3.2 });
    expect(screen.getByText('BGG 3.2')).toBeInTheDocument();
  });

  it('shows best player count when provided', () => {
    renderComponent({ bestPlayerCount: 4 });
    expect(screen.getByText('Best 4p')).toBeInTheDocument();
  });

  it('hides meta pills when not provided', () => {
    renderComponent({ bggWeight: undefined, bestPlayerCount: undefined });
    expect(screen.queryByText(/BGG/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Best/)).not.toBeInTheDocument();
  });

  it('"Dettaglio →" link is always present', () => {
    renderComponent({}, {}, { detailHref: '/games/catan' });
    const link = screen.getByTestId('game-detail-link');
    expect(link).toHaveAttribute('href', '/games/catan');
    expect(link).toHaveTextContent('Dettaglio →');
  });

  it('rejects unsafe href', () => {
    renderComponent({}, {}, { detailHref: '//evil.com' });
    expect(screen.queryByTestId('game-detail-link')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify new ones fail**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx -t "Compact Footer"`
Expected: Tests for `aria-pressed`, `BGG`, `Best` FAIL. Others may pass.

- [ ] **Step 3: Update the footer in the main component**

Replace the existing footer section with:

```tsx
{/* Compact Footer (Issue #336) */}
<div className="mt-auto border-t border-border/10 pt-2 flex items-center justify-between text-xs">
  <div className="flex items-center gap-2">
    {actions?.onToggleFavorite && (
      <button
        type="button"
        className="inline-flex items-center gap-1 text-muted-foreground hover:text-card-foreground transition-colors"
        onClick={e => {
          e.stopPropagation();
          actions.onToggleFavorite?.();
        }}
        aria-pressed={!!actions.isFavorite}
        data-testid="favorite-toggle"
      >
        <Heart
          className={cn('w-3 h-3', actions.isFavorite && 'fill-current')}
          style={actions.isFavorite ? { color: `hsl(${entityColor})` } : undefined}
          data-filled={actions.isFavorite ? 'true' : 'false'}
        />
      </button>
    )}
    {data.bggWeight != null && (
      <span className="text-muted-foreground text-[10px]">
        BGG {data.bggWeight.toFixed(1)}
      </span>
    )}
    {data.bestPlayerCount != null && (
      <span className="text-muted-foreground text-[10px]">
        Best {data.bestPlayerCount}p
      </span>
    )}
  </div>
  {detailHref && isSafeHref(detailHref) && (
    <Link
      href={detailHref}
      className="text-xs font-medium transition-colors font-nunito"
      style={{ color: `hsl(${entityColor})` }}
      onClick={e => e.stopPropagation()}
      data-testid="game-detail-link"
    >
      Dettaglio →
    </Link>
  )}
</div>
```

**Changes from old footer:**
- Heart button: removed "Pref" text label, added `aria-pressed`
- Removed noteCount indicator (noteCount now in NavLinks)
- Added bggWeight and bestPlayerCount meta pills

- [ ] **Step 4: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All tests PASS.

- [ ] **Step 5: Remove old noteCount test**

Delete the test `'shows noteCount when > 0'` and `'hides noteCount indicator when 0'` from the Compact Footer section — noteCount moved to NavLinks.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/GameBackContent.tsx \
       apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "feat(card-back): redesign compact footer with meta pills (#336)

Heart toggle with aria-pressed, BGG weight and best player count pills,
'Dettaglio →' link always present. Remove noteCount from footer (moved to NavLinks)."
```

---

## Chunk 4: Consumer Update + Graceful Degradation Tests + Final Verification

### Task 9: Update MeepleLibraryGameCard consumer

**Files:**
- Modify: `apps/web/src/components/library/MeepleLibraryGameCard.tsx`

- [ ] **Step 1: Read MeepleLibraryGameCard.tsx to get exact current state**

Read the file to see exact line numbers and current code.

- [ ] **Step 2: Update gameBackData mapping**

In the `gameBackData` useMemo:
- Change `timesPlayed: 0` to `timesPlayed: undefined` (was hardcoded — stats row should hide until real data is available)
- All other existing fields remain unchanged

```typescript
// In gameBackData useMemo:
timesPlayed: undefined,  // was: 0 — hide stats row until session API provides real data
```

**Regression note:** `noteCount` was previously shown as a passive indicator in the footer. After redesign, it only appears in NavLinks (requires `onViewNotes` handler + count > 0). Since `onViewNotes` is not wired yet, noteCount will be invisible. This is intentional per spec.

- [ ] **Step 3: Update gameBackActions mapping**

Remove `onNewSession` from the actions object. Add placeholder nav handlers:

```typescript
const gameBackActions: GameBackActions | undefined = useMemo(() => {
  if (!flippable) return undefined;
  return {
    onChatAgent: game.hasKb
      ? () => {
          navigateWithTransition(`/chat/new?game=${game.gameId}`);
        }
      : undefined,
    onToggleFavorite: handleToggleFavorite,
    isFavorite: game.isFavorite,
  };
}, [flippable, game.hasKb, game.gameId, game.isFavorite, handleToggleFavorite, navigateWithTransition]);
```

**Note:** `onViewSessions`, `onViewNotes`, `onViewLinks` are intentionally NOT added yet — their counts are not available from the API, so the NavLinks section will be hidden (correct behavior per spec).

- [ ] **Step 4: Pass subtitle to GameBackContent**

The `MeepleCard` component renders `GameBackContent` inside its flip card. Read `meeple-card.tsx` to trace how `gameBackData` reaches `GameBackContent`. The approach:

1. Read `apps/web/src/components/ui/data-display/meeple-card.tsx`
2. Find where `<GameBackContent>` is rendered
3. If `MeepleCard` already accepts a `gameBackSubtitle` or `subtitle` prop → pass `game.publisher` from the consumer
4. If not → add `gameBackSubtitle?: string` to `MeepleCardProps`, forward it as `subtitle` to `<GameBackContent>`
5. In `MeepleLibraryGameCard.tsx`, pass `gameBackSubtitle={subtitle}` where `subtitle` is already computed (it's the publisher string, ~line 338-339)

- [ ] **Step 5: Run full test suite**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All tests PASS.

- [ ] **Step 6: Type-check the entire frontend**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No type errors related to GameBackContent, GameBackActions, or MeepleLibraryGameCard.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/library/MeepleLibraryGameCard.tsx
git commit -m "refactor(library-card): update consumer for card-back redesign (#336)

Remove onNewSession from actions (moved to detail page).
Pass subtitle (publisher) to GameBackContent."
```

### Task 10: Add graceful degradation + integration tests

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`

- [ ] **Step 1: Add graceful degradation test**

```typescript
// ============================================================================
// Graceful Degradation (Issue #336)
// ============================================================================

describe('Graceful Degradation', () => {
  it('renders only header + footer when no optional data', () => {
    renderComponent(
      {
        complexityRating: null,
        playingTimeMinutes: null,
        minPlayers: null,
        maxPlayers: null,
        averageRating: null,
        timesPlayed: undefined,
        winRate: undefined,
        totalPlayTimeMinutes: undefined,
        lastPlayedLabel: undefined,
        categories: undefined,
        mechanics: undefined,
        bggWeight: undefined,
        bestPlayerCount: undefined,
        hasKb: false,
        kbDocuments: [],
        sessionCount: undefined,
        noteCount: 0,
        entityLinkCount: 0,
      },
      null,  // no actions
      { title: 'Bare Game', detailHref: '/games/bare' }
    );

    // Header always present
    expect(screen.getByText('Bare Game')).toBeInTheDocument();
    // Footer always present
    expect(screen.getByTestId('game-detail-link')).toBeInTheDocument();
    // All optional sections hidden
    expect(screen.queryByTestId('stats-row')).not.toBeInTheDocument();
    expect(screen.queryByTestId('kb-summary')).not.toBeInTheDocument();
    expect(screen.queryByTestId('tag-pills')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-links')).not.toBeInTheDocument();
  });

  it('renders all sections when all data provided', () => {
    renderComponent(
      {
        timesPlayed: 10,
        winRate: 55,
        totalPlayTimeMinutes: 600,
        categories: ['Strategia'],
        mechanics: ['Worker Placement'],
        hasKb: true,
        kbDocuments: [{ id: '1', fileName: 'rules.pdf', status: 'Ready' }],
        sessionCount: 3,
        bggWeight: 3.5,
        bestPlayerCount: 4,
      },
      {
        onViewSessions: vi.fn(),
        onViewNotes: vi.fn(),
        noteCount: 2,
      }
    );

    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByTestId('kb-summary')).toBeInTheDocument();
    expect(screen.getByTestId('tag-pills')).toBeInTheDocument();
    expect(screen.getByTestId('nav-links')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx`
Expected: All tests PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx
git commit -m "test(card-back): add graceful degradation tests (#336)

Verify card renders only header+footer when no optional data.
Verify all 6 sections render when full data provided."
```

### Task 11: Final verification

- [ ] **Step 1: Run full GameBackContent test suite**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/meeple-card-features/__tests__/GameBackContent.test.tsx --reporter=verbose`
Expected: All tests PASS (should be ~40-45 tests total).

- [ ] **Step 2: Run type check**

Run: `cd apps/web && npx tsc --noEmit 2>&1 | head -30`
Expected: No errors.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && npx next lint --dir src/components/ui/data-display/meeple-card-features 2>&1 | head -20`
Expected: No errors.

- [ ] **Step 4: Verify test IDs are consistent**

Quick grep to ensure all new data-testid values follow existing patterns:

```bash
cd apps/web && grep -n 'data-testid' src/components/ui/data-display/meeple-card-features/GameBackContent.tsx
```

Expected test IDs: `game-back-content`, `info-chips`, `complexity-dots`, `stats-row`, `kb-summary`, `kb-status-badge`, `tag-pills`, `nav-links`, `favorite-toggle`, `game-detail-link`.

- [ ] **Step 5: Final commit if any fixes needed**

Only if previous steps revealed issues.
