# [TECH] GameCarousel Technical Documentation

**Issue ID**: GC-006
**Epic**: EPIC-GC-001 (GameCarousel Integration)
**Story Points**: 5
**Priority**: P2
**Status**: To Do
**Assignee**: TBD

---

## 📋 User Story

> Come nuovo sviluppatore del team,
> Voglio documentazione tecnica completa,
> Così da poter utilizzare e modificare il GameCarousel correttamente.

---

## 🎯 Acceptance Criteria

- [ ] README.md nel folder del componente
- [ ] JSDoc completo per tutte le props e types
- [ ] Architecture Decision Record (ADR) per scelte tecniche
- [ ] Guida migrazione da mock data a API reale
- [ ] Performance best practices documentate
- [ ] Troubleshooting section per problemi comuni
- [ ] Changelog per versioni future
- [ ] Link a Storybook nella documentazione

---

## 📄 Documentation Structure

### 1. Component README
```markdown
# GameCarousel

Location: `apps/web/src/components/ui/data-display/game-carousel.tsx`

## Overview

Un carousel immersivo 3D per navigare la collezione di giochi MeepleAI.
Utilizza MeepleCard per la visualizzazione e supporta navigazione
touch, keyboard, e mouse.

## Quick Start

\`\`\`tsx
import { GameCarousel } from '@/components/ui/data-display';

<GameCarousel
  games={games}
  title="Featured Games"
  onGameSelect={(game) => router.push(\`/games/\${game.id}\`)}
/>
\`\`\`

## Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| games | CarouselGame[] | required | Array of games to display |
| title | string | - | Section title |
| subtitle | string | - | Section subtitle |
| onGameSelect | (game) => void | - | Callback on game click |
| autoPlay | boolean | false | Enable auto-rotation |
| autoPlayInterval | number | 5000 | Interval in ms |
| showDots | boolean | true | Show navigation dots |
| sortable | boolean | false | Enable sorting |
| defaultSort | string | 'rating' | Default sort option |

## Features

- 🎯 3D perspective effect with depth
- 📱 Responsive (1/3/5 cards)
- ⌨️ Full keyboard navigation
- 👆 Touch swipe support
- ♿ WCAG AA accessible
- 🔄 Infinite loop
- ⏯️ Optional auto-play

## Related

- [MeepleCard](./meeple-card.md)
- [Storybook](https://storybook.meepleai.com/?path=/docs/data-display-gamecarousel)
- [ADR-XXX](../../adr/adr-xxx-game-carousel.md)
```

### 2. Architecture Decision Record
```markdown
# ADR-XXX: GameCarousel Architecture

## Status
Accepted

## Context
Needed an engaging way to browse games that:
- Works across all device sizes
- Supports various data sources
- Maintains high performance
- Is fully accessible

## Decision

### 1. 3D Perspective via CSS Transforms
- Used CSS `transform: translateX() scale() rotateY()` for depth effect
- Avoided WebGL/Three.js for simplicity and performance
- Trade-off: Less dramatic 3D but better compatibility

### 2. MeepleCard Integration
- Reused existing MeepleCard with `featured` and `grid` variants
- Center card uses `featured`, side cards use `grid`
- Ensures consistency across the design system

### 3. Client-Side State for Navigation
- Position tracked with useState, not URL
- Sorting persisted to URL and localStorage
- Allows smooth animations without page reloads

### 4. Infinite Loop Implementation
- Virtual positioning with modulo arithmetic
- No DOM duplication (performant)
- Trade-off: Slight complexity in position calculation

### 5. React Query for Data Fetching
- Stale-while-revalidate for perceived performance
- 5-minute stale time, 30-minute cache
- Automatic retry on failure

## Consequences

### Positive
- Fast, smooth animations at 60fps
- Works on all modern browsers
- Small bundle size (~8KB gzipped)
- Easy to maintain and extend

### Negative
- No support for browsers without CSS transforms (IE11)
- Infinite loop can be confusing for some users
- Position calculation has edge cases

## Alternatives Considered

1. **Embla Carousel**: Good library, but adds 15KB and learning curve
2. **Swiper**: Feature-rich but heavy (50KB+)
3. **Native scroll-snap**: Simpler but no 3D effect possible
```

