# MeepleCard Mana System — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the MeepleCard component to use a unified Mana visual language with 16 entity types, modular back blocks, deck stack navigation, and a polymorphic drawer.

**Architecture:** Extend the existing MeepleCard system incrementally. New entity types and mana symbols are added to the existing `MeepleEntityType` union and `entityColors` map. Back content moves from monolithic per-entity components to composable blocks. Deck stack uses React Portal for overflow-safe rendering. Drawer becomes a generic entity viewer with dynamic tabs.

**Tech Stack:** React 19, Next.js 16, Tailwind 4, shadcn/ui (Radix), CVA, Vitest, TypeScript

**Spec:** `docs/superpowers/specs/2026-03-15-meeplecard-redesign-mana-system.md`

---

## File Structure Overview

### New Files
```
apps/web/src/components/ui/data-display/
├── mana/                                    # NEW — Mana symbol system
│   ├── ManaSymbol.tsx                       # Core mana symbol component (full/medium/mini)
│   ├── mana-icons.tsx                       # SVG icon definitions for 16 entities
│   ├── mana-config.ts                       # Entity colors, display names, relationship map
│   ├── mana-types.ts                        # Types: ManaSize, EntityRelationshipMap, etc.
│   ├── index.ts                             # Barrel export
│   └── __tests__/
│       ├── ManaSymbol.test.tsx
│       └── mana-config.test.ts
├── meeple-card-features/
│   ├── ManaLinkFooter.tsx                   # NEW — Replaces CardNavigationFooter
│   ├── ManaBadge.tsx                        # NEW — Entity mana badge (top-left)
│   ├── StatusGlow.tsx                       # NEW — Border glow animation
│   ├── PrimaryActions.tsx                   # NEW — 1-2 entity-specific actions
│   └── __tests__/
│       ├── ManaLinkFooter.test.tsx
│       ├── ManaBadge.test.tsx
│       ├── StatusGlow.test.tsx
│       └── PrimaryActions.test.tsx
├── card-back-blocks/                        # NEW — Modular back block system
│   ├── block-types.ts                       # BlockData, BlockAction, CardBackBlock
│   ├── block-registry.ts                    # Entity → Block[] mapping
│   ├── CardBackComposer.tsx                 # Renders blocks for an entity type
│   ├── blocks/
│   │   ├── StatsBlock.tsx
│   │   ├── ActionsBlock.tsx
│   │   ├── TimelineBlock.tsx
│   │   ├── RankingBlock.tsx
│   │   ├── KBPreviewBlock.tsx
│   │   ├── MembersBlock.tsx
│   │   ├── ContentsBlock.tsx
│   │   ├── HistoryBlock.tsx
│   │   ├── ProgressBlock.tsx
│   │   ├── NotesBlock.tsx
│   │   └── DetailLinkBlock.tsx
│   ├── index.ts
│   └── __tests__/
│       ├── CardBackComposer.test.tsx
│       ├── StatsBlock.test.tsx
│       └── ActionsBlock.test.tsx
├── deck-stack/                              # NEW — Deck stack navigation
│   ├── DeckStack.tsx                        # Fan layout with portal
│   ├── DeckStackCard.tsx                    # Compact card for stack items
│   ├── DeckStackContext.tsx                 # State management (open/close)
│   ├── deck-stack-types.ts                  # DeckStackItem, DeckStackProps
│   ├── index.ts
│   └── __tests__/
│       ├── DeckStack.test.tsx
│       └── DeckStackCard.test.tsx
├── extra-meeple-card/
│   └── hooks/
│       └── use-entity-detail.ts             # NEW — Generic entity detail dispatcher
```

### Modified Files
```
apps/web/src/components/ui/data-display/
├── meeple-card-styles.ts                    # ADD 6 new entity types + colors
├── meeple-card/types.ts                     # ADD mana props, deprecate navigateTo
├── meeple-card/constants.ts                 # EXTEND DRAWER_ENTITY_TYPE_MAP
├── meeple-card/variants/MeepleCardGrid.tsx  # USE ManaBadge, ManaLinkFooter, StatusGlow
├── meeple-card-features/FlipCard.tsx        # USE CardBackComposer
├── extra-meeple-card/ExtraMeepleCardDrawer.tsx  # EXTEND DrawerEntityType, dynamic tabs
├── extra-meeple-card/types.ts               # ADD new entity detail types
```

---

## Chunk 1: Phase 1 — Mana Symbol System

### Task 1.1: Extend MeepleEntityType and entityColors

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`
- Test: `apps/web/src/components/ui/data-display/__tests__/meeple-card-styles.test.ts`

- [ ] **Step 1: Write failing test for new entity types**

```typescript
// apps/web/src/components/ui/data-display/__tests__/meeple-card-styles.test.ts
import { entityColors } from '../meeple-card-styles';
import type { MeepleEntityType } from '../meeple-card-styles';

