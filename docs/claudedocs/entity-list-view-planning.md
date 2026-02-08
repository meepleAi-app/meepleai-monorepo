# EntityListView Component - Planning & Design

> Componente generico per visualizzazione liste di entità in 3 modalità: Grid, List, Carousel

**Issue Reference**: TBD
**Created**: 2026-02-08
**Status**: Planning

---

## 📐 Design Mockup

### Layout Overview

```
┌─────────────────────────────────────────────────────────────────┐
│ Featured Games                                    [🔍] [⚙️] [⊞] │  ← Header
├─────────────────────────────────────────────────────────────────┤
│ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ │
│ │   [IMG]     │ │   [IMG]     │ │   [IMG]     │ │   [IMG]     │ │
│ │             │ │             │ │             │ │             │ │  ← Grid Mode
│ │ Game Title  │ │ Game Title  │ │ Game Title  │ │ Game Title  │ │   (default)
│ │ Publisher   │ │ Publisher   │ │ Publisher   │ │ Publisher   │ │
│ │ ★★★★☆ 8.5   │ │ ★★★★☆ 8.5   │ │ ★★★★☆ 8.5   │ │ ★★★★☆ 8.5   │ │
│ └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘ │
│ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ │
│ │   [IMG]     │ │   [IMG]     │ │   [IMG]     │ │   [IMG]     │ │
│ │ ...         │ │ ...         │ │ ...         │ │ ...         │ │
│ └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### Header Breakdown

```
┌─────────────────────────────────────────────────────────────────┐
│ Featured Games                        [🔍 Search] [⚙️] [⊞ Grid] │
│                                         ↑          ↑    ↑        │
│                                      Search    Filter  View      │
│                                       Bar      Panel  Switcher   │
└─────────────────────────────────────────────────────────────────┘
```

**Legend**:
- `[🔍 Search]`: SearchBar component (optional)
- `[⚙️]`: Filter/Sort dropdown (optional)
- `[⊞ Grid]`: ViewModeSwitcher (3 modes: Grid/List/Carousel)

---

### View Mode: Grid

```
┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ GAME        │ │ GAME        │ │ GAME        │ │ GAME        │
│ ┌─────────┐ │ │ ┌─────────┐ │ │ ┌─────────┐ │ │ ┌─────────┐ │
│ │  [IMG]  │ │ │ │  [IMG]  │ │ │ │  [IMG]  │ │ │ │  [IMG]  │ │
│ │ 4/3     │ │ │ │ 4/3     │ │ │ │ 4/3     │ │ │ │ 4/3     │ │
│ └─────────┘ │ │ └─────────┘ │ │ └─────────┘ │ │ └─────────┘ │
│             │ │             │ │             │ │             │
│ Twilight    │ │ Gloomhaven  │ │ Wingspan    │ │ Azul        │
│ Imperium    │ │             │ │             │ │             │
│ FFG         │ │ Cephalofair │ │ Stonemaier  │ │ Plan B      │
│ ★★★★★ 8.7   │ │ ★★★★☆ 8.8   │ │ ★★★★☆ 8.1   │ │ ★★★★☆ 7.9   │
│ 👤 3-6 | 🕒 4h│ │ 👤 1-4 | 🕒 2h│ │ 👤 1-5 | 🕒 1h│ │ 👤 2-4 | 🕒 45m│
└─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘

Responsive: 1 col (mobile) → 2 cols (sm) → 3 cols (lg) → 4 cols (xl)
Card Variant: MeepleCard variant="grid"
Gap: gap-4 (16px)
```

---

### View Mode: List

```
┌─────────────────────────────────────────────────────────────────┐
│ GAME    [IMG]  Twilight Imperium          ★★★★★ 8.7  👤 3-6 🕒 4h│
│         64x64  Fantasy Flight Games                             │
├─────────────────────────────────────────────────────────────────┤
│ GAME    [IMG]  Gloomhaven                ★★★★☆ 8.8  👤 1-4 🕒 2h│
│         64x64  Cephalofair Games                                │
├─────────────────────────────────────────────────────────────────┤
│ GAME    [IMG]  Wingspan                  ★★★★☆ 8.1  👤 1-5 🕒 1h│
│         64x64  Stonemaier Games                                 │
├─────────────────────────────────────────────────────────────────┤
│ GAME    [IMG]  Azul                      ★★★★☆ 7.9  👤 2-4 🕒 45m│
│         64x64  Plan B Games                                     │
└─────────────────────────────────────────────────────────────────┘

