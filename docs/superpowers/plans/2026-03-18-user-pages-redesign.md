# "Il Tavolo" — User Pages Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the 4 user-facing pages (Dashboard, Personal Library, Public Library, Agent) with a board-gaming "tavolo" metaphor, MtG-inspired card anatomy, mana pip connections, and zero mock data.

**Architecture:** Extend existing MeepleCard component with `mechanicIcon` and `stateLabel` overlay slots. Create SVG mechanic icon set. Rebuild page layouts using existing React Query hooks and API endpoints. Remove Wishlist/Proposals tabs. Replace all mock data with real API data + empty states.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui, Framer Motion, React Query, Zustand, Lucide React + custom SVG icons

**Spec:** `docs/superpowers/specs/2026-03-18-user-pages-redesign-design.md`

---

## File Structure

### New Files
```
apps/web/src/
├── components/
│   ├── icons/mechanics/                          # SVG mechanic icon set
│   │   ├── index.ts                              # Barrel export
│   │   ├── MechanicIcon.tsx                      # Wrapper component
│   │   ├── icons/                                # React component icons (.tsx)
│   │   │   ├── EngineBuilding.tsx
│   │   │   ├── AreaControl.tsx
│   │   │   ├── DeckBuilding.tsx
│   │   │   ├── WorkerPlacement.tsx
│   │   │   ├── Cooperative.tsx
│   │   │   ├── Competitive.tsx
│   │   │   ├── DiceRolling.tsx
│   │   │   ├── PuzzleAbstract.tsx
│   │   │   ├── NarrativeRpg.tsx
│   │   │   ├── TilePlacement.tsx
│   │   │   ├── Trading.tsx
│   │   │   ├── SetCollection.tsx
│   │   │   ├── DungeonCrawler.tsx
│   │   │   ├── RouteBuilding.tsx
│   │   │   ├── SocialDeduction.tsx
│   │   │   └── DefaultMechanic.tsx
│   │
│   ├── icons/entity-types/                       # Entity type classification icons
│   │   ├── index.ts
│   │   └── EntityTypeIcon.tsx                    # Maps entity + subtype to icon
│   │
│   ├── dashboard-v2/
│   │   ├── tavolo/                               # New "Il Tavolo" layout
│   │   │   ├── TavoloLayout.tsx                  # 2-col grid (main + feed)
│   │   │   ├── TavoloSection.tsx                 # Section with title + card grid
│   │   │   ├── ActiveSessionCard.tsx             # Green-bordered session card
│   │   │   └── ActivityFeed.tsx                  # Personal activity feed sidebar
│   │   └── DashboardRenderer.tsx                 # MODIFY: swap to tavolo layout
│   │
│   ├── library/
│   │   ├── PersonalLibraryPage.tsx               # New unified personal library
│   │   ├── PublicLibraryPage.tsx                  # New public catalog page
│   │   ├── ShelfRow.tsx                          # Horizontal scroll shelf
│   │   ├── ShelfCard.tsx                         # 140px shelf card
│   │   ├── MechanicFilter.tsx                    # Chip filter using SVG icons
│   │   └── LibraryToolbar.tsx                    # Search + count + view toggle
│   │
│   └── agent/
│       └── AgentCharacterSheet.tsx               # RPG character sheet layout
│
├── app/(authenticated)/
│   ├── library/
│   │   ├── _content.tsx                          # MODIFY: 2 tabs only
│   │   └── public/
│   │       └── page.tsx                          # New Public Library route
│   └── agents/[id]/
│       └── page.tsx                              # MODIFY: use CharacterSheet
```

### Modified Files
```
apps/web/src/
├── components/ui/data-display/
│   ├── meeple-card/types.ts                     # Add mechanicIcon, stateLabel props
│   ├── meeple-card/parts/CardCover.tsx           # Render overlay slots
│   ├── meeple-card/variants/MeepleCardGrid.tsx   # Pass new props to CardCover
│   ├── meeple-card/variants/MeepleCardList.tsx    # Pass new props to CardCover
│   ├── meeple-card/variants/MeepleCardCompact.tsx # Pass new props to CardCover
│   ├── meeple-card/variants/MeepleCardFeatured.tsx
│   ├── meeple-card/variants/MeepleCardHero.tsx
│   ├── meeple-card/variants/MeepleCardExpanded.tsx  # Also needs new props
│   └── meeple-card-styles.ts                    # NOTE: at data-display/ level, not inside meeple-card/
│
├── components/dashboard-v2/
│   ├── DashboardRenderer.tsx                     # Replace zones with TavoloLayout
│   └── empty-states.tsx                          # Update empty state messages
│
├── app/(authenticated)/library/_content.tsx       # Remove wishlist/proposals tabs
├── app/(authenticated)/agents/[id]/page.tsx       # Character sheet layout
│
└── components/empty-state/EmptyState.tsx          # Add entity-specific variants
```

---

## Task 1: SVG Mechanic Icon Set

**Files:**
- Create: `apps/web/src/components/icons/mechanics/MechanicIcon.tsx`
- Create: `apps/web/src/components/icons/mechanics/index.ts`
- Create: 15 SVG files in `apps/web/src/components/icons/mechanics/`
- Test: `apps/web/__tests__/components/icons/mechanics/MechanicIcon.test.tsx`

- [ ] **Step 1: Write failing test for MechanicIcon component**

