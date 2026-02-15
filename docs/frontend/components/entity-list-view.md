# EntityListView Component

> Generic multi-view list component for displaying entities in Grid, List, and Carousel modes.

**Location**: `components/ui/data-display/entity-list-view/`
**Issue**: #3894 (Test Coverage & Polish)
**Epic**: #3820 (MeepleCard System)

## Overview

EntityListView is a fully-featured, generic list component that wraps MeepleCard with:
- **3 view modes**: Grid, List, Carousel (with localStorage persistence)
- **Search**: Fuzzy search with debounce across configurable fields
- **Sort**: Configurable sort options with compare functions
- **Filters**: Select, Checkbox, Range, and DateRange filter types
- **Accessibility**: WCAG 2.1 AA compliant (axe-core verified)

## Quick Start

```tsx
import { EntityListView } from '@/components/ui/data-display/entity-list-view';

<EntityListView
  items={games}
  entity="game"
  persistenceKey="games-browse"
  renderItem={(game) => ({
    id: game.id,
    title: game.title,
    subtitle: game.publisher,
    imageUrl: game.coverUrl,
    rating: game.averageRating,
    ratingMax: 10,
  })}
  onItemClick={(game) => router.push(`/games/${game.id}`)}
  title="Featured Games"
/>
```

## Props Reference

### Required Props

| Prop | Type | Description |
|------|------|-------------|
| `items` | `T[]` | Array of items to display |
| `entity` | `MeepleEntityType` | Entity type for MeepleCard color scheme (`game`, `player`, `collection`, `event`) |
| `persistenceKey` | `string` | Unique key for localStorage persistence |
| `renderItem` | `(item: T) => MeepleCardProps` | Transform item to MeepleCard props |

### View Mode Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `defaultViewMode` | `ViewMode` | `'grid'` | Default view mode |
| `viewMode` | `ViewMode` | - | Controlled view mode |
| `onViewModeChange` | `(mode: ViewMode) => void` | - | Mode change callback |
| `availableModes` | `ViewMode[]` | `['grid', 'list', 'carousel']` | Which modes to show |
| `showViewSwitcher` | `boolean` | `true` | Show/hide the view mode switcher |

### Search & Sort Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `searchable` | `boolean` | `false` | Enable search bar |
| `searchPlaceholder` | `string` | `'Search...'` | Search input placeholder |
| `searchFields` | `string[]` | `[]` | Fields to search (supports dot notation) |
| `onSearch` | `(query, items) => T[]` | - | Custom search function |
| `sortOptions` | `SortOption<T>[]` | `[]` | Sort configurations |
| `defaultSort` | `string` | - | Default sort option value |
| `sort` | `string` | - | Controlled sort value |
| `onSortChange` | `(sort: string) => void` | - | Sort change callback |

### Filter Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `filters` | `FilterConfig<T>[]` | `[]` | Filter configurations |
| `onFilterChange` | `(state: FilterState) => void` | - | Filter change callback |

### Grid & Layout Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `gridColumns` | `GridColumns` | `{default:1, sm:2, lg:3, xl:4}` | Responsive column config |
| `gridGap` | `2\|3\|4\|5\|6\|8` | `4` | Grid gap (Tailwind scale) |
| `carouselOptions` | `CarouselOptions` | - | Carousel configuration |
| `title` | `string` | - | Section title |
| `subtitle` | `string` | - | Section subtitle |
| `className` | `string` | - | Additional CSS classes |
| `cardClassName` | `string` | - | CSS class for individual cards |
| `emptyMessage` | `string` | `'No items to display'` | Empty state message |
| `loading` | `boolean` | `false` | Loading state |
| `onItemClick` | `(item: T) => void` | - | Item click callback |

## Filter Types

### SelectFilter

```tsx
{
  id: 'category',
  type: 'select',
  label: 'Category',
  field: 'category',
  options: [
    { value: 'strategy', label: 'Strategy' },
    { value: 'party', label: 'Party' },
  ],
  multiple: false, // single-select by default
}
```

### CheckboxFilter

```tsx
{
  id: 'hasPdf',
  type: 'checkbox',
  label: 'Has PDF',
  field: 'hasPdf',
}
```

### RangeFilter

```tsx
{
  id: 'rating',
  type: 'range',
  label: 'Rating',
  field: 'rating',
  min: 0,
  max: 10,
  step: 0.1,
  unit: 'stars',
}
```

### DateRangeFilter

```tsx
{
  id: 'createdAt',
  type: 'date-range',
  label: 'Date Added',
  field: 'createdAt',
}
```

## Full Example with Search, Sort, and Filters