Card Variant: MeepleCard variant="list"
Gap: space-y-2 (8px vertical)
Layout: Full width, horizontal flex
```

---

### View Mode: Carousel

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│        ┌────┐     ┌─────────┐     ┌────┐                       │
│        │ ◀  │  ←  │  [IMG]  │  →  │  ▶ │                       │
│        └────┘     │ Featured│     └────┘                       │
│                   │  Card   │                                   │
│                   │         │                                   │
│                   │Twilight │                                   │
│                   │Imperium │                                   │
│                   │  FFG    │                                   │
│                   │★★★★★ 8.7│                                   │
│                   └─────────┘                                   │
│                                                                 │
│              ● ○ ○ ○ ○  [⏸️]                                    │
│              Navigation Dots + AutoPlay                         │
└─────────────────────────────────────────────────────────────────┘

Component: GameCarousel (existing)
Height: h-[400px] (responsive)
Cards Visible: 1 (mobile) → 3 (tablet) → 5 (desktop)
Orientation: horizontal (desktop/tablet), vertical (mobile)
```

---

### ViewModeSwitcher Component

```
┌─────────────────────────┐
│  ⊞   ☰   ←→             │  ← Segmented Control
│ Grid List Carousel      │
│  ▔▔▔                    │  ← Active indicator (orange underline)
└─────────────────────────┘

States:
- Default: border-muted bg-muted/20
- Hover: bg-muted/40
- Active: bg-primary/10 text-primary border-b-2 border-primary
- Focus: ring-2 ring-ring

Icons:
- Grid: Grid3x3 (lucide-react)
- List: List (lucide-react)
- Carousel: ArrowLeftRight or GalleryHorizontal (lucide-react)
```

---

## 🎯 Component API Specification

### Core Props

```typescript
interface EntityListViewProps<T = any> {
  // ========== REQUIRED ==========
  /** Array of items to display */
  items: T[];

  /** Entity type for MeepleCard */
  entity: MeepleEntityType;

  /** Unique key for localStorage persistence */
  persistenceKey: string;

  // ========== VIEW MODE ==========
  /** Default view mode (if no localStorage value) */
  defaultViewMode?: 'grid' | 'list' | 'carousel';

  /** Controlled view mode (overrides internal state) */
  viewMode?: 'grid' | 'list' | 'carousel';

  /** Callback when view mode changes */
  onViewModeChange?: (mode: 'grid' | 'list' | 'carousel') => void;

  /** Enable/disable specific view modes */
  availableModes?: Array<'grid' | 'list' | 'carousel'>;

  // ========== ITEM RENDERING ==========
  /** Transform item to MeepleCard props */
  renderItem: (item: T) => Omit<MeepleCardProps, 'entity' | 'variant'>;

  /** Callback when item is clicked */
  onItemClick?: (item: T) => void;

  /** Custom className for cards */
  cardClassName?: string;

  // ========== GRID CONFIGURATION ==========
  /** Grid responsive columns */
  gridColumns?: {
    default?: number;
    sm?: number;
    md?: number;
    lg?: number;
    xl?: number;
    '2xl'?: number;
  };

  /** Grid gap spacing */
  gridGap?: 2 | 3 | 4 | 5 | 6 | 8;

  // ========== CAROUSEL CONFIGURATION ==========
  /** Carousel-specific options (passed to GameCarousel) */
  carouselOptions?: {
    autoPlay?: boolean;
    autoPlayInterval?: number;
    showDots?: boolean;
    orientation?: 'horizontal' | 'vertical' | 'auto';
  };

  // ========== SEARCH & FILTERS ==========
  /** Enable search bar */
  searchable?: boolean;

  /** Search placeholder text */
  searchPlaceholder?: string;

  /** Item fields to search (dot notation supported) */
  searchFields?: Array<keyof T | string>;

  /** Custom search function */
  onSearch?: (query: string, items: T[]) => T[];

  /** Sort options */
  sortOptions?: Array<{
    value: string;
    label: string;
    icon?: LucideIcon;
    compareFn: (a: T, b: T) => number;
  }>;

  /** Default sort option */
  defaultSort?: string;

  /** Controlled sort value */
  sort?: string;

  /** Callback when sort changes */
  onSortChange?: (sort: string) => void;

  /** Filter configuration */
  filters?: Array<FilterConfig<T>>;

  /** Callback when filters change */
  onFilterChange?: (filters: FilterState) => void;

  // ========== LAYOUT & STYLING ==========
  /** Section title */
  title?: string;

  /** Section subtitle */
  subtitle?: string;

  /** Show ViewModeSwitcher */
  showViewSwitcher?: boolean;

  /** Additional CSS classes */
  className?: string;

  /** Empty state message */
  emptyMessage?: string;

  /** Loading state */
  loading?: boolean;

  /** Test ID */
  'data-testid'?: string;
}
```