```tsx
// apps/web/__tests__/components/icons/mechanics/MechanicIcon.test.tsx
import { render, screen } from '@testing-library/react';
import { MechanicIcon } from '@/components/icons/mechanics';

describe('MechanicIcon', () => {
  it('renders engine-building icon with correct size', () => {
    render(<MechanicIcon mechanic="engine-building" size={24} />);
    const svg = screen.getByRole('img', { name: /engine building/i });
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveAttribute('width', '24');
    expect(svg).toHaveAttribute('height', '24');
  });

  it('renders fallback for unknown mechanic', () => {
    render(<MechanicIcon mechanic="unknown-mechanic" size={24} />);
    const svg = screen.getByRole('img', { name: /board game/i });
    expect(svg).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<MechanicIcon mechanic="area-control" size={20} className="text-amber-500" />);
    const svg = screen.getByRole('img', { name: /area control/i });
    expect(svg).toHaveClass('text-amber-500');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/icons/mechanics/MechanicIcon.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Create SVG icon files**

Create 15 inline SVG icons as React components. Each icon is a 24x24 viewBox with a single-color path using `currentColor`. Store as individual `.tsx` files (not raw SVG) so they work with React's JSX and Tailwind color classes.

```tsx
// apps/web/src/components/icons/mechanics/icons/EngineBuilding.tsx
export function EngineBuildingIcon({ size = 24, className = '' }: { size?: number; className?: string }) {
  return (
    <svg
      role="img"
      aria-label="Engine Building"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
    >
      <circle cx="12" cy="12" r="3" />
      <path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42" />
    </svg>
  );
}
```

Create similar icons for all 15 mechanics: `EngineBuilding`, `AreaControl`, `DeckBuilding`, `WorkerPlacement`, `Cooperative`, `Competitive`, `DiceRolling`, `PuzzleAbstract`, `NarrativeRpg`, `TilePlacement`, `Trading`, `SetCollection`, `DungeonCrawler`, `RouteBuilding`, `SocialDeduction`, plus a `DefaultMechanic` fallback.

- [ ] **Step 4: Create MechanicIcon wrapper component**

```tsx
// apps/web/src/components/icons/mechanics/MechanicIcon.tsx
import { type ComponentType } from 'react';
import { EngineBuildingIcon } from './icons/EngineBuilding';
import { AreaControlIcon } from './icons/AreaControl';
// ... import all icons
import { DefaultMechanicIcon } from './icons/DefaultMechanic';

const MECHANIC_ICONS: Record<string, ComponentType<{ size?: number; className?: string }>> = {
  'engine-building': EngineBuildingIcon,
  'area-control': AreaControlIcon,
  'deck-building': DeckBuildingIcon,
  'worker-placement': WorkerPlacementIcon,
  'cooperative': CooperativeIcon,
  'competitive': CompetitiveIcon,
  'dice-rolling': DiceRollingIcon,
  'puzzle-abstract': PuzzleAbstractIcon,
  'narrative-rpg': NarrativeRpgIcon,
  'tile-placement': TilePlacementIcon,
  'trading': TradingIcon,
  'set-collection': SetCollectionIcon,
  'dungeon-crawler': DungeonCrawlerIcon,
  'route-building': RouteBuildingIcon,
  'social-deduction': SocialDeductionIcon,
};

interface MechanicIconProps {
  mechanic: string;
  size?: number;
  className?: string;
}

export function MechanicIcon({ mechanic, size = 24, className = '' }: MechanicIconProps) {
  const Icon = MECHANIC_ICONS[mechanic] ?? DefaultMechanicIcon;
  return <Icon size={size} className={className} />;
}
```

- [ ] **Step 5: Create barrel export**

```tsx
// apps/web/src/components/icons/mechanics/index.ts
export { MechanicIcon } from './MechanicIcon';
export { MECHANIC_ICONS } from './MechanicIcon';
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/icons/mechanics/MechanicIcon.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/icons/mechanics/ apps/web/__tests__/components/icons/mechanics/
git commit -m "feat(ui): add SVG mechanic icon set for MtG-inspired card anatomy"
```

---

## Task 2: MeepleCard — Add Cover Overlay Slots

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`
- Test: `apps/web/__tests__/components/ui/data-display/meeple-card/CardCoverOverlay.test.tsx`

- [ ] **Step 1: Write failing test for cover overlay slots**

```tsx
// apps/web/__tests__/components/ui/data-display/meeple-card/CardCoverOverlay.test.tsx
import { render, screen } from '@testing-library/react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

describe('MeepleCard cover overlay slots', () => {
  it('renders mechanicIcon in bottom-left of cover', () => {
    render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        mechanicIcon={<span data-testid="mechanic-icon">⚙️</span>}
      />
    );
    expect(screen.getByTestId('mechanic-icon')).toBeInTheDocument();
  });

  it('renders stateLabel in bottom-right of cover', () => {
    render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        stateLabel={{ text: 'Nuovo', variant: 'info' }}
      />
    );
    expect(screen.getByText('Nuovo')).toBeInTheDocument();
  });

  it('renders both overlay slots together', () => {
    render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        mechanicIcon={<span data-testid="mechanic">⚙️</span>}
        stateLabel={{ text: 'Giocato', variant: 'success' }}
      />
    );
    expect(screen.getByTestId('mechanic')).toBeInTheDocument();
    expect(screen.getByText('Giocato')).toBeInTheDocument();
  });

  it('does not render overlays when props are not provided', () => {
    const { container } = render(
      <MeepleCard entity="game" variant="grid" title="Wingspan" />
    );
    expect(container.querySelector('[data-slot="mechanic-icon"]')).toBeNull();
    expect(container.querySelector('[data-slot="state-label"]')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/ui/data-display/meeple-card/CardCoverOverlay.test.tsx`
Expected: FAIL — props not recognized

- [ ] **Step 3: Add types to MeepleCardProps**

In `apps/web/src/components/ui/data-display/meeple-card/types.ts`, add to `MeepleCardProps`:

```ts
/** MtG-inspired cover overlay: classification icon (bottom-left of cover image) */
mechanicIcon?: React.ReactNode;

/** MtG-inspired cover overlay: state badge (bottom-right of cover image) */
stateLabel?: {
  text: string;
  variant: 'success' | 'warning' | 'error' | 'info';
};
```

- [ ] **Step 4: Add overlay styles to meeple-card-styles.ts**

In `apps/web/src/components/ui/data-display/meeple-card-styles.ts`, add:

