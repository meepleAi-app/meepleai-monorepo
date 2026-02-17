# MeepleCard v2 Migration Guide

**Epic #4604** | **PR #4619** | **Created**: 2026-02-17

Guide for migrating consumer sites to MeepleCard v2 visual redesign.

## Overview

MeepleCard v2 is a **visual enhancement** with **zero breaking changes**. All existing implementations continue to work unchanged. This guide covers optional optimizations to leverage new v2 features.

## What Changed

### Visual Enhancements (Automatic)

These improvements apply automatically without code changes:

✅ **Warm shadows** - Layered brown-toned shadows instead of neutral gray
✅ **Entity glow rings** - Hover outline-2 with entity color @ 40% opacity
✅ **Enhanced carousel** - Center card 1.1x scale with warm shadow-2xl
✅ **Shimmer effects** - Cover image shimmer on hover
✅ **Smoother transitions** - 350ms cubic-bezier(0.4,0,0.2,1)
✅ **Dark mode tokens** - Entity color CSS variables for theme consistency

### New Optional Props

**FlipCard Component**:
```tsx
interface FlipCardProps {
  // ... existing props
  entityColor?: string;     // NEW: HSL string e.g. "25 95% 45%"
  entityName?: string;      // NEW: e.g. "Game"
  title?: string;           // NEW: Card title for back header
}
```

**TagBadge Component**:
```tsx
interface TagBadgeProps {
  // ... existing props
  animated?: boolean;  // NEW: Enable pulse animation
}
```

**Note**: TagStrip auto-enables `animated` for "new" tags.

## Migration Steps

### Step 1: No Action Required ✅

If you're satisfied with automatic visual improvements, **you're done**. No code changes needed.

### Step 2: Leverage New Props (Optional)

**For flip cards with entity-colored backs**:

```tsx
// Before (still works)
<MeepleCard
  flippable
  flipData={{ description: "..." }}
/>

// After (v2 enhanced)
<MeepleCard
  flippable
  flipData={{ description: "..." }}
  // These auto-pass to FlipCard via wrapper
  entity="game"      // Already present
  title="Wingspan"   // Already present
  customColor="25 95% 45%"  // Optional override
/>
```

The `meeple-card.tsx` wrapper automatically passes `entityColor`, `entityName`, `title` to FlipCard.

**For animated tags**:

```tsx
// Auto-animated (no change needed)
<MeepleCard tags={['new', 'sale']} />  // "new" tag pulses automatically

// Manual control
<TagBadge tag="new" animated />        // Explicit enable
<TagBadge tag="sale" animated={false} /> // Explicit disable
```

### Step 3: Verify Dark Mode (Recommended)

Test your pages in dark mode to ensure entity colors display correctly:

```tsx
// In your page/layout
<html className="dark">  {/* or use theme toggle */}
  <MeepleCard entity="game" ... />
</html>
```

Entity colors use HSL format and work in both light/dark modes automatically.

## Consumer Site Inventory (23 Sites)

**Games**:
- `/apps/web/src/app/(main)/games/page.tsx` - Grid + filters
- `/apps/web/src/app/(main)/games/[id]/page.tsx` - Detail hero
- `/apps/web/src/app/(main)/catalog/page.tsx` - Community catalog grid

**Player**:
- `/apps/web/src/app/(main)/players/[id]/page.tsx` - Player profile
- `/apps/web/src/app/(main)/leaderboard/page.tsx` - Player list

**Agent**:
- `/apps/web/src/app/(main)/agents/page.tsx` - Agent grid
- `/apps/web/src/app/(main)/agents/[id]/page.tsx` - Agent detail

**Admin**:
- `/apps/web/src/app/admin/(enterprise)/overview/page.tsx` - Stats dashboard
- `/apps/web/src/app/admin/(enterprise)/resources/page.tsx` - Resource cards
- 15+ other admin routes

**No Breaking Changes**: All 23 sites work unchanged with v2 visual improvements.

## Behavioral Changes

### Hover Effects

**Before**: Generic shadow-md → shadow-lg
**After**: Warm shadow-sm → shadow-xl + entity glow ring + lift -translate-y-1.5

**Before**: Cover image scale-105
**After**: Cover scale-105 + shimmer overlay animation

### Carousel

**Before**: Center card scale ~1.0, side cards scale ~0.85
**After**: Center card scale 1.1, side cards scale 0.85 (fixed)

**Before**: Dynamic opacity calc
**After**: Center opacity 1.0, side opacity 0.6 (fixed)

Quick actions remain center-only (no change from #4030).

### Card Flip

**Before**: Perspective 2000px, transition 0.8s
**After**: Perspective 1200px, transition 0.7s (snappier)

**Before**: Generic purple/orange back
**After**: Entity-colored header with diagonal stripe pattern

## Testing Checklist

For each consumer site:

- [ ] Visual regression: Cards render correctly in light mode
- [ ] Visual regression: Cards render correctly in dark mode
- [ ] Hover states: Entity glow ring appears on hover
- [ ] Carousel: Center card scaled 1.1x with enhanced shadow
- [ ] Flip cards: Entity-colored back header displays
- [ ] Tags: "new" tags have pulse animation
- [ ] No layout shifts or broken styles
- [ ] No console errors
- [ ] Accessibility: Focus rings visible, keyboard nav works

## Rollback Plan

If issues arise, revert PR #4619:

```bash
git revert 68e9253b1  # Dark mode tokens
git revert bb960ed46  # Carousel
git revert 3c4d96a29  # Tags
git revert 7469b6f86  # Flip
git revert bd7666ef4  # Layout variants
git revert d8b9ff051  # Base styling
```

Or cherry-pick specific commits to keep some changes.

## Performance Impact

**Zero negative impact**:
- CSS variables: Compile-time, no runtime cost
- Animations: CSS-only (GPU accelerated)
- Shimmer: Triggered only on hover
- Shadows: Native CSS, no JS overhead

**Potential improvement**:
- Fewer DOM nodes (entity glow via outline, not extra element)

## FAQ

**Q: Do I need to update my MeepleCard usage?**
A: No. All changes are visual and backward compatible.

**Q: Will this affect my custom entity colors?**
A: No. `customColor` prop still overrides entity defaults.

**Q: Can I disable the shimmer effect?**
A: Yes, it's CSS-based. Override with:
```css
.group:hover .shimmer-overlay { animation: none; }
```

**Q: How do I test v2 locally before merging?**
A: Checkout PR branch:
```bash
git fetch origin pull/4619/head:test-v2
git checkout test-v2
pnpm dev
```

**Q: What if I prefer the old shadows?**
A: Override in consuming component:
```tsx
<MeepleCard className="shadow-md hover:shadow-lg" ... />
```

## Support

**Issues**: Create GitHub issue with `area/ui` + `meeple-card-v2` labels
**PR**: #4619
**Epic**: #4604
**Design Reference**: `apps/web/src/components/ui/meeple-card-v2-mockup.html`