### 3. Performance Guide
```markdown
# GameCarousel Performance Guide

## Metrics Target

| Metric | Target | Current |
|--------|--------|---------|
| LCP | < 2.5s | TBD |
| FID | < 100ms | TBD |
| CLS | < 0.1 | TBD |
| Bundle Size | < 15KB | ~8KB |

## Best Practices

### Image Optimization
- Use Next.js Image with `loading="lazy"`
- Provide appropriate `sizes` for responsive images
- Use placeholder blur for perceived performance

### Animation Performance
- All animations use `transform` and `opacity` only
- Avoid layout-triggering properties
- Use `will-change: transform` sparingly

### Memory Management
- Only render visible cards (±2 from center)
- Clean up event listeners on unmount
- Use React.memo for card components

### Code Splitting
- Carousel is already tree-shakeable
- Consider dynamic import for pages with multiple carousels

## Lighthouse Audit Checklist

- [ ] Run Lighthouse on /gallery page
- [ ] Check "Avoid large layout shifts" warning
- [ ] Verify image sizes are appropriate
- [ ] Check for unused JavaScript
```

### 4. Troubleshooting Guide
```markdown
# GameCarousel Troubleshooting

## Common Issues

### Cards not showing
**Symptom**: Carousel renders but no cards visible

**Causes**:
1. Empty `games` array
2. API fetch failed silently
3. CSS z-index conflict

**Solutions**:
\`\`\`tsx
// Check if games array has data
console.log('Games:', games);

// Verify API response
const { games, isLoading, error } = useCarouselGames({ source: 'featured' });
if (error) console.error('API Error:', error);
\`\`\`

### Navigation not working
**Symptom**: Arrow clicks don't move carousel

**Causes**:
1. `isAnimating` stuck as true
2. Event handler not attached
3. `games.length` is 0 or 1

**Solutions**:
- Check browser console for errors
- Verify games array has 2+ items
- Try keyboard navigation (focus + arrow keys)

### Performance issues
**Symptom**: Janky animations, low FPS

**Causes**:
1. Too many cards rendering
2. Large images not optimized
3. CSS blur on many elements

**Solutions**:
- Use Next.js Image component
- Reduce blur radius on mobile
- Profile with Chrome DevTools Performance tab

### Accessibility violations
**Symptom**: axe-core reports errors

**Common fixes**:
\`\`\`tsx
// Ensure buttons have accessible names
<button aria-label="Previous game">

// Ensure carousel has role
<section aria-roledescription="carousel" aria-label={title}>

// Ensure live region exists
<div aria-live="polite" className="sr-only">
  {announcement}
</div>
\`\`\`
```

---

## 📁 Files to Create/Modify

| Action | File Path |
|--------|-----------|
| CREATE | `apps/web/src/components/ui/data-display/game-carousel/README.md` |
| CREATE | `docs/01-architecture/adr/adr-xxx-game-carousel.md` |
| CREATE | `docs/07-frontend/components/game-carousel.md` |
| CREATE | `docs/07-frontend/components/game-carousel-performance.md` |
| CREATE | `docs/07-frontend/components/game-carousel-troubleshooting.md` |
| MODIFY | `apps/web/src/components/ui/data-display/game-carousel.tsx` (JSDoc) |

---

## 📝 JSDoc Requirements

All exported functions, components, and types must have JSDoc:

```typescript
/**
 * GameCarousel - Immersive 3D carousel for browsing games
 *
 * @description A visually striking carousel component with depth perspective,
 * featuring a prominent center card with fading side cards.
 *
 * @example
 * ```tsx
 * <GameCarousel
 *   games={games}
 *   title="Featured Games"
 *   onGameSelect={(game) => navigate(`/games/${game.id}`)}
 * />
 * ```
 *
 * @see {@link https://storybook.meepleai.com/?path=/docs/data-display-gamecarousel Storybook}
 * @see {@link ../../docs/adr/adr-xxx-game-carousel.md ADR}
 */
export const GameCarousel = React.memo(function GameCarousel({...}: GameCarouselProps) {

/**
 * Game data for carousel display
 *
 * @property id - Unique game identifier
 * @property title - Game name (displayed as card title)
 * @property subtitle - Publisher or secondary info
 * @property imageUrl - Cover image URL (recommended 400x300)
 * @property rating - Game rating (0-10 scale)
 * @property ratingMax - Maximum rating value (default: 10)
 * @property metadata - Additional info chips (players, duration)
 * @property badge - Optional badge text (e.g., "New", "Top Rated")
 */
export interface CarouselGame {
```

---

## 🔗 Dependencies

- **Blocked By**: All previous issues (needs complete implementation)
- **Blocks**: None

---

## ✅ Definition of Done

- [ ] README.md creato nel folder componente
- [ ] ADR scritto e approvato
- [ ] Documentazione componente in docs/
- [ ] Performance guide documentata
- [ ] Troubleshooting guide documentata
- [ ] JSDoc completo su tutti gli export
- [ ] Link a Storybook funzionante
- [ ] Code review approvato
- [ ] Documentation review da tech lead