### FilterConfig Types

```typescript
type FilterConfig<T> =
  | SelectFilter<T>
  | CheckboxFilter<T>
  | RangeFilter<T>
  | DateRangeFilter<T>;

interface BaseFilter {
  id: string;
  label: string;
}

interface SelectFilter<T> extends BaseFilter {
  type: 'select';
  field: keyof T | string;
  options: Array<{ value: string; label: string }>;
  multiple?: boolean;
}

interface CheckboxFilter<T> extends BaseFilter {
  type: 'checkbox';
  field: keyof T | string;
}

interface RangeFilter<T> extends BaseFilter {
  type: 'range';
  field: keyof T | string;
  min: number;
  max: number;
  step?: number;
  unit?: string;
}

interface DateRangeFilter<T> extends BaseFilter {
  type: 'date-range';
  field: keyof T | string;
}

type FilterState = Record<string, any>;
```

---

## 📁 File Structure

```
apps/web/src/components/ui/data-display/
├── entity-list-view/
│   ├── index.ts                        # Public exports
│   ├── entity-list-view.tsx            # Main component
│   ├── entity-list-view.types.ts       # TypeScript types
│   ├── entity-list-view.stories.tsx    # Storybook stories
│   ├── entity-list-view.test.tsx       # Unit tests
│   │
│   ├── components/
│   │   ├── view-mode-switcher.tsx      # View mode UI control
│   │   ├── search-bar.tsx              # Search input with debounce
│   │   ├── sort-dropdown.tsx           # Sort selection dropdown
│   │   ├── filter-panel.tsx            # Filter controls panel
│   │   ├── empty-state.tsx             # Empty list message
│   │   └── loading-skeleton.tsx        # Loading state
│   │
│   ├── hooks/
│   │   ├── use-view-mode.ts            # View mode state + persistence
│   │   ├── use-search.ts               # Search logic + debounce
│   │   ├── use-sort.ts                 # Sort state management
│   │   └── use-filters.ts              # Filter state + application
│   │
│   ├── utils/
│   │   ├── search-utils.ts             # Search algorithm (fuzzy, exact)
│   │   ├── sort-utils.ts               # Common sort comparators
│   │   └── filter-utils.ts             # Filter application logic
│   │
│   └── __tests__/
│       ├── view-mode-switcher.test.tsx
│       ├── search-bar.test.tsx
│       ├── use-view-mode.test.ts
│       └── integration.test.tsx         # Full component integration
```

---

## 🔧 Implementation Plan

### Phase 1: Core Foundation (Day 1-2)

**Task 1.1: useLocalStorage Hook**
```typescript
// apps/web/src/hooks/useLocalStorage.ts
export function useLocalStorage<T>(
  key: string,
  defaultValue: T
): [T, (value: T | ((prev: T) => T)) => void] {
  // SSR-safe implementation
  // JSON serialization
  // Error handling (quota exceeded, etc.)
}
```

**Task 1.2: useViewMode Hook**
```typescript
// apps/web/src/components/ui/data-display/entity-list-view/hooks/use-view-mode.ts
export function useViewMode(
  persistenceKey: string,
  defaultMode: ViewMode,
  availableModes: ViewMode[]
) {
  const [mode, setMode] = useLocalStorage<ViewMode>(
    `view-mode:${persistenceKey}`,
    defaultMode
  );

  // Validation logic
  // Controlled/uncontrolled support
  return { mode, setMode, isAvailable };
}
```

**Task 1.3: ViewModeSwitcher Component**
```typescript
// Segmented control UI
// Keyboard navigation (Arrow keys)
// ARIA attributes (role="radiogroup")
// Icons from lucide-react
```

