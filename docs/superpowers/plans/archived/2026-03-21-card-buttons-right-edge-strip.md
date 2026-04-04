# Card Action Buttons — Right Edge Strip — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move card hover action buttons (info, wishlist, quick actions) from top-right to a vertically-centered right-edge strip inside the cover, with staggered slide-in animation.

**Architecture:** Register Tailwind animation → add `actionStrip` prop to `CardCover` → extract `CardActionStrip` from `CardActions` → wire variants to pass strip through `CardCover` → reposition `PrimaryActions`.

**Tech Stack:** React 19, TypeScript, Tailwind CSS 4, Vitest

**Spec:** `docs/superpowers/specs/2026-03-21-card-buttons-right-edge-strip-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/web/tailwind.config.js` | Modify | Add `mc-slide-in-right` keyframe + animation |
| `apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx` | Modify | Add `actionStrip` prop, render inside relative wrapper |
| `apps/web/src/components/ui/data-display/meeple-card/parts/CardActions.tsx` | Modify | Extract `CardActionStrip` with vertical layout + stagger; keep `ActionButtons` for featured/hero |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` | Modify | Build strip JSX, pass as `actionStrip` to `CardCover`, reposition `PrimaryActions` |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx` | Modify | Build strip JSX, pass as `actionStrip` to `CardCover` |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx` | Modify | Build strip JSX, pass as `actionStrip` to `CardCover` |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardExpanded.tsx` | Modify | Build strip JSX, pass to cover wrapper |

---

## Task 1: Register Tailwind Animation

**Files:**
- Modify: `apps/web/tailwind.config.js`

- [ ] **Step 1: Add keyframe and animation entry**

Open `apps/web/tailwind.config.js`. In `theme.extend.keyframes` (after `mc-float-up` block around line 91), add:

```js
'mc-slide-in-right': {
  from: { opacity: '0', transform: 'translateX(20px)' },
  to: { opacity: '1', transform: 'translateX(0)' },
},
```

In `theme.extend.animation` (after `mc-float-up` line around line 35), add:

```js
'mc-slide-in-right': 'mc-slide-in-right 200ms cubic-bezier(0.4,0,0.2,1) both',
```

- [ ] **Step 2: Verify build**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/tailwind.config.js
git commit -m "feat(meeple-card): add mc-slide-in-right animation keyframe"
```

---

## Task 2: Add `actionStrip` Prop to CardCover

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx`

- [ ] **Step 1: Add prop and render slot**

Add to `CardCoverProps` interface:

```tsx
/** Action strip rendered inside cover's relative wrapper (cover-relative positioning) */
actionStrip?: React.ReactNode;
```

Add `actionStrip` to the destructured props. Render it inside the `<div className="relative">` wrapper, after `CoverOverlay`:

```tsx
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
    {actionStrip}
  </div>
);
```

- [ ] **Step 2: Typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors (prop is optional)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx
git commit -m "feat(meeple-card): add actionStrip prop to CardCover"
```

---

## Task 3: Extract CardActionStrip from CardActions

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/CardActions.tsx`

- [ ] **Step 1: Create `CardActionStrip` export**

Add a new exported component `CardActionStrip` in the same file. This renders the vertical strip with stagger animation. The existing `CardActions` component keeps only the `ActionButtons` for featured/hero.

Replace the entire file content with:

