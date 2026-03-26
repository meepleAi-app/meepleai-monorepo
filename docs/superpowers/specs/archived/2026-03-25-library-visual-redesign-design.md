# Library Page Visual Redesign — Design Spec

**Date**: 2026-03-25
**Status**: Approved
**Scope**: Library page UX, MeepleCard performance, color palette, icon system
**Branch**: `feature/library-visual-redesign` (from `main-dev`)

---

## 1. Overview

Visual quality and performance improvements to the Library page and MeepleCard component. Five incremental steps, each independently testable and deployable.

**Goals**:
- Fix MeepleCard graphical flicker caused by excessive CSS effects
- Improve icon legibility (metadata 14px→20px, mana pips 18px→24px)
- Replace Neon Holo palette with Hybrid Warm-Modern for board game identity
- Replace tab bar with compact dropdown for more vertical card space
- Integrate Meeple Mana icon set as React SVG components

**Non-Goals**:
- Backend API changes
- New features or business logic
- Responsive layout restructuring (existing breakpoints preserved)
- MeepleCard variant additions (grid/list/compact/featured/hero/expanded stay as-is)

---

## 2. Step 1 — Hybrid Warm-Modern Color Palette

### What Changes

Replace Neon Holo CSS variables in `design-tokens.css` with Hybrid Warm-Modern values.

**Important**: The `--nh-*` tokens currently have zero consumers in the codebase — no component references them directly. Step 1 establishes the token values; Step 2 introduces the first consumer (`bg-[var(--nh-bg-surface)]` on MeepleCard). Step 1 alone will have no visible effect on the running UI. Validation of the warm palette requires completing Step 2.

### Token Changes

| Token | Current (Neon Holo) | New (Hybrid Warm) |
|-------|--------------------|--------------------|
| `--nh-bg-base` | `#0e0c18` (violet) | `#14120e` (warm dark) |
| `--nh-bg-surface` | `#16131f` | `#1e1b16` |
| `--nh-bg-surface-end` | `#1a1628` | `#27231c` |
| `--nh-bg-elevated` | `#1e1a2a` | `#302b22` |
| `--nh-border-default` | `rgba(255,255,255,0.06)` | `rgba(200,160,100,0.08)` |
| `--nh-text-primary` | `#f0ece4` | `#f0ece4` (unchanged) |
| `--nh-text-secondary` | `#7a7484` | `#a09080` (warmer) |
| `--nh-text-muted` | `#555555` | `#6a5d4e` |

| Token | Current Light | New Light |
|-------|--------------|-----------|
| `--nh-bg-base-light` | `#faf9f7` | `#faf8f5` |
| `--nh-bg-surface-light` | `#ffffff` | `#fffcf8` |
| `--nh-bg-surface-end-light` | `#f8f7f5` | `#f5f0e8` |
| `--nh-bg-elevated-light` | `#ffffff` | `#fffcf8` |
| `--nh-border-default-light` | `rgba(0,0,0,0.08)` | `rgba(160,120,60,0.08)` |
| `--nh-text-secondary-light` | `#6b6b6b` | `#5a4a35` |
| `--nh-text-muted-light` | `#999999` | `#8a7a65` |

### Warm Shadows (already exist, keep as-is)

The `--shadow-warm-*` tokens are already warm-toned (`rgba(180,130,80,...)`) and compatible with Hybrid Warm.

### Files Modified

- `apps/web/src/styles/design-tokens.css` — update `--nh-*` variables

### Validation

- Token values updated correctly (diff review)
- No syntax errors in CSS
- Build succeeds without warnings
- **Note**: Visual validation deferred to Step 2 when first consumers are introduced

---

## 3. Step 2 — Fix MeepleCard Flicker & Performance

### Root Causes

1. **HoloOverlay** — `<HoloOverlay />` rendered on every grid card. Applies `holo-slide` 4s infinite animation with gradient background-position shift + `hue-rotate` 6s infinite. Two competing infinite animations on every card.
2. **Glassmorphism `::before`** — `backdrop-filter: blur(12px) saturate(180%)` on `[data-variant="grid"]::before, [data-variant="featured"]::before` in globals.css. Forces GPU compositing layer per card.
3. **`willChange: 'transform, box-shadow, outline'`** — set statically on every grid card's inline style. Creates permanent compositing layers instead of letting browser optimize.
4. **Parchment texture SVG** — `--texture-parchment` applied as `background-image` via CVA on grid variant. SVG filter noise adds rendering cost.