describe('entityColors', () => {
  const ALL_ENTITY_TYPES: MeepleEntityType[] = [
    'game', 'session', 'player', 'event',
    'collection', 'group', 'location', 'expansion',
    'agent', 'kb', 'chatSession', 'note',
    'toolkit', 'tool', 'achievement', 'custom',
  ];

  it('has colors for all 16 entity types', () => {
    ALL_ENTITY_TYPES.forEach(type => {
      expect(entityColors[type]).toBeDefined();
      expect(entityColors[type].hsl).toMatch(/^\d+ \d+% \d+%$/);
    });
  });

  it('has unique colors for each entity type', () => {
    const colors = Object.values(entityColors).map(c => c.hsl);
    expect(new Set(colors).size).toBe(colors.length);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/meeple-card-styles.test.ts`
Expected: FAIL — `collection`, `group`, `location`, `expansion`, `achievement`, `note` not in entityColors

- [ ] **Step 3: Add 6 new entity types to MeepleEntityType union and entityColors**

In `meeple-card-styles.ts`, extend the type union:
```typescript
export type MeepleEntityType =
  | 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chatSession'
  | 'event' | 'toolkit' | 'tool' | 'custom'
  // NEW: Mana system additions
  | 'collection' | 'group' | 'location' | 'expansion' | 'achievement' | 'note';
```

Add to `entityColors`:
```typescript
collection: { hsl: '20 70% 42%', name: 'Copper' },
group: { hsl: '280 50% 48%', name: 'Warm Violet' },
location: { hsl: '200 55% 45%', name: 'Slate Cyan' },
expansion: { hsl: '290 65% 50%', name: 'Magenta' },
achievement: { hsl: '45 90% 48%', name: 'Gold' },
note: { hsl: '40 30% 42%', name: 'Warm Gray' },
```

Also update `custom` color from `220 70% 50%` to `220 15% 45%` (Silver, per spec).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/meeple-card-styles.test.ts`
Expected: PASS

- [ ] **Step 5: Run full typecheck to catch any downstream issues**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS — new types are additive, no breaking changes.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts apps/web/src/components/ui/data-display/__tests__/meeple-card-styles.test.ts
git commit -m "feat(mana): extend MeepleEntityType to 16 entity types with colors"
```

---

### Task 1.2: Create mana-types.ts and mana-config.ts

**Files:**
- Create: `apps/web/src/components/ui/data-display/mana/mana-types.ts`
- Create: `apps/web/src/components/ui/data-display/mana/mana-config.ts`
- Test: `apps/web/src/components/ui/data-display/mana/__tests__/mana-config.test.ts`

- [ ] **Step 1: Write mana-types.ts**

```typescript
// apps/web/src/components/ui/data-display/mana/mana-types.ts
import type { MeepleEntityType } from '../meeple-card-styles';

export type ManaSize = 'full' | 'medium' | 'mini';

export interface ManaDisplayConfig {
  key: MeepleEntityType;
  displayName: string;
  symbol: string;       // emoji placeholder until SVG
  tier: 'core' | 'social' | 'ai' | 'tools';
}

export type EntityRelationshipMap = Record<MeepleEntityType, MeepleEntityType[]>;
```

- [ ] **Step 2: Write failing test for mana-config**

```typescript
// apps/web/src/components/ui/data-display/mana/__tests__/mana-config.test.ts
import { MANA_DISPLAY, ENTITY_RELATIONSHIPS, getManaDisplayName } from '../mana-config';

describe('MANA_DISPLAY', () => {
  it('has display config for all 16 entity types', () => {
    expect(Object.keys(MANA_DISPLAY)).toHaveLength(16);
  });

  it('each config has required fields', () => {
    Object.values(MANA_DISPLAY).forEach(config => {
      expect(config.displayName).toBeTruthy();
      expect(config.symbol).toBeTruthy();
      expect(['core', 'social', 'ai', 'tools']).toContain(config.tier);
    });
  });
});

describe('ENTITY_RELATIONSHIPS', () => {
  it('has relationship arrays for all 16 entity types', () => {
    expect(Object.keys(ENTITY_RELATIONSHIPS)).toHaveLength(16);
  });

  it('custom has empty relationships by default', () => {
    expect(ENTITY_RELATIONSHIPS.custom).toEqual([]);
  });

  it('game links to session, kb, agent, expansion, collection, note', () => {
    expect(ENTITY_RELATIONSHIPS.game).toEqual(
      expect.arrayContaining(['session', 'kb', 'agent', 'expansion', 'collection', 'note'])
    );
  });
});

describe('getManaDisplayName', () => {
  it('returns display name for entity type', () => {
    expect(getManaDisplayName('kb')).toBe('Knowledge');
    expect(getManaDisplayName('chatSession')).toBe('Chat');
    expect(getManaDisplayName('game')).toBe('Game');
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/mana/__tests__/mana-config.test.ts`
Expected: FAIL — module not found

- [ ] **Step 4: Write mana-config.ts**

```typescript
// apps/web/src/components/ui/data-display/mana/mana-config.ts
import type { MeepleEntityType } from '../meeple-card-styles';
import type { ManaDisplayConfig, EntityRelationshipMap } from './mana-types';

export const MANA_DISPLAY: Record<MeepleEntityType, ManaDisplayConfig> = {
  game:        { key: 'game',        displayName: 'Game',        symbol: '🎲', tier: 'core' },
  session:     { key: 'session',     displayName: 'Session',     symbol: '⏳', tier: 'core' },
  player:      { key: 'player',      displayName: 'Player',      symbol: '♟',  tier: 'core' },
  event:       { key: 'event',       displayName: 'Event',       symbol: '✦',  tier: 'core' },
  collection:  { key: 'collection',  displayName: 'Collection',  symbol: '📦', tier: 'social' },
  group:       { key: 'group',       displayName: 'Group',       symbol: '👥', tier: 'social' },
  location:    { key: 'location',    displayName: 'Location',    symbol: '📍', tier: 'social' },
  expansion:   { key: 'expansion',   displayName: 'Expansion',   symbol: '🃏', tier: 'social' },
  agent:       { key: 'agent',       displayName: 'Agent',       symbol: '⚡', tier: 'ai' },
  kb:          { key: 'kb',          displayName: 'Knowledge',   symbol: '📜', tier: 'ai' },
  chatSession: { key: 'chatSession', displayName: 'Chat',        symbol: '💬', tier: 'ai' },
  note:        { key: 'note',        displayName: 'Note',        symbol: '📝', tier: 'ai' },
  toolkit:     { key: 'toolkit',     displayName: 'Toolkit',     symbol: '⚙',  tier: 'tools' },
  tool:        { key: 'tool',        displayName: 'Tool',        symbol: '🔧', tier: 'tools' },
  achievement: { key: 'achievement', displayName: 'Achievement', symbol: '🏆', tier: 'tools' },
  custom:      { key: 'custom',      displayName: 'Custom',      symbol: '✧',  tier: 'tools' },
};

export const ENTITY_RELATIONSHIPS: EntityRelationshipMap = {
  game:        ['session', 'kb', 'agent', 'expansion', 'collection', 'note'],
  session:     ['game', 'player', 'event', 'location', 'note', 'group'],
  player:      ['session', 'group', 'achievement', 'collection', 'note'],
  event:       ['session', 'game', 'player', 'location', 'group'],
  collection:  ['game', 'expansion', 'player'],
  group:       ['player', 'event', 'session', 'location'],
  location:    ['event', 'session', 'group'],
  expansion:   ['game', 'collection'],
  agent:       ['game', 'kb', 'chatSession', 'tool'],
  kb:          ['game', 'agent', 'note'],
  chatSession: ['agent', 'game', 'player'],
  toolkit:     ['tool', 'game'],
  tool:        ['toolkit', 'game'],
  achievement: ['player', 'game', 'session'],
  note:        ['game', 'session', 'player', 'kb', 'event'],
  custom:      [],
};

export function getManaDisplayName(entityType: MeepleEntityType): string {
  return MANA_DISPLAY[entityType].displayName;
}

export function getManaSymbol(entityType: MeepleEntityType): string {
  return MANA_DISPLAY[entityType].symbol;
}

export function getRelatedEntityTypes(entityType: MeepleEntityType): MeepleEntityType[] {
  return ENTITY_RELATIONSHIPS[entityType];
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/mana/__tests__/mana-config.test.ts`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/mana/
git commit -m "feat(mana): add mana types, display config, and entity relationship map"
```

---

### Task 1.3: Build ManaSymbol component

**Files:**
- Create: `apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx`
- Create: `apps/web/src/components/ui/data-display/mana/index.ts`
- Test: `apps/web/src/components/ui/data-display/mana/__tests__/ManaSymbol.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/ui/data-display/mana/__tests__/ManaSymbol.test.tsx
import { render, screen } from '@testing-library/react';
import { ManaSymbol } from '../ManaSymbol';

describe('ManaSymbol', () => {
  it('renders with correct entity type', () => {
    render(<ManaSymbol entity="game" />);
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('renders at full size by default', () => {
    const { container } = render(<ManaSymbol entity="game" />);
    const symbol = container.firstChild as HTMLElement;
    expect(symbol.className).toContain('w-16'); // 64px = w-16
  });

  it('renders at medium size', () => {
    const { container } = render(<ManaSymbol entity="session" size="medium" />);
    const symbol = container.firstChild as HTMLElement;
    expect(symbol.className).toContain('w-7'); // 28px = w-7
  });

  it('renders at mini size', () => {
    const { container } = render(<ManaSymbol entity="player" size="mini" />);
    const symbol = container.firstChild as HTMLElement;
    expect(symbol.className).toContain('w-5'); // 20px = w-5
  });

  it('shows display name when showLabel is true', () => {
    render(<ManaSymbol entity="kb" showLabel />);
    expect(screen.getByText('Knowledge')).toBeInTheDocument();
  });

  it('does not show label by default', () => {
    render(<ManaSymbol entity="kb" />);
    expect(screen.queryByText('Knowledge')).not.toBeInTheDocument();
  });

  it('applies entity color as CSS custom property', () => {
    const { container } = render(<ManaSymbol entity="game" />);
    const symbol = container.firstChild as HTMLElement;
    expect(symbol.style.getPropertyValue('--mana-color')).toBe('25 95% 45%');
  });

  it('supports customColor override for custom entity', () => {
    const { container } = render(<ManaSymbol entity="custom" customColor="180 50% 50%" />);
    const symbol = container.firstChild as HTMLElement;
    expect(symbol.style.getPropertyValue('--mana-color')).toBe('180 50% 50%');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/mana/__tests__/ManaSymbol.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write ManaSymbol component**

```tsx
// apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx
import { memo } from 'react';
import { cn } from '@/lib/utils';
import { entityColors, type MeepleEntityType } from '../meeple-card-styles';
import { MANA_DISPLAY } from './mana-config';
import type { ManaSize } from './mana-types';

const SIZE_CLASSES: Record<ManaSize, string> = {
  full: 'w-16 h-16 text-[1.6rem]',
  medium: 'w-7 h-7 text-sm',
  mini: 'w-5 h-5 text-xs',
};

const LABEL_CLASSES: Record<ManaSize, string> = {
  full: 'text-xs mt-2',
  medium: 'text-[9px] ml-1.5',
  mini: 'text-[8px] ml-1',
};

interface ManaSymbolProps {
  entity: MeepleEntityType;
  size?: ManaSize;
  showLabel?: boolean;
  customColor?: string;
  className?: string;
  onClick?: () => void;
  'data-testid'?: string;
}

export const ManaSymbol = memo(function ManaSymbol({
  entity,
  size = 'full',
  showLabel = false,
  customColor,
  className,
  onClick,
  ...props
}: ManaSymbolProps) {
  const config = MANA_DISPLAY[entity];
  const color = customColor ?? entityColors[entity].hsl;

  const testId = props['data-testid'] ?? `mana-symbol-${entity}`;

  return (
    <span
      className={cn(
        'inline-flex items-center',
        showLabel && size !== 'full' && 'flex-row',
        showLabel && size === 'full' && 'flex-col items-center',
        onClick && 'cursor-pointer',
        className,
      )}
    >
      <span
        data-testid={testId}
        onClick={onClick}
        className={cn(
          'inline-flex items-center justify-center rounded-full relative',
          'shadow-[0_4px_16px_hsl(var(--mana-color)/0.35)]',
          'bg-[radial-gradient(circle_at_35%_35%,hsl(var(--mana-color)/1)_0%,hsl(var(--mana-color)/0.65)_100%)]',
          'ring-2 ring-[hsl(var(--mana-color)/0.35)]',
          'after:absolute after:inset-[3px] after:rounded-full after:border after:border-white/[0.08]',
          SIZE_CLASSES[size],
          onClick && 'transition-transform duration-150 hover:scale-110',
        )}
        style={{ '--mana-color': color } as React.CSSProperties}
      >
        <span className="relative z-[1] drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
          {config.symbol}
        </span>
      </span>
      {showLabel && (
        <span
          className={cn(
            'font-quicksand font-bold uppercase tracking-wider',
            LABEL_CLASSES[size],
          )}
          style={{ color: `hsl(${color})` }}
        >
          {config.displayName}
        </span>
      )}
    </span>
  );
});
```

- [ ] **Step 4: Write barrel export**

```typescript
// apps/web/src/components/ui/data-display/mana/index.ts
export { ManaSymbol } from './ManaSymbol';
export { MANA_DISPLAY, ENTITY_RELATIONSHIPS, getManaDisplayName, getManaSymbol, getRelatedEntityTypes } from './mana-config';
export type { ManaSize, ManaDisplayConfig, EntityRelationshipMap } from './mana-types';
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/mana/__tests__/ManaSymbol.test.tsx`
Expected: PASS

- [ ] **Step 6: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/mana/
git commit -m "feat(mana): build ManaSymbol component with 3 sizes and entity color system"
```

---

## Chunk 2: Phase 2 — Card Front Redesign

### Task 2.1: Build ManaBadge component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-features/ManaBadge.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/ManaBadge.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/ManaBadge.test.tsx
import { render, screen } from '@testing-library/react';
import { ManaBadge } from '../ManaBadge';

describe('ManaBadge', () => {
  it('renders mana symbol and entity display name', () => {
    render(<ManaBadge entity="game" />);
    expect(screen.getByText('Game')).toBeInTheDocument();
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('renders kb as Knowledge', () => {
    render(<ManaBadge entity="kb" />);
    expect(screen.getByText('Knowledge')).toBeInTheDocument();
  });

  it('renders chatSession as Chat', () => {
    render(<ManaBadge entity="chatSession" />);
    expect(screen.getByText('Chat')).toBeInTheDocument();
  });

  it('has backdrop-blur styling', () => {
    const { container } = render(<ManaBadge entity="game" />);
    const badge = container.firstChild as HTMLElement;
    expect(badge.className).toContain('backdrop-blur');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Write ManaBadge component**

```tsx
// ManaBadge.tsx
import { memo } from 'react';
import { cn } from '@/lib/utils';
import { entityColors, type MeepleEntityType } from '../meeple-card-styles';
import { ManaSymbol } from '../mana/ManaSymbol';
import { getManaDisplayName } from '../mana/mana-config';

interface ManaBadgeProps {
  entity: MeepleEntityType;
  className?: string;
}

export const ManaBadge = memo(function ManaBadge({ entity, className }: ManaBadgeProps) {
  const color = entityColors[entity].hsl;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 px-2 py-0.5 rounded-md',
        'backdrop-blur-[8px] font-quicksand font-bold',
        'text-[10px] uppercase tracking-wider text-white',
        className,
      )}
      style={{ backgroundColor: `hsl(${color} / 0.85)` }}
    >
      <ManaSymbol entity={entity} size="mini" data-testid={`mana-symbol-${entity}`} />
      {getManaDisplayName(entity)}
    </span>
  );
});
```

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/ManaBadge.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/ManaBadge.test.tsx
git commit -m "feat(mana): add ManaBadge component for entity type indicator"
```

---

### Task 2.2: Build StatusGlow component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-features/StatusGlow.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/StatusGlow.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/StatusGlow.test.tsx
import { render } from '@testing-library/react';
import { StatusGlow } from '../StatusGlow';

describe('StatusGlow', () => {
  it('renders pulsing glow for active state', () => {
    const { container } = render(<StatusGlow state="active" entityColor="25 95% 45%" />);
    expect(container.firstChild).toHaveClass('animate-pulse');
  });

  it('renders solid glow for complete state', () => {
    const { container } = render(<StatusGlow state="complete" entityColor="25 95% 45%" />);
    expect(container.firstChild).not.toHaveClass('animate-pulse');
    expect(container.firstChild).toHaveClass('opacity-100');
  });

  it('renders no glow for idle state', () => {
    const { container } = render(<StatusGlow state="idle" entityColor="25 95% 45%" />);
    expect(container.firstChild).toHaveClass('opacity-0');
  });

  it('renders red glow for error state', () => {
    const { container } = render(<StatusGlow state="error" entityColor="25 95% 45%" />);
    const el = container.firstChild as HTMLElement;
    expect(el.style.getPropertyValue('--glow-color')).toContain('0 80%'); // red
  });
});
```

- [ ] **Step 2: Run test, verify fail**

- [ ] **Step 3: Implement StatusGlow**

```tsx
// StatusGlow.tsx
import { memo } from 'react';
import { cn } from '@/lib/utils';

type GlowState = 'active' | 'complete' | 'idle' | 'error' | 'new';

interface StatusGlowProps {
  state: GlowState;
  entityColor: string;
  className?: string;
}

const GLOW_CONFIG: Record<GlowState, { animate: boolean; opacity: string; colorOverride?: string }> = {
  active:   { animate: true,  opacity: 'opacity-100' },
  complete: { animate: false, opacity: 'opacity-100' },
  idle:     { animate: false, opacity: 'opacity-0' },
  error:    { animate: true,  opacity: 'opacity-100', colorOverride: '0 80% 55%' },
  new:      { animate: false, opacity: 'opacity-100' },
};

export const StatusGlow = memo(function StatusGlow({ state, entityColor, className }: StatusGlowProps) {
  const config = GLOW_CONFIG[state];
  const glowColor = config.colorOverride ?? entityColor;

  return (
    <span
      className={cn(
        'absolute inset-[-1px] rounded-[14px] pointer-events-none z-0',
        'shadow-[0_0_20px_hsl(var(--glow-color)/0.3)]',
        config.animate && 'animate-pulse',
        config.opacity,
        state === 'new' && 'ring-2 ring-[hsl(var(--glow-color)/0.6)]',
        className,
      )}
      style={{ '--glow-color': glowColor } as React.CSSProperties}
    />
  );
});
```

- [ ] **Step 4: Run test, verify pass**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/StatusGlow.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/StatusGlow.test.tsx
git commit -m "feat(mana): add StatusGlow component for border glow effects"
```

---

### Task 2.3: Build ManaLinkFooter component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-features/ManaLinkFooter.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/ManaLinkFooter.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/ManaLinkFooter.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { ManaLinkFooter } from '../ManaLinkFooter';

describe('ManaLinkFooter', () => {
  const mockOnPipClick = vi.fn();

  it('renders mana pips for linked entity types', () => {
    render(
      <ManaLinkFooter
        linkedEntities={[
          { entityType: 'session', count: 3 },
          { entityType: 'kb', count: 1 },
        ]}
        onPipClick={mockOnPipClick}
      />
    );
    expect(screen.getByTestId('mana-pip-session')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-kb')).toBeInTheDocument();
  });

  it('calls onPipClick with entity type when pip is clicked', () => {
    render(
      <ManaLinkFooter
        linkedEntities={[{ entityType: 'session', count: 3 }]}
        onPipClick={mockOnPipClick}
      />
    );
    fireEvent.click(screen.getByTestId('mana-pip-session'));
    expect(mockOnPipClick).toHaveBeenCalledWith('session');
  });

  it('shows overflow count when more than maxVisible links', () => {
    render(
      <ManaLinkFooter
        linkedEntities={[
          { entityType: 'session', count: 3 },
          { entityType: 'kb', count: 1 },
          { entityType: 'agent', count: 2 },
          { entityType: 'expansion', count: 1 },
          { entityType: 'note', count: 4 },
        ]}
        onPipClick={mockOnPipClick}
        maxVisible={4}
      />
    );
    expect(screen.getByText('+1')).toBeInTheDocument();
  });

  it('renders nothing when linkedEntities is empty', () => {
    const { container } = render(
      <ManaLinkFooter linkedEntities={[]} onPipClick={mockOnPipClick} />
    );
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Run test, verify fail**

- [ ] **Step 3: Implement ManaLinkFooter**

```tsx
// ManaLinkFooter.tsx
import { memo } from 'react';
import { cn } from '@/lib/utils';
import type { MeepleEntityType } from '../meeple-card-styles';
import { ManaSymbol } from '../mana/ManaSymbol';

export interface LinkedEntityInfo {
  entityType: MeepleEntityType;
  count: number;
}

interface ManaLinkFooterProps {
  linkedEntities: LinkedEntityInfo[];
  onPipClick: (entityType: MeepleEntityType) => void;
  maxVisible?: number;
  className?: string;
}

export const ManaLinkFooter = memo(function ManaLinkFooter({
  linkedEntities,
  onPipClick,
  maxVisible = 4,
  className,
}: ManaLinkFooterProps) {
  if (linkedEntities.length === 0) return null;

  const visible = linkedEntities.slice(0, maxVisible);
  const overflow = linkedEntities.length - maxVisible;

  return (
    <div
      className={cn(
        'flex items-center gap-1.5 px-3.5 py-1.5',
        'border-t border-white/5',
        className,
      )}
    >
      {visible.map(({ entityType }) => (
        <ManaSymbol
          key={entityType}
          entity={entityType}
          size="mini"
          onClick={() => onPipClick(entityType)}
          data-testid={`mana-pip-${entityType}`}
          className="transition-transform duration-150 hover:scale-125"
        />
      ))}
      {overflow > 0 && (
        <span className="text-[9px] font-semibold text-slate-500 ml-auto">
          +{overflow}
        </span>
      )}
    </div>
  );
});
```

- [ ] **Step 4: Run test, verify pass**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/ManaLinkFooter.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/ManaLinkFooter.test.tsx
git commit -m "feat(mana): add ManaLinkFooter with clickable mana pips"
```

---

### Task 2.4: Build PrimaryActions component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-features/PrimaryActions.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/PrimaryActions.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/PrimaryActions.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { PrimaryActions } from '../PrimaryActions';

describe('PrimaryActions', () => {
  it('renders up to 2 action buttons', () => {
    const actions = [
      { icon: '▶', label: 'Play', onClick: vi.fn() },
      { icon: '💬', label: 'Ask AI', onClick: vi.fn() },
    ];
    render(<PrimaryActions actions={actions} />);
    expect(screen.getAllByRole('button')).toHaveLength(2);
  });

  it('truncates to 2 max even if more provided', () => {
    const actions = [
      { icon: '▶', label: 'Play', onClick: vi.fn() },
      { icon: '💬', label: 'Ask AI', onClick: vi.fn() },
      { icon: '⚙', label: 'Config', onClick: vi.fn() },
    ];
    render(<PrimaryActions actions={actions} />);
    expect(screen.getAllByRole('button')).toHaveLength(2);
  });

  it('calls onClick when action clicked', () => {
    const onClick = vi.fn();
    render(<PrimaryActions actions={[{ icon: '▶', label: 'Play', onClick }]} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('renders nothing when no actions', () => {
    const { container } = render(<PrimaryActions actions={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Run test, verify fail**

- [ ] **Step 3: Implement PrimaryActions**

```tsx
// PrimaryActions.tsx
import { memo } from 'react';
import { cn } from '@/lib/utils';

export interface PrimaryAction {
  icon: string;
  label: string;
  onClick: () => void;
}

interface PrimaryActionsProps {
  actions: PrimaryAction[];
  className?: string;
}

export const PrimaryActions = memo(function PrimaryActions({ actions, className }: PrimaryActionsProps) {
  const visible = actions.slice(0, 2);
  if (visible.length === 0) return null;

  return (
    <div className={cn('flex gap-1.5', className)}>
      {visible.map((action) => (
        <button
          key={action.label}
          type="button"
          onClick={(e) => { e.stopPropagation(); action.onClick(); }}
          title={action.label}
          className={cn(
            'w-[30px] h-[30px] rounded-lg border-none',
            'bg-black/50 backdrop-blur-[8px] text-slate-200',
            'text-sm flex items-center justify-center',
            'transition-colors duration-150 hover:bg-white/20',
            'cursor-pointer',
          )}
        >
          {action.icon}
        </button>
      ))}
    </div>
  );
});
```

- [ ] **Step 4: Run test, verify pass**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/PrimaryActions.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/PrimaryActions.test.tsx
git commit -m "feat(mana): add PrimaryActions component for card front actions"
```

---

### Task 2.5: Add mana props to MeepleCardProps and update MeepleCardGrid

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`

- [ ] **Step 1: Add new props to MeepleCardProps in types.ts**

Add these props to the interface (near the existing `navigateTo` prop):

```typescript
// Mana system props (Phase 2)
/** @deprecated Use linkedEntities instead */
navigateTo?: ResolvedNavigationLink[];
linkedEntities?: LinkedEntityInfo[];
onManaPipClick?: (entityType: MeepleEntityType) => void;
primaryActions?: PrimaryAction[];
glowState?: 'active' | 'complete' | 'idle' | 'error' | 'new';
```

- [ ] **Step 2: Integrate ManaBadge, StatusGlow, ManaLinkFooter, PrimaryActions into MeepleCardGrid**

Replace the existing entity badge rendering with `ManaBadge`. Add `StatusGlow` wrapper. Replace `CardNavigationFooter` with `ManaLinkFooter` when `linkedEntities` is provided (fall back to `CardNavigationFooter` for backward compat). Add `PrimaryActions` in the action zone.

**Key changes in MeepleCardGrid.tsx:**
- Import `ManaBadge`, `StatusGlow`, `ManaLinkFooter`, `PrimaryActions`
- Replace entity badge span with `<ManaBadge entity={entity} />`
- Add `{glowState && <StatusGlow state={glowState} entityColor={entityColors[entity].hsl} />}` as first child
- Footer: render `ManaLinkFooter` if `linkedEntities` present, else keep `CardNavigationFooter`
- Actions: render `PrimaryActions` if `primaryActions` present, else keep existing quick actions

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 4: Run existing MeepleCard tests to verify no regression**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/ --reporter=verbose`
Expected: All existing tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git commit -m "feat(mana): integrate mana components into MeepleCardGrid variant"
```

---

## Chunk 3: Phase 3 — Modular Back Blocks

### Task 3.1: Create block type system

**Files:**
- Create: `apps/web/src/components/ui/data-display/card-back-blocks/block-types.ts`
- Test: `apps/web/src/components/ui/data-display/card-back-blocks/__tests__/block-types.test.ts`

- [ ] **Step 1: Write block-types.ts**

Copy the `BlockType`, `BlockAction`, `BlockData`, and `CardBackBlock` types directly from the spec (lines 229-261). These types are already fully specified in the spec.

- [ ] **Step 2: Write test for type completeness**

```typescript
// __tests__/block-types.test.ts
import type { BlockType, BlockData } from '../block-types';

describe('block-types', () => {
  it('BlockType covers all 11 block types', () => {
    const allTypes: BlockType[] = [
      'stats', 'actions', 'timeline', 'ranking', 'kbPreview',
      'members', 'contents', 'history', 'progress', 'notes', 'detailLink',
    ];
    // TypeScript compile-time check — if this compiles, types are correct
    expect(allTypes).toHaveLength(11);
  });
});
```

- [ ] **Step 3: Run test, verify pass**

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/card-back-blocks/
git commit -m "feat(mana): add card back block type system with 11 block types"
```

---

### Task 3.2: Create block-registry.ts

**Files:**
- Create: `apps/web/src/components/ui/data-display/card-back-blocks/block-registry.ts`
- Test: `apps/web/src/components/ui/data-display/card-back-blocks/__tests__/block-registry.test.ts`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/block-registry.test.ts
import { getBlocksForEntity } from '../block-registry';

describe('block-registry', () => {
  it('returns correct blocks for game entity', () => {
    const blocks = getBlocksForEntity('game');
    expect(blocks.map(b => b.type)).toEqual(['stats', 'actions', 'kbPreview']);
  });

  it('returns correct blocks for session entity', () => {
    const blocks = getBlocksForEntity('session');
    expect(blocks.map(b => b.type)).toEqual(['ranking', 'timeline', 'actions']);
  });

  it('returns blocks for all 16 entity types', () => {
    const allTypes = [
      'game', 'session', 'player', 'event', 'collection', 'group',
      'location', 'expansion', 'agent', 'kb', 'chatSession', 'note',
      'toolkit', 'tool', 'achievement', 'custom',
    ] as const;
    allTypes.forEach(type => {
      const blocks = getBlocksForEntity(type);
      expect(blocks.length).toBeGreaterThan(0);
      // Every entity has at least an ActionsBlock
      expect(blocks.some(b => b.type === 'actions')).toBe(true);
    });
  });
});
```

- [ ] **Step 2: Run test, verify fail**

- [ ] **Step 3: Implement block-registry.ts**

Implement `getBlocksForEntity()` using the entity→block mapping from the spec (lines 150-222). Each entry returns an array of `{ type: BlockType, title: string }` objects.

- [ ] **Step 4: Run test, verify pass**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/card-back-blocks/block-registry.ts apps/web/src/components/ui/data-display/card-back-blocks/__tests__/block-registry.test.ts
git commit -m "feat(mana): add block registry with entity-to-block mapping for 16 types"
```

---

### Task 3.3: Build individual block components (StatsBlock, ActionsBlock)

**Files:**
- Create: `apps/web/src/components/ui/data-display/card-back-blocks/blocks/StatsBlock.tsx`
- Create: `apps/web/src/components/ui/data-display/card-back-blocks/blocks/ActionsBlock.tsx`
- Test: `apps/web/src/components/ui/data-display/card-back-blocks/__tests__/StatsBlock.test.tsx`
- Test: `apps/web/src/components/ui/data-display/card-back-blocks/__tests__/ActionsBlock.test.tsx`

- [ ] **Step 1: Write failing tests for StatsBlock and ActionsBlock**

- [ ] **Step 2: Implement StatsBlock** — renders key-value pairs with optional icons, entity-colored title
- [ ] **Step 3: Implement ActionsBlock** — renders list of action buttons with variant styling
- [ ] **Step 4: Run tests, verify pass**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(mana): add StatsBlock and ActionsBlock back components"
```

---

### Task 3.4: Build remaining block components

**Files:**
- Create: `blocks/TimelineBlock.tsx`, `blocks/RankingBlock.tsx`, `blocks/KBPreviewBlock.tsx`
- Create: `blocks/MembersBlock.tsx`, `blocks/ContentsBlock.tsx`, `blocks/HistoryBlock.tsx`
- Create: `blocks/ProgressBlock.tsx`, `blocks/NotesBlock.tsx`, `blocks/DetailLinkBlock.tsx`

Each block follows the same pattern: accept `CardBackBlock` props, render title with entity color accent, render block-specific content.

- [ ] **Step 1: Write tests for all 9 remaining blocks** (one test file per block, test rendering and edge cases)
- [ ] **Step 2: Implement all 9 blocks**
- [ ] **Step 3: Run all tests, verify pass**
- [ ] **Step 4: Commit**

```bash
git commit -m "feat(mana): add 9 remaining card back block components"
```

---

### Task 3.5: Build CardBackComposer and integrate with FlipCard

**Files:**
- Create: `apps/web/src/components/ui/data-display/card-back-blocks/CardBackComposer.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/FlipCard.tsx`
- Test: `apps/web/src/components/ui/data-display/card-back-blocks/__tests__/CardBackComposer.test.tsx`

- [ ] **Step 1: Write failing test for CardBackComposer**

```typescript
// __tests__/CardBackComposer.test.tsx
import { render, screen } from '@testing-library/react';
import { CardBackComposer } from '../CardBackComposer';

describe('CardBackComposer', () => {
  it('renders blocks for game entity with provided data', () => {
    render(
      <CardBackComposer
        entity="game"
        blockData={{
          stats: { type: 'stats', entries: [{ label: 'Plays', value: 12 }] },
          actions: { type: 'actions', actions: [{ label: 'Play', onClick: vi.fn() }] },
        }}
      />
    );
    expect(screen.getByText('Plays')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Play')).toBeInTheDocument();
  });

  it('renders empty state for blocks with no data', () => {
    render(<CardBackComposer entity="location" blockData={{}} />);
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test, verify fail**
- [ ] **Step 3: Implement CardBackComposer** — uses `getBlocksForEntity()` to get block list, renders each block component with matching data from `blockData` prop
- [ ] **Step 4: Integrate into FlipCard** — when `flippable` and entity type is provided, render `CardBackComposer` on the back face instead of `GameBackContent`/`SessionBackContent`
- [ ] **Step 5: Run existing FlipCard tests to verify no regression**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/CartaEstesa.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(mana): add CardBackComposer and integrate with FlipCard"
```

---

## Chunk 4: Phase 4 — Deck Stack

### Task 4.1: Create deck-stack types and context

**Files:**
- Create: `apps/web/src/components/ui/data-display/deck-stack/deck-stack-types.ts`
- Create: `apps/web/src/components/ui/data-display/deck-stack/DeckStackContext.tsx`

- [ ] **Step 1: Write deck-stack-types.ts**

```typescript
import type { MeepleEntityType } from '../meeple-card-styles';

export interface DeckStackItem {
  id: string;
  entityType: MeepleEntityType;
  title: string;
  status?: string;
}

export interface DeckStackState {
  isOpen: boolean;
  sourceEntityType: MeepleEntityType | null;
  items: DeckStackItem[];
  anchorRect: DOMRect | null;
}
```

- [ ] **Step 2: Write DeckStackContext** — provides `openDeckStack(entityType, items, anchorRect)` and `closeDeckStack()` methods

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(mana): add deck stack types and context"
```

---

### Task 4.2: Build DeckStackCard compact component

**Files:**
- Create: `apps/web/src/components/ui/data-display/deck-stack/DeckStackCard.tsx`
- Test: `apps/web/src/components/ui/data-display/deck-stack/__tests__/DeckStackCard.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/DeckStackCard.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { DeckStackCard } from '../DeckStackCard';

describe('DeckStackCard', () => {
  it('renders mana symbol, title, and optional status', () => {
    render(
      <DeckStackCard
        item={{ id: '1', entityType: 'game', title: 'Terraforming Mars', status: 'Owned' }}
        index={0}
        onClick={vi.fn()}
      />
    );
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
    expect(screen.getByText('Owned')).toBeInTheDocument();
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('applies rotation based on index', () => {
    const { container } = render(
      <DeckStackCard
        item={{ id: '1', entityType: 'session', title: 'Session 1' }}
        index={2}
        onClick={vi.fn()}
      />
    );
    const card = container.firstChild as HTMLElement;
    expect(card.style.transform).toContain('rotate');
  });

  it('calls onClick with item id', () => {
    const onClick = vi.fn();
    render(
      <DeckStackCard
        item={{ id: 'abc', entityType: 'game', title: 'Test' }}
        index={0}
        onClick={onClick}
      />
    );
    fireEvent.click(screen.getByText('Test'));
    expect(onClick).toHaveBeenCalledWith('abc', 'game');
  });
});
```

- [ ] **Step 2: Run test, verify fail**
- [ ] **Step 3: Implement DeckStackCard** — 120×48px compact card with mana symbol, title (truncated), optional status chip, entity accent left border, rotation transform based on index
- [ ] **Step 4: Run test, verify pass**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(mana): add DeckStackCard compact component"
```

---

### Task 4.3: Build DeckStack main component with portal

**Files:**
- Create: `apps/web/src/components/ui/data-display/deck-stack/DeckStack.tsx`
- Create: `apps/web/src/components/ui/data-display/deck-stack/index.ts`
- Test: `apps/web/src/components/ui/data-display/deck-stack/__tests__/DeckStack.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/DeckStack.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { DeckStack } from '../DeckStack';

describe('DeckStack', () => {
  const items = [
    { id: '1', entityType: 'game' as const, title: 'Catan' },
    { id: '2', entityType: 'game' as const, title: 'Wingspan' },
    { id: '3', entityType: 'game' as const, title: 'Spirit Island' },
  ];

  it('renders items when open', () => {
    render(<DeckStack isOpen items={items} onItemClick={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Spirit Island')).toBeInTheDocument();
  });

  it('renders nothing when closed', () => {
    const { container } = render(
      <DeckStack isOpen={false} items={items} onItemClick={vi.fn()} onClose={vi.fn()} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('shows "View all N" when more than 5 items', () => {
    const manyItems = Array.from({ length: 8 }, (_, i) => ({
      id: String(i), entityType: 'game' as const, title: `Game ${i}`,
    }));
    render(<DeckStack isOpen items={manyItems} onItemClick={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByText(/View all 8/)).toBeInTheDocument();
  });

  it('calls onClose when clicking outside', () => {
    const onClose = vi.fn();
    render(<DeckStack isOpen items={items} onItemClick={vi.fn()} onClose={onClose} />);
    // Click the backdrop
    fireEvent.click(screen.getByTestId('deck-stack-backdrop'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onClose on Escape key', () => {
    const onClose = vi.fn();
    render(<DeckStack isOpen items={items} onItemClick={vi.fn()} onClose={onClose} />);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test, verify fail**
- [ ] **Step 3: Implement DeckStack** — renders via React Portal, fan layout with staggered animation (300ms cubic-bezier), max 5 visible + "View all N", backdrop click and Escape to close, hover lift on cards
- [ ] **Step 4: Run test, verify pass**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(mana): add DeckStack component with portal and fan animation"
```

---

### Task 4.4: Wire ManaLinkFooter clicks to DeckStack

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`

- [ ] **Step 1: Add DeckStack state management to MeepleCardGrid**

When `onManaPipClick` is provided on MeepleCardProps, pass it to `ManaLinkFooter`. The parent page/component manages the DeckStack state. Add `DeckStackContext.Provider` wrapping the card grid in consuming pages.

- [ ] **Step 2: Run existing card tests to verify no regression**
- [ ] **Step 3: Commit**

```bash
git commit -m "feat(mana): wire ManaLinkFooter to DeckStack in MeepleCardGrid"
```

---

## Chunk 5: Phase 5 — Drawer Evolution

### Task 5.1: Create useEntityDetail generic hook

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/hooks/use-entity-detail.ts`
- Test: `apps/web/src/components/ui/data-display/extra-meeple-card/hooks/__tests__/use-entity-detail.test.ts`

- [ ] **Step 1: Write failing test**

```typescript
// __tests__/use-entity-detail.test.ts
import { renderHook } from '@testing-library/react';
import { useEntityDetail } from '../use-entity-detail';

describe('useEntityDetail', () => {
  it('returns null data for unimplemented entity types', () => {
    const { result } = renderHook(() => useEntityDetail('collection', 'test-id'));
    expect(result.current.data).toBeNull();
    expect(result.current.loading).toBe(false);
    expect(result.current.error).toBeNull();
  });
});
```

- [ ] **Step 2: Run test, verify fail**
- [ ] **Step 3: Implement useEntityDetail** — dispatcher that routes to `useGameDetail`, `useAgentDetail`, `useChatDetail`, `useKbDetail` for existing types, returns `{ data: null, loading: false, error: null }` for new types
- [ ] **Step 4: Run test, verify pass**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(mana): add generic useEntityDetail dispatcher hook"
```

---

### Task 5.2: Extend DrawerEntityType and add dynamic tab generation

**Files:**
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/constants.ts`

- [ ] **Step 1: Extend DrawerEntityType to support all 16 entity types**

In `ExtraMeepleCardDrawer.tsx`:
```typescript
export type DrawerEntityType = MeepleEntityType; // was: 'game' | 'agent' | 'chat' | 'kb' | 'links'
```

Update `DRAWER_ENTITY_TYPE_MAP` in constants.ts to include all 16 mappings.

- [ ] **Step 2: Add dynamic tab generation based on entity relationships**

In the drawer component, generate tabs from `ENTITY_RELATIONSHIPS[entityType]`:
- First tab: Overview (always present, uses entity's own mana symbol)
- Subsequent tabs: One per linked entity type that has actual data

- [ ] **Step 3: Add "Coming soon" state for unimplemented entity drawer content**

For entity types without dedicated content components, render `DrawerComingSoon` from `drawer-states.tsx`.

- [ ] **Step 4: Run existing drawer tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/extra-meeple-card/__tests__/`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(mana): extend drawer to support all 16 entity types with dynamic tabs"
```

---

## Chunk 6: Phase 6 — New Entity Type Stubs

### Task 6.1: Add stub configurations for 6 new entity types

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/navigation-icons.tsx`

- [ ] **Step 1: Add navigation icon mappings for new entity types**

Add icon components (using Lucide icons) for: `collection`, `group`, `location`, `expansion`, `achievement`, `note`. Update the `ENTITY_NAV_ICONS` map.

- [ ] **Step 2: Run typecheck to verify all entity type switch/case branches are covered**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS — no exhaustive check errors

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(mana): add navigation icons and stubs for 6 new entity types"
```

---

### Task 6.2: Update entity-link system for new entity types

**Files:**
- Modify: `apps/web/src/components/ui/data-display/entity-link/entity-link-types.ts`

- [ ] **Step 1: Add new entity types to LINK_ENTITY_CONFIG**

Add entries for: `Collection`, `Group`, `Location`, `Expansion`, `Achievement`, `Note` with matching colors from the mana system.

- [ ] **Step 2: Run typecheck**
- [ ] **Step 3: Commit**

```bash
git commit -m "feat(mana): extend entity link system for 6 new entity types"
```

---

### Task 6.3: Final integration test and cleanup

**Files:**
- Create: `apps/web/src/components/ui/data-display/__tests__/mana-integration.test.tsx`

- [ ] **Step 1: Write integration test**

```typescript
// __tests__/mana-integration.test.tsx
import { render, screen } from '@testing-library/react';
import { MeepleCard } from '../meeple-card';

describe('MeepleCard Mana Integration', () => {
  const ALL_ENTITY_TYPES = [
    'game', 'session', 'player', 'event',
    'collection', 'group', 'location', 'expansion',
    'agent', 'kb', 'chatSession', 'note',
    'toolkit', 'tool', 'achievement', 'custom',
  ] as const;

  ALL_ENTITY_TYPES.forEach(entity => {
    it(`renders ${entity} card without errors`, () => {
      expect(() =>
        render(<MeepleCard entity={entity} title={`Test ${entity}`} />)
      ).not.toThrow();
    });
  });

  it('renders mana badge for game card', () => {
    render(<MeepleCard entity="game" title="Catan" />);
    expect(screen.getByText('Game')).toBeInTheDocument();
  });

  it('renders mana link footer when linkedEntities provided', () => {
    render(
      <MeepleCard
        entity="game"
        title="Catan"
        linkedEntities={[{ entityType: 'session', count: 3 }]}
        onManaPipClick={vi.fn()}
      />
    );
    expect(screen.getByTestId('mana-pip-session')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run integration test**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/mana-integration.test.tsx`
Expected: PASS

- [ ] **Step 3: Run full test suite**

Run: `cd apps/web && pnpm test`
Expected: All tests PASS

- [ ] **Step 4: Run typecheck and lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git commit -m "test(mana): add integration tests for all 16 entity types"
```

- [ ] **Step 6: Clean up temporary files**

Remove `docs/frontend/meeple-card-anatomy.py` (generator script, not needed in repo).

```bash
git rm docs/frontend/meeple-card-anatomy.py
git commit -m "chore: remove temporary anatomy generator script"
```

---

## Summary

| Chunk | Phase | Tasks | Estimated Commits |
|-------|-------|-------|-------------------|
| 1 | Mana Symbol System | 3 tasks (types, config, component) | 3 |
| 2 | Card Front Redesign | 5 tasks (badge, glow, footer, actions, integration) | 5 |
| 3 | Modular Back Blocks | 5 tasks (types, registry, blocks, composer) | 5 |
| 4 | Deck Stack | 4 tasks (types, card, stack, wiring) | 4 |
| 5 | Drawer Evolution | 2 tasks (hook, drawer extension) | 2 |
| 6 | New Entity Stubs | 3 tasks (icons, links, integration test) | 4 |
| **Total** | | **22 tasks** | **23 commits** |