```tsx
'use client';

/**
 * CardActions - Action strip + featured/hero action buttons
 *
 * CardActionStrip: vertical right-edge strip with stagger slide-in (rendered inside CardCover)
 * CardActions: featured/hero ActionButtons (rendered in content area)
 *
 * @module components/ui/data-display/meeple-card/parts/CardActions
 */

import React from 'react';

import { cn } from '@/lib/utils';

import { QuickActionsMenu } from '../../meeple-card-features/QuickActionsMenu';
import { WishlistButton } from '../../meeple-card-features/WishlistButton';
import { MeepleCardInfoButton } from '../../meeple-card-info-button';
import { ActionButtons } from '../../meeple-card-parts';
import { MeepleCardQuickActions } from '../../meeple-card-quick-actions';

import type { DrawerEntityType } from '../../extra-meeple-card/ExtraMeepleCardDrawer';
import type {
  MeepleCardProps,
  MeepleCardAction,
  MeepleCardVariant,
  MeepleEntityType,
  QuickAction,
} from '../types';

// ============================================================================
// CardActionStrip — vertical right-edge strip inside cover
// ============================================================================

export interface CardActionStripProps {
  entity: MeepleEntityType;
  customColor?: string;
  entityQuickActions?: QuickAction[];
  quickActions?: MeepleCardProps['quickActions'];
  userRole?: MeepleCardProps['userRole'];
  showWishlistBtn: boolean;
  isWishlisted?: boolean;
  onWishlistToggle?: (id: string, isWishlisted: boolean) => void;
  showInfoButton?: boolean;
  entityId?: string;
  infoHref?: string;
  infoTooltip?: string;
  drawerEntityType?: DrawerEntityType;
  onDrawerOpen?: () => void;
  testId?: string;
  hasQuickActions: boolean;
}

/**
 * Vertical action strip rendered inside CardCover's relative wrapper.
 * Positioned right-edge, vertically centered in safe zone (top/bottom 40px clearance).
 * Each button slides in from right with stagger delay.
 */
export function CardActionStrip({
  entity,
  customColor,
  entityQuickActions,
  quickActions,
  userRole,
  showWishlistBtn,
  isWishlisted,
  onWishlistToggle,
  showInfoButton,
  entityId,
  infoHref,
  infoTooltip,
  drawerEntityType,
  onDrawerOpen,
  testId,
  hasQuickActions,
}: CardActionStripProps) {
  // Collect action items into an array for stagger indexing
  const items: React.ReactNode[] = [];

  if (entityQuickActions && entityQuickActions.length > 0) {
    items.push(
      <MeepleCardQuickActions
        key="entity-qa"
        actions={entityQuickActions}
        entityType={entity}
        customColor={customColor}
        size="sm"
      />
    );
  }

  if (hasQuickActions && !entityQuickActions && quickActions) {
    items.push(
      <QuickActionsMenu key="legacy-qa" actions={quickActions} userRole={userRole} size="sm" />
    );
  }

  if (showWishlistBtn && !entityQuickActions && onWishlistToggle) {
    items.push(
      <WishlistButton
        key="wishlist"
        gameId={testId || 'card'}
        isWishlisted={!!isWishlisted}
        onToggle={onWishlistToggle}
        size="sm"
      />
    );
  }

  if (showInfoButton && entityId && drawerEntityType) {
    items.push(
      <MeepleCardInfoButton
        key="info-drawer"
        onClick={onDrawerOpen}
        entityType={entity}
        customColor={customColor}
        tooltip={infoTooltip}
        size="sm"
      />
    );
  } else if (showInfoButton && infoHref && !entityId) {
    items.push(
      <MeepleCardInfoButton
        key="info-link"
        href={infoHref}
        entityType={entity}
        customColor={customColor}
        tooltip={infoTooltip}
        size="sm"
      />
    );
  }

  if (items.length === 0) return null;

  return (
    <div
      className={cn(
        'absolute right-1.5 top-10 bottom-10',
        'flex flex-col items-center justify-center gap-1.5',
        'z-15 pointer-events-none',
        'hidden md:flex'
      )}
      data-testid="card-action-strip"
    >
      {items.map((item, index) => (
        <div
          key={index}
          className={cn(
            'pointer-events-auto',
            'opacity-0 group-hover:animate-mc-slide-in-right'
          )}
          style={{ animationDelay: `${index * 50}ms` }}
        >
          {item}
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// CardActions — featured/hero ActionButtons (content area)
// ============================================================================

export interface CardActionsProps {
  variant: MeepleCardVariant;
  entity: MeepleEntityType;
  customColor?: string;
  actions: MeepleCardAction[];
}

/**
 * Renders featured/hero action buttons in the content area.
 * The hover strip is now handled by CardActionStrip inside CardCover.
 */
export function CardActions({
  variant,
  entity,
  customColor,
  actions,
}: CardActionsProps) {
  const showActions = actions.length > 0 && (variant === 'featured' || variant === 'hero');
  if (!showActions) return null;

  return <ActionButtons actions={actions} entity={entity} customColor={customColor} />;
}
```

- [ ] **Step 2: Update parts/index.ts exports**

In `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts`, add:

```tsx
export { CardActionStrip } from './CardActions';
export type { CardActionStripProps } from './CardActions';
```