### Fixes

| Issue | Fix | File |
|-------|-----|------|
| HoloOverlay always-on | Conditional render: only when `props.showHolo === true` (opt-in). Default: no holo. | `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` |
| Glassmorphism `::before` | Remove BOTH `[data-variant="grid"]::before` AND `[data-variant="featured"]::before` rules from globals.css. Also remove the matching `isolation: isolate` rule for both selectors. | `apps/web/src/styles/globals.css` |
| Static `willChange` | Remove `willChange` from inline style object (line ~254) | `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` |
| Parchment texture | Remove `[background-image:var(--texture-parchment)]` from CVA grid variant. Also remove from `featured` and `expanded` variants for consistency. Keep only on `hero` variant where the textured effect is intentional. | `apps/web/src/components/ui/data-display/meeple-card-styles.ts` |

### HoloOverlay Opt-in API

```tsx
// MeepleCardGrid.tsx — change from always-render to conditional
// IMPORTANT: destructure showHolo from props (add to destructuring block ~line 61-134)
const { showHolo = false, ...otherProps } = props;
// ...
{showHolo && <HoloOverlay />}
```

Add `showHolo?: boolean` to `MeepleCardProps` in `types.ts`. Default: `false`. Hero and featured variants can default to `true` if desired.

### Card Background Change

With glassmorphism and parchment removed, update the card background across ALL non-hero variants to use warm tokens:

```diff
// In meeple-card-styles.ts — grid, list, featured, expanded variants:
- 'bg-card border border-border/50'
+ 'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]'
```

Hero variant keeps its current styling (absolute positioning, overlay-based).

### Files Modified

- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` — conditional HoloOverlay (add `showHolo` to destructuring block), remove `willChange` from inline style
- `apps/web/src/components/ui/data-display/meeple-card/types.ts` — add `showHolo?: boolean`
- `apps/web/src/components/ui/data-display/meeple-card-styles.ts` — remove parchment from grid/featured/expanded CVA variants, update `bg-card` to `bg-[var(--nh-bg-surface)]` on grid/list/featured/expanded
- `apps/web/src/styles/globals.css` — remove `[data-variant="grid"]::before, [data-variant="featured"]::before` glassmorphism rule AND matching `isolation: isolate` rule

### Validation

- Scroll a grid of 20+ cards smoothly (no jank, no flicker)
- Cards hover lift still works (`translateY(-6px)`)
- No visual artifacts on hover/unhover transitions
- `showHolo={true}` on a featured card still renders holo effect
- Featured variant cards render correctly without glassmorphism
- Card backgrounds show warm `#1e1b16` in dark mode
- Performance: check Chrome DevTools Layers panel — should see fewer compositing layers

---

## 4. Step 3 — Resize Icons and Mana Pips

### Metadata Footer Icons

**Current**: `w-3.5 h-3.5` (14px), low opacity, matches `text-foreground/65`
**New**: `w-5 h-5` (20px), `opacity-[0.85]`, color `hsl(25,80%,55%)` (warm orange entity accent)

File: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` metadata footer section (~line 498-528)

```diff
- 'flex items-center gap-1.5 text-[0.7rem] font-semibold text-foreground/65 dark:text-foreground/60'
+ 'flex items-center gap-2 text-[0.78rem] font-semibold text-foreground/70 dark:text-[rgba(200,180,140,0.75)]'
```

Icon size in metadata items:
```diff
- {item.icon && <item.icon className="w-3.5 h-3.5" aria-hidden="true" />}
+ {item.icon && <item.icon className="w-5 h-5 text-[hsl(25,80%,55%)] opacity-[0.85]" aria-hidden="true" />}
```

**Note**: Use `opacity-[0.85]` (Tailwind arbitrary value syntax), not `opacity-85` which is not in the default Tailwind v4 scale.

### ManaSymbol Sizes

**Current mini size**: `w-5 h-5 text-xs` (20px)
**New mini size**: `w-6 h-6 text-sm` (24px)

Add a new `small` size (20px) for the top-right cover pip to prevent overflow:

File: `apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx`

```diff
const SIZE_CLASSES: Record<ManaSize, string> = {
  full: 'w-16 h-16 text-[1.6rem]',
  medium: 'w-7 h-7 text-sm',
+ small: 'w-5 h-5 text-xs',
  mini: 'w-6 h-6 text-sm',
};

