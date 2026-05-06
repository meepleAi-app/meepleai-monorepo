# MeepleCard Cover — 4-Corner Overlay Redesign

**Date**: 2026-03-21
**Status**: Draft
**Scope**: Redesign MeepleCard cover overlay from 2-slot (bottom only) to 4-corner layout

## Problem

The current MeepleCard cover overlay only has 2 slots (bottom-left: `mechanicIcon`, bottom-right: `stateLabel`). The `ManaBadge` (entity type indicator) lives OUTSIDE the cover as an absolute-positioned element on the card itself. This creates:
- Inconsistent layering between ManaBadge and cover overlay
- No room for a title/label in the cover area
- No support for subtype classification icons
- Limited information density on the cover

## Decision Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Overlay layout | 4 absolute-positioned corners | Flexible, no grid clipping issues, each slot independent |
| ManaBadge location | Moves INTO cover overlay (top-right) | Consolidates all cover metadata in one system |
| Label slot | Top-left, multi-label stack | Supports primary label + N secondary context labels |
| Subtype icons | Bottom-left, replaces mechanicIcon | More flexible, supports N icons with tooltips |

## Architecture

### 4-Corner Slot System

```
┌─────────────────────────────────┐
│  ↖ LABEL(S)        TIPO CARD ↗ │
│  (top:8, left:8)   (top:8, r:8)│
│                                 │
│         🎨 Cover Image          │
│                                 │
│  ↙ SUBTYPES         STATO ↘    │
│  (bot:8, left:8)   (bot:8, r:8)│
└─────────────────────────────────┘
```

| Slot | Position | Content | Styling |
|------|----------|---------|---------|
| **Top-Left** | `top:8px; left:8px` | Label stack (primary + secondaries) | Entity-color bg/85%, backdrop-blur(8px), Quicksand 700, white text |
| **Top-Right** | `top:8px; right:8px` | Entity type (mana orb + name) | Mana dot 16px + uppercase label, dark glass bg rgba(0,0,0,0.4) |
| **Bottom-Left** | `bottom:8px; left:8px` | Subtype icons | 24px rounded-md, dark glass bg, hover scale(1.15) |
| **Bottom-Right** | `bottom:8px; right:8px` | State badge | Semantic color pill (success/warning/error/info), backdrop-blur |

### Multi-Label Stack (Top-Left)

Labels stack vertically with `flex-direction: column; gap: 4px`:

| Type | Font | Padding | Opacity | Background |
|------|------|---------|---------|------------|
| Primary | 11px Quicksand 700 | 3px 9px | 1.0 | Entity color / 85% |
| Secondary | 9px Quicksand 700 | 2px 8px | 0.9 | Context entity color / 75% |

Secondary labels use the color of the entity they reference (e.g., KB label = teal, Agent label = amber, Session label = indigo).

## Migration from Current System

### What Changes

| Current | New |
|---------|-----|
| `ManaBadge` as absolute element on card (line 279 MeepleCardGrid) | Moves into cover overlay top-right slot |
| `coverOverlayStyles.container` (flex, bottom-only) | New `CoverOverlay` component with 4 absolute slots |
| `mechanicIcon` prop (single icon, bottom-left) | `subtypeIcons` prop (array of icons, bottom-left) |
| `stateLabel` prop (bottom-right) | Kept, same position, same styling |
| No label on cover | `coverLabels` prop (array, top-left stack) |

### What Stays the Same

- `CoverImage` component (unchanged)
- Entity color system (`entityColors`, HSL values)
- `ManaSymbol` component (reused in top-right slot)
- Hover shimmer animation on cover
- Aspect ratios per variant

### Backward Compatibility

- `mechanicIcon` prop deprecated but still works (mapped to `subtypeIcons[0]`)
- `stateLabel` prop unchanged
- `ManaBadge` still renders if `coverLabels` is not provided (fallback)

## Props Changes

### CardCover Props (Extended)

```typescript
export interface CardCoverProps {
  src?: string;
  alt: string;
  variant: MeepleCardVariant;
  entity: MeepleEntityType;
  customColor?: string;
  showShimmer?: boolean;
  className?: string;
  // EXISTING — kept for backward compat
  mechanicIcon?: React.ReactNode;
  stateLabel?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' };
  // NEW — 4-corner system
  coverLabels?: CoverLabel[];
  subtypeIcons?: SubtypeIcon[];
}

export interface CoverLabel {
  text: string;
  color?: string;  // HSL string, defaults to entity color
  primary?: boolean; // default false, first label is primary
}

export interface SubtypeIcon {
  icon: React.ReactNode;
  tooltip: string;
}
```

### MeepleCard Props (New forwarded props)

```typescript
// Added to MeepleCardProps in types.ts
coverLabels?: CoverLabel[];
subtypeIcons?: SubtypeIcon[];
```

## Files Touched

| File | Change |
|------|--------|
| `meeple-card/parts/CardCover.tsx` | Replace 2-slot overlay with 4-corner `CoverOverlay` |
| `meeple-card/parts/CoverOverlay.tsx` (NEW) | 4-corner overlay component |
| `meeple-card/types.ts` | + `CoverLabel`, `SubtypeIcon` interfaces, + props |
| `meeple-card/variants/MeepleCardGrid.tsx` | Remove external `ManaBadge`, forward new props to CardCover |
| `meeple-card/variants/MeepleCardList.tsx` | Forward new props (list variant may skip cover overlay) |
| `meeple-card/variants/MeepleCardFeatured.tsx` | Forward new props |
| `meeple-card/variants/MeepleCardHero.tsx` | Forward new props |
| `meeple-card-features/ManaBadge.tsx` | Deprecated — still exported but marked |
| `meeple-card-styles.ts` | Update `coverOverlayStyles` to 4-corner system |

## Out of Scope

- New entity types or colors
- Card back (flip) changes
- Navigation footer changes
- Compact variant (has no cover)
- Expanded variant changes beyond prop forwarding

## Testing Strategy

- **Unit**: `CoverOverlay` renders all 4 slots when props provided
- **Unit**: `CoverOverlay` renders only provided slots (partial)
- **Unit**: Multi-label stack renders primary + N secondaries
- **Unit**: `mechanicIcon` backward compat maps to `subtypeIcons[0]`
- **Unit**: ManaBadge fallback when no `coverLabels`
- **Visual**: Storybook stories for all variants with 4-corner overlay