```ts
export const coverOverlayStyles = {
  container: 'absolute bottom-0 left-0 right-0 flex items-end justify-between p-1.5 pointer-events-none',
  mechanicIcon: 'pointer-events-auto rounded-sm bg-black/60 p-0.5 backdrop-blur-sm',
  stateLabel: {
    base: 'pointer-events-auto rounded-sm px-1.5 py-0.5 text-[10px] font-semibold backdrop-blur-sm',
    success: 'bg-emerald-500/80 text-white',
    warning: 'bg-amber-500/80 text-black',
    error: 'bg-red-500/80 text-white',
    info: 'bg-blue-500/80 text-white',
  },
} as const;
```

- [ ] **Step 5: Modify CardCover.tsx to render overlays**

In `apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx`:

The current implementation just wraps `CoverImage` with no container div. Wrap it in a `<div className="relative">` and add the overlay:

```tsx
// Add props: mechanicIcon and stateLabel to CardCoverProps
// Wrap CoverImage in relative container and add overlay
export function CardCover({ src, alt, variant, entity, customColor, mechanicIcon, stateLabel }: CardCoverProps) {
  if (variant === 'compact') return null;
  return (
    <div className="relative">
      <CoverImage src={src} alt={alt} variant={variant} entity={entity} customColor={customColor} />
      {(mechanicIcon || stateLabel) && (
        <div className={coverOverlayStyles.container}>
          {mechanicIcon ? (
            <div data-slot="mechanic-icon" className={coverOverlayStyles.mechanicIcon}>
              {mechanicIcon}
            </div>
          ) : <div />}
          {stateLabel && (
            <div
              data-slot="state-label"
              className={cn(
                coverOverlayStyles.stateLabel.base,
                coverOverlayStyles.stateLabel[stateLabel.variant]
              )}
            >
              {stateLabel.text}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 6: Pass new props through all variant files**

In each variant file (`MeepleCardGrid.tsx`, `MeepleCardList.tsx`, `MeepleCardCompact.tsx`, `MeepleCardFeatured.tsx`, `MeepleCardHero.tsx`, `MeepleCardExpanded.tsx`), destructure `mechanicIcon` and `stateLabel` from props and pass to `<CardCover>`.

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/ui/data-display/meeple-card/CardCoverOverlay.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/ apps/web/__tests__/components/ui/data-display/meeple-card/
git commit -m "feat(ui): add MtG-inspired cover overlay slots to MeepleCard (mechanicIcon + stateLabel)"
```

---

## Task 3: EntityTypeIcon — Classification Icons for All Entity Types

**Files:**
- Create: `apps/web/src/components/icons/entity-types/EntityTypeIcon.tsx`
- Create: `apps/web/src/components/icons/entity-types/index.ts`
- Test: `apps/web/__tests__/components/icons/entity-types/EntityTypeIcon.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/__tests__/components/icons/entity-types/EntityTypeIcon.test.tsx
import { render, screen } from '@testing-library/react';
import { EntityTypeIcon } from '@/components/icons/entity-types';

describe('EntityTypeIcon', () => {
  it('renders agent type icon for "rules-expert"', () => {
    render(<EntityTypeIcon entity="agent" subtype="rules-expert" size={20} />);
    const icon = screen.getByRole('img');
    expect(icon).toBeInTheDocument();
  });

  it('renders session type icon for "competitive"', () => {
    render(<EntityTypeIcon entity="session" subtype="competitive" size={20} />);
    expect(screen.getByRole('img')).toBeInTheDocument();
  });

  it('renders game mechanic via MechanicIcon', () => {
    render(<EntityTypeIcon entity="game" subtype="engine-building" size={20} />);
    expect(screen.getByRole('img', { name: /engine building/i })).toBeInTheDocument();
  });

  it('renders fallback for unknown entity/subtype', () => {
    render(<EntityTypeIcon entity="custom" subtype="unknown" size={20} />);
    expect(screen.getByRole('img')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/icons/entity-types/EntityTypeIcon.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement EntityTypeIcon**

```tsx
// apps/web/src/components/icons/entity-types/EntityTypeIcon.tsx
import { MechanicIcon } from '@/components/icons/mechanics';
import { BookOpen, Swords, Users, HelpCircle, Map, MessageSquare, Wrench, FileText, Gamepad2 } from 'lucide-react';

const AGENT_TYPE_ICONS: Record<string, typeof BookOpen> = {
  'rules-expert': BookOpen,
  'strategy-advisor': Swords,
  'faq-helper': HelpCircle,
  'setup-guide': Map,
};

const SESSION_TYPE_ICONS: Record<string, typeof BookOpen> = {
  'competitive': Swords,
  'cooperative': Users,
  'tutorial': BookOpen,
  'solo': Gamepad2,
};

const CHAT_CONTEXT_ICONS: Record<string, typeof BookOpen> = {
  'rules': BookOpen,
  'strategy': Swords,
  'setup': Map,
  'general': MessageSquare,
};

const KB_TYPE_ICONS: Record<string, typeof BookOpen> = {
  'rulebook': BookOpen,
  'scenario': Map,
  'faq': HelpCircle,
  'reference': FileText,
};

interface EntityTypeIconProps {
  entity: string;
  subtype: string;
  size?: number;
  className?: string;
}