const LABEL_CLASSES: Record<ManaSize, string> = {
  full: 'text-xs mt-2',
  medium: 'text-[9px] ml-1.5',
+ small: 'text-[8px] ml-1',
  mini: 'text-[8px] ml-1',
};
```

Also update `mana-types.ts` to add `'small'` to the `ManaSize` union type.

### Entity Mana Pip (Top-Right Corner)

The top-right entity pip in `CoverOverlay.tsx` currently uses `size="mini"`. With mini bumped to 24px, the pip would overflow the label container's `max-w-[calc(100%-50px)]`. Fix: use the new `small` size (20px) for the cover pip, and update the label container width guard:

File: `apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx`

```diff
// Top-Right: Entity Type Pip (line ~81)
- <ManaSymbol entity={entity} size="mini" ... />
+ <ManaSymbol entity={entity} size="small" ... />

// Top-Left: Label container max-width (line ~54)
- 'max-w-[calc(100%-50px)]'
+ 'max-w-[calc(100%-52px)]'
```

### CoverOverlay Subtype Icons

**Current**: `w-6 h-6` (24px), `bg-black/45`, `border border-white/[0.12]`
**New**: `w-7 h-7` (28px), `bg-[rgba(20,18,14,0.7)]`, `border border-[rgba(200,160,100,0.2)]`

File: `apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx` (~line 98-113)

```diff
- 'w-6 h-6 rounded-md'
- 'backdrop-blur-[8px] bg-black/45'
- 'border border-white/[0.12]'
+ 'w-7 h-7 rounded-md'
+ 'backdrop-blur-[8px] bg-[rgba(20,18,14,0.7)]'
+ 'border border-[rgba(200,160,100,0.2)]'
```

### ManaSymbol Glow Enhancement

Current: `boxShadow: 0 4px 16px hsl(color / 0.35)`
New: `boxShadow: 0 4px 14px hsl(color / 0.4)` + `outline: 2px solid` (was 2px, keep)

### Files Modified

- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` — metadata icon size and color
- `apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx` — add `small` size, bump `mini` to 24px
- `apps/web/src/components/ui/data-display/mana/mana-types.ts` — add `'small'` to ManaSize union
- `apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx` — subtype icon size 28px with warm border, entity pip uses `small` size, label max-width update

### Validation

- Metadata icons clearly visible at 20px with orange accent
- Mana pips in footer readable and vibrant at 24px (mini)
- Top-right entity pip at 20px (small) — proportional to cover, no label overlap
- Subtype icons in bottom-left 28px with warm border
- Mobile 320px: no overflow, icons proportional
- Existing ManaSymbol `full` and `medium` sizes unaffected

---

## 5. Step 4 — Replace Tab Bar with Dropdown

### Current State

Two separate filter systems exist:

1. **`LibraryContextBar.tsx`** — renders filter pills ("Tutti", "Posseduti", "Wishlist", "Prestati") in the MiniNav. Has its own **disconnected** `useState` — never reaches the data layer. This is the component to remove.

2. **`LibraryFilters`** (in `CollectionPageClient.tsx`) — the real, API-connected filter system using `GetUserLibraryParams` with `stateFilter: GameStateType[]`, `sortBy`, `sortDescending`, and `searchQuery`. This already works and must be preserved.

### Target State

Remove `LibraryContextBar` (the disconnected MiniNav tab bar). The existing `LibraryFilters` component and its API-connected filtering remain as-is. Add a compact `<Select>` dropdown in the page header as a quick-access shortcut that maps to the `LibraryFilters` state.

### Dropdown → API Filter Mapping

| Dropdown Label | `GameStateType[]` Filter Value |
|---------------|-------------------------------|
| Tutti | `[]` (empty = no filter) |
| Posseduti | `['Owned']` |
| Wishlist | `['Wishlist']` |
| Prestati | `['InPrestito']` |

The dropdown sets `stateFilter` on the existing `LibraryFilters` state, keeping the existing API integration intact.

### Implementation

**Remove**:
- `apps/web/src/components/library/LibraryContextBar.tsx` — delete file
- `apps/web/src/app/(authenticated)/library/layout.tsx` — remove `ContextBarRegistrar` wrapper and `LibraryContextBar` import

**Modify** (in `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx` header area):

