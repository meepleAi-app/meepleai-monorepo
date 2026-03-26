# GameCarousel Usage Guide

> Immersive 3D carousel for browsing board games with depth perspective effects

## Introduction

GameCarousel is an interactive carousel component designed for showcasing board games with a Netflix-style browsing experience. It features a prominent center card with fading side cards that create a depth illusion.

### Why GameCarousel?

- **Immersive Experience**: 3D perspective with scale and blur effects
- **Responsive Design**: 1 card on mobile, 3 on tablet, 5 on desktop
- **Touch Support**: Swipe gestures for mobile navigation
- **Keyboard Accessible**: Arrow keys and Enter for full navigation
- **WCAG 2.1 AA**: Comprehensive accessibility compliance
- **Performance**: Optimized with React.memo and smooth CSS transitions

## Quick Start

```tsx
import { GameCarousel, useFeaturedGames } from '@/components/ui/data-display';

function FeaturedSection() {
  const { data, isLoading } = useFeaturedGames(10);

  if (isLoading) return <GameCarouselSkeleton />;

  return (
    <GameCarousel
      games={data.games}
      title="Featured Games"
      subtitle="Top-rated by the community"
      onGameSelect={(game) => router.push(`/games/${game.id}`)}
      showDots
    />
  );
}
```

## Props Reference

### GameCarousel

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `games` | `CarouselGame[]` | required | Array of games to display |
| `title` | `string` | - | Title displayed above carousel |
| `subtitle` | `string` | - | Subtitle below title |
| `showDots` | `boolean` | `true` | Show navigation dots |
| `autoPlay` | `boolean` | `false` | Enable auto-rotation |
| `autoPlayInterval` | `number` | `5000` | Auto-play interval (ms) |
| `sortable` | `boolean` | `false` | Enable sort dropdown |
| `defaultSort` | `CarouselSortValue` | `'rating'` | Default sort option |
| `sort` | `CarouselSortValue` | - | Controlled sort value |
| `onSortChange` | `(sort) => void` | - | Sort change handler |
| `onGameSelect` | `(game) => void` | - | Game click handler |
| `className` | `string` | - | Additional CSS classes |

### CarouselGame Interface

```typescript
interface CarouselGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  ratingMax?: number;
  metadata?: Array<{
    icon: LucideIcon;
    value: string;
  }>;
  badge?: string;
}
```

### CarouselSortValue

```typescript
type CarouselSortValue = 'rating' | 'popularity' | 'name' | 'date';
```

## Data Hooks

### useCarouselGames

The main hook for fetching carousel data with TanStack Query integration.

```tsx
import { useCarouselGames } from '@/hooks/queries/useCarouselGames';

// Featured games (sorted by rating)
const { data, isLoading, error } = useCarouselGames({
  source: 'featured',
  limit: 10,
});

// Category games
const { data } = useCarouselGames({
  source: 'category',
  categoryId: 'strategy-uuid',
  limit: 10,
});

// User's library (requires authentication)
const { data } = useCarouselGames({
  source: 'user-library',
  limit: 10,
});
```

### Convenience Hooks

```tsx
// Featured games (highest rated)
const { data } = useFeaturedGames(10);

// Trending games (recently popular)
const { data } = useTrendingGames(10);

// Category-specific games
const { data } = useCategoryGames('strategy-uuid', 10);

// User's game library
const { data } = useUserLibraryGames(10);
```

### useCarouselSort

Manages sort state with localStorage persistence and optional URL sync.

```tsx
import { useCarouselSort } from '@/hooks/useCarouselSort';

const { sort, setSort, isDefault, clearPreference } = useCarouselSort({
  carouselKey: 'featured-games',
  defaultSort: 'rating',
  syncWithUrl: true,
});
```

## Features

### Responsive Breakpoints

| Screen Size | Cards Shown | Card Width |
|-------------|-------------|------------|
| Mobile (< 640px) | 1 | 260px |
| Tablet (640-1024px) | 3 | 280-300px |
| Desktop (≥ 1024px) | 5 | 340px |

### Navigation Methods

1. **Navigation Arrows**: Click prev/next buttons on sides
2. **Dot Indicators**: Click dots to jump to specific game
3. **Keyboard**: ArrowLeft/ArrowRight to navigate, Enter/Space to select
4. **Touch Gestures**: Swipe left/right on mobile devices

### Auto-Play

```tsx
<GameCarousel
  games={games}
  autoPlay
  autoPlayInterval={3000} // 3 seconds
/>
```

Auto-play automatically pauses on hover and resumes on mouse leave.

### Sorting Controls

```tsx
// Uncontrolled (internal state)
<GameCarousel
  games={games}
  sortable
  defaultSort="rating"
  onSortChange={(sort) => console.log('Sort changed:', sort)}
/>

// Controlled (external state)
const [sort, setSort] = useState<CarouselSortValue>('rating');

<GameCarousel
  games={games}
  sortable
  sort={sort}
  onSortChange={setSort}
/>
```

### Sort Options

| Value | Label | API Sort Field |
|-------|-------|----------------|
| `rating` | Rating | `averageRating` (desc) |
| `popularity` | Popular | `modifiedAt` (desc) |
| `name` | A-Z | `title` (asc) |
| `date` | Newest | `createdAt` (desc) |