export function EntityTypeIcon({ entity, subtype, size = 20, className = '' }: EntityTypeIconProps) {
  if (entity === 'game') {
    return <MechanicIcon mechanic={subtype} size={size} className={className} />;
  }

  const iconMap: Record<string, Record<string, typeof BookOpen>> = {
    agent: AGENT_TYPE_ICONS,
    session: SESSION_TYPE_ICONS,
    chatSession: CHAT_CONTEXT_ICONS,
    kb: KB_TYPE_ICONS,
  };

  const Icon = iconMap[entity]?.[subtype] ?? Wrench;
  return <Icon role="img" size={size} className={className} />;
}
```

- [ ] **Step 4: Create barrel export**

```tsx
// apps/web/src/components/icons/entity-types/index.ts
export { EntityTypeIcon } from './EntityTypeIcon';
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/icons/entity-types/EntityTypeIcon.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/icons/entity-types/ apps/web/__tests__/components/icons/entity-types/
git commit -m "feat(ui): add EntityTypeIcon for classification across all entity types"
```

---

## Task 4: ActivityFeed Component

**Files:**
- Create: `apps/web/src/components/dashboard-v2/tavolo/ActivityFeed.tsx`
- Test: `apps/web/__tests__/components/dashboard-v2/tavolo/ActivityFeed.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/__tests__/components/dashboard-v2/tavolo/ActivityFeed.test.tsx
import { render, screen } from '@testing-library/react';
import { ActivityFeed } from '@/components/dashboard-v2/tavolo/ActivityFeed';

// Mock useActivityTimeline
vi.mock('@/hooks/useActivityTimeline', () => ({
  useActivityTimeline: vi.fn(),
}));

import { useActivityTimeline } from '@/hooks/useActivityTimeline';

const mockItems = [
  { id: '1', type: 'chat_saved', topic: 'Regole Gloomhaven', chatId: 'a1', gameName: 'Gloomhaven', timestamp: new Date().toISOString() },
  { id: '2', type: 'game_added', topic: 'Aggiunto alla libreria', gameName: 'Azul', timestamp: new Date(Date.now() - 3600000).toISOString() },
];

describe('ActivityFeed', () => {
  it('renders activity events from API', () => {
    (useActivityTimeline as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { items: mockItems, totalCount: 2, hasMore: false },
      isLoading: false,
    });

    render(<ActivityFeed />);
    expect(screen.getByText(/Gloomhaven/)).toBeInTheDocument();
    expect(screen.getByText(/Azul/)).toBeInTheDocument();
  });

  it('renders empty state when no events', () => {
    (useActivityTimeline as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { items: [], totalCount: 0, hasMore: false },
      isLoading: false,
    });

    render(<ActivityFeed />);
    expect(screen.getByText(/nessuna attività/i)).toBeInTheDocument();
  });

  it('renders loading skeleton', () => {
    (useActivityTimeline as ReturnType<typeof vi.fn>).mockReturnValue({
      data: undefined,
      isLoading: true,
    });

    const { container } = render(<ActivityFeed />);
    expect(container.querySelector('[data-testid="feed-skeleton"]')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/dashboard-v2/tavolo/ActivityFeed.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement ActivityFeed**

```tsx
// apps/web/src/components/dashboard-v2/tavolo/ActivityFeed.tsx
'use client';

import { useActivityTimeline } from '@/hooks/useActivityTimeline';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { MessageSquare, Plus, Gamepad2, FileText, Trophy } from 'lucide-react';

const EVENT_ICONS: Record<string, typeof MessageSquare> = {
  chat_saved: MessageSquare,
  game_added: Plus,
  session_completed: Gamepad2,
  pdf_uploaded: FileText,
  achievement_unlocked: Trophy,
};

export function ActivityFeed() {
  const { data, isLoading } = useActivityTimeline({
    types: [],
    search: '',
    skip: 0,
    take: 20,
    order: 'desc',
  });

  if (isLoading) {
    return (
      <div data-testid="feed-skeleton" className="space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="animate-pulse">
            <div className="h-3 w-16 rounded bg-[#21262d] mb-1" />
            <div className="h-4 w-full rounded bg-[#21262d]" />
          </div>
        ))}
      </div>
    );
  }

  const items = data?.items ?? [];

  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <MessageSquare className="mb-2 h-8 w-8 text-[#484f58]" />
        <p className="text-xs text-[#484f58]">Nessuna attività recente</p>
      </div>
    );
  }

  return (
    <div className="space-y-0">
      {items.map((item) => {
        const Icon = EVENT_ICONS[item.type] ?? MessageSquare;
        return (
          <div key={item.id} className="border-b border-[#21262d] px-0 py-2.5 last:border-b-0">
            <div className="flex items-center gap-1 text-[10px] text-[#484f58]">
              <Icon className="h-3 w-3" />
              {formatDistanceToNow(new Date(item.timestamp), { addSuffix: true, locale: it })}
            </div>
            <p className="text-xs text-[#8b949e]">
              {item.topic && <span>{item.topic} </span>}
              {item.gameName && (
                <span className="font-semibold text-[#f0a030]">{item.gameName}</span>
              )}
            </p>
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/dashboard-v2/tavolo/ActivityFeed.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard-v2/tavolo/ActivityFeed.tsx apps/web/__tests__/components/dashboard-v2/tavolo/
git commit -m "feat(dashboard): add ActivityFeed component with real API data"
```

---

## Task 5: TavoloLayout — Dashboard Main Layout

**Files:**
- Create: `apps/web/src/components/dashboard-v2/tavolo/TavoloLayout.tsx`
- Create: `apps/web/src/components/dashboard-v2/tavolo/TavoloSection.tsx`
- Create: `apps/web/src/components/dashboard-v2/tavolo/ActiveSessionCard.tsx`
- Create: `apps/web/src/components/dashboard-v2/tavolo/index.ts`
- Test: `apps/web/__tests__/components/dashboard-v2/tavolo/TavoloLayout.test.tsx`

- [ ] **Step 1: Write failing test for TavoloLayout**

```tsx
// apps/web/__tests__/components/dashboard-v2/tavolo/TavoloLayout.test.tsx
import { render, screen } from '@testing-library/react';
import { TavoloLayout } from '@/components/dashboard-v2/tavolo';

// Mock child components
vi.mock('@/components/dashboard-v2/tavolo/ActivityFeed', () => ({
  ActivityFeed: () => <div data-testid="activity-feed">Feed</div>,
}));

describe('TavoloLayout', () => {
  it('renders tavolo area and feed sidebar', () => {
    render(
      <TavoloLayout>
        <div data-testid="tavolo-content">Content</div>
      </TavoloLayout>
    );
    expect(screen.getByTestId('tavolo-content')).toBeInTheDocument();
    expect(screen.getByTestId('activity-feed')).toBeInTheDocument();
  });

  it('renders with correct grid layout classes', () => {
    const { container } = render(
      <TavoloLayout>
        <div>Content</div>
      </TavoloLayout>
    );
    const grid = container.firstChild;
    expect(grid).toHaveClass('grid');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/dashboard-v2/tavolo/TavoloLayout.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement TavoloLayout**

```tsx
// apps/web/src/components/dashboard-v2/tavolo/TavoloLayout.tsx
'use client';

import { type ReactNode } from 'react';
import { ActivityFeed } from './ActivityFeed';

interface TavoloLayoutProps {
  children: ReactNode;
}

export function TavoloLayout({ children }: TavoloLayoutProps) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_280px]">
      {/* Tavolo (main area) */}
      <div className="min-w-0 space-y-6">
        {children}
      </div>

      {/* Feed sidebar */}
      <aside className="hidden lg:block">
        <div className="sticky top-20 rounded-xl border border-dashed border-[#30363d] bg-[#0d1117] p-4">
          <h3 className="mb-3 text-[11px] font-bold uppercase tracking-wider text-[#a855f7]">
            📜 Feed Attività
          </h3>
          <ActivityFeed />
        </div>
      </aside>
    </div>
  );
}
```

- [ ] **Step 4: Implement TavoloSection**

```tsx
// apps/web/src/components/dashboard-v2/tavolo/TavoloSection.tsx
'use client';