```tsx
<div className="flex items-center gap-3">
  <h1 className="font-quicksand font-bold text-xl">I Miei Giochi</h1>
  <Select value={quickFilter} onValueChange={handleQuickFilter}>
    <SelectTrigger className="w-[140px] h-8 text-sm">
      <SelectValue />
    </SelectTrigger>
    <SelectContent>
      <SelectItem value="all">Tutti</SelectItem>
      <SelectItem value="owned">Posseduti</SelectItem>
      <SelectItem value="wishlist">Wishlist</SelectItem>
      <SelectItem value="loaned">Prestati</SelectItem>
    </SelectContent>
  </Select>
  <div className="ml-auto flex items-center gap-2">
    {/* Sort button (from existing LibraryFilters) */}
    {/* View toggle (from existing state) */}
    {/* Search (from existing LibraryFilters) */}
  </div>
</div>
```

The `handleQuickFilter` function maps dropdown values to `GameStateType[]` and updates the existing filter state that `LibraryFilters` already manages.

### Files Modified

- `apps/web/src/app/(authenticated)/library/layout.tsx` — remove ContextBarRegistrar
- `apps/web/src/components/library/LibraryContextBar.tsx` — delete file
- `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx` — add inline Select mapped to stateFilter

### Validation

- Library page loads without ContextBar
- Dropdown shows all filter options
- Dropdown "Posseduti" sets `stateFilter: ['Owned']` → API returns only owned games
- Dropdown "Tutti" clears filter → all games shown
- Existing `LibraryFilters` search, favorites, sort all still functional
- Run existing `LibraryFilters.test.tsx` suite — all tests pass
- Mobile: dropdown accessible on small screens
- No broken imports or missing component references

---

## 6. Step 5 — Meeple Mana SVG Icon Set

### Overview

Create 16 React SVG icon components following the existing `MechanicIcon.tsx` pattern. These replace the emoji symbols in `mana-config.ts` with proper SVG icons.

### Prerequisite: Asset Delivery

SVG source files are in an external repository (`D:\Repositories\meepleai-monorepo-dev\data\design\`). Before implementation:
1. Copy `meeplemana1_2.png` to `apps/web/src/assets/design/` for reference
2. Vectorize each icon from the PNG into 24x24 SVG paths (manual tracing required)
3. Alternative: if the PDF contains vector paths, extract directly

This step can begin with placeholder SVG paths and be refined when final vector assets are available.

### Component Structure

```
apps/web/src/components/icons/entities/
├── EntityIcon.tsx          (router component)
├── index.ts                (exports)
└── icons/
    ├── GameIcon.tsx         🎲
    ├── SessionIcon.tsx      ⏳
    ├── PlayerIcon.tsx       ♟
    ├── EventIcon.tsx        ✦
    ├── CollectionIcon.tsx   📦
    ├── GroupIcon.tsx        👥
    ├── LocationIcon.tsx     📍
    ├── ExpansionIcon.tsx    🃏
    ├── AgentIcon.tsx        ⚡
    ├── KnowledgeIcon.tsx    📜
    ├── ChatIcon.tsx         💬
    ├── NoteIcon.tsx         📝
    ├── ToolkitIcon.tsx      ⚙
    ├── ToolIcon.tsx         🔧
    ├── AchievementIcon.tsx  🏆
    └── CustomIcon.tsx       ✧
```

### Icon Component Pattern

Following `MechanicIcon.tsx`:

```tsx
interface EntityIconProps {
  entity: MeepleEntityType;
  size?: number;       // default 24
  className?: string;
}

export function EntityIcon({ entity, size = 24, className }: EntityIconProps) {
  const IconComponent = ENTITY_ICONS[entity] ?? CustomIcon;
  return <IconComponent size={size} className={className} />;
}
```

Each individual icon:

```tsx
interface IconProps {
  size?: number;
  className?: string;
}

export function GameIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Game"
    >
      {/* SVG path derived from Meeple Mana design */}
    </svg>
  );
}
```

### Integration with ManaSymbol

**Implementation order**: Update `mana-types.ts` FIRST (add `Icon` field to `ManaDisplayConfig`), then `mana-config.ts`, then `ManaSymbol.tsx`.

1. Update `mana-types.ts` — add optional `Icon` to `ManaDisplayConfig`:
```tsx
import type { ComponentType } from 'react';

