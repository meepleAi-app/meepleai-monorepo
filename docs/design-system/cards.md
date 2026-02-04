# Design System: Cards

> MeepleCard as the canonical card component for MeepleAI

## Overview

All card-based UI in MeepleAI uses the **MeepleCard** component system. This ensures visual consistency, accessibility compliance, and maintainable code across the application.

## Canonical Component

```tsx
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
```

**Do NOT use**:
- `GameCard` (deprecated, uses MeepleCard internally)
- `PlayerCard` (deprecated)
- Custom card implementations

## Entity Color Tokens

Each entity type has a semantic color that communicates its purpose:

| Entity | HSL Value | Hex | Usage |
|--------|-----------|-----|-------|
| Game | `25 95% 45%` | `#E07B00` | Board games, video games |
| Player | `262 83% 58%` | `#8B5CF6` | User profiles, participants |
| Collection | `168 76% 42%` | `#14B8A6` | Game collections, lists |
| Event | `350 89% 60%` | `#F43F5E` | Tournaments, meetups |
| Custom | `220 70% 50%` | `#3B82F6` | Default for custom entities |

### Color Application

1. **Left Border**: 4px accent (grid/featured variants)
2. **Top Badge**: Entity type label with background
3. **Gradient Overlay**: Subtle color wash on cover images
4. **Action Buttons**: Primary button background
5. **Ribbon**: Corner ribbon (hero variant)

### Customizing Colors

Use the `customColor` prop with HSL values (space-separated, no `hsl()` wrapper):

```tsx
<MeepleCard
  entity="custom"
  customColor="142 76% 36%"  // Green
  title="Custom Entity"
/>
```

## Spacing Guidelines

### Card Padding

| Variant | Padding |
|---------|---------|
| Grid | `p-4` (16px) |
| List | `p-3` (12px) |
| Compact | `p-2` (8px) |
| Featured | `p-5` (20px) |
| Hero | `p-6` (24px) |

### Card Gaps

Recommended spacing between cards:

```tsx
// Grid layout
<div className="grid gap-4">  // 16px gap
  {cards.map(card => <MeepleCard {...card} />)}
</div>

// List layout
<div className="space-y-2">  // 8px gap
  {cards.map(card => <MeepleCard variant="list" {...card} />)}
</div>
```

### Internal Spacing

- **Title to subtitle**: `mb-0.5` (2px) for grid, `mb-1` (4px) for featured
- **Subtitle to metadata**: `mb-2` (8px)
- **Metadata chips gap**: `gap-2` (8px)
- **Action buttons gap**: `gap-2` (8px), `mt-3` (12px) from content

## Typography

### Font Family

All card titles use **Quicksand** (brand font):

```css
font-family: 'Quicksand', var(--font-sans);
font-weight: 700;  /* Bold for titles */
```

### Title Sizes

| Variant | Size | Line Height |
|---------|------|-------------|
| Hero | `text-2xl` (24px) | `leading-tight` |
| Featured | `text-xl` (20px) | `leading-tight` |
| Grid | `text-lg` (18px) | `leading-tight` |
| List | `text-base` (16px) | `leading-tight` |
| Compact | `text-sm` (14px) | `leading-tight` |

### Subtitle & Metadata

```css
/* Subtitles */
font-size: 0.875rem;  /* 14px */
color: var(--muted-foreground);

/* Metadata chips */
font-size: 0.75rem;  /* 12px */
color: var(--muted-foreground);
```

## Dark Mode

MeepleCard automatically adapts to dark mode:

### Light Mode

- **Background**: `bg-card/90` with glass morphism
- **Border**: `border-border/50`
- **Text**: `text-card-foreground`

### Dark Mode

- **Background**: `dark:bg-card` (solid, no blur)
- **Border**: Same opacity
- **Text**: Same foreground

### Glass Morphism

Grid variant uses glass effect in light mode:

```css
/* Light mode */
backdrop-blur-[12px]
backdrop-saturate-[180%]
bg-card/90

/* Dark mode - disabled for performance */
dark:backdrop-blur-none
dark:bg-card
```

## Responsive Behavior

### Grid Breakpoints

```tsx
// Recommended responsive grid
<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
  {games.map(game => (
    <MeepleCard entity="game" variant="grid" {...game} />
  ))}
</div>
```

### Image Sizes

MeepleCard uses responsive `sizes` for optimal image loading:

```tsx
sizes={
  variant === 'hero'
    ? '100vw'
    : variant === 'featured'
      ? '(max-width: 768px) 100vw, 50vw'
      : variant === 'grid'
        ? '(max-width: 640px) 50vw, (max-width: 1024px) 33vw, 25vw'
        : '64px'
}
```

### Variant Switching

Consider switching variants at breakpoints for optimal UX:

```tsx
function ResponsiveCard({ game }: { game: Game }) {
  const isMobile = useMediaQuery('(max-width: 640px)');

  return (
    <MeepleCard
      entity="game"
      variant={isMobile ? 'list' : 'grid'}
      {...game}
    />
  );
}
```

## Interaction States

### Hover Effects

| Variant | Hover Effect |
|---------|--------------|
| Grid | `hover:-translate-y-1` + `hover:shadow-lg` |
| List | `hover:translate-x-1` + `hover:shadow-md` |
| Compact | `hover:bg-card` |
| Featured | `hover:-translate-y-2` + `hover:shadow-xl` |
| Hero | `hover:scale-[1.01]` + `hover:shadow-2xl` |

### Focus States

All variants use consistent focus ring:

```css
focus-visible:outline-none
focus-visible:ring-2
focus-visible:ring-ring
focus-visible:ring-offset-2
```

### Active States

Action buttons have press feedback:

```css
hover:-translate-y-0.5
disabled:hover:translate-y-0
```

## Accessibility Checklist

When implementing cards:

- [ ] Use `MeepleCard` component (not custom implementations)
- [ ] Provide `title` prop (required for screen readers)
- [ ] Use semantic `metadata` with icons
- [ ] Test keyboard navigation (Tab, Enter, Space)
- [ ] Verify focus indicators are visible
- [ ] Check color contrast in both themes
- [ ] Ensure loading states are announced

## Anti-Patterns

### DON'T: Create Custom Cards

```tsx
// Bad - creates inconsistency
<div className="rounded-lg bg-card p-4">
  <h3>{game.title}</h3>
</div>

// Good - use MeepleCard
<MeepleCard entity="game" title={game.title} />
```

### DON'T: Override Core Styles

```tsx
// Bad - breaks design system
<MeepleCard className="!bg-red-500 !p-8" />

// Good - use provided customization
<MeepleCard entity="custom" customColor="0 84% 60%" />
```

### DON'T: Mix Entity Types

```tsx
// Bad - confusing semantics
<MeepleCard entity="player" title={game.title} />

// Good - match entity to content
<MeepleCard entity="game" title={game.title} />
```

## Migration Timeline

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 1 | ✅ Complete | MeepleCard core implementation |
| Phase 2 | ✅ Complete | GameCard converted to wrapper |
| Phase 3 | 🔄 In Progress | Update all GameCard consumers |
| Phase 4 | 📅 Planned | Remove deprecated wrappers |

## Related Documentation

- [MeepleCard README](../../apps/web/src/components/ui/data-display/README.md)
- [MeepleCard Usage Guide](./components/meeple-card.md)
- [ADR-041: MeepleCard System](../01-architecture/adr/adr-041-meeple-card-universal-system.md)
