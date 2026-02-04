# ADR-041: MeepleCard Universal Card System

## Status

**Accepted** (2026-02-04)

## Context

The MeepleAI frontend had multiple card components for different entity types:

- `GameCard` - Board game display with cover image and metadata
- `PlayerCard` - User profile cards (planned)
- Collection cards, event cards, etc.

Each component was implemented independently with:
- Different styling approaches (some CVA, some inline)
- Inconsistent spacing, typography, and color usage
- Duplicated code for common patterns (images, ratings, metadata)
- No unified design language across entity types

This led to:
1. **Maintenance burden**: Bug fixes and improvements needed replication
2. **Inconsistent UX**: Users experienced different interactions per card type
3. **Design drift**: Components diverged over time without governance
4. **Accessibility gaps**: A11y features implemented inconsistently

## Decision

Create a **unified MeepleCard component** that serves as the canonical card system for all MeepleAI entities.

### Core Design Principles

1. **Polymorphic Entity Support**: Single component handles games, players, collections, events, and custom entities through the `entity` prop
2. **CVA Variant System**: Use class-variance-authority for type-safe, composable variants
3. **Semantic Color Coding**: Each entity type has a distinct color (orange/game, purple/player, teal/collection, rose/event)
4. **Accessibility First**: WCAG AA compliance with keyboard navigation and screen reader support
5. **Performance Optimized**: React.memo with lazy-loaded images

### Component Architecture

```
MeepleCard (main)
├── EntityIndicator (color-coded badge/border)
├── CoverImage (lazy-loaded with gradient overlay)
├── Content
│   ├── Title
│   ├── Subtitle
│   ├── RatingDisplay
│   ├── MetadataChips
│   └── ActionButtons (featured/hero variants)
└── MeepleCardSkeleton (loading state)
```

### Variant System

| Variant | Dimensions | Use Case |
|---------|------------|----------|
| `grid` | 4:3 aspect cover | Catalog grids, card collections |
| `list` | Horizontal, 64px thumb | Search results, lists |
| `compact` | No image, minimal | Sidebars, dropdowns |
| `featured` | 16:9 cover + actions | Featured content |
| `hero` | Full-bleed background | Hero sections |

### Migration Strategy

1. Create MeepleCard as new canonical component
2. Convert existing components to deprecated wrappers using MeepleCard internally
3. Update barrel exports to expose both (old for compatibility, new for features)
4. Document migration path in component README
5. Gradually update consumers to use MeepleCard directly

## Consequences

### Positive

- **Unified Design Language**: Consistent visual identity across all card types
- **Reduced Maintenance**: Single source of truth for card behavior and styling
- **Better Accessibility**: A11y features implemented once, applied everywhere
- **Faster Development**: New entity types just need a color definition
- **Type Safety**: Full TypeScript support with discriminated unions

### Negative

- **Migration Effort**: Existing consumers need updates (mitigated by wrapper pattern)
- **Learning Curve**: Teams need to learn new prop structure
- **Bundle Size**: Single component is larger than individual components (offset by tree-shaking)

### Neutral

- **Design Lock-in**: Changes to card design affect all entities (this is intentional)
- **Prop Complexity**: More props than single-purpose components (tradeoff for flexibility)

## Implementation

### Files Created

- `apps/web/src/components/ui/data-display/meeple-card.tsx` - Main component
- `apps/web/src/components/ui/data-display/__tests__/meeple-card.test.tsx` - Unit tests
- `apps/web/src/components/ui/data-display/__tests__/meeple-card.a11y.test.tsx` - A11y tests
- `apps/web/src/components/ui/data-display/__tests__/meeple-card.snapshot.test.tsx` - Snapshots
- `apps/web/src/components/ui/data-display/meeple-card.stories.tsx` - Storybook docs

### Adapter Components

- `MeepleGameCatalogCard` - Wraps MeepleCard for SharedGameCatalog
- `MeepleGameWidget` - Wraps MeepleCard for Dashboard widgets
- `GameCard` (deprecated) - Legacy wrapper using MeepleCard internally

## Related Issues

- #3325 - MeepleCard Epic (parent)
- #3326 - Core MeepleCard Component Implementation
- #3328 - MeepleCard Unit & Accessibility Tests
- #3329 - MeepleCard Storybook Documentation
- #3331 - GameCard Migration to MeepleCard
- #3334 - MeepleCard Integration with SharedGameCatalog & Dashboard
- #3336 - MeepleCard Documentation & Usage Guide

## References

- [class-variance-authority](https://cva.style/docs) - Variant management
- [WCAG 2.1 AA](https://www.w3.org/WAI/WCAG21/quickref/) - Accessibility guidelines
- [React.memo](https://react.dev/reference/react/memo) - Performance optimization