- [ ] **Step 3: Typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: Errors in variant files (they still pass old props to `CardActions`) — expected, fixed in next tasks.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/CardActions.tsx apps/web/src/components/ui/data-display/meeple-card/parts/index.ts
git commit -m "feat(meeple-card): extract CardActionStrip with right-edge stagger animation"
```

---

## Task 4: Wire MeepleCardGrid

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`

- [ ] **Step 1: Import CardActionStrip**

Add import:

```tsx
import { CardActionStrip } from '../parts/CardActions';
```

- [ ] **Step 2: Build strip JSX and pass to CardCover**

Build a `stripElement` before the return, using the same props currently passed to `CardActions`:

```tsx
const hasStripActions =
  !!(entityQuickActions) ||
  !!(showInfoButton && (entityId || infoHref)) ||
  showWishlistBtn ||
  hasQuickActions;

const stripElement = hasStripActions ? (
  <CardActionStrip
    entity={entity}
    customColor={customColor}
    entityQuickActions={entityQuickActions}
    quickActions={quickActions}
    userRole={userRole}
    showWishlistBtn={showWishlistBtn}
    isWishlisted={isWishlisted}
    onWishlistToggle={onWishlistToggle}
    showInfoButton={showInfoButton}
    entityId={entityId}
    infoHref={infoHref}
    infoTooltip={infoTooltip}
    drawerEntityType={drawerEntityType}
    onDrawerOpen={() => setDrawerOpen(true)}
    testId={testId}
    hasQuickActions={hasQuickActions}
  />
) : null;
```

Pass to `CardCover`:

```tsx
<CardCover
  src={coverSrc}
  alt={title}
  variant={variant}
  entity={entity}
  customColor={customColor}
  coverLabels={coverLabels}
  showEntityType
  subtypeIcons={subtypeIcons}
  mechanicIcon={mechanicIcon}
  stateLabel={stateLabel}
  actionStrip={stripElement}
/>
```

- [ ] **Step 3: Simplify CardActions call**

Replace the existing `CardActions` call (which passes ~15 props) with the simplified version that only handles featured/hero buttons:

```tsx
<CardActions
  variant={variant}
  entity={entity}
  customColor={customColor}
  actions={actions}
/>
```

- [ ] **Step 4: Reposition PrimaryActions**

Move `PrimaryActions` from `absolute top-10 right-2.5 z-[3]` to the content area (below title). Change the className from absolute positioning to relative:

```tsx
{primaryActions && primaryActions.length > 0 && (
  <PrimaryActions
    actions={primaryActions}
    className="mt-1 opacity-0 group-hover:opacity-100 transition-opacity"
  />
)}
```

Place this after the subtitle block inside the content area.

- [ ] **Step 5: Typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: May still have errors in other variants — ok.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git commit -m "feat(meeple-card): wire CardActionStrip into grid variant via CardCover"
```

---

## Task 5: Wire Featured, Hero, Expanded Variants

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardExpanded.tsx`

- [ ] **Step 1: Update MeepleCardFeatured**

Same pattern as Grid: import `CardActionStrip`, build `stripElement`, pass as `actionStrip` to `CardCover`, simplify `CardActions` call to only `{ variant, entity, customColor, actions }`.

- [ ] **Step 2: Update MeepleCardHero**

Same pattern as Featured.

- [ ] **Step 3: Update MeepleCardExpanded**

`Expanded` has its own cover wrapper (not `CardCover`). Add `actionStrip` rendering after the `CoverOverlay` inside the cover `<div>`. Build `stripElement` using `CardActionStrip` with the available props.

- [ ] **Step 4: Update MeepleCardList — no strip**

`MeepleCardList` does NOT get a strip (thumbnail too small). Simplify its `CardActions` call to `{ variant, entity, customColor, actions }`. No `actionStrip` prop to `CardCover`.

- [ ] **Step 5: Typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/
git commit -m "feat(meeple-card): wire CardActionStrip into featured/hero/expanded variants"
```

---

## Task 6: Final Verification

- [ ] **Step 1: TypeScript check**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 2: Lint**

Run: `cd apps/web && pnpm lint`
Expected: No new errors

- [ ] **Step 3: Build**

Run: `cd apps/web && pnpm build`
Expected: No errors

- [ ] **Step 4: Final commit if any remaining changes**

```bash
git status
# Only commit if there are actual remaining changes
```