import { type ReactNode } from 'react';

interface TavoloSectionProps {
  icon: string;
  title: string;
  children: ReactNode;
}

export function TavoloSection({ icon, title, children }: TavoloSectionProps) {
  return (
    <section>
      <div className="mb-3 flex items-center gap-2">
        <span className="text-sm">{icon}</span>
        <h2 className="text-sm font-semibold uppercase tracking-wider text-[#8b949e]">
          {title}
        </h2>
        <div className="h-px flex-1 bg-[#30363d]" />
      </div>
      {children}
    </section>
  );
}
```

- [ ] **Step 5: Implement ActiveSessionCard**

```tsx
// apps/web/src/components/dashboard-v2/tavolo/ActiveSessionCard.tsx
'use client';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card/types';

interface ActiveSessionCardProps {
  session: {
    id: string;
    gameName: string;
    gameImageUrl?: string;
    playerCount: number;
    duration: string;
    gameId?: string;
  };
}

export function ActiveSessionCard({ session }: ActiveSessionCardProps) {
  return (
    <div className="rounded-xl border-l-4 border-l-emerald-500 bg-[#21262d]">
      <MeepleCard
        entity="session"
        variant="list"
        title={session.gameName}
        subtitle={`Sessione in corso • ${session.playerCount} giocatori • ${session.duration}`}
        imageUrl={session.gameImageUrl}
        stateLabel={{ text: 'Attiva', variant: 'success' }}
      />
    </div>
  );
}
```

- [ ] **Step 6: Create barrel export**

```tsx
// apps/web/src/components/dashboard-v2/tavolo/index.ts
export { TavoloLayout } from './TavoloLayout';
export { TavoloSection } from './TavoloSection';
export { ActiveSessionCard } from './ActiveSessionCard';
export { ActivityFeed } from './ActivityFeed';
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/dashboard-v2/tavolo/TavoloLayout.test.tsx`
Expected: PASS (2 tests)

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/dashboard-v2/tavolo/ apps/web/__tests__/components/dashboard-v2/tavolo/
git commit -m "feat(dashboard): add TavoloLayout, TavoloSection, and ActiveSessionCard components"
```

---

## Task 6: Dashboard — Wire Up DashboardRenderer with Tavolo

**Files:**
- Modify: `apps/web/src/components/dashboard-v2/DashboardRenderer.tsx`
- Modify: `apps/web/src/components/dashboard-v2/empty-states.tsx`

- [ ] **Step 1: Read current DashboardRenderer**

Read `apps/web/src/components/dashboard-v2/DashboardRenderer.tsx` completely to understand current zone structure.

- [ ] **Step 2: Replace zone layout with TavoloLayout**

Replace the current zone rendering with TavoloLayout. Keep the DashboardEngine/Provider wrapper. The content inside TavoloLayout uses existing data hooks (`useDashboardData`, `useAgents`, etc.) and renders:

1. `TavoloSection icon="🎮" title="Sessioni attive"` → ActiveSessionCard for each active session, or empty state
2. `TavoloSection icon="📚" title="Giochi recenti dalla tua libreria"` → Grid of MeepleCard (game, grid variant) with mana pips
3. `TavoloSection icon="🤖" title="I tuoi agenti"` → Grid of MeepleCard (agent, compact variant)

Each MeepleCard should use `mechanicIcon` and `stateLabel` props where data is available.

- [ ] **Step 3: Update empty-states.tsx**

Update the empty state messages to match the spec:
- No games: "Il tavolo è vuoto — esplora il catalogo" + CTA to Public Library
- No sessions: "Nessuna partita in corso — inizia a giocare"
- No agents: "Configura il tuo primo agente AI"

- [ ] **Step 4: Verify dashboard renders**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds. No type errors.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard-v2/
git commit -m "feat(dashboard): integrate TavoloLayout into DashboardRenderer"
```

---

## Task 7: Library — ShelfCard and ShelfRow Components

**Files:**
- Create: `apps/web/src/components/library/ShelfCard.tsx`
- Create: `apps/web/src/components/library/ShelfRow.tsx`
- Create: `apps/web/src/components/library/index.ts`
- Test: `apps/web/__tests__/components/library/ShelfCard.test.tsx`

- [ ] **Step 1: Write failing test for ShelfCard**

```tsx
// apps/web/__tests__/components/library/ShelfCard.test.tsx
import { render, screen } from '@testing-library/react';
import { ShelfCard } from '@/components/library/ShelfCard';

