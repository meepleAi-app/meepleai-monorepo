# MeepleCard Cover 4-Corner Overlay — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign MeepleCard cover overlay from 2-slot bottom-only to 4-corner layout with label stack, entity type badge, subtype icons, and state badge.

**Architecture:** New `CoverOverlay` component with 4 absolute-positioned corner slots replaces the current flex bottom-bar. `ManaBadge` moves from card-level into the cover overlay top-right. Backward compatible — existing `mechanicIcon`/`stateLabel` props still work.

**Tech Stack:** React 19 / TypeScript / Tailwind CSS / Vitest

**Spec:** `docs/superpowers/specs/2026-03-21-meeple-card-cover-4corner-design.md`

---

## Task 1: Types — CoverLabel and SubtypeIcon Interfaces

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Test: `apps/web/src/components/ui/data-display/meeple-card/__tests__/cover-types.test.ts`

- [ ] **Step 1: Write type-check test**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/__tests__/cover-types.test.ts
import { describe, it, expect } from 'vitest';
import type { CoverLabel, SubtypeIcon } from '../types';

describe('Cover overlay types', () => {
  it('CoverLabel accepts minimal props', () => {
    const label: CoverLabel = { text: 'Catan' };
    expect(label.text).toBe('Catan');
    expect(label.color).toBeUndefined();
    expect(label.primary).toBeUndefined();
  });

  it('CoverLabel accepts full props', () => {
    const label: CoverLabel = { text: 'KB indexed', color: '174 60% 40%', primary: false };
    expect(label.color).toBe('174 60% 40%');
  });

  it('SubtypeIcon accepts props', () => {
    const icon: SubtypeIcon = { icon: '⚔️', tooltip: 'Strategy' };
    expect(icon.tooltip).toBe('Strategy');
  });
});
```

- [ ] **Step 2: Run — verify fail**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/cover-types.test.ts`
Expected: FAIL — types not found

- [ ] **Step 3: Add types to types.ts**

In `apps/web/src/components/ui/data-display/meeple-card/types.ts`, add:

```typescript
/** Label displayed in the cover overlay top-left slot */
export interface CoverLabel {
  /** Label text */
  text: string;
  /** HSL color string (defaults to entity color) */
  color?: string;
  /** Primary label uses larger font (default false) */
  primary?: boolean;
}

/** Subtype icon displayed in the cover overlay bottom-left slot */
export interface SubtypeIcon {
  /** Icon content (emoji or React node) */
  icon: React.ReactNode;
  /** Tooltip text on hover */
  tooltip: string;
}
```

Also add to `MeepleCardProps` interface:

```typescript
/** Labels stack in cover overlay top-left */
coverLabels?: CoverLabel[];
/** Subtype icons in cover overlay bottom-left */
subtypeIcons?: SubtypeIcon[];
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/cover-types.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/__tests__/cover-types.test.ts
git commit -m "feat(meeple-card): add CoverLabel and SubtypeIcon types"
```

---

## Task 2: CoverOverlay Component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/__tests__/CoverOverlay.test.tsx`

- [ ] **Step 1: Write tests for 4-corner rendering**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/__tests__/CoverOverlay.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { CoverOverlay } from '../parts/CoverOverlay';