```tsx
import { EntityListView } from '@/components/ui/data-display/entity-list-view';
import type { SortOption, FilterConfig } from '@/components/ui/data-display/entity-list-view';
import { Star, ArrowDownAZ } from 'lucide-react';

interface Game {
  id: string;
  title: string;
  publisher: string;
  rating: number;
  category: string;
  hasPdf: boolean;
}

const sortOptions: SortOption<Game>[] = [
  { value: 'rating', label: 'Rating', icon: Star, compareFn: (a, b) => b.rating - a.rating },
  { value: 'name', label: 'Name', icon: ArrowDownAZ, compareFn: (a, b) => a.title.localeCompare(b.title) },
];

const filters: FilterConfig<Game>[] = [
  {
    id: 'category',
    type: 'select',
    label: 'Category',
    field: 'category',
    options: [
      { value: 'strategy', label: 'Strategy' },
      { value: 'party', label: 'Party' },
    ],
  },
  { id: 'hasPdf', type: 'checkbox', label: 'Has PDF', field: 'hasPdf' },
  { id: 'rating', type: 'range', label: 'Rating', field: 'rating', min: 0, max: 10, step: 0.1 },
];

<EntityListView<Game>
  items={games}
  entity="game"
  persistenceKey="games-dashboard"
  renderItem={(game) => ({
    id: game.id,
    title: game.title,
    subtitle: game.publisher,
    rating: game.rating,
    ratingMax: 10,
  })}
  searchable
  searchFields={['title', 'publisher']}
  sortOptions={sortOptions}
  defaultSort="rating"
  filters={filters}
  title="Game Collection"
  subtitle="Browse and filter your games"
  onItemClick={(game) => router.push(`/games/${game.id}`)}
/>
```

## Data Pipeline

Items flow through a search → filter → sort pipeline:

```
items (raw)
  → useSearch(items, query, fields)     // fuzzy search
  → useFilters(searchedItems, config)   // apply active filters
  → useSort(filteredItems, options)     // sort by selected option
  → displayItems (rendered)
```

## Architecture

```
entity-list-view/
├── entity-list-view.tsx          # Main component
├── entity-list-view.types.ts     # TypeScript types
├── index.ts                      # Barrel export
├── components/
│   ├── empty-state.tsx           # Empty state UI
│   ├── filter-panel.tsx          # Filter controls panel
│   ├── loading-skeleton.tsx      # Loading skeletons
│   ├── search-bar.tsx            # Search input with Ctrl+K
│   ├── sort-dropdown.tsx         # Sort option dropdown
│   ├── view-mode-switcher.tsx    # Grid/List/Carousel toggle
│   └── filters/
│       ├── select-filter.tsx     # Select dropdown filter
│       ├── checkbox-filter.tsx   # Boolean toggle filter
│       ├── range-filter.tsx      # Numeric slider filter
│       ├── date-range-filter.tsx # Date picker filter
│       └── filter-chip.tsx       # Active filter chip
├── hooks/
│   ├── use-debounce.ts           # Debounce utility
│   ├── use-filters.ts            # Filter state management
│   ├── use-search.ts             # Fuzzy search logic
│   ├── use-sort.ts               # Sort state management
│   └── use-view-mode.ts          # View mode + localStorage
├── utils/
│   ├── filter-utils.ts           # Filter application logic
│   └── search-utils.ts           # Fuzzy search algorithm
└── __tests__/
    ├── entity-list-view.test.tsx  # Core component tests
    ├── integration.test.tsx       # Mode persistence tests
    ├── filters.test.tsx           # Filter component tests
    ├── accessibility.test.tsx     # WCAG 2.1 AA audit
    └── performance.test.tsx       # Performance benchmarks
```

## Test Coverage

- **208 tests** across 12 test files (including debounce/search/sort unit tests)
- Performance: 100 items renders in <3s (jsdom), search 1000 items in <100ms
- Accessibility: axe-core passes on all view modes and states
- Keyboard: Tab navigation, Ctrl+K search focus, Escape to close, arrow key navigation

## Migration Guide

### From CollectionGrid

CollectionGrid was removed in Issue #3894 as it was unused by production code (CollectionDashboard renders MeepleCards directly). No migration needed.

### From GameGrid

GameGrid cannot be directly migrated to EntityListView because:
1. It uses `MeepleGameCatalogCard` (specialized adapter with library hooks like "Add to Library"), not `MeepleCard`
2. Its parent (`/games` page) is a Server Component with server-side data fetching and pagination
3. View mode is URL-driven (`?view=grid`), not localStorage-driven

**Future migration path**: Add a `renderCard` prop to EntityListView that allows custom card rendering instead of the default MeepleCard.

### From Custom Grid Implementations

Replace custom grid/list rendering with EntityListView:

```tsx
// Before: Custom implementation
<div className="grid grid-cols-4 gap-4">
  {sortedGames.map(game => (
    <MeepleCard entity="game" title={game.title} ... />
  ))}
</div>

// After: EntityListView
<EntityListView
  items={games}
  entity="game"
  persistenceKey="my-games"
  renderItem={(game) => ({ id: game.id, title: game.title, ... })}
  sortOptions={sortOptions}
  searchable
  searchFields={['title']}
/>
```

## Accessibility

- `role="region"` with `aria-label` on the main section
- `role="radiogroup"` for view mode switcher with keyboard arrow key support
- `role="searchbox"` with `aria-label` for search input
- `aria-live="polite"` announcements for item count and view mode changes
- Ctrl+K keyboard shortcut for search focus
- No focus traps, visible focus indicators on all interactive elements
- Tested with axe-core on all view modes and states (grid, list, empty, loading)