**Deliverables**:
- ✅ useLocalStorage hook + tests
- ✅ useViewMode hook + tests
- ✅ ViewModeSwitcher component + Storybook
- ✅ Basic EntityListView scaffold (Grid mode only)

---

### Phase 2: View Modes Integration (Day 2-3)

**Task 2.1: Grid Layout**
```typescript
// Responsive grid with TailwindCSS
// MeepleCard variant="grid"
// Configurable columns via gridColumns prop
```

**Task 2.2: List Layout**
```typescript
// Vertical stack with space-y
// MeepleCard variant="list"
// Full-width cards
```

**Task 2.3: Carousel Integration**
```typescript
// Wrap existing GameCarousel component
// Transform items to CarouselGame format
// Pass through carouselOptions
```

**Task 2.4: View Mode Persistence**
```typescript
// Load from localStorage on mount
// Save on view mode change
// Namespace by persistenceKey
```

**Deliverables**:
- ✅ All 3 view modes functional
- ✅ Smooth transitions between modes
- ✅ localStorage persistence working
- ✅ Responsive behavior tested

---

### Phase 3: Search & Sort (Day 3-4)

**Task 3.1: SearchBar Component**
```typescript
// Input with search icon
// Debounce 300ms
// Clear button (X icon)
// Keyboard shortcuts (⌘K to focus)
```

**Task 3.2: useSearch Hook**
```typescript
export function useSearch<T>(
  items: T[],
  searchFields: string[],
  customSearch?: (query: string, items: T[]) => T[]
) {
  const [query, setQuery] = useState('');
  const debouncedQuery = useDebounce(query, 300);

  const filteredItems = useMemo(() => {
    if (!debouncedQuery) return items;

    if (customSearch) {
      return customSearch(debouncedQuery, items);
    }

    return defaultSearch(items, debouncedQuery, searchFields);
  }, [items, debouncedQuery, searchFields, customSearch]);

  return { query, setQuery, filteredItems };
}
```

**Task 3.3: SortDropdown Component**
```typescript
// Reuse GameCarousel sort pattern
// Generic for any sort options
// Icons from lucide-react
```

**Task 3.4: useSort Hook**
```typescript
export function useSort<T>(
  items: T[],
  sortOptions: SortOption<T>[],
  defaultSort: string
) {
  const [currentSort, setCurrentSort] = useState(defaultSort);

  const sortedItems = useMemo(() => {
    const option = sortOptions.find(o => o.value === currentSort);
    if (!option) return items;

    return [...items].sort(option.compareFn);
  }, [items, currentSort, sortOptions]);

  return { currentSort, setCurrentSort, sortedItems };
}
```

**Deliverables**:
- ✅ SearchBar with debounce
- ✅ Default fuzzy search algorithm
- ✅ SortDropdown component
- ✅ Search + Sort integration in EntityListView

---

### Phase 4: Filters (Day 4-5)

**Task 4.1: FilterPanel Component**
```typescript
// Collapsible panel (shadcn/ui Collapsible)
// Filter chips display (active filters)
// Clear all button
```

**Task 4.2: Individual Filter Components**
```typescript
// SelectFilter: shadcn/ui Select
// CheckboxFilter: shadcn/ui Checkbox
// RangeFilter: shadcn/ui Slider
// DateRangeFilter: shadcn/ui Calendar + Popover
```

**Task 4.3: useFilters Hook**
```typescript
export function useFilters<T>(
  items: T[],
  filterConfig: FilterConfig<T>[]
) {
  const [filterState, setFilterState] = useState<FilterState>({});

  const filteredItems = useMemo(() => {
    return applyFilters(items, filterState, filterConfig);
  }, [items, filterState, filterConfig]);

  const clearFilters = () => setFilterState({});
  const activeFiltersCount = Object.keys(filterState).length;

  return {
    filterState,
    setFilterState,
    filteredItems,
    clearFilters,
    activeFiltersCount
  };
}
```

**Deliverables**:
- ✅ FilterPanel with collapsible UI
- ✅ 4 filter types implemented
- ✅ Filter application logic
- ✅ Active filter chips display

---

### Phase 5: Polish & Testing (Day 5-6)

**Task 5.1: Accessibility**
- Keyboard navigation (Tab, Arrow keys, Enter, Escape)
- ARIA labels and roles
- Focus management
- Screen reader announcements