describe('CoverOverlay', () => {
  it('renders nothing when no props provided', () => {
    const { container } = render(<CoverOverlay entity="game" />);
    expect(container.querySelector('[data-testid="cover-overlay"]')).toBeNull();
  });

  it('renders top-left label when coverLabels provided', () => {
    render(<CoverOverlay entity="game" coverLabels={[{ text: 'Catan', primary: true }]} />);
    expect(screen.getByTestId('cover-label-0')).toHaveTextContent('Catan');
  });

  it('renders multiple labels stacked', () => {
    render(
      <CoverOverlay
        entity="game"
        coverLabels={[
          { text: 'Catan', primary: true },
          { text: 'KB indexed', color: '174 60% 40%' },
        ]}
      />
    );
    expect(screen.getByTestId('cover-label-0')).toHaveTextContent('Catan');
    expect(screen.getByTestId('cover-label-1')).toHaveTextContent('KB indexed');
  });

  it('renders top-right entity type badge with mana symbol', () => {
    render(<CoverOverlay entity="game" showEntityType />);
    expect(screen.getByTestId('cover-entity-type')).toBeInTheDocument();
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('renders bottom-left subtype icons', () => {
    render(
      <CoverOverlay
        entity="game"
        subtypeIcons={[
          { icon: '⚔️', tooltip: 'Strategy' },
          { icon: '💎', tooltip: 'Resources' },
        ]}
      />
    );
    expect(screen.getByTitle('Strategy')).toBeInTheDocument();
    expect(screen.getByTitle('Resources')).toBeInTheDocument();
  });

  it('renders bottom-right state label', () => {
    render(<CoverOverlay entity="game" stateLabel={{ text: 'Owned', variant: 'success' }} />);
    expect(screen.getByTestId('cover-state-label')).toHaveTextContent('Owned');
  });

  it('backward compat: mechanicIcon maps to subtypeIcons[0]', () => {
    render(<CoverOverlay entity="game" mechanicIcon={<span data-testid="mech">⚔️</span>} />);
    expect(screen.getByTestId('mech')).toBeInTheDocument();
  });

  it('renders only provided slots', () => {
    render(<CoverOverlay entity="kb" stateLabel={{ text: 'Indexed', variant: 'success' }} />);
    expect(screen.getByTestId('cover-state-label')).toBeInTheDocument();
    expect(screen.queryByTestId('cover-labels')).toBeNull();
    expect(screen.queryByTestId('cover-subtypes')).toBeNull();
  });
});
```

- [ ] **Step 2: Run — verify fail**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/CoverOverlay.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement CoverOverlay**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx
'use client';

import { memo } from 'react';

import { cn } from '@/lib/utils';

import { getManaDisplayName } from '../../mana/mana-config';
import { ManaSymbol } from '../../mana/ManaSymbol';
import { entityColors, type MeepleEntityType } from '../../meeple-card-styles';
import type { CoverLabel, SubtypeIcon } from '../types';

export interface CoverOverlayProps {
  entity: MeepleEntityType;
  customColor?: string;
  /** Top-left: label stack */
  coverLabels?: CoverLabel[];
  /** Top-right: show entity type badge with mana symbol */
  showEntityType?: boolean;
  /** Bottom-left: subtype classification icons */
  subtypeIcons?: SubtypeIcon[];
  /** Bottom-left: legacy single icon (backward compat, maps to subtypeIcons[0]) */
  mechanicIcon?: React.ReactNode;
  /** Bottom-right: state badge */
  stateLabel?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' };
}

const STATE_COLORS = {
  success: 'bg-emerald-500/80 text-white',
  warning: 'bg-amber-500/80 text-black',
  error: 'bg-red-500/80 text-white',
  info: 'bg-blue-500/80 text-white',
} as const;

export const CoverOverlay = memo(function CoverOverlay({
  entity,
  customColor,
  coverLabels,
  showEntityType,
  subtypeIcons,
  mechanicIcon,
  stateLabel,
}: CoverOverlayProps) {
  const color = customColor ?? entityColors[entity].hsl;

  // Backward compat: mechanicIcon → subtypeIcons[0]
  const effectiveSubtypes = subtypeIcons ?? (mechanicIcon ? [{ icon: mechanicIcon, tooltip: '' }] : undefined);

  const hasContent = coverLabels?.length || showEntityType || effectiveSubtypes?.length || stateLabel;
  if (!hasContent) return null;

  return (
    <div
      className="absolute inset-0 z-[3] pointer-events-none"
      data-testid="cover-overlay"
    >
      {/* Top-Left: Label Stack */}
      {coverLabels && coverLabels.length > 0 && (
        <div
          className="pointer-events-auto absolute top-2 left-2 flex flex-col gap-1 max-w-[155px]"
          data-testid="cover-labels"
        >
          {coverLabels.map((label, i) => (
            <span
              key={i}
              data-testid={`cover-label-${i}`}
              className={cn(
                'inline-flex items-center gap-1 rounded-[5px]',
                'backdrop-blur-[8px] font-quicksand font-bold text-white',
                'text-shadow-sm overflow-hidden text-ellipsis whitespace-nowrap w-fit',
                label.primary ? 'text-[11px] px-[9px] py-[3px]' : 'text-[9px] px-2 py-[2px] opacity-90'
              )}
              style={{ backgroundColor: `hsl(${label.color ?? color} / ${label.primary ? 0.85 : 0.75})` }}
            >
              {label.text}
            </span>
          ))}
        </div>
      )}

      {/* Top-Right: Entity Type Badge */}
      {showEntityType && (
        <div
          className={cn(
            'pointer-events-auto absolute top-2 right-2',
            'inline-flex items-center gap-1 px-2 py-[3px] rounded-md',
            'backdrop-blur-[8px] bg-black/40',
            'font-quicksand font-bold text-[9px] uppercase tracking-wider text-white'
          )}
          data-testid="cover-entity-type"
        >
          <ManaSymbol entity={entity} size="mini" data-testid={`mana-symbol-${entity}`} />
          {getManaDisplayName(entity)}
        </div>
      )}

      {/* Bottom-Left: Subtype Icons */}
      {effectiveSubtypes && effectiveSubtypes.length > 0 && (
        <div
          className="pointer-events-auto absolute bottom-2 left-2 flex gap-1"
          data-testid="cover-subtypes"
        >
          {effectiveSubtypes.map((sub, i) => (
            <span
              key={i}
              title={sub.tooltip}
              className={cn(
                'flex items-center justify-center',
                'w-6 h-6 rounded-md',
                'backdrop-blur-[8px] bg-black/45',
                'border border-white/[0.12]',
                'text-xs cursor-pointer',
                'transition-transform duration-150 hover:scale-[1.15] hover:bg-black/60'
              )}
            >
              {sub.icon}
            </span>
          ))}
        </div>
      )}

      {/* Bottom-Right: State Label */}
      {stateLabel && (
        <span
          className={cn(
            'pointer-events-auto absolute bottom-2 right-2',
            'px-[9px] py-[3px] rounded-md',
            'backdrop-blur-[8px]',
            'font-quicksand font-bold text-[9px] uppercase tracking-wider',
            STATE_COLORS[stateLabel.variant]
          )}
          data-testid="cover-state-label"
        >
          {stateLabel.text}
        </span>
      )}
    </div>
  );
});
```

- [ ] **Step 4: Export from parts/index.ts**

In `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts`, add:

```typescript
export { CoverOverlay } from './CoverOverlay';
export type { CoverOverlayProps } from './CoverOverlay';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/CoverOverlay.test.tsx`
Expected: PASS (8/8)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx apps/web/src/components/ui/data-display/meeple-card/parts/index.ts apps/web/src/components/ui/data-display/meeple-card/__tests__/CoverOverlay.test.tsx
git commit -m "feat(meeple-card): add CoverOverlay component with 4-corner slot system"
```

---

## Task 3: Integrate CoverOverlay into CardCover

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/__tests__/CardCover.test.tsx`

- [ ] **Step 1: Write test for CardCover with new overlay**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/__tests__/CardCover.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { CardCover } from '../parts/CardCover';

describe('CardCover with CoverOverlay', () => {
  it('renders CoverOverlay when coverLabels provided', () => {
    render(
      <CardCover
        alt="Test"
        variant="grid"
        entity="game"
        coverLabels={[{ text: 'Catan', primary: true }]}
      />
    );
    expect(screen.getByTestId('cover-overlay')).toBeInTheDocument();
    expect(screen.getByTestId('cover-label-0')).toHaveTextContent('Catan');
  });

  it('backward compat: mechanicIcon still renders', () => {
    render(
      <CardCover
        alt="Test"
        variant="grid"
        entity="game"
        mechanicIcon={<span data-testid="old-icon">⚔️</span>}
      />
    );
    expect(screen.getByTestId('old-icon')).toBeInTheDocument();
  });

  it('compact variant returns null', () => {
    const { container } = render(
      <CardCover alt="Test" variant="compact" entity="game" />
    );
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Run — verify fail**

- [ ] **Step 3: Update CardCover to use CoverOverlay**

Replace the overlay section in `CardCover.tsx`:

```tsx
import { CoverOverlay } from './CoverOverlay';
import type { CoverLabel, SubtypeIcon } from '../types';

export interface CardCoverProps {
  src?: string;
  alt: string;
  variant: MeepleCardVariant;
  entity: MeepleEntityType;
  customColor?: string;
  showShimmer?: boolean;
  className?: string;
  // Legacy (backward compat)
  mechanicIcon?: React.ReactNode;
  stateLabel?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' };
  // NEW — 4-corner system
  coverLabels?: CoverLabel[];
  subtypeIcons?: SubtypeIcon[];
  showEntityType?: boolean;
}

export function CardCover({
  src, alt, variant, entity, customColor,
  className: _className,
  mechanicIcon, stateLabel,
  coverLabels, subtypeIcons, showEntityType,
}: CardCoverProps) {
  if (variant === 'compact') return null;

  return (
    <div className="relative">
      <CoverImage src={src} alt={alt} variant={variant} entity={entity} customColor={customColor} />
      <CoverOverlay
        entity={entity}
        customColor={customColor}
        coverLabels={coverLabels}
        showEntityType={showEntityType}
        subtypeIcons={subtypeIcons}
        mechanicIcon={mechanicIcon}
        stateLabel={stateLabel}
      />
    </div>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/CardCover.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx apps/web/src/components/ui/data-display/meeple-card/__tests__/CardCover.test.tsx
git commit -m "feat(meeple-card): integrate CoverOverlay into CardCover, backward compat"
```

---

## Task 4: Update MeepleCardGrid — Remove External ManaBadge

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid-overlay.test.tsx`

- [ ] **Step 1: Write test for new overlay in grid variant**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid-overlay.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MeepleCardGrid } from '../variants/MeepleCardGrid';

// Stub window.matchMedia for jsdom
beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false, media: query, onchange: null,
      addListener: () => {}, removeListener: () => {},
      addEventListener: () => {}, removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  });
});

describe('MeepleCardGrid overlay migration', () => {
  it('renders entity type in cover overlay instead of external ManaBadge', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Catan"
        actions={[]}
      />
    );
    // Entity type should be inside cover overlay, not as external ManaBadge
    expect(screen.getByTestId('cover-entity-type')).toBeInTheDocument();
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('forwards coverLabels to CardCover', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Catan"
        actions={[]}
        coverLabels={[{ text: 'Catan', primary: true }]}
      />
    );
    expect(screen.getByTestId('cover-label-0')).toHaveTextContent('Catan');
  });

  it('forwards subtypeIcons to CardCover', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Catan"
        actions={[]}
        subtypeIcons={[{ icon: '⚔️', tooltip: 'Strategy' }]}
      />
    );
    expect(screen.getByTitle('Strategy')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run — verify fail**

- [ ] **Step 3: Modify MeepleCardGrid**

In `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`:

1. **Remove** the external `ManaBadge` line (currently line 279):
   ```tsx
   // DELETE THIS LINE:
   <ManaBadge entity={entity} className="absolute top-2 left-2.5 z-[2]" />
   ```

2. **Forward** new props to `CardCover`:
   ```tsx
   <CardCover
     src={coverSrc}
     alt={title}
     variant={variant}
     entity={entity}
     customColor={customColor}
     mechanicIcon={mechanicIcon}
     stateLabel={stateLabel}
     coverLabels={props.coverLabels}
     subtypeIcons={props.subtypeIcons}
     showEntityType  // Always show entity type in cover overlay
   />
   ```

3. **Destructure** new props from `props` at the top of the component:
   ```tsx
   const {
     // ... existing props ...
     coverLabels,
     subtypeIcons,
   } = props;
   ```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid-overlay.test.tsx`
Expected: PASS

- [ ] **Step 5: Run full MeepleCard test suite to check for regressions**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/`
Expected: All pass (ManaBadge removal may break some existing tests — fix any that assert `ManaBadge` presence by updating to assert `cover-entity-type` instead)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx apps/web/src/components/ui/data-display/meeple-card/__tests__/
git commit -m "feat(meeple-card): migrate ManaBadge into cover overlay for grid variant"
```

---

## Task 5: Update Other Variants (List, Featured, Hero)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx`

- [ ] **Step 1: Forward new props in MeepleCardList**

List variant has a small thumbnail (64x64). Forward `showEntityType` and `stateLabel` to `CardCover`. `coverLabels` and `subtypeIcons` are skipped for list variant (not enough space). Remove external `ManaBadge` if present (check if list variant renders it).

- [ ] **Step 2: Forward new props in MeepleCardFeatured**

Featured variant (16:9) has room for all 4 slots. Forward all new props to `CardCover`. Remove external `ManaBadge` if present.

- [ ] **Step 3: Forward new props in MeepleCardHero**

Hero variant (full bleed) has room for all 4 slots. Forward all new props. Remove external `ManaBadge` if present.

- [ ] **Step 4: Run full variant tests**

Run: `cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/
git commit -m "feat(meeple-card): forward 4-corner overlay props to list/featured/hero variants"
```

---

## Task 6: Deprecate ManaBadge + Update Exports

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/ManaBadge.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts`

- [ ] **Step 1: Add deprecation JSDoc to ManaBadge**

```tsx
/**
 * @deprecated Use CoverOverlay with showEntityType prop instead.
 * Kept for backward compatibility in non-card contexts.
 */
export const ManaBadge = memo(function ManaBadge({ entity, className }: ManaBadgeProps) {
  // ... existing code unchanged ...
});
```

- [ ] **Step 2: Re-export CoverOverlay types from card index**

In `apps/web/src/components/ui/data-display/meeple-card/index.ts`, ensure `CoverLabel` and `SubtypeIcon` are exported.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/ManaBadge.tsx apps/web/src/components/ui/data-display/meeple-card/index.ts apps/web/src/components/ui/data-display/meeple-card/parts/index.ts
git commit -m "chore(meeple-card): deprecate ManaBadge, export CoverOverlay types"
```

---

## Task 7: Update coverOverlayStyles in meeple-card-styles.ts

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

- [ ] **Step 1: Check current coverOverlayStyles usage**

Search for `coverOverlayStyles` references. If only `CardCover.tsx` and `ShelfCard.tsx` use it, update safely. `ShelfCard` uses the old bottom-bar style — it should keep working with the legacy path.

- [ ] **Step 2: Update coverOverlayStyles**

Add new 4-corner style constants alongside existing ones (don't delete old styles — ShelfCard may use them):

```typescript
export const coverOverlay4Corner = {
  container: 'absolute inset-0 z-[3] pointer-events-none',
  slot: 'pointer-events-auto absolute',
  topLeft: 'top-2 left-2',
  topRight: 'top-2 right-2',
  bottomLeft: 'bottom-2 left-2',
  bottomRight: 'bottom-2 right-2',
} as const;
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts
git commit -m "feat(meeple-card): add coverOverlay4Corner style constants"
```

---

## Task 8: Final Verification

- [ ] **Step 1: Run full frontend test suite**

Run: `cd apps/web && pnpm test`
Expected: All pass

- [ ] **Step 2: TypeScript check**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 3: Lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 4: Build**

Run: `cd apps/web && pnpm build`
Expected: No errors

- [ ] **Step 5: Final commit if any remaining changes**

```bash
git status
# Only commit if there are actual remaining changes
```