## Accessibility

### ARIA Implementation

- `role="region"` with `aria-label` on carousel container
- `aria-roledescription="carousel"` for screen readers
- `role="tab"` on dot indicators with `aria-selected`
- `aria-live="polite"` region announces current game
- `aria-label` on all navigation buttons

### Keyboard Navigation

| Key | Action |
|-----|--------|
| ArrowLeft | Previous game |
| ArrowRight | Next game |
| Enter | Select focused game |
| Space | Select focused game |
| Tab | Navigate between interactive elements |

### Focus Management

- Navigation arrows receive focus indicator
- Dot indicators are individually focusable
- Game cards have `tabIndex="0"` when active
- Focus is maintained after navigation actions

## Visual Effects

### 3D Depth Perspective

The carousel creates depth through:

1. **Scale**: Center card at 100%, side cards at 70-80%
2. **Blur**: Side cards have subtle blur (1-2px)
3. **Opacity**: Side cards fade to 60-80%
4. **Z-Index**: Center card elevated above sides
5. **Transform**: Slight rotation for depth illusion

### Animations

- **Transition Duration**: 300ms ease-out
- **Hover Effects**: Scale 1.02 on center card
- **Auto-play Transition**: Smooth slide between cards

## Loading States

### Skeleton Loader

```tsx
import { GameCarouselSkeleton } from '@/components/ui/data-display';

// Use during data loading
if (isLoading) {
  return <GameCarouselSkeleton />;
}
```

### Empty State

When `games` is empty, displays:
> "No games to display"

## Integration Examples

### Homepage Section

```tsx
// apps/web/src/components/home/GamesCarouselSection.tsx
export function GamesCarouselSection() {
  const { data, isLoading, error } = useFeaturedGames(10);

  if (error) return null;
  if (isLoading) return <GameCarouselSkeleton />;
  if (!data?.games.length) return null;

  return (
    <section className="py-16 bg-muted/30">
      <GameCarousel
        games={data.games}
        title="Featured Games"
        subtitle="Discover top-rated games loved by our community"
        showDots
        onGameSelect={(game) => router.push(`/games/${game.id}`)}
      />
    </section>
  );
}
```

### Gallery Page

```tsx
// apps/web/src/app/(public)/gallery/page.tsx
export default function GalleryPage() {
  return (
    <main>
      <GameCarousel
        games={featuredGames}
        title="Featured Games"
        sortable
        defaultSort="rating"
      />

      <GameCarousel
        games={trendingGames}
        title="Trending Now"
        sortable
        defaultSort="popularity"
      />

      <GameCarousel
        games={strategyGames}
        title="Strategy Games"
      />
    </main>
  );
}
```

## Storybook

View interactive examples at:
```
pnpm storybook
# Navigate to UI/Data Display/GameCarousel
```

### Available Stories

- **Default**: Basic carousel with mock data
- **AutoPlay**: Automatic rotation demo
- **WithSorting**: Sorting controls enabled
- **ControlledSort**: External sort state management
- **Loading**: Skeleton loading state
- **Empty**: No games available
- **FewCards**: Adapting to small datasets
- **NoHeader**: Carousel without title
- **FullFeatured**: All features enabled
- **Responsive**: Viewport resize preview
- **DarkMode**: Dark theme styling
- **NoImages**: Placeholder fallback
- **NoRatings**: Without rating display

## Testing

### Running Tests

```bash
cd apps/web

# Unit tests
pnpm test src/components/ui/data-display/__tests__/game-carousel.test.tsx

# Accessibility tests
pnpm test src/components/ui/data-display/__tests__/game-carousel.a11y.test.tsx

# Hook tests
pnpm test src/hooks/queries/__tests__/useCarouselGames.test.tsx
pnpm test src/hooks/__tests__/useCarouselSort.test.tsx
```

### Test Coverage

- **Component Tests**: 44 tests covering rendering, navigation, keyboard, auto-play, sorting, touch gestures
- **A11y Tests**: 31 tests for WCAG compliance using jest-axe
- **Hook Tests**: 47 tests for data fetching and sort state management

## File Structure

```
apps/web/src/
├── components/ui/data-display/
│   ├── game-carousel.tsx          # Main component
│   ├── game-carousel.stories.tsx  # Storybook stories
│   └── __tests__/
│       ├── game-carousel.test.tsx
│       └── game-carousel.a11y.test.tsx
├── hooks/
│   ├── useCarouselSort.ts        # Sort persistence hook
│   ├── queries/
│   │   └── useCarouselGames.ts   # Data fetching hooks
│   └── __tests__/
│       ├── useCarouselSort.test.tsx
│       └── useCarouselGames.test.tsx
```

## Related Documentation

- [MeepleCard Component](./meeple-card.md) - Card component used within carousel
- [Design System Cards](../../design-system/cards.md) - Design specifications
- [Storybook Guide](../../frontend/storybook-guide.md) - How to use Storybook

## Changelog

### v1.0.0 (Issue #3585)

- Initial implementation with 3D depth perspective
- TanStack Query integration for data fetching
- Sort controls with localStorage persistence
- Comprehensive accessibility support (WCAG 2.1 AA)
- Touch gesture support for mobile
- Auto-play with pause-on-hover
- Storybook documentation
- 122 tests across 4 test files
