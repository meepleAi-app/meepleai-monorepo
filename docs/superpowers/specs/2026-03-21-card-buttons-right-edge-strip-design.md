# Card Action Buttons — Right Edge Strip (C2) Design

**Date**: 2026-03-21
**Status**: Approved
**Scope**: Move card hover action buttons from top-right overlay to centered right-edge vertical strip

## Problem

After implementing the 4-corner CoverOverlay system, the existing card action buttons (info, wishlist, quick actions menu) positioned at `absolute top-2.5 right-2.5 z-15` conflict with the new mana pip in the top-right corner of the cover overlay. With multiple buttons, they also overlap cover labels in the top-left.

## Decision Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Position | Right edge, vertically centered in safe zone | Avoids all 4 corner overlay slots |
| Safe zone | Top 40px and bottom 40px clearance | Prevents overlap with labels/pip (top) and subtypes/state (bottom) |
| Animation | Slide-in from right, staggered sequentially | Elegant cascade effect, each button enters ~50ms after previous |
| Trigger | Hover (desktop), tap/select (mobile) | Matches existing behavior, only position changes |
| Z-index | z-15 | Matches existing z-index, above HoloOverlay and CoverOverlay |
| DOM location | Inside CardCover's relative wrapper | Cover-relative positioning (not card-relative) |

## Architecture

### Layout Diagram

```
┌─────────────────────────────┐
│  ↖ LABELS        PIP ↗     │  ← top 40px: clearance zone
│                             │
│                    [i] ←    │  ← safe zone: buttons here
│                    [♥] ←    │     right: 6px
│                    [⋮] ←    │     gap: 6px
│                             │
│  ↙ SUBTYPES      STATE ↘   │  ← bottom 40px: clearance zone
└─────────────────────────────┘
```

### DOM Structure Change

The action strip moves from inside `contentVariants` div to inside `CardCover`'s `<div className="relative">` wrapper. This ensures `absolute` positioning is relative to the cover image, not the entire card.

```tsx
// CardCover.tsx — new structure
<div className="relative">
  <CoverImage ... />
  <CoverOverlay ... />
  {/* NEW: action strip lives here, cover-relative */}
  {actionStrip}
</div>
```

`CardActions` renders the strip element, but it is passed to `CardCover` as a `children` or `actionStrip` prop, or the strip is extracted into a new sub-component rendered by `CardCover`.

**Chosen approach**: `CardCover` accepts an optional `actionStrip` React node prop. Each variant passes the strip JSX to `CardCover`. `CardActions` is split: the strip part renders via `actionStrip` prop, while featured/hero `ActionButtons` remain in the content area.

### CSS Positioning

```
position: absolute
right: 6px
top: 40px
bottom: 40px
display: flex
flex-direction: column
align-items: center
justify-content: center
gap: 6px
z-index: 15
pointer-events: none  (container)
pointer-events: auto  (each button)
```

### Animation

- **Direction**: `translateX(20px) → translateX(0)` + `opacity: 0 → 1`
- **Duration**: 200ms per button
- **Stagger**: ~50ms delay between each button
- **Easing**: `cubic-bezier(0.4, 0, 0.2, 1)`
- **Fill mode**: `both` (applies `from` state during delay period, prevents flash on fast hover-out)
- **Desktop trigger**: `group-hover` (existing pattern)
- **Mobile trigger**: tap via `useMobileInteraction` hook (existing)

### Stagger Implementation

Each button gets an incremental `animation-delay` via inline style:

```tsx
style={{ animationDelay: `${index * 50}ms` }}  // backtick template literal
```

### Tailwind Config — Register Keyframe

Add `mc-slide-in-right` to `tailwind.config.js` following project `mc-` prefix convention:

```js
// tailwind.config.js → theme.extend.keyframes
'mc-slide-in-right': {
  from: { opacity: '0', transform: 'translateX(20px)' },
  to: { opacity: '1', transform: 'translateX(0)' },
},

// tailwind.config.js → theme.extend.animation
'mc-slide-in-right': 'mc-slide-in-right 200ms cubic-bezier(0.4,0,0.2,1) both',
```

On hover: `animate-mc-slide-in-right` with per-button `animationDelay`.
When not hovered: `opacity-0 pointer-events-none` (no animation).

## File Changes

### Modified Files

| File | Change |
|------|--------|
| `apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx` | Add `actionStrip?: React.ReactNode` prop, render inside relative wrapper |
| `apps/web/src/components/ui/data-display/meeple-card/parts/CardActions.tsx` | Extract strip into `CardActionStrip` component with vertical layout + stagger animation; keep `ActionButtons` for featured/hero |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` | Pass `actionStrip` to `CardCover` instead of rendering `CardActions` strip inline; remove `PrimaryActions` absolute top-right positioning (move into strip or remove) |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx` | Pass `actionStrip` to `CardCover` |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx` | Pass `actionStrip` to `CardCover` |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardExpanded.tsx` | Pass `actionStrip` to cover wrapper |
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx` | Do NOT pass `actionStrip` (thumbnail too small) |
| `apps/web/tailwind.config.js` | Add `mc-slide-in-right` keyframe and animation entry |

## Edge Cases

### PrimaryActions conflict
`PrimaryActions` in `MeepleCardGrid` is currently at `absolute top-10 right-2.5 z-[3]`. This would overlap with the new strip. **Resolution**: Move `PrimaryActions` into the strip (append after the standard action buttons) or reposition to a non-conflicting location (e.g., content area below title).

### ChatSession unread badge offset
The current `CardActions` shifts from `top-2.5` to `top-10` when `entity === 'chatSession' && unreadCount > 0`. In the new strip layout (vertically centered in safe zone), this offset is no longer needed — the strip doesn't overlap the unread badge position. **Resolution**: Remove the chatSession conditional offset.

### List variant suppression
`CardActions` renders whenever `hasTopRightActions` is true, regardless of variant. **Resolution**: The strip is rendered as a prop passed to `CardCover` by each variant. `MeepleCardList` simply doesn't pass `actionStrip`, so no strip appears. The featured/hero `ActionButtons` component continues rendering in the content area via `CardActions`.

### Fast hover-out animation flash
Using `animation-fill-mode: both` ensures the `from` keyframe (opacity: 0, translateX: 20px) applies during the delay period, preventing buttons from flashing visible before their stagger delay starts.

## Backward Compatibility

- Same props API: `entityQuickActions`, `showInfoButton`, `quickActions`, `showWishlistBtn`
- Same visibility logic: only rendered when `hasTopRightActions` is true
- Same hover/mobile trigger behavior
- Featured/hero `ActionButtons` remain in content area unchanged
- Only the visual position and animation of the hover strip change

## Variants

| Variant | Behavior |
|---------|----------|
| Grid | Full strip with stagger animation |
| Featured | Full strip (16:9 cover has room) |
| Hero | Full strip |
| List | No strip (thumbnail too small) — buttons remain in content area if needed |
| Compact | No cover, no strip |
| Expanded | Full strip |