**Task 5.2: Performance**
- React.memo for components
- useMemo for heavy computations
- Consider virtualization for 100+ items (react-window)
- Lazy loading for carousel images

**Task 5.3: Testing**
```typescript
// Unit tests
- useLocalStorage hook
- useViewMode hook
- useSearch hook
- useSort hook
- useFilters hook

// Component tests
- ViewModeSwitcher
- SearchBar
- SortDropdown
- FilterPanel
- EntityListView

// Integration tests
- Full user flow (switch modes, search, sort, filter)
- Persistence across page reload
- Empty states
- Loading states
- Error states
```

**Task 5.4: Documentation**
- Storybook stories for all components
- Usage examples in docs
- API reference
- Migration guide (from old patterns)

**Deliverables**:
- ✅ 90%+ test coverage
- ✅ WCAG AA accessibility
- ✅ Complete Storybook documentation
- ✅ Performance benchmarks

---

## 🎨 Usage Examples

### Example 1: Simple Grid/List/Carousel

```typescript
import { EntityListView } from '@/components/ui/data-display/entity-list-view';

function GamesPage() {
  const { data: games, isLoading } = useGames();

  return (
    <EntityListView
      items={games}
      entity="game"
      persistenceKey="games-browse"
      defaultViewMode="grid"

      renderItem={(game) => ({
        id: game.id,
        title: game.title,
        subtitle: game.publisher,
        imageUrl: game.coverUrl,
        rating: game.averageRating,
        ratingMax: 10,
        metadata: [
          { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
          { icon: Clock, value: `${game.playtime}m` },
        ],
      })}

      onItemClick={(game) => router.push(`/games/${game.id}`)}

      loading={isLoading}
      title="Featured Games"
      subtitle="Explore our collection"
    />
  );
}
```

---

### Example 2: With Search & Sort

```typescript
function GamesPage() {
  const { data: games } = useGames();

  return (
    <EntityListView
      items={games}
      entity="game"
      persistenceKey="games-browse"

      // Search
      searchable
      searchPlaceholder="Search games by name or publisher..."
      searchFields={['title', 'publisher', 'designer']}

      // Sort
      sortOptions={[
        {
          value: 'rating',
          label: 'Rating',
          icon: Star,
          compareFn: (a, b) => b.averageRating - a.averageRating
        },
        {
          value: 'name',
          label: 'Name',
          icon: ArrowDownAZ,
          compareFn: (a, b) => a.title.localeCompare(b.title)
        },
        {
          value: 'year',
          label: 'Year',
          icon: Calendar,
          compareFn: (a, b) => b.yearPublished - a.yearPublished
        },
      ]}
      defaultSort="rating"

      renderItem={(game) => ({ /* ... */ })}
      onItemClick={(game) => router.push(`/games/${game.id}`)}
    />
  );
}
```

---

### Example 3: With Filters

```typescript
function GamesPage() {
  const { data: games } = useGames();
  const categories = useGameCategories(); // ['Strategy', 'Party', ...]

  return (
    <EntityListView
      items={games}
      entity="game"
      persistenceKey="games-browse"

      searchable
      sortOptions={GAME_SORT_OPTIONS}

      // Filters
      filters={[
        {
          id: 'category',
          type: 'select',
          label: 'Category',
          field: 'category',
          options: categories.map(c => ({ value: c, label: c })),
          multiple: true,
        },
        {
          id: 'players',
          type: 'range',
          label: 'Players',
          field: 'maxPlayers',
          min: 1,
          max: 10,
          unit: 'players',
        },
        {
          id: 'owned',
          type: 'checkbox',
          label: 'Only My Games',
          field: 'isOwned',
        },
        {
          id: 'year',
          type: 'date-range',
          label: 'Publication Year',
          field: 'yearPublished',
        },
      ]}

      renderItem={(game) => ({ /* ... */ })}
      onItemClick={(game) => router.push(`/games/${game.id}`)}
    />
  );
}
```

---

### Example 4: Custom Grid Layout

```typescript
function GamesPage() {
  return (
    <EntityListView
      items={games}
      entity="game"
      persistenceKey="games-browse"

      // Custom responsive columns
      gridColumns={{
        default: 1,  // Mobile: 1 column
        sm: 2,       // Small: 2 columns
        md: 2,       // Medium: 2 columns (tablets vertical)
        lg: 3,       // Large: 3 columns
        xl: 4,       // XL: 4 columns
        '2xl': 5,    // 2XL: 5 columns
      }}

      gridGap={6}  // Larger gap (24px)

      renderItem={(game) => ({ /* ... */ })}
    />
  );
}
```