describe('ShelfCard', () => {
  it('renders game title and subtitle', () => {
    render(
      <ShelfCard
        title="Wingspan"
        subtitle="Engine Building • 1-5 gioc."
        coverGradient="from-emerald-500 to-emerald-700"
        coverIcon="🌿"
      />
    );
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText(/Engine Building/)).toBeInTheDocument();
  });

  it('renders mana pips when provided', () => {
    render(
      <ShelfCard
        title="Wingspan"
        subtitle="Engine Building"
        coverIcon="🌿"
        manaPips={[
          { type: 'agent', active: true },
          { type: 'kb', active: false },
        ]}
      />
    );
    const pips = screen.getAllByTestId(/mana-pip/);
    expect(pips).toHaveLength(2);
  });

  it('renders "in library" badge when inLibrary is true', () => {
    render(
      <ShelfCard
        title="Wingspan"
        subtitle="test"
        coverIcon="🌿"
        inLibrary={true}
      />
    );
    expect(screen.getByText(/nella tua libreria/i)).toBeInTheDocument();
  });

  it('renders add button when inLibrary is false and onAdd provided', () => {
    const onAdd = vi.fn();
    render(
      <ShelfCard
        title="Wingspan"
        subtitle="test"
        coverIcon="🌿"
        inLibrary={false}
        onAdd={onAdd}
      />
    );
    expect(screen.getByRole('button', { name: /aggiungi/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/library/ShelfCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement ShelfCard**

```tsx
// apps/web/src/components/library/ShelfCard.tsx
'use client';

import { cn } from '@/lib/utils';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';

type ManaPipType = 'agent' | 'kb' | 'session' | 'chatSession';

interface ManaPip {
  type: ManaPipType;
  active: boolean;
}

interface ShelfCardProps {
  title: string;
  subtitle: string;
  coverIcon?: string;
  coverGradient?: string;
  imageUrl?: string;
  manaPips?: ManaPip[];
  inLibrary?: boolean;
  onAdd?: () => void;
  onClick?: () => void;
  mechanicIcon?: React.ReactNode;
  stateLabel?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' };
}

const PIP_LABELS: Record<ManaPipType, string> = {
  agent: '🤖',
  kb: '📄',
  session: '🎮',
  chatSession: '💬',
};

const PIP_COLORS: Record<ManaPipType, string> = {
  agent: entityColors.agent.hsl,
  kb: entityColors.kb.hsl,
  session: entityColors.session.hsl,
  chatSession: entityColors.chatSession.hsl,
};

const STATE_STYLES = {
  success: 'bg-emerald-500/80 text-white',
  warning: 'bg-amber-500/80 text-black',
  error: 'bg-red-500/80 text-white',
  info: 'bg-blue-500/80 text-white',
};

export function ShelfCard({
  title, subtitle, coverIcon, coverGradient, imageUrl,
  manaPips, inLibrary, onAdd, onClick, mechanicIcon, stateLabel,
}: ShelfCardProps) {
  return (
    <div
      className="w-[140px] flex-shrink-0 cursor-pointer overflow-hidden rounded-xl border border-[#30363d] bg-[#21262d] transition-all duration-200 hover:-translate-y-1 hover:border-[#f0a030] hover:shadow-[0_8px_24px_rgba(240,160,48,0.15)]"
      onClick={onClick}
    >
      {/* Cover */}
      <div className={cn('relative flex h-[100px] items-center justify-center', coverGradient && `bg-gradient-to-br ${coverGradient}`)}>
        {imageUrl ? (
          <img src={imageUrl} alt={title} className="h-full w-full object-cover" />
        ) : (
          <span className="text-3xl">{coverIcon}</span>
        )}
        {/* MtG overlays */}
        {(mechanicIcon || stateLabel) && (
          <div className="absolute bottom-0 left-0 right-0 flex items-end justify-between p-1">
            {mechanicIcon ? <div className="rounded-sm bg-black/60 p-0.5">{mechanicIcon}</div> : <div />}
            {stateLabel && (
              <span className={cn('rounded-sm px-1 py-0.5 text-[8px] font-semibold', STATE_STYLES[stateLabel.variant])}>
                {stateLabel.text}
              </span>
            )}
          </div>
        )}
      </div>

      {/* Info */}
      <div className="p-2">
        <h4 className="truncate text-xs font-semibold text-[#e6edf3]">{title}</h4>
        <p className="truncate text-[10px] text-[#8b949e]">{subtitle}</p>

        {/* Mana pips */}
        {manaPips && manaPips.length > 0 && (
          <div className="mt-1.5 flex gap-1">
            {manaPips.map((pip) => (
              <div
                key={pip.type}
                data-testid={`mana-pip-${pip.type}`}
                className={cn(
                  'flex h-[18px] w-[18px] items-center justify-center rounded-full text-[8px]',
                  pip.active
                    ? `text-white`
                    : 'bg-[#30363d] text-[#484f58]'
                )}
                style={pip.active ? { backgroundColor: `hsl(${PIP_COLORS[pip.type]})` } : undefined}
                title={pip.type}
              >
                {PIP_LABELS[pip.type]}
              </div>
            ))}
          </div>
        )}

        {/* In library / Add button */}
        {inLibrary === true && (
          <div className="mt-1.5 text-center text-[10px] text-emerald-500">✓ Nella tua libreria</div>
        )}
        {inLibrary === false && onAdd && (
          <button
            onClick={(e) => { e.stopPropagation(); onAdd(); }}
            aria-label="Aggiungi"
            className="mt-1.5 w-full rounded-md bg-emerald-500 px-2 py-1 text-[10px] font-semibold text-white transition-colors hover:bg-emerald-600"
          >
            + Aggiungi
          </button>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Implement ShelfRow**

```tsx
// apps/web/src/components/library/ShelfRow.tsx
'use client';

import { type ReactNode } from 'react';

interface ShelfRowProps {
  children: ReactNode;
}

export function ShelfRow({ children }: ShelfRowProps) {
  return (
    <div className="flex gap-3 overflow-x-auto pb-3 scrollbar-thin scrollbar-track-transparent scrollbar-thumb-[#30363d]">
      {children}
    </div>
  );
}
```

- [ ] **Step 5: Create barrel export**

```tsx
// apps/web/src/components/library/index.ts
export { ShelfCard } from './ShelfCard';
export { ShelfRow } from './ShelfRow';
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/library/ShelfCard.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/library/ apps/web/__tests__/components/library/
git commit -m "feat(library): add ShelfCard and ShelfRow components for vetrina layout"
```

---

## Task 8: MechanicFilter — Chip Filter Component

**Files:**
- Create: `apps/web/src/components/library/MechanicFilter.tsx`
- Test: `apps/web/__tests__/components/library/MechanicFilter.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/__tests__/components/library/MechanicFilter.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { MechanicFilter } from '@/components/library/MechanicFilter';

describe('MechanicFilter', () => {
  const mechanics = ['engine-building', 'area-control', 'deck-building'];

  it('renders filter chips for each mechanic', () => {
    render(<MechanicFilter mechanics={mechanics} selected={[]} onSelect={vi.fn()} />);
    expect(screen.getByText(/engine building/i)).toBeInTheDocument();
    expect(screen.getByText(/area control/i)).toBeInTheDocument();
    expect(screen.getByText(/deck building/i)).toBeInTheDocument();
  });

  it('highlights selected mechanic', () => {
    render(<MechanicFilter mechanics={mechanics} selected={['area-control']} onSelect={vi.fn()} />);
    const chip = screen.getByText(/area control/i).closest('button');
    expect(chip).toHaveClass('bg-[#f0a030]');
  });

  it('calls onSelect when chip clicked', () => {
    const onSelect = vi.fn();
    render(<MechanicFilter mechanics={mechanics} selected={[]} onSelect={onSelect} />);
    fireEvent.click(screen.getByText(/engine building/i));
    expect(onSelect).toHaveBeenCalledWith('engine-building');
  });
});
```

- [ ] **Step 2: Run test, verify fail, implement, run test, verify pass**

Implement `MechanicFilter` as a row of buttons with `MechanicIcon` + label. Selected chips get `bg-[#f0a030] text-[#0d1117]`, unselected get `bg-[#21262d] text-[#8b949e]`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/library/MechanicFilter.tsx apps/web/__tests__/components/library/MechanicFilter.test.tsx
git commit -m "feat(library): add MechanicFilter chip component with SVG icons"
```

---

## Task 9: Personal Library Page

**Files:**
- Create: `apps/web/src/components/library/PersonalLibraryPage.tsx`
- Create: `apps/web/src/components/library/LibraryToolbar.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/_content.tsx`
- Test: `apps/web/__tests__/components/library/PersonalLibraryPage.test.tsx`

- [ ] **Step 1: Write failing test for PersonalLibraryPage**

Test that it renders two sections ("Dal catalogo condiviso" and "I tuoi giochi creati"), uses ShelfRow with ShelfCards, shows empty state when no games, and renders the toolbar.

- [ ] **Step 2: Implement LibraryToolbar**

Search input + game count + Grid/List toggle. Wrap existing search/view patterns from `CollectionPageClient`.

- [ ] **Step 3: Implement PersonalLibraryPage**

Use `useLibrary()` hook to fetch data. Split games into two lists:
- Shared catalog games (where `sharedGameId` exists)
- Private games (where `privateGameId` exists)

Render each in a `TavoloSection` + `ShelfRow` with `ShelfCard` components. Include mana pips from entity link data. Add "Crea gioco" CTA card at end of private games row.

- [ ] **Step 4: Modify _content.tsx — Remove Wishlist/Proposals tabs**

In `apps/web/src/app/(authenticated)/library/_content.tsx`:
- Remove `?tab=wishlist` and `?tab=proposals` cases
- Keep default (Personal Library) and add `?tab=public` for Public Library
- Replace `CollectionPageClient` dynamic import with `PersonalLibraryPage`
- Tab labels: "La mia Libreria" (default) | "Catalogo Condiviso" (public)

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run __tests__/components/library/`
Expected: All pass

- [ ] **Step 6: Run build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/library/ apps/web/src/app/\(authenticated\)/library/ apps/web/__tests__/components/library/
git commit -m "feat(library): replace collection/wishlist/proposals with PersonalLibraryPage vetrina layout"
```

---

## Task 10: Public Library Page

**Files:**
- Create: `apps/web/src/components/library/PublicLibraryPage.tsx`
- Test: `apps/web/__tests__/components/library/PublicLibraryPage.test.tsx`

- [ ] **Step 1: Write failing test**

Test that it renders search bar, trending section (using ShelfRow), mechanic filter chips, game grid with "Aggiungi"/"Nella tua libreria" states, and Load More button.

- [ ] **Step 2: Implement PublicLibraryPage**

Create missing hooks and use existing ones:
- Create `useCatalogTrending()` hook wrapping `useQuery` + `fetchCatalogTrending()` from `lib/api/catalog.ts`
- Use `useSharedGames(params)` or create wrapper for `GET /api/v1/shared-games` with `useQuery`
- Use `useLibrary()` to check which games are already in user's library

Layout:
1. Centered search bar (large)
2. `TavoloSection icon="🔥" title="Trending questa settimana"` → ShelfRow
3. `TavoloSection icon="📋" title="Tutti i giochi"` → MechanicFilter + Grid of ShelfCards
4. Load More button at bottom

- [ ] **Step 3: Wire into _content.tsx as `?tab=public`**

- [ ] **Step 4: Run tests and build**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/PublicLibraryPage.tsx apps/web/__tests__/components/library/
git commit -m "feat(library): add PublicLibraryPage with trending, filters, and catalog grid"
```

---

## Task 11: Agent — Character Sheet Layout

**Files:**
- Create: `apps/web/src/components/agent/AgentCharacterSheet.tsx`
- Modify: `apps/web/src/app/(authenticated)/agents/[id]/page.tsx`
- Test: `apps/web/__tests__/components/agent/AgentCharacterSheet.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/__tests__/components/agent/AgentCharacterSheet.test.tsx
import { render, screen } from '@testing-library/react';
import { AgentCharacterSheet } from '@/components/agent/AgentCharacterSheet';

const mockAgent = {
  id: '1',
  name: 'Agente Gloomhaven',
  type: 'rules-expert',
  invocationCount: 12,
  lastUsed: new Date().toISOString(),
  gameId: 'g1',
  gameName: 'Gloomhaven',
};

describe('AgentCharacterSheet', () => {
  it('renders agent name and type badge', () => {
    render(<AgentCharacterSheet agent={mockAgent} />);
    expect(screen.getByText('Agente Gloomhaven')).toBeInTheDocument();
    expect(screen.getByText(/rules expert/i)).toBeInTheDocument();
  });

  it('renders stats in portrait section', () => {
    render(<AgentCharacterSheet agent={mockAgent} />);
    expect(screen.getByText('12')).toBeInTheDocument(); // invocation count
  });

  it('renders Equipaggiamento section', () => {
    render(<AgentCharacterSheet agent={mockAgent} />);
    expect(screen.getByText(/equipaggiamento/i)).toBeInTheDocument();
  });

  it('renders Area Azione section', () => {
    render(<AgentCharacterSheet agent={mockAgent} />);
    expect(screen.getByText(/area azione/i)).toBeInTheDocument();
  });

  it('renders Storia section', () => {
    render(<AgentCharacterSheet agent={mockAgent} />);
    expect(screen.getByText(/storia/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/agent/AgentCharacterSheet.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement AgentCharacterSheet**

Two-column layout (portrait sticky left + scrollable body right):

**Portrait (280px)**:
- Avatar with gradient (`hsl(${entityColors.agent})`)
- Agent name + type badge
- "Collegato a {gameName}" link
- Stats grid (2x2): Invocazioni, Chat, PDF, Ultimo uso
- Mana pips (32px) for connections
- "Configura" button

**Body sections** (no tabs, all visible):
1. **Equipaggiamento — Knowledge Base**: Create `useAgentKbDocs(gameId)` hook wrapping `useQuery` + `GET /api/v1/knowledge-base/{gameId}/documents` (gameId obtained from agent detail). The `knowledgeBaseClient` may need a new method. List PDF documents with status badges. "Carica PDF" CTA.
2. **Area Azione — Chat**: Embed existing `AgentChatTab` component (from `extra-meeple-card/entities/`). Readiness validation.
3. **Storia — Conversazioni Recenti**: Use `GET /chat-threads/my?gameId={gameId}`. List threads.

- [ ] **Step 4: Modify agents/[id]/page.tsx**

Replace current `MeepleCard hero + AgentExtraMeepleCard` layout with `<AgentCharacterSheet agent={agentData} />`. Keep SSR metadata generation.

- [ ] **Step 5: Run tests and build**

Run: `cd apps/web && pnpm vitest run __tests__/components/agent/ && pnpm build`
Expected: All pass, build succeeds

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/agent/ apps/web/src/app/\(authenticated\)/agents/ apps/web/__tests__/components/agent/
git commit -m "feat(agent): replace tabbed layout with RPG character sheet"
```

---

## Task 12: Remove Mock Data and Verify Empty States

**Files:**
- Modify: Various dashboard and library components
- Test: Visual verification

- [ ] **Step 1: Grep for mock/placeholder data in dashboard-v2**

Run: `cd apps/web && grep -rn "mock\|placeholder\|fake\|dummy\|sample\|hardcoded" src/components/dashboard-v2/ --include="*.tsx" --include="*.ts"`

Remove any hardcoded mock arrays, fake data generators, or placeholder content found.

- [ ] **Step 2: Grep for mock data in library components**

Run: `cd apps/web && grep -rn "mock\|placeholder\|fake\|dummy\|sample\|hardcoded" src/components/library/ src/app/\(authenticated\)/library/ --include="*.tsx" --include="*.ts"`

Remove any found.

- [ ] **Step 3: Verify all empty states render correctly**

Check that every section (dashboard sessions, games, agents, library, agent KB, chat) falls back to the appropriate `EmptyState` component with the spec-defined messages when data arrays are empty.

- [ ] **Step 4: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: All tests pass

- [ ] **Step 5: Run build**

Run: `cd apps/web && pnpm build`
Expected: Clean build, no errors

- [ ] **Step 6: Commit**

```bash
git add -u
git commit -m "chore(cleanup): remove all mock/placeholder data, verify empty states"
```

---

## Task 13: Final Integration Test and Type Check

**Files:** None (verification only)

- [ ] **Step 1: Run full typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors

- [ ] **Step 2: Run linter**

Run: `cd apps/web && pnpm lint`
Expected: No lint errors

- [ ] **Step 3: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: All tests pass

- [ ] **Step 4: Run build**

Run: `cd apps/web && pnpm build`
Expected: Clean build

- [ ] **Step 5: Commit any final fixes**

If any issues found in steps 1-4, fix and commit.

- [ ] **Step 6: Create PR**

Create PR to the **parent branch** (detect with `git config branch.<current>.parent` or `git merge-base`). Do NOT hardcode the target — always detect the correct parent. Title: `feat(ui): "Il Tavolo" user pages redesign — dashboard, library, agent`.

PR body should summarize:
- Dashboard: tavolo layout + activity feed + mana pips
- Library: personal/public split, vetrina layout, MtG card anatomy
- Agent: RPG character sheet (no tabs)
- Card system: mechanic icons (SX), state badges (DX), mana pip connections
- Removed: Wishlist, Proposals, all mock data