export interface ManaDisplayConfig {
  key: string;
  displayName: string;
  symbol: string;
  tier: string;
  Icon?: ComponentType<{ size?: number; className?: string }>;
}
```

2. Update `mana-config.ts` — add Icon component references:
```tsx
import { GameIcon } from '@/components/icons/entities/icons/GameIcon';
// ...
export const MANA_DISPLAY: Record<MeepleEntityType, ManaDisplayConfig> = {
  game: { key: 'game', displayName: 'Game', symbol: '🎲', tier: 'core', Icon: GameIcon },
  // ...
};
```

3. Update `ManaSymbol.tsx` — prefer SVG icon when available:
```tsx
<span className="relative z-[1] drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
  {config.Icon ? <config.Icon size={iconSize} className="text-white" /> : config.symbol}
</span>
```

This is backwards-compatible: emoji remains as fallback for any entity type without an Icon.

### Files Created

- `apps/web/src/components/icons/entities/EntityIcon.tsx`
- `apps/web/src/components/icons/entities/index.ts`
- `apps/web/src/components/icons/entities/icons/*.tsx` (16 files)
- `apps/web/src/assets/design/meeplemana1_2.png` (reference copy)

### Files Modified

- `apps/web/src/components/ui/data-display/mana/mana-types.ts` — add `Icon` to ManaDisplayConfig (MUST be first)
- `apps/web/src/components/ui/data-display/mana/mana-config.ts` — add Icon references (after types)
- `apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx` — render SVG when available

### Validation

- All 16 icons render at various sizes (16, 20, 24, 32px)
- Icons use `currentColor` and respect theme
- ManaSymbol shows SVG instead of emoji
- Fallback to emoji if Icon is undefined
- Accessibility: `role="img"` + `aria-label` on each SVG
- No import errors or missing components

---

## 7. Implementation Order & Dependencies

```
Step 1: Color Palette ──────────────────────────┐
Step 2: Fix MeepleCard Flicker ─────────────────┤ (MUST run after Step 1, introduces first token consumers)
Step 3: Resize Icons & Mana Pips ───────────────┤ (can run after Step 2)
Step 4: Replace Tab Bar with Dropdown ──────────┤ (independent of Steps 2-3)
Step 5: Meeple Mana SVG Icon Set ──────────────┘ (independent, can parallelize)
```

Steps 1→2→3 are sequential (each builds on the previous visual base).
Step 4 is independent (can be done anytime after Step 1).
Step 5 is independent (can be started in parallel, requires asset vectorization).

## 8. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Color palette breaks existing components | Low | Tokens have no consumers until Step 2. Visual review after Step 2. |
| Removing HoloOverlay changes card identity | Medium | Opt-in mechanism preserves ability to enable per-card. |
| Glassmorphism removal affects featured variant | Medium | Spec explicitly removes both grid AND featured `::before`. Visual review of featured cards. |
| Dropdown filter loses discoverability vs pills | Low | Standard UI pattern, title-adjacent placement is clear. Existing LibraryFilters preserved. |
| SVG icons don't match emoji visual weight | Medium | Test at multiple sizes, adjust stroke width if needed. Emoji fallback available. |
| Icon resize breaks mobile layout | Low | Test at 320px, existing grid responsive breakpoints handle sizing. |
| Mini pip bump to 24px overflows cover labels | Low | New `small` size (20px) for cover pip, `max-w` guard updated. |

## 9. Testing Checklist

- [ ] Dark mode: backgrounds warm-brown, text readable (after Step 2)
- [ ] Light mode: backgrounds cream, text readable (after Step 2)
- [ ] MeepleCard grid: no flicker on scroll (20+ cards)
- [ ] MeepleCard hover: lift animation smooth
- [ ] MeepleCard featured variant: renders correctly without glassmorphism
- [ ] Metadata icons: 20px, orange accent `opacity-[0.85]`, clearly visible
- [ ] Mana pips: 24px (mini), vibrant glow, clickable
- [ ] Cover entity pip: 20px (small), proportional, no label overlap
- [ ] 4-corner overlay: all corners visible, no overlap, warm borders on subtype icons
- [ ] Library dropdown: filter works, all options present
- [ ] Dropdown maps correctly to `GameStateType` API values
- [ ] Sort + view toggle: still functional
- [ ] Existing `LibraryFilters.test.tsx` passes after Step 4
- [ ] `CollectionPageClient` filter integration works with API
- [ ] Mobile 320px: no overflow, icons proportional
- [ ] Entity SVG icons: render at 16/20/24/32px
- [ ] ManaSymbol: SVG replaces emoji, fallback works
- [ ] Existing tests pass (no regressions)