---

### Example 5: Carousel Configuration

```typescript
function FeaturedGamesCarousel() {
  return (
    <EntityListView
      items={featuredGames}
      entity="game"
      persistenceKey="home-featured"

      defaultViewMode="carousel"
      availableModes={['carousel']} // Lock to carousel only

      carouselOptions={{
        autoPlay: true,
        autoPlayInterval: 5000,
        showDots: true,
        orientation: 'auto', // Horizontal desktop, vertical mobile
      }}

      renderItem={(game) => ({ /* ... */ })}
      onItemClick={(game) => router.push(`/games/${game.id}`)}

      title="Featured This Week"
      showViewSwitcher={false} // Hide switcher (carousel only)
    />
  );
}
```

---

## ⚡ Performance Considerations

### 1. Virtualization (Optional, Phase 2+)

For lists with 100+ items, consider virtualization:

```typescript
import { useVirtualizer } from '@tanstack/react-virtual';

// Inside EntityListView (Grid mode)
const parentRef = useRef<HTMLDivElement>(null);

const virtualizer = useVirtualizer({
  count: filteredItems.length,
  getScrollElement: () => parentRef.current,
  estimateSize: () => 320, // Card height estimate
  overscan: 5,
});
```

**When to enable**:
- ✅ 100+ items in list
- ✅ Performance issues detected
- ❌ Small lists (<50 items) - overhead not worth it

---

### 2. Memoization Strategy

```typescript
// EntityListView.tsx
export const EntityListView = React.memo(function EntityListView<T>({
  items,
  // ... props
}: EntityListViewProps<T>) {
  // Memoize expensive computations
  const filteredItems = useMemo(() => {
    let result = items;

    // Apply search
    if (searchQuery) {
      result = searchItems(result, searchQuery, searchFields);
    }

    // Apply filters
    if (activeFilters) {
      result = applyFilters(result, activeFilters);
    }

    // Apply sort
    if (sortOption) {
      result = [...result].sort(sortOption.compareFn);
    }

    return result;
  }, [items, searchQuery, activeFilters, sortOption]);

  // ...
});
```

---

### 3. Debouncing & Throttling

```typescript
// SearchBar.tsx
const debouncedSearch = useMemo(
  () => debounce((query: string) => {
    onSearch(query);
  }, 300),
  [onSearch]
);

// Filter changes
const throttledFilterUpdate = useMemo(
  () => throttle((filters: FilterState) => {
    onFilterChange(filters);
  }, 150),
  [onFilterChange]
);
```

---

### 4. Image Loading Optimization

```typescript
// In renderItem
renderItem={(game) => ({
  imageUrl: game.coverUrl,
  // MeepleCard already uses Next.js Image with:
  // - Lazy loading
  // - Responsive sizes
  // - Blur placeholder
  // No additional optimization needed
})}
```

---

## 🧪 Test Strategy

### Unit Tests

```typescript
describe('useViewMode', () => {
  it('should initialize with defaultMode', () => {
    const { result } = renderHook(() =>
      useViewMode('test-key', 'grid', ['grid', 'list', 'carousel'])
    );
    expect(result.current.mode).toBe('grid');
  });

  it('should persist mode to localStorage', () => {
    const { result } = renderHook(() =>
      useViewMode('test-key', 'grid', ['grid', 'list', 'carousel'])
    );

    act(() => {
      result.current.setMode('list');
    });

    expect(localStorage.getItem('view-mode:test-key')).toBe('"list"');
  });

  it('should restore mode from localStorage', () => {
    localStorage.setItem('view-mode:test-key', '"carousel"');

    const { result } = renderHook(() =>
      useViewMode('test-key', 'grid', ['grid', 'list', 'carousel'])
    );

    expect(result.current.mode).toBe('carousel');
  });
});

describe('useSearch', () => {
  const items = [
    { id: 1, title: 'Twilight Imperium', publisher: 'FFG' },
    { id: 2, title: 'Gloomhaven', publisher: 'Cephalofair' },
  ];

  it('should filter items by search query', () => {
    const { result } = renderHook(() =>
      useSearch(items, ['title', 'publisher'])
    );

    act(() => {
      result.current.setQuery('twilight');
    });

    // Wait for debounce
    await waitFor(() => {
      expect(result.current.filteredItems).toHaveLength(1);
      expect(result.current.filteredItems[0].title).toBe('Twilight Imperium');
    });
  });
});
```

---

### Component Tests

```typescript
describe('EntityListView', () => {
  const mockGames = [/* ... */];

  it('should render grid mode by default', () => {
    render(
      <EntityListView
        items={mockGames}
        entity="game"
        persistenceKey="test"
        renderItem={(game) => ({ title: game.title })}
      />
    );

    expect(screen.getAllByTestId('meeple-card')).toHaveLength(mockGames.length);
    expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
  });

  it('should switch to list mode when clicked', async () => {
    render(<EntityListView {...props} />);

    const listButton = screen.getByRole('button', { name: /list/i });
    await userEvent.click(listButton);

    expect(screen.getByTestId('list-layout')).toBeInTheDocument();
    expect(localStorage.getItem('view-mode:test')).toBe('"list"');
  });

  it('should filter items by search query', async () => {
    render(<EntityListView {...props} searchable />);

    const searchInput = screen.getByPlaceholderText(/search/i);
    await userEvent.type(searchInput, 'twilight');

    await waitFor(() => {
      expect(screen.getAllByTestId('meeple-card')).toHaveLength(1);
    });
  });
});
```

---

### Integration Tests

```typescript
describe('EntityListView - Full Flow', () => {
  it('should persist view mode across page reload', async () => {
    const { unmount } = render(<EntityListView {...props} />);

    // Switch to list mode
    await userEvent.click(screen.getByRole('button', { name: /list/i }));

    unmount();

    // Remount component
    render(<EntityListView {...props} />);

    // Should still be in list mode
    expect(screen.getByTestId('list-layout')).toBeInTheDocument();
  });

  it('should combine search, sort, and filter', async () => {
    render(
      <EntityListView
        {...props}
        searchable
        sortOptions={SORT_OPTIONS}
        filters={FILTER_CONFIG}
      />
    );

    // Search
    await userEvent.type(screen.getByPlaceholderText(/search/i), 'strategy');

    // Sort by rating
    await userEvent.click(screen.getByRole('button', { name: /sort/i }));
    await userEvent.click(screen.getByRole('option', { name: /rating/i }));

    // Filter by player count
    const playerSlider = screen.getByLabelText(/players/i);
    fireEvent.change(playerSlider, { target: { value: 4 } });

    // Verify combined filters applied
    await waitFor(() => {
      const cards = screen.getAllByTestId('meeple-card');
      expect(cards.length).toBeGreaterThan(0);
      // Additional assertions on filtered + sorted results
    });
  });
});
```

---

## 📊 Success Metrics

### Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Initial Render | < 200ms | Time to interactive |
| View Mode Switch | < 50ms | Mode change latency |
| Search Debounce | 300ms | Input → filter update |
| Memory Usage | < 10MB | Component overhead |
| Lighthouse Score | > 95 | Performance audit |

### Test Coverage

| Area | Target |
|------|--------|
| Unit Tests | > 90% |
| Component Tests | > 85% |
| Integration Tests | > 70% |
| E2E Tests | Critical flows |

### Accessibility

- ✅ WCAG 2.1 AA Compliant
- ✅ Keyboard navigable
- ✅ Screen reader compatible
- ✅ Color contrast 4.5:1 minimum

---

## 🚀 Next Steps

1. **Review & Approval**:
   - Review API design
   - Validate mockup layout
   - Confirm priorities

2. **Create Tasks**:
   - GitHub issues for each phase
   - Assign to sprint/milestone

3. **Prototype**:
   - Quick prototype of ViewModeSwitcher UI
   - User testing with stakeholders

4. **Implementation**:
   - Start Phase 1 (Foundation)
   - Daily progress updates

---

## 📚 References

- [MeepleCard Component](../../apps/web/src/components/ui/data-display/meeple-card.tsx)
- [GameCarousel Component](../../apps/web/src/components/ui/data-display/game-carousel.tsx)
- [shadcn/ui Documentation](https://ui.shadcn.com)
- [Tailwind CSS Grid](https://tailwindcss.com/docs/grid-template-columns)
- [React Virtualization](https://tanstack.com/virtual/latest)
