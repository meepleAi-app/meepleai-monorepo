# MeepleAI - Frontend (Next.js)

Componenti UI, layout e navigazione, dashboard admin, design token, MeepleCard, storybook.

**Data generazione**: 8 marzo 2026

**File inclusi**: 20

---

## Indice

1. frontend/README.md
2. frontend/components/batch-jobs.md
3. frontend/components/entity-list-view.md
4. frontend/components/game-carousel.md
5. frontend/components/meeple-card-v2-migration.md
6. frontend/components/meeple-card.md
7. frontend/dashboard-collection-centric-option-a.md
8. frontend/dashboard-hub-implementation-plan.md
9. frontend/DASHBOARD-HUB-INDEX.md
10. frontend/DASHBOARD-HUB-QUICK-REFERENCE.md
11. frontend/dashboard-overview-hub.md
12. frontend/entity-link-card-relationships.md
13. frontend/game-session-toolkit-technical.md
14. frontend/layout-components.md
15. frontend/layout-spec.md
16. frontend/layout-wireframes.md
17. frontend/meeple-card-v2-design-tokens.md
18. frontend/migrations/DASHBOARD-HUB-MIGRATION-GUIDE.md
19. frontend/navigability-analysis.md
20. frontend/storybook-guide.md

---



<div style="page-break-before: always;"></div>

## frontend/README.md

# Frontend Documentation

**Next.js 14 App Router + React 18**

---

## Quick Start

**Prerequisites**:
- Node.js 20+
- pnpm 8+

**Setup**:
*(blocco di codice rimosso)*

---

## Architecture

### Tech Stack

**Framework**:
- Next.js 14 (App Router)
- React 18 (Server + Client Components)
- TypeScript 5

**UI**:
- Tailwind CSS 3
- shadcn/ui components
- Radix UI primitives
- Lucide icons

**State Management**:
- Zustand (client state)
- React Query (server state)
- React Hook Form (forms)

**Routing**:
- File-based routing (`src/app/`)
- Route groups for organization
- Server-side data fetching

---

## Project Structure

*(blocco di codice rimosso)*

---

## Development Workflow

### Adding Features

1. **Create Component**:
*(blocco di codice rimosso)*

2. **Add Route**:
*(blocco di codice rimosso)*

3. **Add Tests**:
*(blocco di codice rimosso)*

---

## State Management Patterns

### Zustand Store

*(blocco di codice rimosso)*

### React Query

*(blocco di codice rimosso)*

---

## Testing

### Unit Tests (Vitest)

*(blocco di codice rimosso)*

### E2E Tests (Playwright)

*(blocco di codice rimosso)*

**Target**: 85%+ coverage

---

## Performance Optimization

### Server Components

Use Server Components by default, Client Components only when needed:

*(blocco di codice rimosso)*

### Image Optimization

*(blocco di codice rimosso)*

### Code Splitting

*(blocco di codice rimosso)*

---

## Styling

### Tailwind Conventions

**Spacing**: Use Tailwind spacing scale (4px increments)
*(blocco di codice rimosso)*

**Colors**: Use theme colors from `tailwind.config.ts`
*(blocco di codice rimosso)*

**Responsive**: Mobile-first approach
*(blocco di codice rimosso)*

### shadcn/ui Components

*(blocco di codice rimosso)*

---

## API Integration

### API Clients

*(blocco di codice rimosso)*

### Error Handling

*(blocco di codice rimosso)*

---

## Code Standards

**Naming**:
- PascalCase: Components, types
- camelCase: Functions, variables
- UPPER_SNAKE_CASE: Constants

**File Organization**:
- One component per file
- Co-locate tests with components
- Group by feature, not by type

**Type Safety**:
- Use TypeScript strict mode
- Avoid `any`, prefer `unknown`
- Define prop interfaces

---

## Related Documentation

- [API Documentation](../03-api/README.md)
- [Testing Guide](../05-testing/README.md)
- [User Flows](../11-user-flows/README.md)
- [Development Guide](../02-development/README.md)

---

**Last Updated**: 2026-01-31
**Maintainer**: Frontend Team


---



<div style="page-break-before: always;"></div>

## frontend/components/batch-jobs.md

# Batch Jobs Components

**Issue**: #3693 - Batch Job System
**Location**: `apps/web/src/components/admin/enterprise/batch-jobs/`
**Status**: Completed

## Overview

React components for managing background batch jobs in the Enterprise Admin section. Provides a complete UI for viewing, creating, monitoring, and managing asynchronous processing tasks.

## Architecture

### Component Hierarchy

*(blocco di codice rimosso)*

## Components

### 1. BatchJobsTab.tsx

**Purpose**: Main container component for batch job management.

**Features**:
- Fetches jobs from API with pagination
- Status filtering (All, Queued, Running, Completed, Failed, Cancelled)
- Auto-refresh every 5s for running/queued jobs
- Manual refresh button
- Create job button
- Pagination controls

**API Integration**:
*(blocco di codice rimosso)*

**State Management**:
- Jobs list with pagination
- Status filter
- Loading states
- Selected job for detail view
- Create modal visibility

### 2. BatchJobQueueViewer.tsx

**Purpose**: Table component displaying batch jobs with status and actions.

**Columns**:
| Column | Description |
|--------|-------------|
| Type | Job type (ResourceForecast, CostAnalysis, etc.) |
| Status | Badge with color coding (Queued, Running, Completed, Failed, Cancelled) |
| Progress | Progress bar (0-100%) for running jobs |
| Duration | Time taken (formatted: Xs, Xm Ys, Xh Ym) |
| Created | Creation timestamp |
| Actions | Cancel, Retry, Delete buttons |

**Status Badges**:
- **Queued**: Gray badge with clock icon
- **Running**: Blue badge with play icon (animated)
- **Completed**: Green badge with check icon
- **Failed**: Red badge with alert icon
- **Cancelled**: Yellow badge with X icon

**Actions**:
- **Cancel**: Available for Queued/Running jobs
- **Retry**: Available for Failed jobs
- **Delete**: Available for all jobs (with confirmation)

**Interactions**:
- Row click opens JobDetailModal
- Action buttons stop event propagation
- Loading states for async actions

### 3. JobDetailModal.tsx

**Purpose**: Detailed view of a single batch job with comprehensive information.

**Sections**:

#### Job Info
- Status with animated icon
- Duration (formatted)
- Created timestamp
- Started timestamp
- Completed timestamp
- Progress percentage

#### Parameters
- JSON pretty-print of job configuration
- Collapsible pre-formatted code block

#### Results (Completed jobs only)
- JSON pretty-print of job results
- Download results button
- Summary information

#### Error Details (Failed jobs only)
- Error message display
- Red-highlighted error box
- Stack trace (if available)

#### Logs
- Real-time log streaming for running jobs
- Color-coded log levels (INFO, ERROR)
- Timestamp formatting
- Auto-scroll to latest
- Mock implementation (ready for real-time integration)

**Actions**:
- **Retry**: For failed jobs (creates new job)
- **Cancel**: For queued/running jobs
- **Close**: Close modal

**Auto-Refresh**:
- Polls every 3s for running jobs
- Automatically updates status changes
- Stops polling when job completes

### 4. CreateJobModal.tsx

**Purpose**: Form for creating new batch jobs with type selection and parameter configuration.

**Job Types**:
| Type | Label | Description |
|------|-------|-------------|
| ResourceForecast | Resource Forecast | Predict future resource usage and requirements |
| CostAnalysis | Cost Analysis | Analyze cost trends and optimization opportunities |
| DataCleanup | Data Cleanup | Remove old or unused data to optimize storage |
| BggSync | BGG Sync | Synchronize data with BoardGameGeek catalog |
| AgentBenchmark | Agent Benchmark | Run performance benchmarks for AI agents |

**Form Fields**:

#### Job Type Selector
- Dropdown with all available job types
- Shows description on selection
- Required field

#### Parameters Editor
- JSON textarea with syntax validation
- Real-time validation
- Error highlighting
- Optional field (defaults to empty)
- Example parameters shown below

**Validation**:
- Job type required
- Parameters must be valid JSON if provided
- Shows error messages inline
- Disables submit button when invalid

**Example Parameters**:
*(blocco di codice rimosso)*

## API Client Integration

### Added to adminClient.ts

*(blocco di codice rimosso)*

## Schema Definitions

### Added to admin.schemas.ts

*(blocco di codice rimosso)*

## Usage Example

### Integration in Enterprise Admin

*(blocco di codice rimosso)*

## UI Components Used

### shadcn/ui
- **Table**: Data display with sorting
- **Badge**: Status indicators with variants
- **Progress**: Progress bar for running jobs
- **Dialog**: Modal overlays
- **Button**: Actions and navigation
- **Select**: Dropdown menus
- **Label**: Form labels
- **Textarea**: JSON parameter input
- **ScrollArea**: Scrollable content areas

### lucide-react Icons
- `PlusIcon`: Create job
- `RefreshCwIcon`: Refresh jobs
- `ClockIcon`: Queued status
- `PlayCircleIcon`: Running status, retry action
- `CheckCircleIcon`: Completed status
- `AlertCircleIcon`: Failed status
- `StopCircleIcon`: Cancel action
- `XCircleIcon`: Cancelled status, close action
- `TrashIcon`: Delete action
- `DownloadIcon`: Download results

## Features

### Real-Time Updates
- Auto-refresh every 5s when running jobs exist
- JobDetailModal polls every 3s for running job
- Manual refresh button with loading state
- Optimistic UI updates

### Error Handling
- Toast notifications for all actions
- Inline validation for JSON parameters
- Error messages displayed in detail modal
- Graceful fallback for API failures

### Accessibility
- Keyboard navigation for tables
- ARIA labels and roles
- Focus management in modals
- Screen reader friendly status indicators

### Responsive Design
- Mobile-friendly table overflow
- Responsive dialog sizes
- Flexible column widths
- Touch-friendly action buttons

## Performance Considerations

### Optimizations
- Conditional auto-refresh (only when needed)
- Debounced parameter validation
- Memoized format functions
- Lazy loading of job details
- Efficient state updates

### Resource Management
- Cleanup intervals on unmount
- Stop polling when modal closes
- Limit log display (max 100 lines)
- Paginated job list

## Testing Recommendations

### Unit Tests
- Status badge rendering for all statuses
- Duration formatting edge cases
- JSON validation in CreateJobModal
- Action button visibility logic

### Integration Tests
- API calls with correct parameters
- Pagination navigation
- Filter application
- Modal open/close behavior

### E2E Tests
- Create job workflow
- Cancel running job
- Retry failed job
- View job details
- Real-time updates

## Future Enhancements

### Planned Features
1. **Real-time Log Streaming**: Replace mock logs with SSE/WebSocket
2. **Bulk Actions**: Select multiple jobs for batch operations
3. **Advanced Filtering**: Date range, duration range, job type
4. **Job Templates**: Save frequently used job configurations
5. **Scheduled Jobs**: Cron-like scheduling interface
6. **Job Dependencies**: Chain jobs with dependencies
7. **Export Results**: CSV/JSON export for completed jobs
8. **Job History**: Historical view with analytics

### Improvements
- Virtual scrolling for large job lists
- Advanced JSON editor with syntax highlighting
- Job comparison view
- Performance metrics dashboard
- Cost tracking per job type

## Related Issues

- **#3693**: Batch Job System (this implementation)
- **#3689**: Layout Base & Navigation System (EnterpriseTabSystem)
- **#3691**: Audit Log System (similar table patterns)
- **#3692**: Token Management (related resource management)

## File Structure

*(blocco di codice rimosso)*

## Dependencies

### Required Packages
- `react` (^18.0.0)
- `lucide-react` (^0.263.1)
- `sonner` (^1.0.0) - Toast notifications
- `zod` (^3.22.0) - Schema validation
- `@radix-ui/react-progress` (shadcn/ui)
- `@radix-ui/react-dialog` (shadcn/ui)
- `@radix-ui/react-select` (shadcn/ui)

### Type Safety
- Full TypeScript strict mode
- Zod schema validation
- Type-safe API client methods
- Properly typed component props

---

**Last Updated**: 2026-02-06
**Author**: Frontend Architect
**Status**: ✅ Ready for Integration


---



<div style="page-break-before: always;"></div>

## frontend/components/entity-list-view.md

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

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

### CheckboxFilter

*(blocco di codice rimosso)*

### RangeFilter

*(blocco di codice rimosso)*

### DateRangeFilter

*(blocco di codice rimosso)*

## Full Example with Search, Sort, and Filters

*(blocco di codice rimosso)*

## Data Pipeline

Items flow through a search → filter → sort pipeline:

*(blocco di codice rimosso)*

## Architecture

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

## Accessibility

- `role="region"` with `aria-label` on the main section
- `role="radiogroup"` for view mode switcher with keyboard arrow key support
- `role="searchbox"` with `aria-label` for search input
- `aria-live="polite"` announcements for item count and view mode changes
- Ctrl+K keyboard shortcut for search focus
- No focus traps, visible focus indicators on all interactive elements
- Tested with axe-core on all view modes and states (grid, list, empty, loading)


---



<div style="page-break-before: always;"></div>

## frontend/components/game-carousel.md

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

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

### CarouselSortValue

*(blocco di codice rimosso)*

## Data Hooks

### useCarouselGames

The main hook for fetching carousel data with TanStack Query integration.

*(blocco di codice rimosso)*

### Convenience Hooks

*(blocco di codice rimosso)*

### useCarouselSort

Manages sort state with localStorage persistence and optional URL sync.

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

Auto-play automatically pauses on hover and resumes on mouse leave.

### Sorting Controls

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

### Empty State

When `games` is empty, displays:
> "No games to display"

## Integration Examples

### Homepage Section

*(blocco di codice rimosso)*

### Gallery Page

*(blocco di codice rimosso)*

## Storybook

View interactive examples at:
*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

### Test Coverage

- **Component Tests**: 44 tests covering rendering, navigation, keyboard, auto-play, sorting, touch gestures
- **A11y Tests**: 31 tests for WCAG compliance using jest-axe
- **Hook Tests**: 47 tests for data fetching and sort state management

## File Structure

*(blocco di codice rimosso)*

## Related Documentation

- [MeepleCard Component](./meeple-card.md) - Card component used within carousel
- [Design System Cards](../../design-system/cards.md) - Design specifications
- [Storybook Guide](../../07-frontend/storybook-guide.md) - How to use Storybook

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


---



<div style="page-break-before: always;"></div>

## frontend/components/meeple-card-v2-migration.md

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
*(blocco di codice rimosso)*

**TagBadge Component**:
*(blocco di codice rimosso)*

**Note**: TagStrip auto-enables `animated` for "new" tags.

## Migration Steps

### Step 1: No Action Required ✅

If you're satisfied with automatic visual improvements, **you're done**. No code changes needed.

### Step 2: Leverage New Props (Optional)

**For flip cards with entity-colored backs**:

*(blocco di codice rimosso)*

The `meeple-card.tsx` wrapper automatically passes `entityColor`, `entityName`, `title` to FlipCard.

**For animated tags**:

*(blocco di codice rimosso)*

### Step 3: Verify Dark Mode (Recommended)

Test your pages in dark mode to ensure entity colors display correctly:

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

**Q: How do I test v2 locally before merging?**
A: Checkout PR branch:
*(blocco di codice rimosso)*

**Q: What if I prefer the old shadows?**
A: Override in consuming component:
*(blocco di codice rimosso)*

## Support

**Issues**: Create GitHub issue with `area/ui` + `meeple-card-v2` labels
**PR**: #4619
**Epic**: #4604
**Design Reference**: `apps/web/src/components/ui/meeple-card-v2-mockup.html`


---



<div style="page-break-before: always;"></div>

## frontend/components/meeple-card.md

# MeepleCard Usage Guide

> The canonical card component for all MeepleAI entity displays

**Epic #4604 (v2)** | **Updated**: 2026-02-17

## Introduction

MeepleCard is MeepleAI's universal card component, designed to display games, players, sessions, agents, documents, chat conversations, events, and custom entities with a consistent, accessible interface.

**Latest Updates (Epic #4604 - v2 Visual Redesign)**:
- ✅ **Warm aesthetics**: Glassmorphism bg-white/90, warm shadows (rgba(180,130,80,...))
- ✅ **Entity glow rings**: Hover outline-2 with entity color @ 40% opacity
- ✅ **3D card flip**: Perspective 1200px, 0.7s transition, entity-colored back header
- ✅ **Enhanced carousel**: Center 1.1x scale, warm shadow-2xl
- ✅ **Tag animations**: Pulse effect for "new" tags
- ✅ **Shimmer effects**: Cover image shimmer on hover
- ✅ **Dark mode**: Entity color CSS variables, consistent tokens

**Previous Updates (Epic #4029)**:
- ✅ Extended to **7 entity types** (added session, agent, document, chatSession)
- ✅ Entity-specific **quick actions** with hover reveal
- ✅ **Info button** for detail page navigation
- ✅ **Board game card proportions** (7:10 aspect ratio)

### Why MeepleCard?

- **Consistency**: One component for all entity types ensures uniform UX
- **Accessibility**: Built-in WCAG AA compliance with keyboard navigation
- **Performance**: React.memo optimized with lazy-loaded images
- **Flexibility**: 7 entity types × 5 layout variants × modular features
- **Maintainability**: Single source of truth reduces bug surface
- **Quick Actions**: Entity-specific actions via `useEntityActions` hook

### Replacing Legacy Components

**GameCard/PlayerCard are deprecated**. For migration guide, see `/apps/web/MIGRATION_GAMECARD_TO_MEEPLECARD.md`.

## v2 Visual Design (Epic #4604)

**Approved Mockup**: `apps/web/src/components/ui/meeple-card-v2-mockup.html`

**Key Design Tokens**:
*(blocco di codice rimosso)*

**Visual Features**:
- **Entity Glow Rings**: Hover outline-2 with entity color @ 40% opacity
- **Warm Shadows**: Layered shadows with warm brown tones
- **Shimmer Effect**: Cover image shimmer on hover
- **3D Flip**: Perspective 1200px, smooth 0.7s cubic-bezier transition
- **Pulse Tags**: Animated "new" tags with scale pulse
- **Enhanced Carousel**: Center card 1.1x scale with entity glow

## Entity Types (7 Total)

Each entity type has a semantic color that appears in the left border, badge, and gradient overlays. Issue #4030 extended support from 5 to 7 entity types.

### Game (Orange)

*(blocco di codice rimosso)*

**Color**: `hsl(25 95% 45%)` - Warm orange for board games

### Player (Purple)

*(blocco di codice rimosso)*

**Color**: `hsl(262 83% 58%)` - Vibrant purple for users

### Session (Indigo)

*(blocco di codice rimosso)*

**Color**: `hsl(240 60% 55%)` - Indigo for game sessions
**Actions**: Riprendi, Usa Toolkit, Condividi codice

### Agent (Amber)

*(blocco di codice rimosso)*

**Color**: `hsl(38 92% 50%)` - Amber for AI agents
**Actions**: Chat, Statistiche

### Document (Slate)

*(blocco di codice rimosso)*

**Color**: `hsl(210 40% 55%)` - Slate for documents
**Actions**: Download, Chat sui contenuti

### ChatSession (Blue)

*(blocco di codice rimosso)*

**Color**: `hsl(220 80% 55%)` - Blue for chat conversations
**Actions**: Continua Chat, Esporta

### Event (Rose)

*(blocco di codice rimosso)*

**Color**: `hsl(350 89% 60%)` - Energetic rose for events

### Custom

*(blocco di codice rimosso)*

**Color**: Customizable via `customColor` prop (HSL format)

## Layout Variants

### Grid (Default)

Best for catalog displays and card grids.

*(blocco di codice rimosso)*

**Features**:
- 4:3 aspect ratio cover image
- Glass morphism background
- Hover: lifts up (-translate-y-1)
- Left border color accent

### List

Best for search results and sidebar lists.

*(blocco di codice rimosso)*

**Features**:
- Horizontal layout with 64px thumbnail
- Compact metadata display
- Hover: slides right (translate-x-1)
- Dot color indicator

### Compact

Best for sidebars, dropdowns, and space-constrained areas.

*(blocco di codice rimosso)*

**Features**:
- Minimal footprint, no image
- Small dot indicator
- Subtle hover effect
- Text-only display

### Featured

Best for featured content and promotional areas.

*(blocco di codice rimosso)*

**Features**:
- 16:9 aspect ratio cover
- Action buttons support
- Enhanced gradient overlay
- Stronger hover lift

### Hero

Best for hero sections and spotlight content.

*(blocco di codice rimosso)*

**Features**:
- Full-bleed background image
- Ribbon entity indicator
- Rich gradient overlay
- 320px minimum height

## Customization

### Custom Colors

Override the entity color with HSL values:

*(blocco di codice rimosso)*

### Metadata Icons

Use Lucide React icons for metadata:

*(blocco di codice rimosso)*

### Action Buttons

Available in featured and hero variants:

*(blocco di codice rimosso)*

## Accessibility

MeepleCard is built with accessibility in mind:

### Keyboard Navigation

- **Tab**: Focus the card
- **Enter/Space**: Activate click handler
- **Focus visible**: Clear ring indicator

### Screen Readers

*(blocco di codice rimosso)*

### Best Practices

1. **Always provide `title`**: Required for screen reader announcement
2. **Use semantic metadata**: Icons should have descriptive values
3. **Test with keyboard**: Ensure all interactions work without mouse
4. **Provide alt text**: Image alt defaults to title

## Performance

### React.memo

MeepleCard uses `React.memo` to prevent unnecessary re-renders:

*(blocco di codice rimosso)*

### Lazy Loading

Images are lazy-loaded with blur placeholders:

*(blocco di codice rimosso)*

### Optimization Tips

1. **Memoize callbacks**: Use `useCallback` for onClick handlers
2. **Virtualize lists**: For 50+ cards, use react-window or similar
3. **Optimize images**: Use Next.js Image optimization
4. **Avoid inline objects**: Extract metadata arrays to stable references

## Migration Guide

### From GameCard

*(blocco di codice rimosso)*

### Using Adapter Components

For catalog and dashboard contexts, use the pre-built adapters:

*(blocco di codice rimosso)*

## Troubleshooting

### Card not clickable

Ensure `onClick` is provided and the card doesn't have action buttons:

*(blocco di codice rimosso)*

### Rating shows wrong stars

Check `ratingMax` matches your scale:

*(blocco di codice rimosso)*

### Custom color not working

Ensure HSL format without `hsl()` wrapper:

*(blocco di codice rimosso)*

## Related Documentation

- [Component README](../../../apps/web/src/components/ui/data-display/README.md)
- [ADR-041: MeepleCard Universal System](../01-architecture/adr/adr-041-meeple-card-universal-system.md)
- [Design System: Cards](../design-system/cards.md)


---



<div style="page-break-before: always;"></div>

## frontend/dashboard-collection-centric-option-a.md

# Dashboard Collezione-Centrica - Opzione A

**Aesthetic Direction**: **Editorial Gaming** - Mix tra rivista di design e catalogo ludico premium

---

## 🎨 Design Philosophy

### Visual Identity
- **Tone**: Sofisticato ma giocoso, organizzato senza sterilità
- **Typography**:
  - **Display**: Playfair Display (caratteriale, editoriale)
  - **Body**: Geist Sans / Inter (leggibile, moderno)
- **Color Palette**:
  - **Primary**: Burnt Orange `#D97706` (energia, gioco)
  - **Secondary**: Olive Green `#65A30D` (natura, strategia)
  - **Accent**: Deep Blue `#1E40AF` (profondità, competenza)
  - **Neutrals**: Stone (50-900) per warmth
- **Motion**: Microinterazioni fluide, transizioni eleganti, hover states sorprendenti

### Layout Principles
- **Asymmetric Grid**: Elementi che rompono la rigidità del layout
- **Generous White Space**: Respiro tra sezioni dense
- **Visual Hierarchy**: Contrasto dimensionale e cromatico forte
- **Glassmorphism**: Profondità attraverso blur e trasparenze stratificate

### Memorability Factor
**Card giochi con effetto "flip" al hover** che rivelano statistiche dettagliate, con animazione glassmorphic e profondità 3D.

---

## 📐 Layout Structure (Markdown Skeleton)

*(blocco di codice rimosso)*

---

## 🃏 Component Breakdown

### 1. Header (Sticky)
**Features**:
- Logo + Brand (MeepleAI con Gamepad icon)
- Search Bar prominente con suggerimenti live
- User Profile con dropdown
- Notifications badge con pulse animation

**Responsive**:
- Desktop: Search inline nel header
- Mobile: Search move sotto header, full-width

**Tech**:
*(blocco di codice rimosso)*

---

### 2. Stats Overview (Glassmorphic Cards)
**Layout**: 4-column grid (responsive: 2-col mobile, 4-col desktop)

**Cards**:
1. **Totale Giochi**: 127 giochi (Amber gradient)
2. **Giocati (30gg)**: 23 + Streak 🔥 7 giorni (Emerald gradient)
3. **Wishlist**: 15 + Trend +3 mese (Blue gradient)
4. **Trending**: +3 giochi in crescita (Purple gradient)

**Visual Effects**:
- Glassmorphism: `backdrop-blur-xl` + gradient borders
- Decorative orb: Blurred circle per depth
- Hover: Lift effect (`y: -4px`, `scale: 1.02`)

**Tech**:
*(blocco di codice rimosso)*

---

### 3. Filter Sidebar (Collapsible)
**Sections**:
- **Categoria**: Multi-select badges (Strategia, Party, Famiglia, Astratto, Cooperativo)
- **Difficoltà**: Checkboxes con visual dots (●○○, ●●○, ●●●)
- **Giocatori**: Badge groups (1-2, 3-4, 5+)
- **Stato**: All | Owned | Played | Wishlist
- **Ordinamento**: Recent | Alphabetical | Rating | Duration
- **Reset Filtri**: Clear button

**Responsive**:
- Desktop: Fixed sidebar (280px width)
- Mobile: Bottom sheet modal (Framer Motion slide-up)

**Animation**:
- Collapse: Framer Motion `width` + `opacity` transition
- Badges: Hover color shift (`hover:bg-amber-100`)

**Tech**:
*(blocco di codice rimosso)*

---

### 4. Game Card (Flip Animation) ⭐ KEY FEATURE
**Front (Default State)**:
- Cover image (full-height, object-cover)
- Gradient overlay (bottom fade: `from-black/80 to-transparent`)
- Title (Playfair Display, white, bold)
- Rating stars (amber-400: ★★★★☆)
- Complexity dots (badge: ●●○)

**Back (Hover State)** - **3D Flip Effect**:
- Glassmorphic background (`from-amber-50 to-orange-50`, blur)
- Stats grid:
  - **Partite giocate**: 23
  - **Durata media**: 90 min
  - **Ultimo gioco**: 15 gen
- Quick Actions:
  - ✓ **Segna Giocato** (Emerald CTA)
  - ⭐ **Wishlist Toggle** (Outline button)

**Animation Details**:
*(blocco di codice rimosso)*

**CSS Magic**:
*(blocco di codice rimosso)*

**Responsive**:
- Grid view: 1-col (mobile) → 2-col (sm) → 3-col (lg) → 4-col (xl)
- List view: Horizontal layout, no flip (cover + title + stats inline)

---

### 5. Toolbar (Above Grid)
**Left Side**:
- **Filtri Toggle**: Button con badge count (es. "12 giochi")
- **Filter Status Badge**: Active filters indicator

**Right Side**:
- **View Mode Toggle**: Grid icon | List icon (active state styling)

**Tech**:
*(blocco di codice rimosso)*

---

### 6. Floating Action Button (FAB)
**Position**: Fixed bottom-right (24px margin)
**Size**: 56x56px circular
**Visual**:
- Gradient: `from-amber-500 to-orange-600`
- Shadow: `shadow-2xl shadow-amber-500/50` (glowing effect)
- Icon: Plus (+) white

**Interaction**:
- Hover: `scale(1.1)` + shadow intensifies
- Tap: `scale(0.95)` (tactile feedback)
- Click: Opens "Add Game" modal/drawer

**Tech**:
*(blocco di codice rimosso)*

---

## 🔧 Technical Implementation

### Tech Stack
- **Framework**: Next.js 14 (App Router)
- **Language**: TypeScript
- **Styling**: Tailwind CSS + shadcn/ui
- **Animation**: Framer Motion
- **State**: Zustand (filter store) + TanStack Query (data fetching)

### File Structure
*(blocco di codice rimosso)*

### Zustand Store (collection-filters-store.ts)
*(blocco di codice rimosso)*

### API Integration (TanStack Query)
*(blocco di codice rimosso)*

---

## ♿ Accessibility

### Keyboard Navigation
- ✅ Tab order: Header → Search → Filters → Cards → FAB
- ✅ Arrow keys: Navigate grid (optional enhancement)
- ✅ Enter/Space: Activate buttons and checkboxes
- ✅ Escape: Close modals, reset search

### Screen Reader Support
- ✅ ARIA labels on all interactive elements
- ✅ Semantic HTML (`<header>`, `<main>`, `<aside>`)
- ✅ Live regions for filter updates (`aria-live="polite"`)
- ✅ Card descriptions (`aria-describedby` for stats)

### Visual Accessibility
- ✅ Contrast ratios: 4.5:1 minimum (WCAG AA)
- ✅ Focus indicators: Visible ring on all focusable elements
- ✅ Color independence: Icons + text labels (not color-only)
- ✅ Reduced motion: `prefers-reduced-motion` media query

---

## 📱 Responsive Behavior

### Breakpoints (Tailwind)
- **Mobile**: < 640px (sm)
- **Tablet**: 640px - 1024px (sm-md)
- **Desktop**: > 1024px (lg+)

### Layout Adaptations
| Element          | Mobile (< 640px)       | Tablet (640-1024px)    | Desktop (> 1024px)    |
|------------------|------------------------|------------------------|-----------------------|
| **Header Search**| Below header, full-w   | Below header, full-w   | Inline, max-w-2xl     |
| **Stats Grid**   | 2 columns              | 4 columns              | 4 columns             |
| **Sidebar**      | Bottom sheet modal     | Drawer (swipe-in)      | Fixed sidebar (280px) |
| **Games Grid**   | 1-2 columns            | 2-3 columns            | 3-4 columns           |
| **Card Height**  | 280px                  | 320px                  | 360px                 |
| **FAB**          | Bottom-right (56px)    | Bottom-right (56px)    | Bottom-right (56px)   |

---

## 🎬 Animation Choreography

### Page Load Sequence (Staggered Reveals)
1. **Header** (0ms): Fade in + slide down
2. **Stats Cards** (100ms delay each): Scale up + fade in
3. **Sidebar** (400ms): Slide in from left
4. **Game Cards** (500ms + 50ms stagger): Fade up + scale

*(blocco di codice rimosso)*

### Microinteractions
- **Card Hover**: Lift (`y: -4px`) + shadow intensify (200ms ease-out)
- **Card Flip**: 600ms spring animation with preserve-3d
- **Badge Select**: Background color shift (150ms)
- **FAB Pulse**: Scale breathing animation (2s infinite)
- **Search Focus**: Ring expand + border color shift (200ms)

### Scroll Behavior
- **Infinite Scroll**: Load more cards on 80% viewport intersection
- **Sticky Header**: Maintains position with backdrop-blur
- **Parallax Stats** (optional): Stats cards subtle vertical shift on scroll

---

## 🚀 Performance Optimizations

### Code Splitting
*(blocco di codice rimosso)*

### Image Optimization
- **Next.js Image**: Automatic WebP, lazy loading, responsive srcset
- **Blur Placeholder**: LQIP (Low-Quality Image Placeholder)
- **Priority Loading**: First 8 cards get `priority` flag

### Memoization
*(blocco di codice rimosso)*

### Virtualization (Large Collections)
*(blocco di codice rimosso)*

---

## 🎨 Design Tokens (CSS Variables)

*(blocco di codice rimosso)*

---

## 🧪 Testing Strategy

### Unit Tests (Vitest)
- ✅ StatsCard: Renders correct values and gradients
- ✅ FilterSidebar: Toggle logic, reset functionality
- ✅ GameCardFlip: Flip state, quick actions
- ✅ Zustand Store: Filter updates, persistence

### Integration Tests
- ✅ Filter → API → Grid update flow
- ✅ Search → Debounce → Results refresh
- ✅ View mode toggle → Layout change

### E2E Tests (Playwright)
- ✅ User can search and filter games
- ✅ Card flip reveals stats on hover
- ✅ FAB opens add game modal
- ✅ Mobile: Sidebar opens as bottom sheet

### Visual Regression (Chromatic)
- ✅ Snapshot all component states
- ✅ Cross-browser consistency
- ✅ Responsive breakpoint validation

---

## 🔄 Future Enhancements

### Phase 2 (Post-MVP)
- **Advanced Search**: Fuzzy matching, autocomplete suggestions
- **Bulk Actions**: Select multiple cards, batch operations
- **Drag & Drop**: Reorder games, create custom collections
- **Custom Views**: Save filter presets, shareable URLs

### Phase 3 (Premium Features)
- **AI Recommendations**: "Games you might like" section
- **Social Sharing**: Share collection with friends (public URL)
- **Analytics Dashboard**: Play frequency heatmap, genre distribution charts
- **Offline Mode**: PWA with local caching for collection access

---

## 📊 Success Metrics

### User Engagement
- **Time on Page**: > 3 minutes average
- **Card Interactions**: > 5 cards flipped per session
- **Filter Usage**: > 60% of users apply at least one filter
- **Return Rate**: > 40% weekly active users

### Performance
- **LCP (Largest Contentful Paint)**: < 2.5s
- **FID (First Input Delay)**: < 100ms
- **CLS (Cumulative Layout Shift)**: < 0.1

### Accessibility
- **Lighthouse Accessibility Score**: > 95
- **Keyboard Navigation**: 100% of features accessible
- **Screen Reader Compatibility**: Tested with NVDA/VoiceOver

---

## 🎯 Summary

**Opzione A: Dashboard Collezione-Centrica** offre:
- ✨ **Estetica distintiva**: Editorial Gaming con Playfair Display + palette terrosa
- 🃏 **Flip Cards 3D**: Microinterazione memorabile e funzionale
- 🎨 **Glassmorphism**: Profondità visiva senza pesantezza
- ⚡ **Performance**: Code splitting, lazy loading, memoization
- ♿ **Accessibility**: WCAG AA compliant, keyboard + screen reader support
- 📱 **Responsive**: Mobile-first design con adattamenti intelligenti

**Differenziazione competitiva**:
- Nessun altro dashboard di collezione giochi usa flip cards 3D con glassmorphism
- Layout asimmetrico rompe la monotonia delle griglie standard
- Palette terrosa distingue da generici purple/blue corporate

**Implementation Ready**:
- ✅ Componenti completamente implementati in TypeScript
- ✅ Zustand store configurato con persistence
- ✅ Framer Motion animations integrate
- ✅ shadcn/ui components utilizzati
- ✅ Tailwind CSS con design tokens estensibili

---

**Next Steps**:
1. Review design con stakeholder
2. Test usability con utenti beta
3. Integration con API backend
4. Deploy su staging per feedback
5. Iterazione basata su metriche


---



<div style="page-break-before: always;"></div>

## frontend/dashboard-hub-implementation-plan.md

# Dashboard Hub - Implementation Plan

**Goal**: Trasformare la dashboard attuale in un hub riassuntivo multi-sezione con collegamenti a pagine specializzate.

**Document Status**: Planning Phase
**Created**: 2026-01-21
**Owner**: Frontend Team

---

## 📋 Overview

### Current State
- Dashboard esistente con sezioni base (Issue #1836)
- Componenti parziali: GreetingSection, ActiveSessionsSection, LibraryQuotaSection, ChatHistorySection
- Focus su recent games grid (6 giochi)

### Target State
- Dashboard hub con 10 sezioni integrate
- Snapshot multi-dominio (Library, Sessions, Chat, Wishlist, Catalog)
- AI-powered insights e suggerimenti personalizzati
- Collegamenti espliciti a pagine dedicate
- Collection Dashboard (flip cards 3D) spostata a `/library`

---

## 🎯 Epic Structure

### Epic 1: Dashboard Hub Core (MVP) - **Phase 1**
**Priority**: P0 - Critical
**Target**: Sprint Current + 1
**Estimated Effort**: 21 story points (3 sprints)

**Scope**:
- Refactoring layout dashboard principale
- Backend API aggregata `/api/v1/dashboard`
- Integrazione componenti esistenti
- Nuove sezioni: Enhanced Activity Feed, Library Snapshot completo
- Responsive design (mobile-first)

**Issues**:
1. #TBD-001: Backend - Dashboard Aggregated API Endpoint
2. #TBD-002: Frontend - Dashboard Hub Layout Refactoring
3. #TBD-003: Frontend - Enhanced Activity Feed Timeline
4. #TBD-004: Frontend - Library Snapshot Component
5. #TBD-005: Frontend - Quick Actions Grid Enhancement
6. #TBD-006: Frontend - Responsive Layout Mobile/Desktop
7. #TBD-007: Testing - Dashboard Hub Integration Tests

---

### Epic 2: AI Insights & Recommendations - **Phase 2**
**Priority**: P1 - High
**Target**: Sprint +2
**Estimated Effort**: 13 story points (2 sprints)

**Scope**:
- AI-powered insights widget
- RAG-based game recommendations
- Backlog alerts (giochi non giocati 30+ giorni)
- Wishlist management + highlights
- Catalog trending analytics

**Issues**:
1. #TBD-008: Backend - AI Insights Service (RAG Integration)
2. #TBD-009: Frontend - AI Insights Widget Component
3. #TBD-010: Backend - Wishlist Management API
4. #TBD-011: Frontend - Wishlist Highlights Component
5. #TBD-012: Backend - Catalog Trending Analytics
6. #TBD-013: Frontend - Catalog Trending Widget

---

### Epic 3: Gamification & Advanced Features - **Phase 3**
**Priority**: P2 - Medium
**Target**: Sprint +4
**Estimated Effort**: 8 story points (1-2 sprints)

**Scope**:
- Achievement system (badges, streaks, milestones)
- User level progression
- Advanced activity timeline (filters, search)
- Personalized dashboard customization

**Issues**:
1. #TBD-014: Backend - Achievement System & Badge Engine
2. #TBD-015: Frontend - Achievements Widget Component
3. #TBD-016: Frontend - Advanced Activity Timeline (Filters)
4. #TBD-017: Frontend - Dashboard Layout Customization

---

## 📊 Dependencies Map

*(blocco di codice rimosso)*

---

## 🚀 Rollout Strategy

### Sprint Planning

**Sprint N (Current)**: Planning & Design
- ✅ Requirements gathering (this document)
- ✅ Epic creation and issue breakdown
- [ ] API contract definition (backend/frontend alignment)
- [ ] UI mockups approval

**Sprint N+1**: Epic 1 Foundation
- Backend: Dashboard aggregated API (#TBD-001)
- Frontend: Layout refactoring (#TBD-002)
- Component development: Activity Feed, Library Snapshot

**Sprint N+2**: Epic 1 Completion + Epic 2 Start
- Frontend: Responsive design + testing
- Backend: AI Insights service development
- Integration testing

**Sprint N+3**: Epic 2 Completion
- Wishlist management
- Catalog trending
- AI insights widget

**Sprint N+4**: Epic 3 (Optional)
- Gamification features
- Advanced timeline
- Dashboard customization

---

## 🎨 Design Assets Needed

### UI/UX
- [ ] Dashboard hub wireframes (Figma)
- [ ] Activity feed timeline design
- [ ] AI insights widget visual design
- [ ] Mobile responsive mockups
- [ ] Icon set for quick actions

### Data Visualization
- [ ] Stats card color palette finalization
- [ ] Progress bar styles (library quota)
- [ ] Badge/achievement visual system
- [ ] Timeline event type icons

---

## 📐 API Contracts

### New Endpoints

*(blocco di codice rimosso)*

---

## 🧪 Testing Strategy

### Unit Tests
- All new components (Activity Feed, Library Snapshot, AI Insights, etc.)
- Zustand stores (if new state management needed)
- API client functions

### Integration Tests
- Dashboard data aggregation
- Component integration (parent/child data flow)
- API endpoint integration

### E2E Tests (Playwright)
- User navigates dashboard → clicks stats card → lands on correct page
- Activity feed displays recent events correctly
- AI insights click-through to filtered views
- Mobile responsive behavior

### Visual Regression (Chromatic)
- Dashboard layout snapshots (desktop/tablet/mobile)
- Component states (loading, error, empty, populated)
- Dark mode compatibility (if applicable)

---

## 📊 Success Metrics

### User Engagement
- **Time on Dashboard**: > 2 minutes average (up from 1min)
- **Click-through Rate**: > 40% users click at least one CTA
- **Return Rate**: > 50% daily active users visit dashboard first

### Performance
- **Dashboard Load Time**: < 1.5s (aggregated API)
- **LCP (Largest Contentful Paint)**: < 2.5s
- **FID (First Input Delay)**: < 100ms

### Feature Adoption
- **AI Insights Engagement**: > 30% users click insights within 7 days
- **Quick Actions Usage**: > 60% users use at least one quick action
- **Wishlist Interaction**: > 25% users manage wishlist from dashboard

---

## 🔄 Migration Strategy

### Code Changes
1. **Preserve Existing Dashboard**: Keep current `/dashboard/page.tsx` as backup
2. **Create New Hub**: Implement in `/dashboard/hub-page.tsx` (temporary)
3. **Feature Flag**: Use feature flag to toggle between old/new dashboard
4. **Gradual Rollout**: 10% → 50% → 100% users over 2 weeks
5. **Cleanup**: Remove old dashboard after 100% rollout + 1 week monitoring

### Data Migration
- No data migration needed (read-only dashboard)
- Backend API changes backward-compatible

### User Communication
- [ ] Changelog announcement
- [ ] In-app notification: "Nuova dashboard disponibile!"
- [ ] User guide/tutorial (optional)

---

## 🚨 Risks & Mitigations

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Backend API performance** (aggregated call slow) | High | Medium | Implement Redis caching (5min TTL), optimize queries, pagination |
| **RAG service availability** (Epic 2 blocker) | Medium | Low | Graceful degradation (hide AI insights if service down) |
| **Mobile performance** (too many sections) | Medium | Medium | Lazy loading, virtualization, collapsible sections |
| **Breaking changes** (existing components) | Low | Low | Backward compatibility, feature flags, gradual rollout |

### Product Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **User confusion** (too much info) | Medium | Medium | User testing, progressive disclosure, clear CTAs |
| **Low engagement** (AI insights ignored) | Medium | Medium | A/B testing, personalization tuning, user feedback loop |
| **Feature creep** (scope expansion) | High | High | Strict epic boundaries, MVP-first approach, defer Phase 3 if needed |

---

## 📝 Open Questions

### Product Decisions
- [ ] Should achievements be part of MVP or deferred to Phase 3?
- [ ] How many sections should be visible on first load (mobile)?
- [ ] Should users be able to customize dashboard layout order?
- [ ] Dark mode support required for launch?

### Technical Decisions
- [ ] Use single aggregated API call vs. multiple parallel calls?
- [ ] Client-side caching strategy (React Query staleTime)?
- [ ] WebSocket for real-time activity feed updates?
- [ ] Analytics tracking: Mixpanel vs. custom solution?

### Design Decisions
- [ ] Animation budget (how much Framer Motion)?
- [ ] Icon library: Lucide vs. custom icons?
- [ ] Mobile navigation: Bottom sheet vs. hamburger menu?

---

## 📚 Reference Documents

- [Dashboard Overview Hub Spec](./dashboard-overview-hub.md)
- [Collection Dashboard (Opzione A)](./dashboard-collection-centric-option-a.md)
- [Current Dashboard Implementation](../apps/web/src/app/(public)/dashboard/page.tsx)
- [Issue #1836 - Original Dashboard](https://github.com/org/repo/issues/1836)
- [Issue #2445 - Library Quota Section](https://github.com/org/repo/issues/2445)
- [Issue #2617 - Active Sessions Widget](https://github.com/org/repo/issues/2617)
- [Issue #2612 - Recently Added Games](https://github.com/org/repo/issues/2612)

---

## 🎯 Next Actions

1. **Immediate** (This Sprint):
   - [ ] Review this implementation plan with team
   - [ ] Get stakeholder approval on Epic priorities
   - [ ] Create GitHub Epic and Issue tickets
   - [ ] Define API contracts with backend team
   - [ ] Start UI mockups in Figma

2. **Sprint N+1** (Next Sprint):
   - [ ] Backend: Implement aggregated dashboard API
   - [ ] Frontend: Begin layout refactoring
   - [ ] Setup feature flag system
   - [ ] Create test environment

3. **Continuous**:
   - [ ] Weekly sync between frontend/backend teams
   - [ ] Bi-weekly user testing sessions
   - [ ] Monitor performance metrics
   - [ ] Collect user feedback via in-app surveys

---

**Status**: ✅ Planning Complete - Ready for Epic/Issue Creation
**Next Step**: Create individual Epic and Issue documents


---



<div style="page-break-before: always;"></div>

## frontend/DASHBOARD-HUB-INDEX.md

# Dashboard Hub - Documentation Index

**Central index for all Dashboard Hub documentation**

---

## 📖 Documentation Structure

### Planning & Specifications
| Document | Description | Status |
|----------|-------------|--------|
| [Epic Details](./epics/epic-dashboard-hub-core.md) | Epic #3901 breakdown with issues and timeline | ✅ Complete |
| [Dashboard Overview Spec](./dashboard-overview-hub.md) | Detailed layout specification and design | ✅ Complete |
| [Implementation Plan](./dashboard-hub-implementation-plan.md) | Technical implementation roadmap | ✅ Complete |
| [Quick Reference](./DASHBOARD-HUB-QUICK-REFERENCE.md) | Developer quick reference guide | ✅ Complete |

### Related Epics
| Epic | Description | Link |
|------|-------------|------|
| **Epic 1** | Dashboard Hub Core (MVP) | [#3901](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3901) |
| **Epic 2** | AI Insights & Recommendations | [#3902](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3902) |
| **Epic 3** | Gamification & Advanced Features | [#3906](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3906) |

---

## 🎯 Epic #3901 Sub-Issues

### Backend (3 issues - 7 SP)
| Issue | Title | SP | Status |
|-------|-------|----|----|
| TBD | Backend: Dashboard Aggregated API Endpoint | 3 | 🔲 To Create |
| TBD | Backend: Activity Timeline Aggregation Service | 2 | 🔲 To Create |
| TBD | Backend: Cache Invalidation Strategy | 2 | 🔲 To Create |

### Frontend (5 issues - 11 SP)
| Issue | Title | SP | Status |
|-------|-------|----|----|
| TBD | Frontend: Dashboard Hub Layout Refactoring + Cleanup Legacy | 3 | 🔲 To Create |
| TBD | Frontend: Enhanced Activity Feed Timeline Component | 3 | 🔲 To Create |
| TBD | Frontend: Library Snapshot Component | 2 | 🔲 To Create |
| TBD | Frontend: Quick Actions Grid Enhancement | 2 | 🔲 To Create |
| TBD | Frontend: Responsive Layout Mobile/Desktop | 3 | 🔲 To Create |

### Testing (1 issue - 3 SP)
| Issue | Title | SP | Status |
|-------|-------|----|----|
| TBD | Testing: Dashboard Hub Integration & E2E Tests | 3 | 🔲 To Create |

**Total**: 8 issues, 21 story points

---

## 📐 Layout Sections

### Phase 1 (MVP - Current Sprint)
| Section | Component | Status | Depends On |
|---------|-----------|--------|------------|
| Hero + Stats | `HeroSection.tsx` | ✅ Existing | - |
| Active Sessions | `ActiveSessionsWidget.tsx` | ✅ Existing (Issue #2617) | - |
| Library Snapshot | `LibrarySnapshot.tsx` | 🔲 To Build | Backend API |
| Activity Feed | `ActivityFeed.tsx` | 🔲 To Build | Activity Service |
| Chat History | `ChatHistory.tsx` | ✅ Existing | - |
| Quick Actions | `QuickActionsGrid.tsx` | ✅ Existing | - |

### Phase 2 (Post-MVP)
| Section | Component | Status | Epic |
|---------|-----------|--------|------|
| AI Insights | `AIInsightsWidget.tsx` | 🔄 Future | Epic 2 |
| Wishlist Highlights | `WishlistHighlights.tsx` | 🔄 Future | Epic 2 |
| Catalog Trending | `CatalogTrending.tsx` | 🔄 Future | Epic 2 |

### Phase 3 (Enhancement)
| Section | Component | Status | Epic |
|---------|-----------|--------|------|
| Achievements | `AchievementsWidget.tsx` | 🔄 Future | Epic 3 |
| Advanced Timeline | `AdvancedTimeline.tsx` | 🔄 Future | Epic 3 |
| Personalized Recs | `PersonalizedRecs.tsx` | 🔄 Future | Epic 3 |

---

## 🔌 API Endpoints

### Dashboard Core (Epic 1)
| Endpoint | Method | Purpose | Cache | Performance |
|----------|--------|---------|-------|-------------|
| `/api/v1/dashboard` | GET | Aggregated dashboard data | 5 min | < 500ms |

### Future Endpoints (Epic 2 & 3)
| Endpoint | Method | Purpose | Epic |
|----------|--------|---------|------|
| `/api/v1/dashboard/insights` | GET | AI recommendations | Epic 2 |
| `/api/v1/dashboard/wishlist` | GET | Wishlist highlights | Epic 2 |
| `/api/v1/dashboard/trending` | GET | Catalog trending | Epic 2 |
| `/api/v1/dashboard/achievements` | GET | User achievements | Epic 3 |

---

## 📊 Data Model

### DashboardData Interface
*(blocco di codice rimosso)*

---

## 🧪 Testing Strategy

### Unit Tests (Vitest)
| Component | Coverage Target | Status |
|-----------|----------------|--------|
| `DashboardHub.tsx` | > 85% | 🔲 Pending |
| `LibrarySnapshot.tsx` | > 85% | 🔲 Pending |
| `ActivityFeed.tsx` | > 85% | 🔲 Pending |
| `QuickActionsGrid.tsx` | > 85% | ✅ Existing |

### Integration Tests
| Test Suite | Coverage | Status |
|------------|----------|--------|
| Dashboard Data Flow | API → Components | 🔲 Pending |
| Navigation Links | All sections | 🔲 Pending |
| Empty States | All components | 🔲 Pending |

### E2E Tests (Playwright)
| User Flow | Description | Status |
|-----------|-------------|--------|
| Login → Dashboard | Stats visible, all sections load | 🔲 Pending |
| Active Session Click | Navigate to session page | 🔲 Pending |
| Activity Event Click | Navigate to linked entity | 🔲 Pending |
| Quick Action Click | Navigate to correct page | 🔲 Pending |
| Mobile Responsive | All sections on mobile | 🔲 Pending |

### Visual Regression (Chromatic)
| Viewport | State | Status |
|----------|-------|--------|
| Mobile (375px) | Populated | 🔲 Pending |
| Tablet (768px) | Populated | 🔲 Pending |
| Desktop (1440px) | Populated | 🔲 Pending |
| Desktop (1440px) | Loading | 🔲 Pending |
| Desktop (1440px) | Empty | 🔲 Pending |
| Desktop (1440px) | Error | 🔲 Pending |

---

## 🎨 Design System

### Color Palette
| Category | Color | Hex | Usage |
|----------|-------|-----|-------|
| Collezione | Amber | `#F59E0B` | Library stats, cards |
| Sessioni | Emerald | `#10B981` | Active sessions |
| Chat AI | Blue | `#3B82F6` | Chat history |
| Wishlist | Purple | `#8B5CF6` | Wishlist highlights |
| Insights | Yellow | `#FBBF24` | AI suggestions |
| Achievements | Gold | `#D97706` | Badges, gamification |

### Typography
| Element | Font | Size | Weight |
|---------|------|------|--------|
| Section Title | Inter | 1.5rem | 600 |
| Card Title | Inter | 1.125rem | 500 |
| Body Text | Inter | 1rem | 400 |
| Stats | Inter | 2rem | 700 |

### Spacing
| Breakpoint | Gap | Padding |
|------------|-----|---------|
| Mobile | 1rem | 1rem |
| Tablet | 1.5rem | 1.5rem |
| Desktop | 2rem | 2rem |

---

## 🚀 Timeline

### Sprint N+1 (Weeks 1-2)
- [ ] Backend: Dashboard Aggregated API (#1)
- [ ] Backend: Activity Timeline Service (#2)
- [ ] Frontend: Start Layout Refactoring (#3)

### Sprint N+2 (Weeks 3-4)
- [ ] Frontend: Complete Layout Refactoring (#3)
- [ ] Frontend: Activity Feed Component (#4)
- [ ] Frontend: Library Snapshot Component (#5)
- [ ] Frontend: Quick Actions Enhancement (#6)
- [ ] QA: Start testing

### Sprint N+3 (Weeks 5-6)
- [ ] Frontend: Responsive Layout (#7)
- [ ] QA: Integration & E2E Tests (#8)
- [ ] Deploy to staging with feature flag
- [ ] Stakeholder demo

---

## 📈 Success Metrics

### Development Metrics
| Metric | Target | Current |
|--------|--------|---------|
| Code Coverage | > 85% | TBD |
| API Response (p99) | < 500ms | TBD |
| Bundle Size Increase | < 50KB | TBD |
| Lighthouse Performance | > 90 | TBD |
| Lighthouse Accessibility | > 95 | TBD |

### User Metrics (Post-Launch)
| Metric | Target | Current |
|--------|--------|---------|
| Dashboard Load Time | < 1.5s | TBD |
| CTR (Stats → Pages) | > 40% | TBD |
| Time on Dashboard | > 2 min | TBD |
| Mobile Bounce Rate | < 15% | TBD |

---

## 🗑️ Legacy Code Cleanup

### Files to Remove
*(blocco di codice rimosso)*

### Validation Checklist
- [ ] `grep -r "UserDashboard" apps/web/src/` returns 0 results
- [ ] `grep -r "dashboard-client-legacy" apps/web/src/` returns 0 results
- [ ] All imports updated to new components
- [ ] All tests passing after cleanup
- [ ] No broken links or references

---

## 🔗 External References

### GitHub Issues
- [Epic #3901 - Dashboard Hub Core (MVP)](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3901)
- [Issue #2617 - Active Sessions Widget](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2617)
- [Issue #2445 - Library Quota Section](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2445)
- [Issue #2612 - Recent Activity Feed](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2612)

### Design References
- GitHub Dashboard Activity Feed
- Notion Workspace Overview
- Linear Project Dashboard

### Tech Stack
- [Next.js 16 App Router](https://nextjs.org/docs)
- [React 19](https://react.dev/)
- [TanStack Query](https://tanstack.com/query)
- [Tailwind CSS 4](https://tailwindcss.com/)
- [shadcn/ui](https://ui.shadcn.com/)
- [Framer Motion](https://www.framer.com/motion/)

---

## 📞 Contact & Ownership

| Role | Owner | Contact |
|------|-------|---------|
| Epic Owner | Frontend Team Lead | TBD |
| Backend Lead | Backend Team Lead | TBD |
| QA Lead | QA Team Lead | TBD |

---

**Last Updated**: 2026-02-09
**Epic**: [#3901](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3901)
**Status**: Planning Phase
**Next Review**: Sprint Planning N+1


---



<div style="page-break-before: always;"></div>

## frontend/DASHBOARD-HUB-QUICK-REFERENCE.md

# Dashboard Hub - Quick Reference

**Quick reference guide for Dashboard Hub development**

---

## 🚀 Quick Start

*(blocco di codice rimosso)*

---

## 📍 File Locations

### Backend
*(blocco di codice rimosso)*

### Frontend
*(blocco di codice rimosso)*

---

## 🔌 API Endpoints

### Aggregated Dashboard Data
*(blocco di codice rimosso)*

**Cache**: Redis, TTL 5 minutes
**Performance**: < 500ms (p99)

---

## 🧩 Component Structure

### Main Layout
*(blocco di codice rimosso)*

### Data Fetching
*(blocco di codice rimosso)*

---

## 🎨 Responsive Breakpoints

| Breakpoint | Width | Layout | Cols |
|------------|-------|--------|------|
| **Mobile** | < 640px | Stack | 1-col |
| **Tablet** | 640-1024px | Split | 2-col |
| **Desktop** | > 1024px | Asymmetric | 3-col |

*(blocco di codice rimosso)*

---

## 🎨 Color Coding

| Section | Color | Tailwind |
|---------|-------|----------|
| 📚 Collezione | Amber/Orange | `amber-500` |
| 🎲 Sessioni | Emerald/Green | `emerald-500` |
| 💬 Chat AI | Blue | `blue-500` |
| ⭐ Wishlist | Purple | `purple-500` |
| 💡 Insights | Yellow | `yellow-500` |
| 🏆 Achievements | Gold | `yellow-600` |

---

## 📊 Success Criteria Checklist

### User Experience
- [ ] Snapshot collezione visible in < 2s
- [ ] 1-click session continuation
- [ ] Clear CTA for dedicated pages
- [ ] Mobile fully functional (< 640px)

### Technical
- [ ] API response < 500ms (cached)
- [ ] Lighthouse Performance > 90
- [ ] Test coverage > 85%
- [ ] Zero breaking changes

### Business
- [ ] CTR dashboard → library > 40%
- [ ] Time on dashboard > 2 minutes
- [ ] Mobile bounce rate < 15%

---

## 🧪 Testing Commands

*(blocco di codice rimosso)*

---

## 🔗 Navigation Flow

*(blocco di codice rimosso)*

---

## 🧹 Legacy Code Cleanup

**Files to Remove** (after migration):
*(blocco di codice rimosso)*

---

## 🚨 Common Issues

### API Timeout
*(blocco di codice rimosso)*

### Slow Dashboard Load
*(blocco di codice rimosso)*

### Broken Component
*(blocco di codice rimosso)*

---

## 📦 Dependencies

### External (Already Implemented)
- ✅ Active Sessions Widget (Issue #2617)
- ✅ Library Quota Section (Issue #2445)
- ✅ Chat History Section (existing)
- ✅ Greeting Section (existing)

### Internal (Epic #3901)
*(blocco di codice rimosso)*

---

## 🎯 Performance Targets

| Metric | Target | Actual |
|--------|--------|--------|
| API response time (p99) | < 500ms | TBD |
| Dashboard load time | < 1.5s | TBD |
| Lighthouse Performance | > 90 | TBD |
| Lighthouse Accessibility | > 95 | TBD |
| Test coverage | > 85% | TBD |
| Bundle size increase | < 50KB | TBD |

---

## 📚 Full Documentation

- [Dashboard Overview Spec](./dashboard-overview-hub.md)
- [Epic Details](./epics/epic-dashboard-hub-core.md)
- [Implementation Plan](./dashboard-hub-implementation-plan.md)
- [Index](./DASHBOARD-HUB-INDEX.md)

---

**Last Updated**: 2026-02-09
**Status**: Planning Phase
**Epic**: #3901


---



<div style="page-break-before: always;"></div>

## frontend/dashboard-overview-hub.md

# Dashboard Overview Hub - Layout Riassuntivo

**Obiettivo**: Hub centrale post-login con overview multi-sezione e collegamenti rapidi a pagine specializzate.

**Design Philosophy**: Informativa ma non densa, con chiare call-to-action per approfondire.

---

## 📐 Layout Skeleton (Full Dashboard)

*(blocco di codice rimosso)*

---

## 🧩 Sezioni Dettagliate

### 1. Hero Section + Stats Overview
**Dati mostrati**:
- Greeting personalizzato con nome utente
- Ultimo accesso
- 4 metriche chiave:
  - **Collezione**: Totale giochi + trend mese (+3)
  - **Giocati**: Partite 30gg + streak 🔥
  - **Chat AI**: Conversazioni 7gg
  - **Wishlist**: Giochi desiderati + trend

**Interazione**: Cliccando su ogni card → naviga a pagina dedicata

---

### 2. Active Sessions Widget (Issue #2617)
**Dati mostrati**:
- Numero sessioni attive
- Lista ultime 2 sessioni in corso:
  - Nome gioco
  - Giocatori (es. 3/4)
  - Progresso (es. Turno 12)
  - Durata parziale

**CTA**:
- **[Continua ▶]** → Riprendi sessione (`/sessions/{id}`)
- **[Vedi Tutte →]** → Elenco completo (`/sessions`)

**Se nessuna sessione attiva**: Mostra "Nessuna sessione attiva" + CTA "Inizia Nuova Partita"

---

### 3. Library Snapshot (Collezione Overview)
**Dati mostrati**:
- **Quota**: Giochi posseduti / Limite (es. 127/200 = 64%) con progress bar
- **Top 3 Giochi**:
  - Cover thumbnail
  - Titolo
  - Rating ★★★★★
  - Numero partite giocate

**CTA**: **[Vedi Collezione Completa]** → `/library`

**Design**: Card compatta con grid 3 giochi (1-col mobile, 3-col desktop)

---

### 4. Recent Activity Feed (Timeline)
**Dati mostrati** (Issue #2612 + estensioni):
- Timeline cronologica ultimi 5 eventi:
  - 📚 Giochi aggiunti alla libreria
  - 🎲 Partite completate
  - 💬 Chat salvate
  - ⭐ Wishlist updates
  - 🏆 Achievement unlocked

**Formato**:
*(blocco di codice rimosso)*

**CTA**: **[Vedi Tutta la Timeline →]** → `/activity` (pagina dedicata)

---

### 5. AI Insights & Suggestions
**Dati mostrati** (AI-Powered):
- 💡 Suggerimenti personalizzati basati su:
  - **Giochi non giocati da 30+ giorni** (backlog alert)
  - **Regole salvate da rivedere** (chat history analysis)
  - **Giochi simili nel catalogo** (RAG recommendations)
  - **Streak maintenance** (gamification nudge)

**Interazione**: Ogni insight è cliccabile → azione diretta
- "5 giochi non giocati" → Filtro collezione con `lastPlayed < 30d`
- "Regole salvate" → Chat history filtrata per quel gioco
- "Giochi simili" → Catalogo con filtro similarità

**Tecnologia**: Backend usa RAG embeddings per suggerimenti contestuali

---

### 6. Chat History (Conversazioni AI)
**Dati mostrati**:
- Ultime 4 conversazioni con AI agent
- Formato:
  - Titolo conversazione (auto-generated o custom)
  - Data/ora

**CTA**:
- **[Nuova Chat +]** → Apri chat AI (`/chat`)
- **[Vedi Tutte le Chat →]** → History completa (`/chat/history`)

**Design**: Lista compatta con hover highlight

---

### 7. Quick Actions Grid
**Collegamenti rapidi** (5 azioni primarie):
1. 📚 **Vai alla Collezione** → `/library`
2. 🎲 **Nuova Sessione** → `/sessions/new`
3. 💬 **Chat AI Regole** → `/chat`
4. 🔍 **Esplora Catalogo** → `/games/catalog`
5. ⚙️ **Impostazioni** → `/settings`

**Design**: Card grid 5-col (2-col mobile) con icon + label

---

### 8. Wishlist Highlights
**Dati mostrati**:
- Top 5 giochi nella wishlist (ordinati per priorità o data aggiunta)
- Solo titolo (no cover per compattezza)

**CTA**: **[Gestisci Wishlist →]** → `/wishlist`

**Future**: Alert su disponibilità/prezzi (integrazione esterna)

---

### 9. Catalog Trending (Community Insights)
**Dati mostrati**:
- Top 3 giochi trending nel catalogo condiviso
- Metrica: Incremento ricerche/aggiunte settimanale (es. +15%)

**CTA**: **[Vedi Catalogo Completo →]** → `/games/catalog`

**Design**: Lista numerata compatta

---

### 10. Achievements & Badges (Gamification - Optional)
**Dati mostrati**:
- Ultimi 3 achievement sbloccati
- Esempi:
  - 🎖️ "Giocatore Costante" - 7 giorni streak
  - 🎖️ "Collezionista" - 100+ giochi
  - 🎖️ "Esperto AI" - 50+ chat

**CTA**: **[Vedi Tutti i Badge →]** → `/achievements`

**Design**: Card con emoji badge + titolo + descrizione breve

---

## 🎨 Design Principles

### Visual Hierarchy
1. **Hero Stats**: Grandi, colorati, immediate attention
2. **Active Sessions**: Urgency (sessioni in corso)
3. **Library + Activity**: Info-dense ma scannable
4. **AI Insights**: Highlight con colori distintivi (amber/yellow)
5. **Chat + Quick Actions**: Accessibili ma non dominanti
6. **Wishlist + Trending**: Scoperta e ispirazione
7. **Achievements**: Delight e engagement

### Information Density
- **Mobile**: Una colonna, sezioni collassabili
- **Tablet**: 2 colonne, sidebar/main layout
- **Desktop**: 3 colonne asimmetriche (sidebar + main + aside)

### Color Coding
- 📚 **Collezione**: Amber/Orange (warmth, ownership)
- 🎲 **Sessioni**: Emerald/Green (active, progress)
- 💬 **Chat AI**: Blue (knowledge, assistance)
- ⭐ **Wishlist**: Purple (aspiration, future)
- 💡 **Insights**: Yellow (attention, discovery)
- 🏆 **Achievements**: Gold (reward, gamification)

---

## 🔗 Navigation Flow

### Dashboard come Hub
*(blocco di codice rimosso)*

### Deep Links da Dashboard
- **Stat card "Collezione"** → `/library`
- **Stat card "Giocati"** → `/sessions/history`
- **Stat card "Chat AI"** → `/chat`
- **Stat card "Wishlist"** → `/wishlist`
- **"Continua Sessione"** → `/sessions/{id}`
- **"Vedi Collezione Completa"** → `/library` (con la tua UI flip cards)
- **AI Insight "Giochi non giocati"** → `/library?filter=unplayed`

---

## 📱 Responsive Layout

### Mobile (< 640px)
*(blocco di codice rimosso)*

### Desktop (> 1024px)
*(blocco di codice rimosso)*

---

## 🚀 Implementation Priority

### Phase 1 (MVP - Current Sprint)
1. ✅ Hero + Stats Overview (già presente: GreetingSection)
2. ✅ Active Sessions Widget (già presente: Issue #2617)
3. ✅ Library Snapshot (già presente: LibraryQuotaSection)
4. ✅ Recent Activity Feed (parziale: RecentlyAddedSection Issue #2612)
5. ✅ Chat History (già presente: ChatHistorySection)
6. ✅ Quick Actions (già presente: QuickActions)

### Phase 2 (Post-MVP)
7. 🔄 AI Insights & Suggestions (richiede RAG backend)
8. 🔄 Wishlist Highlights (richiede wishlist management)
9. 🔄 Catalog Trending (richiede analytics backend)

### Phase 3 (Enhancement)
10. 🔄 Achievements & Badges (gamification system)
11. 🔄 Advanced Activity Timeline (filtri, search)
12. 🔄 Personalized Recommendations (ML model)

---

## 🎯 Key Differences vs Opzione A

| Aspetto | Opzione A (Collection Focus) | Dashboard Hub (Questa) |
|---------|------------------------------|------------------------|
| **Scope** | Solo collezione giochi | Multi-sezione (library, sessions, chat, wishlist) |
| **Profondità** | Deep (flip cards, filtri avanzati) | Shallow (snapshot + CTA) |
| **Navigation** | Standalone page | Hub con collegamenti a pagine dedicate |
| **Use Case** | Gestire collezione dettagliatamente | Overview generale + quick access |
| **Ideal Path** | `/library` (pagina dedicata) | `/` o `/dashboard` (landing post-login) |

**Conclusione**: L'Opzione A (flip cards collection) dovrebbe diventare la **pagina `/library`**, mentre questa dashboard hub diventa il **landing post-login `/dashboard`**.

---

## 📊 Data Sources (API Endpoints)

*(blocco di codice rimosso)*

**Performance**: Singola chiamata aggregata vs. multiple chiamate per sezione (cache server-side 5 min)

---

Vuoi che creo il **componente React completo** per questa dashboard hub? Oppure preferisci prima discutere quali sezioni prioritizzare?

---



<div style="page-break-before: always;"></div>

## frontend/entity-link-card-relationships.md

# EntityLink — Card Relationships Design Spec

> **Status**: Design approved, ready for implementation
> **Last updated**: 2026-02-23
> **Epic**: EntityLink System + Card Navigation Graph Completion

---

## 1. Contesto

Il sistema EntityLink permette di creare collegamenti espliciti tra entità di tipo diverso.
I link sono visibili direttamente sulla MeepleCard e approfondibili nel Drawer.

### Tipi di entità supportati

| Tipo | Colore HSL | Icona |
|---|---|---|
| `game` | `25 95% 45%` (Orange) | Gamepad2 |
| `player` | `262 83% 58%` (Purple) | User |
| `session` | `240 60% 55%` (Indigo) | PlayCircle |
| `agent` | `38 92% 50%` (Amber) | Bot |
| `document` | `210 40% 55%` (Slate) | FileText |
| `chatSession` | `220 80% 55%` (Blue) | MessageCircle |
| `event` | `350 89% 60%` (Rose) | Calendar |
| `toolkit` | `142 70% 45%` (Green) | Wrench | ← **NUOVO** |

---

## 2. Link Type Taxonomy

### Colori per tipo (indipendenti dal colore entità)

| LinkType | Colore | Direzionalità | Descrizione |
|---|---|---|---|
| `expansion_of` | Amber `38 92% 50%` | → directed | Seafarers è espansione di Catan |
| `sequel_of` | Blue `220 80% 55%` | → directed | Pandemic Legacy S2 → S1 |
| `reimplements` | Orange `25 95% 45%` | → directed | Reimplementazione dell'originale |
| `companion_to` | Green `142 70% 45%` | ↔ bilateral | Giochi che si abbinano bene |
| `related_to` | Slate `210 40% 55%` | ↔ bilateral | Collegamento generico |
| `part_of` | Purple `262 83% 58%` | → directed | Session/Collection → Event |
| `collaborates_with` | Indigo `240 60% 55%` | ↔ bilateral | Agent pipeline multi-agente |
| `specialized_by` | Violet `270 70% 58%` | → directed | Agent generalista → specialista |

### Chip visuale per link type

*(blocco di codice rimosso)*

---

## 3. Regole di Dominio (Business Rules)

*(blocco di codice rimosso)*

---

## 4. Mockup — MeepleCard

### 4.1 Game Card (grid variant) — completa

*(blocco di codice rimosso)*

### 4.2 Agent Card con link

*(blocco di codice rimosso)*

### 4.3 Session Card con link

*(blocco di codice rimosso)*

---

## 5. Mockup — EntityLinkBadge

Badge glassmorphism nell'angolo in alto a destra della card image:

*(blocco di codice rimosso)*

- Mostra solo se count > 0
- Click → apre Drawer al tab Links
- Colore testo: neutral (non entity-colored)

---

## 6. Mockup — EntityLinkPreviewRow

Ultima riga del footer card, dopo il CardNavigationFooter:

*(blocco di codice rimosso)*

**Varianti:**
*(blocco di codice rimosso)*

Stile:
*(blocco di codice rimosso)*

---

## 7. Mockup — Drawer: Links Tab

*(blocco di codice rimosso)*

**Note UI:**
- Sezioni collassabili (accordion) se count > 3
- `[✕]` visibile solo se user è owner del link
- BGG-imported links: `[🌐 BGG]` badge invece di `[✕]` (non eliminabili)
- `[→]` apre nested drawer o naviga alla entità target

---

## 8. Mockup — EntityLinkCard (mini, riusabile)

*(blocco di codice rimosso)*

Props:
*(blocco di codice rimosso)*

---

## 9. Mockup — AddEntityLinkModal

*(blocco di codice rimosso)*

**Logica link type → target entity type:**
*(blocco di codice rimosso)*

---

## 10. Mockup — Graph View

### Layout: pagina `/library/[gameId]` — sezione "Connections"

*(blocco di codice rimosso)*

**Implementazione**: React Flow (`@xyflow/react`)

*(blocco di codice rimosso)*

**View toggle**: List (RelatedEntitiesSection) ↔ Graph (React Flow)
Persistenza: `localStorage` per preferenza list/graph per utente

---

## 11. Inventory Componenti

| Componente | File | Priorità | Note |
|---|---|---|---|
| `EntityLinkBadge` | `meeple-card/entity-link-badge.tsx` | 🔴 core | Corner top-right |
| `EntityLinkPreviewRow` | `meeple-card/entity-link-preview-row.tsx` | 🔴 core | Footer last row |
| `EntityLinkChip` | `ui/entity-link-chip.tsx` | 🔴 core | Riusabile ovunque |
| `EntityLinkCard` | `ui/entity-link-card.tsx` | 🔴 core | Mini-card in drawer |
| `RelatedEntitiesSection` | `ui/related-entities-section.tsx` | 🔴 core | Drawer Links tab |
| `AddEntityLinkModal` | `ui/add-entity-link-modal.tsx` | 🔴 core | Modal creazione |
| `EntityRelationshipGraph` | `ui/entity-relationship-graph.tsx` | 🟡 avanzato | React Flow graph |
| Links tab in Drawer | `meeple-card/extra-meeple-card-drawer.tsx` | 🔴 core | Estensione esistente |

---

## 12. Nuovi Props MeepleCard

*(blocco di codice rimosso)*

---

## 13. Epic Breakdown

### Epic A — EntityRelationships Backend (14 issues)

| # | Issue | BC |
|---|---|---|
| 1 | `EntityRelationships` BC scaffold + DDD folders | nuovo BC |
| 2 | `EntityLink` aggregate + `EntityLinkType` enum | Domain |
| 3 | EF Core config + migration `entity_links` | Infrastructure |
| 4 | `CreateEntityLinkCommand` + Validator + Handler | Application |
| 5 | `DeleteEntityLinkCommand` + Handler | Application |
| 6 | `GetEntityLinksQuery` (per source, con bidirezionalità) | Application |
| 7 | `GetEntityLinkCountQuery` (per badge) | Application |
| 8 | Endpoints user: `GET/POST/DELETE /api/v1/library/entity-links` | Routing |
| 9 | Endpoints admin: `GET/POST/DELETE /api/v1/admin/entity-links` | Routing |
| 10 | Fix: `Session.Games` da opzionale a `1..*` (migration + validator) | SessionTracking |
| 11 | Fix: `Agent` validazione min 1 KbCard dal Game associato | KnowledgeBase |
| 12 | `BggExpansionImporter` (fetch + auto-create EntityLinks) | Infrastructure |
| 13 | `adminClient` methods EntityLink (frontend api client) | Frontend |
| 14 | Tests: unit + integration + E2E (70+ test) | Tests |

### Epic B — GameToolkit (13 issues)

| # | Issue |
|---|---|
| 1 | `Toolkit` + `ToolkitWidget` domain + `WidgetType` enum |
| 2 | EF Core config + migration `toolkits`, `toolkit_widgets` |
| 3 | Auto-create default Toolkit on Game add to library |
| 4 | `GetToolkitQuery` + `OverrideToolkitCommand` |
| 5 | `ToolkitSessionState` in SessionTracking BC |
| 6 | Widget: `RandomGenerator` |
| 7 | Widget: `TurnManager` |
| 8 | Widget: `ScoreTracker` |
| 9 | Widget: `ResourceManager` |
| 10 | Widget: `NoteManager` |
| 11 | Widget: `Whiteboard` |
| 12 | `toolkit` MeepleEntityType + Card + Drawer |
| 13 | Tests |

### Epic C — Card Navigation Graph Completion (9 issues)

| # | Issue |
|---|---|
| 1 | `EntityLinkBadge` component |
| 2 | `EntityLinkPreviewRow` component |
| 3 | `EntityLinkChip` component |
| 4 | `EntityLinkCard` mini-card component |
| 5 | `RelatedEntitiesSection` + Links tab in Drawer |
| 6 | `AddEntityLinkModal` con search autocomplete |
| 7 | `EntityRelationshipGraph` (React Flow, list/graph toggle) |
| 8 | Aggiorna `ENTITY_NAVIGATION_GRAPH` (collection, event, toolkit) |
| 9 | Tests E2E navigation links |

---

## 14. Design Tokens di Riferimento

*(blocco di codice rimosso)*


---



<div style="page-break-before: always;"></div>

## frontend/game-session-toolkit-technical.md

# Game Session Toolkit - Technical Implementation Guide

**Audience**: Frontend Developers
**Last Updated**: 2026-01-30

---

## Architecture Overview

### State Management Flow

*(blocco di codice rimosso)*

### Component Hierarchy

*(blocco di codice rimosso)*

---

## State Management

### sessionStore Structure

*(blocco di codice rimosso)*

### Optimistic UI Pattern

*(blocco di codice rimosso)*

---

## SSE Integration

### useSessionSync Hook

**Purpose**: Manage SSE connection for real-time session updates

**Features**:
- EventSource connection to `/api/v1/sessions/{id}/stream`
- Auto-reconnect with exponential backoff (1s, 2s, 4s, 8s, 16s max)
- Connection status tracking
- Event type handlers

**Usage**:

*(blocco di codice rimosso)*

### Event Flow

*(blocco di codice rimosso)*

---

## Game Templates System

### Template Lookup Strategy

*(blocco di codice rimosso)*

### Template Application

**Game-Specific Sessions**:
1. User navigates to `/library/games/{gameId}/toolkit`
2. Fetch game details: `GET /api/v1/games/{gameId}`
3. Lookup template: `getGameTemplateByName(game.name)`
4. Display template preview (categories, rounds, rules)
5. On "Start Session": Pass `gameId` + `sessionType: 'GameSpecific'`
6. Active session loads template: `ScoreInput` pre-fills categories/rounds

**Fallback Behavior**:
- No template found → Generic session with empty categories/rounds
- User can manually add categories during session

---

## Testing Strategy

### Unit Test Coverage

**useSessionSync Hook** (5 tests):
- Initialization with default state
- EventSource creation with correct URL
- Callback prop acceptance
- Custom API base URL support
- Cleanup on unmount

**sessionStore** (10 tests):
- Create session success/error
- Join session by code
- Optimistic score update
- Optimistic rollback on error
- Pause/Resume/Finalize lifecycle

**game-templates** (17 tests):
- Template structure validation
- Slug lookup (exact, case-insensitive)
- Fuzzy name matching
- Player count validation
- Emoji icon validation

### E2E Test Scenarios

**toolkit-create-session.spec.ts**:
- Landing page display
- Create session flow
- Join by code validation
- Participant management
- Mobile responsive
- Dark mode

**toolkit-realtime-sync.spec.ts**:
- 2-browser context SSE sync
- Connection status indicator
- Pause/Resume lifecycle
- Finalize and redirect
- Optimistic UI verification

**game-toolkit-flow.spec.ts**:
- Library card toolkit button
- Template preview display
- Session creation with template
- Category pre-fill validation
- Player count validation

**session-history.spec.ts**:
- History page display
- Filter interactions
- Empty state navigation
- Modal interactions (planned Phase 2)

---

## Performance Considerations

### SSE Connection Management

**Max Reconnects**: 5 attempts
**Backoff Strategy**: Exponential (1s → 30s cap)
**Memory**: EventSource cleanup on unmount prevents leaks

### Optimistic UI

**Benefits**:
- Instant feedback (0ms perceived latency)
- Works during network instability
- Graceful degradation

**Trade-offs**:
- Potential flash if server rejects
- Requires rollback logic
- Slightly more complex state management

### Component Rendering

**Scoreboard**:
- Virtualization not needed (typical sessions: 2-10 players)
- Real-time animations use CSS transforms (GPU-accelerated)
- Score updates trigger minimal re-renders (React key optimization)

**Session History**:
- Pagination: 20 items/page (prevents DOM bloat)
- Lazy loading planned for Phase 2

---

## Security Considerations

### Session Code Generation

**Format**: 6-character uppercase alphanumeric
**Entropy**: 36^6 = 2,176,782,336 combinations
**Collision Risk**: Negligible for typical usage
**Backend Validation**: Required for join operations

### Score Manipulation

**Protection**:
- All score updates via authenticated endpoints
- Server validates session ownership
- Optimistic UI reverts on 401/403 errors
- SSE events broadcast to session participants only

### XSS Prevention

**Participant Names**: Sanitized by backend
**Session Notes**: Not yet implemented (planned sanitization)
**Avatar Colors**: Validated hex format on backend

---

## Accessibility (WCAG 2.1 AA)

### Keyboard Navigation

- Tab order: Filters → Session cards → Action buttons
- Enter/Space: Activate buttons and links
- Escape: Close modals

### Screen Readers

- ARIA labels on all interactive elements
- Session status announced
- Score updates announced (via toast)
- Modal dialogs properly labeled

### Color Contrast

- All text meets 4.5:1 ratio
- Status badges use semantic colors with sufficient contrast
- Dark mode: Enhanced contrast ratios

---

## Troubleshooting

### SSE Not Connecting

**Symptoms**: `isConnected` stays false, no real-time updates

**Debugging**:
1. Check browser console for EventSource errors
2. Verify backend `/stream` endpoint responds
3. Check CORS configuration
4. Verify session ID is valid

**Solution**:
*(blocco di codice rimosso)*

### Optimistic UI Shows Wrong Score

**Symptoms**: Score flashes different value after submission

**Cause**: Backend validation adjusted the score value

**Solution**: This is expected behavior - backend is source of truth

### Template Not Loading

**Symptoms**: Generic session loads instead of game template

**Debugging**:
*(blocco di codice rimosso)*

**Solution**: Add game to `GAME_TEMPLATES` or adjust fuzzy matching logic

---

## Code Review Checklist

Before submitting GST-related PRs:

- [ ] TypeCheck passes: `pnpm typecheck`
- [ ] Lint passes: `pnpm lint`
- [ ] Unit tests added: 85%+ coverage
- [ ] E2E tests for critical flows
- [ ] Mobile responsive tested
- [ ] Dark mode verified
- [ ] SSE cleanup in useEffect return
- [ ] Optimistic UI with rollback
- [ ] No unused imports/variables
- [ ] ARIA labels on interactive elements

---

## Related Files

**Core Implementation**:
- `apps/web/src/lib/hooks/useSessionSync.ts` - SSE hook
- `apps/web/src/lib/stores/sessionStore.ts` - State management
- `apps/web/src/lib/config/game-templates.ts` - Templates
- `apps/web/src/components/session/` - Reusable components

**Routes**:
- `apps/web/src/app/(authenticated)/toolkit/` - Generic toolkit routes
- `apps/web/src/app/(authenticated)/library/games/[gameId]/toolkit/` - Game-specific routes

**Tests**:
- `apps/web/src/lib/hooks/__tests__/useSessionSync.test.ts`
- `apps/web/src/lib/stores/__tests__/sessionStore.test.ts`
- `apps/web/src/lib/config/__tests__/game-templates.test.ts`
- `apps/web/__tests__/e2e/toolkit-*.spec.ts`

---

**Version**: 1.0.0
**Epic**: EPIC-GST-001
**Issues**: #3163, #3164, #3165


---



<div style="page-break-before: always;"></div>

## frontend/layout-components.md

# MeepleAI Layout Components Breakdown

> **Companion to**: `layout-spec.md`, `layout-wireframes.md`
> **Purpose**: Component architecture per sviluppatori React
> **Created**: 2026-02-01

---

## Component Tree Overview

*(blocco di codice rimosso)*

---

## 1. Layout Provider

### `LayoutProvider`

Context provider che gestisce lo stato globale del layout.

*(blocco di codice rimosso)*

**State derivato da:**
- Route corrente (next/navigation)
- Selezione utente
- Focus state
- Scroll position
- Viewport size

---

## 2. Navbar Components

### `Navbar`

*(blocco di codice rimosso)*

**Breakpoint behavior:**
- Mobile: Hamburger + Logo (center) + Search icon + Profile icon
- Tablet: Logo + Some nav items + Search + Profile
- Desktop: Logo + All nav items + Search input + Profile with name

---

### `HamburgerButton`

*(blocco di codice rimosso)*

**Animazione:** Hamburger ↔ X morph (200ms)

---

### `Logo`

*(blocco di codice rimosso)*

---

### `NavItems` / `NavItem`

*(blocco di codice rimosso)*

**Default items:**
*(blocco di codice rimosso)*

---

### `GlobalSearch`

*(blocco di codice rimosso)*

**Sub-components:**

*(blocco di codice rimosso)*

---

### `ProfileBar`

*(blocco di codice rimosso)*

**Sub-components:**

*(blocco di codice rimosso)*

---

### `HamburgerMenu`

*(blocco di codice rimosso)*

**Animazione:** Slide-in from left (250ms) + overlay fade

---

## 3. Smart FAB

### `SmartFAB`

*(blocco di codice rimosso)*

**Hooks interni:**

*(blocco di codice rimosso)*

---

### `QuickMenu`

*(blocco di codice rimosso)*

**Animazione:** Scale + fade in from FAB position (150ms)

---

## 4. Breadcrumb

### `Breadcrumb`

*(blocco di codice rimosso)*

**Animazione:** Fade + slide 8dp on context change (150ms)

---

## 5. ActionBar

### `ActionBar`

*(blocco di codice rimosso)*

---

### `ActionBarItem`

*(blocco di codice rimosso)*

---

### `OverflowMenu`

*(blocco di codice rimosso)*

---

### `MultiSelectBar`

*(blocco di codice rimosso)*

---

## 6. Shared Types

*(blocco di codice rimosso)*

---

## 7. Hooks

### `useLayoutContext`

*(blocco di codice rimosso)*

### `useActionBar`

*(blocco di codice rimosso)*

### `useFAB`

*(blocco di codice rimosso)*

### `useMultiSelect`

*(blocco di codice rimosso)*

### `useResponsive`

*(blocco di codice rimosso)*

### `useScrollDirection`

*(blocco di codice rimosso)*

---

## 8. Animation Utilities

### `useMorphTransition`

*(blocco di codice rimosso)*

### `useStaggeredAnimation`

*(blocco di codice rimosso)*

---

## 9. Context Configuration

### Action Definitions

*(blocco di codice rimosso)*

### FAB Configuration

*(blocco di codice rimosso)*

---

## 10. File Structure

*(blocco di codice rimosso)*

---

## 11. Component Count Summary

| Category | Components | Hooks | Config Files |
|----------|------------|-------|--------------|
| Layout Core | 3 | 2 | - |
| Navbar | 10 | - | 1 |
| HamburgerMenu | 2 | - | - |
| SmartFAB | 3 | 3 | 1 |
| Breadcrumb | 1 | - | - |
| ActionBar | 4 | 1 | 1 |
| Utilities | - | 4 | - |
| **Total** | **23** | **10** | **3** |

---

## 12. Implementation Priority

### Phase 1: Core Structure
1. `LayoutProvider` + types
2. `Layout` wrapper
3. `useResponsive` hook
4. Basic `Navbar` (logo + hamburger)

### Phase 2: Navigation
5. `NavItems` + `NavItem`
6. `HamburgerMenu`
7. `ProfileBar` (guest + user)
8. `GlobalSearch` (basic)

### Phase 3: ActionBar
9. `ActionBar` + `ActionBarItem`
10. `OverflowMenu`
11. `useActionBar` hook
12. Context-action mapping

### Phase 4: Smart FAB
13. `SmartFAB`
14. `useFAB` + `useLongPress`
15. `QuickMenu`
16. FAB visibility logic

### Phase 5: Polish
17. `Breadcrumb`
18. `MultiSelectBar`
19. All animations
20. Accessibility audit

---

## Appendix: Dependencies

*(blocco di codice rimosso)*


---



<div style="page-break-before: always;"></div>

## frontend/layout-spec.md

# MeepleAI Layout Specification v1.0

> **Status**: Approved
> **Created**: 2026-02-01
> **Authors**: Brainstorming Session

---

## Overview

Layout system per MeepleAI con focus su mobile-first, AI-first experience. Il design separa navigazione (top) da azioni contestuali (bottom) con un Smart FAB dinamico su mobile.

---

## 1. Struttura Generale

### Mobile (<640px)

*(blocco di codice rimosso)*

### Tablet (640-1024px)

*(blocco di codice rimosso)*

### Desktop (>1024px)

*(blocco di codice rimosso)*

---

## 2. Header Components

### 2.1 Navbar

| Elemento | Mobile | Tablet | Desktop |
|----------|--------|--------|---------|
| **Logo** | Centro, link home | Sinistra | Sinistra |
| **Nav primaria** | Hamburger (☰) | Inline parziale | Inline completo |
| **Search** | Icona → espande | Input medio | Input largo |
| **Search scope** | SharedGameCatalog | SharedGameCatalog | SharedGameCatalog |

#### Nav Items (quando visibili)
1. Libreria (`/library`)
2. Catalogo (`/catalog`)
3. Sessioni (`/sessions`)
4. (Hamburger only) Impostazioni, Aiuto, Documenti

### 2.2 ProfileBar

| Stato | Mobile | Desktop |
|-------|--------|---------|
| **Guest** | 👤 icona → Login modal | "Login" / "Registrati" buttons |
| **Logged in** | Avatar → dropdown | Avatar + username + dropdown |

#### Dropdown Menu (logged in)
- Profilo
- Le mie statistiche
- Impostazioni
- ---
- Logout

---

## 3. Smart FAB (Mobile Only)

### 3.1 Principio

FAB singolo che cambia icona/azione in base al contesto corrente. Mostra sempre l'azione più probabile per quel contesto.

### 3.2 Context Mapping

| Contesto | FAB Icon | Azione Primaria | Long-press Menu |
|----------|----------|-----------------|-----------------|
| Library Overview | ➕ | Aggiungi gioco | 🔍 Cerca, 📷 Scan barcode |
| Library vuota | ➕ | Aggiungi primo gioco | 🔗 Importa BGG, 📷 Scan |
| Game Detail (in libreria) | ▶️ | Nuova sessione | 🤖 Chiedi AI, 📤 Condividi |
| Game Detail (non in libreria) | ➕ | Aggiungi a libreria | ❤️ Wishlist, 📤 Condividi |
| Session Active | 🤖 | Chiedi all'AI | ⏱️ Timer, 📝 Punteggi |
| Session Setup | ▶️ | Inizia partita | 👥 Add player, 🎲 Randomize |
| Session End | 💾 | Salva risultati | 📤 Condividi, 🔄 Rigioca |
| Document Viewer | 🤖 | Chiedi su sezione | 🔍 Cerca nel doc, 📋 Copia |
| Catalog Browse | 🔍 | Ricerca avanzata | ➕ Proponi gioco |
| Search Results | ➕ | Aggiungi top result | 📋 Confronta giochi |
| Wishlist | 🛒 | Cerca dove comprare | ➕ Sposta in libreria |
| Notifications | ✅ | Segna tutte lette | 🗑️ Elimina tutte |

### 3.3 Visibility Rules

*(blocco di codice rimosso)*

### 3.4 Positioning

*(blocco di codice rimosso)*

### 3.5 Long-press Quick Menu

*(blocco di codice rimosso)*

- **Max items**: 2-3 azioni
- **Animazione**: scale + fade in (150ms)
- **Dismiss**: tap outside, selezione azione, back gesture
- **Haptic**: medium feedback on appear

### 3.6 Visual States

| Stato | Appearance |
|-------|------------|
| Default | 56dp, elevation 6, brand color |
| Hover/Focus | elevation 8, subtle glow |
| Pressed | 54dp, elevation 4, darker shade |
| Disabled | 40% opacity, no elevation |
| Loading | Spinner icon, pulsing |
| Hidden | Fade out 150ms |

---

## 4. Breadcrumb Contestuale

### 4.1 Scopo

Indicatore visivo del contesto corrente, sincronizzato con FAB e ActionBar.

### 4.2 Formato

*(blocco di codice rimosso)*

**Esempi:**
- 🎲 Settlers of Catan
- 📚 La mia libreria
- 🤖 Chat AI
- 📄 Regolamento.pdf
- 🔍 Risultati ricerca

### 4.3 Posizionamento

*(blocco di codice rimosso)*

### 4.4 Animazione

- **Transition**: fade + slide 8dp
- **Duration**: 150ms
- **Sync**: morphs insieme a FAB e ActionBar

---

## 5. ActionBar

### 5.1 Responsive Slots

| Breakpoint | Azioni Visibili | Overflow Menu |
|------------|-----------------|---------------|
| Mobile <640px | 3 | ⋮ sempre presente |
| Tablet 640-1024px | 4 | ⋮ se >4 azioni |
| Desktop >1024px | 5-6 | ⋮ se >6 azioni |

### 5.2 Priority System

Ogni azione ha priority 1-6. Le azioni con priority più bassa appaiono prima.

*(blocco di codice rimosso)*

### 5.3 Context Action Definitions

#### Library Overview
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Aggiungi gioco | ➕ |
| 2 | Filtra | 🔍 |
| 3 | Ordina | ↕️ |
| 4 | Cambia vista | 📊 |
| 5 | Esporta | 📤 |

#### Game Detail
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Nuova sessione | ▶️ |
| 2 | Aggiungi a libreria | ➕ |
| 3 | Chiedi regole AI | 🤖 |
| 4 | Wishlist | ❤️ |
| 5 | Condividi | 📤 |
| 6 | Segnala errore | 🐛 |

#### Session Active
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Chiedi all'AI | 🤖 |
| 2 | Timer | ⏱️ |
| 3 | Punteggi | 📝 |
| 4 | Pausa | ⏸️ |
| 5 | Termina | ✅ |

#### AI Chat
| Priority | Azione | Icon |
|----------|--------|------|
| 1 | Invia | 📤 |
| 2 | Allega | 📎 |
| 3 | Voice input | 🎤 |
| 4 | Nuova chat | 🔄 |
| 5 | Cronologia | 📋 |

### 5.4 Empty States

| Stato | ActionBar Behavior |
|-------|-------------------|
| Libreria vuota | CTA: `[➕ Aggiungi il tuo primo gioco]` |
| Nessun risultato | CTA: `[🔄 Modifica ricerca]` |
| Errore | Azioni disabilitate (grayed) + retry button |
| Loading | Skeleton placeholders |

### 5.5 Multi-selection Mode

Quando utente seleziona multipli elementi:

*(blocco di codice rimosso)*

- Prima icona: deseleziona tutto
- Counter: elementi selezionati
- Azioni: batch operations

---

## 6. Transitions & Animations

### 6.1 Morph Configuration

*(blocco di codice rimosso)*

### 6.2 Haptic Feedback (Mobile)

| Event | Feedback |
|-------|----------|
| FAB tap | Ripple + haptic light |
| FAB long-press | Tooltip + haptic medium |
| Context change | Subtle pulse + haptic light |
| Action success | Haptic success pattern |
| Action error | Haptic error pattern |

---

## 7. Conflict Resolution

### 7.1 Priority Rules

| Situazione | Comportamento |
|------------|---------------|
| Modal/Dialog aperto | ActionBar hidden, FAB hidden |
| Sidebar aperta (es. chat) | Primary content ActionBar prevale |
| Multi-selezione attiva | ActionBar switch a batch mode |
| Keyboard aperta (mobile) | ActionBar hidden, FAB hidden |
| Scroll veloce | FAB hidden temporarily |
| Error state critico | Focus su recovery, azioni limitate |

### 7.2 Z-Index Hierarchy

*(blocco di codice rimosso)*

---

## 8. Accessibility

### 8.1 Requirements

- [ ] FAB ha aria-label dinamico basato su contesto
- [ ] ActionBar navigabile via keyboard (Tab, Enter, Space)
- [ ] Quick menu dismissable via Escape
- [ ] Breadcrumb è landmark region
- [ ] Tutti i touch target >= 44dp
- [ ] Color contrast ratio >= 4.5:1
- [ ] Reduced motion rispettato

### 8.2 Screen Reader Announcements

| Event | Announcement |
|-------|-------------|
| Context change | "Ora in [context]. Azione principale: [action]" |
| FAB action | "[action] attivato" |
| Multi-select enter | "Modalità selezione multipla. [n] elementi selezionati" |
| Multi-select exit | "Selezione annullata" |

---

## 9. Technical Notes

### 9.1 State Management

*(blocco di codice rimosso)*

### 9.2 Context Detection

Il contesto viene determinato da:
1. Route corrente (`/library`, `/games/:id`, etc.)
2. Stato applicazione (sessione attiva, chat aperta)
3. Selezione utente (elementi selezionati)
4. Focus (input focused, modal open)

### 9.3 Performance Considerations

- FAB e ActionBar usano `will-change: transform` per GPU acceleration
- Morph animations usano solo `transform` e `opacity` (no layout thrashing)
- Long-press detection: 500ms threshold
- Scroll velocity detection: 50ms sampling rate

---

## Appendix A: Component Checklist

- [ ] `<Navbar />`
- [ ] `<ProfileBar />`
- [ ] `<HamburgerMenu />`
- [ ] `<GlobalSearch />`
- [ ] `<SmartFAB />`
- [ ] `<QuickMenu />`
- [ ] `<Breadcrumb />`
- [ ] `<ActionBar />`
- [ ] `<ActionBarItem />`
- [ ] `<OverflowMenu />`
- [ ] `<MultiSelectBar />`

---

## Appendix B: Revision History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-01 | Initial specification |


---



<div style="page-break-before: always;"></div>

## frontend/layout-wireframes.md

# MeepleAI Layout Wireframes

> **Companion to**: `layout-spec.md`
> **Purpose**: Visual reference per designer e sviluppatori
> **Created**: 2026-02-01

---

## 1. Mobile Wireframes (<640px)

### 1.1 Base Layout Structure

*(blocco di codice rimosso)*

### 1.2 Hamburger Menu Open

*(blocco di codice rimosso)*

### 1.3 Search Expanded

*(blocco di codice rimosso)*

### 1.4 FAB Long-press Quick Menu

*(blocco di codice rimosso)*

### 1.5 Multi-select Mode

*(blocco di codice rimosso)*

---

## 2. Tablet Wireframes (640-1024px)

### 2.1 Base Layout

*(blocco di codice rimosso)*

### 2.2 Split View (Tablet Landscape)

*(blocco di codice rimosso)*

---

## 3. Desktop Wireframes (>1024px)

### 3.1 Base Layout

*(blocco di codice rimosso)*

### 3.2 Logged-in User (ProfileBar)

*(blocco di codice rimosso)*

### 3.3 Library Grid View (Desktop)

*(blocco di codice rimosso)*

---

## 4. Context-Specific Wireframes

### 4.1 Game Detail Page (Mobile)

*(blocco di codice rimosso)*

### 4.2 Active Session (Mobile)

*(blocco di codice rimosso)*

### 4.3 Empty Library State (Mobile)

*(blocco di codice rimosso)*

---

## 5. Animation Storyboards

### 5.1 FAB Morph Transition

*(blocco di codice rimosso)*

### 5.2 ActionBar Context Switch

*(blocco di codice rimosso)*

### 5.3 Quick Menu Appear

*(blocco di codice rimosso)*

---

## 6. Spacing & Sizing Reference

### 6.1 Touch Targets

*(blocco di codice rimosso)*

### 6.2 Spacing System

*(blocco di codice rimosso)*

---

## 7. Color Usage Guide

### 7.1 Semantic Colors

*(blocco di codice rimosso)*

### 7.2 State Colors

*(blocco di codice rimosso)*

---

## 8. Accessibility Annotations

### 8.1 Focus Order

*(blocco di codice rimosso)*

### 8.2 ARIA Labels

*(blocco di codice rimosso)*

---

## Appendix: Export Checklist for Designer

- [ ] Mobile base layout (375×812)
- [ ] Tablet base layout (768×1024)
- [ ] Desktop base layout (1440×900)
- [ ] Hamburger menu open state
- [ ] Search expanded state
- [ ] FAB all states (default, hover, pressed, loading)
- [ ] Quick menu open
- [ ] Multi-select mode
- [ ] Empty states (library, search, errors)
- [ ] Context variations (library, game, session, chat)
- [ ] Animation specs (Lottie or After Effects)
- [ ] Dark mode variants
- [ ] Accessibility focus states


---



<div style="page-break-before: always;"></div>

## frontend/meeple-card-v2-design-tokens.md

# MeepleCard v2 Design Tokens

**Epic #4604** | **Created**: 2026-02-17

Comprehensive design token reference for MeepleCard v2 visual redesign.

## Color System

### Entity Colors (HSL)

All 7 entity types use consistent HSL color format for easy theme customization:

*(blocco di codice rimosso)*

**Usage Examples**:
- **Border accent**: Left edge 4px → 6px on hover
- **Entity badge**: Top-left pill with entity name
- **Glow rings**: `outline-2` with `hsla(var(--color-entity-game), 0.4)`
- **Gradient overlays**: Hero/featured variants use entity color gradients
- **Flip card header**: Entity-colored header with diagonal stripe pattern

### Shadows - Warm Toned

v2 shadows use warm brown tones instead of neutral grays for a premium, organic feel:

*(blocco di codice rimosso)*

**Application**:
- Grid: `shadow-warm-sm` → `shadow-warm-xl` on hover
- Featured: `shadow-warm-md` → `shadow-warm-xl` on hover
- Hero: `shadow-warm-xl` → `shadow-warm-2xl` on hover
- Carousel center: `shadow-warm-2xl` always

## Typography

### Font Families

*(blocco di codice rimosso)*

**Usage**:
- **Card titles**: `font-quicksand font-bold` (700 weight)
- **Body text**: `font-nunito` (400-600 weight)
- **Entity badges**: `font-quicksand font-bold text-[10px] uppercase tracking-wider`
- **Metadata**: `font-nunito font-semibold text-xs`

### Text Shadows (Hero Variant)

*(blocco di codice rimosso)*

## Layout & Spacing

### Border Radius

*(blocco di codice rimosso)*

### Card Padding

| Variant | Content Padding | Notes |
|---------|----------------|-------|
| Grid | `p-4` (1rem) | Compact vertical |
| List | `p-3` (0.75rem) | Horizontal layout |
| Compact | `p-2` (0.5rem) | Minimal |
| Featured | `px-5 py-4` | More spacious |
| Hero | `p-6` (1.5rem) | Maximum breathing room |

### Aspect Ratios

| Variant | Cover Aspect | Rationale |
|---------|-------------|-----------|
| Grid | `7/10` | Board game card proportions |
| List | `64x64px` | Square thumbnail |
| Compact | `40x40px` | Minimal square |
| Featured | `16/9` | Widescreen promo |
| Hero | `absolute inset-0` | Full background |

## Visual Effects

### Glassmorphism

*(blocco di codice rimosso)*

**Quick Action Buttons**:
*(blocco di codice rimosso)*

### Hover Transforms

| Element | Default | Hover | Transition |
|---------|---------|-------|------------|
| Grid card | `translate-y-0` | `-translate-y-1.5` | `350ms cubic-bezier(0.4,0,0.2,1)` |
| Featured card | `translate-y-0` | `-translate-y-2` | `350ms cubic-bezier(0.4,0,0.2,1)` |
| Hero card | `scale-1` | `scale-[1.01]` | `350ms cubic-bezier(0.4,0,0.2,1)` |
| Cover image | `scale-100` | `scale-105` | `500ms` |
| Quick action | `scale-100` | `scale-110` | `300ms ease-out` |

### Entity Glow Rings

*(blocco di codice rimosso)*

Example for game entity:
*(blocco di codice rimosso)*

### Shimmer Effect

Applied to cover images on hover:

*(blocco di codice rimosso)*

## Animations

### Tag Pulse (for "new" tags)

*(blocco di codice rimosso)*

### Quick Actions Float-Up

Staggered animation for quick action buttons reveal:

*(blocco di codice rimosso)*

## 3D Card Flip

### Flip Container

*(blocco di codice rimosso)*

### Flip Animation

*(blocco di codice rimosso)*

### Card Faces

*(blocco di codice rimosso)*

### Card Back Header

Entity-colored header with diagonal stripe pattern:

*(blocco di codice rimosso)*

## Carousel Scaling

| Position | Scale | Opacity | Blur | Notes |
|----------|-------|---------|------|-------|
| Center (focus) | `1.1` | `1.0` | `0px` | Enhanced prominence |
| Side (±1) | `0.85` | `0.6` | `2px` | Fade background |
| Far (±2+) | `0.5` | `0` | `0px` | Hidden |

**Center Card Enhancements**:
- Shadow: `var(--shadow-warm-2xl)`
- Quick actions: Always visible (not just hover)
- Info button: Always visible
- Entity glow ring: Inherited from card hover

## Responsive Behavior

All visual effects respect `prefers-reduced-motion`:

*(blocco di codice rimosso)*

## Dark Mode Specifics

Dark mode automatically adjusts:
- Shadows: Neutral black instead of warm brown
- Glassmorphism: `rgba(30, 25, 20, 0.7)` backdrop
- Entity colors: Same HSL values (work in both modes)
- Text: Uses semantic `--card-foreground` token
- Borders: Uses semantic `--border` token

## Migration Notes

**Breaking Changes**: None - all changes are visual enhancements.

**New Props**:
- FlipCard: `entityColor`, `entityName`, `title` (optional)
- TagBadge: `animated` (optional, auto-enabled for "new" tags)

**Deprecated**: None

**Consumer Site Impact**: Zero code changes required, visual improvements apply automatically.

## References

- **Mockup**: `apps/web/src/components/ui/meeple-card-v2-mockup.html`
- **PR**: #4619
- **Epic**: #4604
- **Design System**: `apps/web/src/styles/design-tokens.css`


---



<div style="page-break-before: always;"></div>

## frontend/migrations/DASHBOARD-HUB-MIGRATION-GUIDE.md

# Dashboard Hub Migration Guide

**From**: UserDashboard.tsx (legacy, 1137 lines)
**To**: Dashboard.tsx (modern hub layout)
**Issue**: #3910 - Dashboard Hub Layout Refactoring + Cleanup Legacy
**Date**: 2026-02-09

---

## Migration Overview

### What Changed

**Removed** (1137 lines total):
- `UserDashboard.tsx` - Monolithic dashboard component
- `UserDashboardCompact.tsx` - Compact variant
- `dashboard-client.tsx` - Legacy client component
- Mock constants: `MOCK_STATS`, `MOCK_QUICK_GAMES`, `MOCK_ACTIVITIES`
- Sub-components: `StatCardCompact`, `QuickGameCard`, `ActivityRow`

**Added** (Modular approach):
- `Dashboard.tsx` - Main hub layout (Section-based)
- `LibrarySnapshot.tsx` - Library widget (Issue #3912)
- `ActivityFeed.tsx` - Activity timeline (Issue #3911)
- `QuickActionsGrid.tsx` - Enhanced quick actions (Issue #3913)
- `HeroStats.tsx` - Stats overview
- `DashboardSection.tsx` - Reusable section wrapper
- Responsive layout (mobile/tablet/desktop) (Issue #3914)

---

## Breaking Changes

### Import Paths Changed

**Before**:
*(blocco di codice rimosso)*

**After**:
*(blocco di codice rimosso)*

---

### Component API Changed

**Before** (UserDashboard.tsx):
*(blocco di codice rimosso)*

**After** (Dashboard.tsx - Section-based):
*(blocco di codice rimosso)*

---

### Data Fetching Changed

**Before** (Client-side fetch in UserDashboard):
*(blocco di codice rimosso)*

**After** (Aggregated API in page.tsx):
*(blocco di codice rimosso)*

**Benefits**:
- Single API call vs multiple
- Reduced client-side fetching
- Better performance (< 500ms cached vs 3-5s multiple calls)
- Server-side data aggregation

---

### Styling Changed

**Before** (Inline styles + CSS modules):
*(blocco di codice rimosso)*

**After** (Tailwind + Glassmorphism):
*(blocco di codice rimosso)*

**Design System**:
- Glassmorphic cards: `bg-white/70 backdrop-blur-md`
- Amber accent: `bg-amber-100 text-amber-900`
- Fonts: `font-quicksand` (headings), `font-nunito` (body)

---

## Migration Steps

### Step 1: Update Page Component

**File**: `apps/web/src/app/(authenticated)/dashboard/page.tsx`

**Before**:
*(blocco di codice rimosso)*

**After**:
*(blocco di codice rimosso)*

---

### Step 2: Remove Legacy Imports

**Search and replace** across codebase:

*(blocco di codice rimosso)*

**If found**, update import:
*(blocco di codice rimosso)*

---

### Step 3: Update Component Usage

**Pattern**: Replace monolithic UserDashboard with modular sections

**Before**:
*(blocco di codice rimosso)*

**After**:
*(blocco di codice rimosso)*

---

### Step 4: Update API Calls

**Consolidate multiple API calls** into single aggregated endpoint

**Before** (Multiple calls):
*(blocco di codice rimosso)*

**After** (Single aggregated call):
*(blocco di codice rimosso)*

**Performance Improvement**: 3-5s (multiple calls) → < 500ms (single aggregated, cached)

---

### Step 5: Test Migration

**Unit Tests**:
*(blocco di codice rimosso)*

**E2E Tests**:
*(blocco di codice rimosso)*

**TypeScript**:
*(blocco di codice rimosso)*

---

## Feature Mapping

### Legacy → Modern Component Mapping

| Legacy Feature | Modern Component | Status |
|----------------|------------------|--------|
| Stats overview | HeroStats.tsx | ✅ Enhanced |
| Library quota | LibrarySnapshot.tsx | ✅ Enhanced |
| Recent games | LibrarySnapshot.topGames | ✅ Enhanced |
| Quick actions | QuickActionsGrid.tsx | ✅ Enhanced |
| Activity feed | ActivityFeed.tsx | ✅ New implementation |
| Chat history | ChatHistorySection.tsx | ✅ Enhanced |
| Active sessions | ActiveSessionsWidget.tsx | ✅ Enhanced |

---

### Functionality Additions

**New Features** (not in legacy):
- ✅ Responsive layout (mobile/tablet/desktop)
- ✅ Glassmorphic design (modern aesthetic)
- ✅ Section-based architecture (modular, reusable)
- ✅ SSE real-time updates (/api/v1/dashboard/stream)
- ✅ AI insights widget (Epic #3905)
- ✅ Wishlist highlights (Epic #3905)
- ✅ Catalog trending (Epic #3905)
- ✅ Achievements widget (Epic #3906)
- ✅ Timeline filters & search (Epic #3906)

**Performance Improvements**:
- ✅ Aggregated API (< 500ms vs 3-5s)
- ✅ Redis caching (5-min TTL)
- ✅ Cache invalidation on user actions
- ✅ Lazy loading images
- ✅ Skeleton loading states

---

## Rollback Procedure (Emergency)

**If migration causes issues**, rollback is NOT possible because legacy code was removed.

**Alternative**:
1. Revert PR that removed UserDashboard.tsx
2. Restore from git history:
   *(blocco di codice rimosso)*
3. Fix imports
4. Rebuild

**Recommendation**: DO NOT ROLLBACK. Fix forward instead. Dashboard.tsx is tested and functional.

---

## FAQ

### Q: Can I use both UserDashboard and Dashboard?

**A**: No. UserDashboard.tsx was removed in Issue #3910. Use Dashboard.tsx only.

---

### Q: How do I customize the dashboard layout?

**A**: Use `sections` prop to define which widgets to display:

*(blocco di codice rimosso)*

---

### Q: Can I add custom sections?

**A**: Yes. Create your component and add to sections:

*(blocco di codice rimosso)*

---

### Q: How do I handle loading states?

**A**: Use `isLoading` prop on individual components:

*(blocco di codice rimosso)*

Components have built-in Skeleton loading states.

---

### Q: Mobile layout broken after migration?

**A**: Ensure responsive classes applied:

*(blocco di codice rimosso)*

Test: Resize browser < 640px, should see single column.

---

## Support

**Issues**: Create issue on GitHub with label `area/dashboard`
**Documentation**: `docs/frontend/dashboard-hub-guide.md`
**Tests**: `apps/web/e2e/dashboard-user-journey.spec.ts`

---

**Migration Status**: ✅ COMPLETE (Issue #3910 closed)


---



<div style="page-break-before: always;"></div>

## frontend/navigability-analysis.md

# Analisi della Navigabilità — MeepleAI Web App

**Data**: 2026-02-21
**Stato**: ✅ Implementato — Tutti i fix critici e alti applicati
**Scope**: Frontend — `apps/web/src`

---

## Indice

1. [Architettura attuale della navigazione](#1-architettura-attuale)
2. [Mappa delle route](#2-mappa-delle-route)
3. [Problemi critici identificati](#3-problemi-critici)
4. [Analisi per componente](#4-analisi-per-componente)
5. [Matrice di accessibilità dei link](#5-matrice-di-accessibilità)
6. [Raccomandazioni](#6-raccomandazioni)
7. [Piano di priorità](#7-piano-di-priorità)

---

## 1. Architettura attuale

### Layout complessivo

*(blocco di codice rimosso)*

### File chiave

| File | Responsabilità |
|------|---------------|
| `config/navigation.ts` | Source of truth — `UNIFIED_NAV_ITEMS` |
| `components/layout/Sidebar/SidebarContextNav.tsx` | Routing context → pannello sidebar |
| `components/layout/Sidebar/SidebarNav.tsx` | Navigazione standard (default context) |
| `components/layout/Navbar/UniversalNavbar.tsx` | Navbar globale con logo → `/dashboard` |
| `components/layout/ActionBar/UnifiedActionBar.tsx` | Bottom nav mobile |
| `components/layout/MobileNavDrawer.tsx` | Drawer hamburger mobile |
| `components/layout/Breadcrumb/Breadcrumb.tsx` | Indicatore contesto (solo mobile) |

---

## 2. Mappa delle route

### Route principali autenticate

*(blocco di codice rimosso)*

### Context map del SidebarContextNav

| Pathname | Pannello mostrato | Link Dashboard? |
|----------|-------------------|-----------------|
| `/dashboard` | DashboardPanel | ✅ Sì (Overview) |
| `/library/*` | LibraryPanel | ❌ No |
| `/games/*` | GamesPanel | ❌ No |
| `/play-records` | SidebarNav (default) | ✅ Sì |
| `/players` | SidebarNav (default) | ✅ Sì |
| `/chat` | SidebarNav (default) | ✅ Sì |
| `/profile` | SidebarNav (default) | ✅ Sì |
| `/settings` | SidebarNav (default) | ✅ Sì |
| `/agents` | SidebarNav (default) | ✅ Sì |
| `/sessions` | SidebarNav (default) | ✅ Sì |
| `/admin/*` | SidebarNav (default) | ✅ Sì |

---

## 3. Problemi critici

### P1 — CRITICO: LibraryPanel senza link "← Dashboard"

**Impatto**: Chiunque navighi in `/library/*` perde visibilità del link Dashboard nella sidebar.

**Riproducibilità**:
1. Vai su `/dashboard`
2. Clicca "Libreria" nella sidebar → vai su `/library`
3. La sidebar diventa LibraryPanel
4. **Non esiste più "Dashboard" nella sidebar**
5. L'unico ritorno è il logo nella navbar (non ovvio)

**File coinvolto**: `SidebarContextNav.tsx` — `LibraryPanel` (riga 110-124)

*(blocco di codice rimosso)*

---

### P2 — CRITICO: GamesPanel senza link "← Dashboard"

**Impatto**: Uguale a P1 ma per tutte le route `/games/*` incluso `/games/[id]`.

**Riproducibilità**:
1. Vai su `/dashboard`
2. Clicca "Catalogo giochi" → vai su `/games`
3. La sidebar diventa GamesPanel (solo filtri)
4. Naviga su `/games/some-id` (dettaglio gioco)
5. **Nessun link Dashboard né breadcrumb "← Catalogo"**

**File coinvolto**: `SidebarContextNav.tsx` — `GamesPanel` (riga 126-142)

---

### P3 — ALTO: Nessun breadcrumb desktop

**Impatto**: Gli utenti desktop non hanno un indicatore visivo del percorso corrente. Il componente `Breadcrumb` è dichiaratamente mobile-only (position fixed bottom, sopra ActionBar).

**File coinvolto**: `components/layout/Breadcrumb/Breadcrumb.tsx`

*(blocco di codice rimosso)*

**Pagine problematiche**:
- `/library/favorites` → nessun percorso visivo "Dashboard → Libreria → Preferiti"
- `/games/[id]` → nessun percorso visivo "Dashboard → Catalogo → Nome Gioco"
- `/admin/analytics` → nessun percorso "Dashboard → Admin → Analytics"

---

### P4 — ALTO: Route orfane — link esistono solo in DashboardPanel

Le seguenti route sono accessibili **solo** quando si è su `/dashboard` (DashboardPanel) e scompaiono dalla navigazione una volta entrati:

| Route | Dove è linkato | Disponibile fuori dal Dashboard? |
|-------|---------------|----------------------------------|
| `/play-records` | DashboardPanel "Sessioni recenti" | ❌ No (non in UNIFIED_NAV_ITEMS) |
| `/players` | DashboardPanel "Giocatori" | ❌ No (non in UNIFIED_NAV_ITEMS) |

**Scenario problematico**:
- Utente su `/play-records` vuole tornare all'elenco → la sidebar mostra SidebarNav standard → `/play-records` NON è nell'elenco → utente non trova più la voce.
- Navigare "avanti" da `/play-records` (es. su un dettaglio) → ancora più lost.

---

### P5 — ALTO: Route completamente inaccessibili dalla navigazione principale

| Route | Accessibile da | Nota |
|-------|----------------|------|
| `/knowledge-base` | Nessun link nel nav principale | Nessuna voce in UNIFIED_NAV_ITEMS |
| `/toolkit` | Nessun link visibile (priority 4, rimosso da header) | In navigation.ts commento: "removed from header, kept for ActionBar" ma non presente in NAV_ITEMS |
| `/settings` | Solo ProfileBar dropdown | Non nel nav principale |
| `/editor` | Solo ProfileBar dropdown (minRole: editor) | Corretto per ruolo, ma scomparso |

---

### P6 — MEDIO: Cambio di sidebar confuso (context switch)

**Problema UX**: Il contenuto della sidebar cambia completamente a seconda della sezione. Un utente abituale impara dove trovare Dashboard nella SidebarNav standard, ma quando entra in `/library` o `/games`, quella voce sparisce e appare un pannello contestuale diverso.

**Attesa utente**: La navigazione "principale" rimane sempre visibile; i link contestuali si aggiungono.
**Comportamento attuale**: La navigazione principale viene **sostituita** dai link contestuali.

---

### P7 — MEDIO: Logo come unico "tasto Home" persistente non è ovvio

Il logo nella navbar (`<Link href="/dashboard">`) è l'unico elemento di navigazione **sempre visibile** che porta alla dashboard. Questo è un pattern UX noto ma richiede che l'utente sappia che il logo è cliccabile.

**Problematico per**:
- Nuovi utenti non abituati all'app
- Contesti dove il logo è piccolo o collassato

---

### P8 — BASSO: `/games/[id]` mostra GamesPanel con filtri lista, non contesto dettaglio

**Impatto**: Quando si apre un dettaglio gioco (`/games/abc123`), la sidebar mostra filtri per la lista catalogo (Top BGG, 2 Giocatori, ecc.). Questi filtri non sono contestualmente rilevanti per una pagina di dettaglio.

**Aspettativa**: Il pannello dettaglio gioco dovrebbe avere link come "← Torna al catalogo", "Sessioni con questo gioco", "Aggiungi alla libreria".

---

## 4. Analisi per componente

### 4.1 SidebarContextNav — Riepilogo criticità

*(blocco di codice rimosso)*

**Soluzione minima**: Aggiungere un link "← Dashboard" (o separatore + link) in cima a `LibraryPanel` e `GamesPanel`.

### 4.2 UNIFIED_NAV_ITEMS — Route mancanti

Dalla `config/navigation.ts` attuale:

*(blocco di codice rimosso)*

**Analisi**: `play-records` e `players` sono route di navigazione frequente (esposte nel DashboardPanel) ma non hanno una voce stabile nella navigazione globale.

### 4.3 UniversalNavbar — Punti di forza e lacune

**Punti di forza**:
- ✅ Logo sempre linkato a `/dashboard` (h-14, fisso top, sempre visibile)
- ✅ Ricerca giochi funzionale con link diretto a `/games/{id}`
- ✅ ProfileBar con accesso a `/profile`, `/settings`, `/editor`, `/admin/overview`

**Lacune**:
- ❌ Nessuna breadcrumb integrata nella navbar su desktop
- ❌ Il ProfileBar mostra `/settings` ma questa non è nella nav principale

### 4.4 UnifiedActionBar (Mobile) — Copertura

| Item | Link | Presente? |
|------|------|-----------|
| Home | `/dashboard` | ✅ |
| Libreria | `/library` | ✅ |
| FAB | `/chat` | ✅ |
| Catalogo | `/games` | ✅ |
| Play Records | `/play-records` | ❌ |
| Giocatori | `/players` | ❌ |
| Sessioni | `/sessions` | ❌ (overflow?) |
| Agenti | `/agents` | ❌ (overflow?) |

La mobile ActionBar copre solo 3 voci + FAB. Il resto va nel hamburger menu o è inaccessibile direttamente.

### 4.5 MobileNavDrawer — Copertura

Il drawer hamburger usa `useNavigationItems()` che restituisce le voci di `UNIFIED_NAV_ITEMS`:
- ✅ Dashboard, Library, Chat, Catalog, Agents, Sessions
- ❌ Play Records, Players, Knowledge Base non sono inclusi

---

## 5. Matrice di accessibilità dei link

La seguente tabella indica da quale pagina si può raggiungere ogni route, tramite quale meccanismo:

| Route destinazione | Via Navbar Logo | Via Sidebar (Desktop) | Via ActionBar (Mobile) | Via Hamburger (Mobile) | Via Breadcrumb | Via Link in pagina |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| `/dashboard` | ✅ sempre | ✅ solo se default context | ✅ Home | ✅ | ❌ | dipende |
| `/library` | ❌ | ✅ via SidebarNav o LibraryPanel | ✅ | ✅ | ❌ | dipende |
| `/games` | ❌ | ✅ | ✅ Catalogo | ✅ | ❌ | dipende |
| `/chat` | ❌ | ✅ | ✅ FAB | ✅ | ❌ | — |
| `/play-records` | ❌ | ✅ solo da Dashboard context | ❌ | ❌ | ❌ | ⚠️ solo da dashboard page |
| `/players` | ❌ | ✅ solo da Dashboard context | ❌ | ❌ | ❌ | ⚠️ solo da dashboard page |
| `/profile` | ❌ | ❌ (hideFromMainNav) | ❌ | ❌ | ❌ | ✅ ProfileBar |
| `/settings` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ ProfileBar |
| `/agents` | ❌ | ✅ (strumenti) | ❌ | ✅ | ❌ | — |
| `/sessions` | ❌ | ✅ (strumenti) | ❌ | ✅ | ❌ | — |
| `/knowledge-base` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ **INACCESSIBILE** |
| `/admin/**` | ❌ | ❌ (solo se autenticato admin) | ❌ | ❌ | ❌ | ✅ ProfileBar (admin) |

**Legenda**: ✅ = sempre accessibile | ⚠️ = condizionale | ❌ = non accessibile via questo meccanismo

---

## 6. Raccomandazioni

### R1 — Aggiungere link "← Dashboard" a LibraryPanel e GamesPanel (P1, P2)

**Priorità**: CRITICA
**File**: `SidebarContextNav.tsx`
**Pattern suggerito**:

*(blocco di codice rimosso)*

Alternativa: aggiungere sempre una sezione "Home" in cima a ogni pannello contestuale.

---

### R2 — Breadcrumb desktop sopra il contenuto principale (P3)

**Priorità**: ALTA
**File**: `AuthenticatedLayout.tsx`, nuovo componente `DesktopBreadcrumb`
**Approccio**: Breadcrumb orizzontale statico posizionato nella zona di contenuto (sotto navbar, sopra pagina), visibile solo su desktop (`hidden md:flex`).

*(blocco di codice rimosso)*

---

### R3 — Aggiungere `/play-records` e `/players` a UNIFIED_NAV_ITEMS (P4)

**Priorità**: ALTA
**File**: `config/navigation.ts`
**Opzione A**: Aggiungerli come voci di primo livello (gruppo "strumenti" o separato)
**Opzione B**: Aggiungerli come figli di "Dashboard" in un pannello dedicato

---

### R4 — Aggiungere `/knowledge-base` alla navigazione (P5)

**Priorità**: ALTA — route attualmente inaccessibile
**File**: `config/navigation.ts`
**Azione**: Aggiungere voce `knowledge-base` in UNIFIED_NAV_ITEMS, probabilmente nel gruppo "strumenti" o come voce principale.

---

### R5 — Breadcrumb o back-button su pagine dettaglio (P8)

**Priorità**: MEDIA
**File**: `app/(authenticated)/games/[id]/page.tsx`
**Approccio**: Aggiungere un `<BackButton>` inline nella pagina o nel layout, per tornare a `/games`.

---

### R6 — Riconsiderare la strategia di context switch della sidebar (P6)

**Priorità**: MEDIA
**Opzioni**:

**A) Navigation primaria sempre visibile** (raccomandato):
- Sidebar divisa in due zone:
  - Zona fissa superiore: link principali sempre visibili (Dashboard, Library, Games, Chat)
  - Zona contestuale inferiore: link specifici del contesto corrente

**B) Back link persistente nel pannello contestuale** (soluzione minima):
- Ogni pannello contestuale mostra "← Dashboard" come prima voce
- Mantiene l'approccio attuale ma risolve il problema di ritorno

**C) Breadcrumb prominente** (complementare):
- Sopra il contenuto principale, breadcrumb fisso (già raccomandato in R2)

---

## 7. Piano di priorità

| # | Issue | Priorità | Stato | Note |
|---|-------|----------|-------|------|
| R1 | Back link in LibraryPanel e GamesPanel | 🔴 CRITICA | ✅ Fatto | `SidebarContextNav.tsx` — link Dashboard in cima a ogni pannello contestuale |
| R4 | `/knowledge-base` nella navigazione | 🔴 CRITICA | ✅ Fatto | `config/navigation.ts` — aggiunto in gruppo `strumenti` |
| R3 | `/play-records` e `/players` in nav | 🟠 ALTA | ✅ Fatto | `config/navigation.ts` — aggiunti in gruppo `strumenti` |
| R2 | Breadcrumb desktop | 🟠 ALTA | ✅ Fatto | Nuovo `DesktopBreadcrumb.tsx` + fix `AuthenticatedLayout.tsx` |
| R5 | Back button su `/games/[id]` | 🟡 MEDIA | ✅ Già presente | Il componente aveva già `<ArrowLeft /> Torna al Catalogo` (riga 248) |
| R6 | Strategia sidebar ibrida | 🟡 MEDIA | 🕐 Posticipato | R1 risolve il caso critico; la ristrutturazione completa è un refactor a sé |

---

## Appendice: Componenti e file di riferimento

*(blocco di codice rimosso)*

---

*Documento generato il 2026-02-21 — Analisi della navigabilità MeepleAI Web*


---



<div style="page-break-before: always;"></div>

## frontend/storybook-guide.md

# Storybook Guide

Guide for developing, documenting, and testing UI components with Storybook.

## Quick Start

*(blocco di codice rimosso)*

## File Structure

Stories are colocated with their components:

*(blocco di codice rimosso)*

## Creating a New Story

### 1. Basic Story Template

*(blocco di codice rimosso)*

### 2. Story Categories

Use consistent title patterns:

| Category | Title Pattern | Example |
|----------|---------------|---------|
| UI Primitives | `UI/[Component]` | `UI/Button` |
| Data Display | `UI/[Component]` | `UI/Card` |
| Overlays | `UI/[Component]` | `UI/Dialog` |
| Forms | `Forms/[Component]` | `Forms/LoginForm` |
| Admin | `Admin/[Component]` | `Admin/UserTable` |
| Layout | `Layout/[Component]` | `Layout/Header` |
| Features | `[Feature]/[Component]` | `Chat/MessageList` |

### 3. Decorators

Add context providers or wrappers:

*(blocco di codice rimosso)*

### 4. Dark Theme Story

*(blocco di codice rimosso)*

## Available Addons

| Addon | Purpose | Usage |
|-------|---------|-------|
| **Docs** | Auto-generate documentation | Add `tags: ['autodocs']` |
| **A11y** | Accessibility testing | Automatic panel in toolbar |
| **Themes** | Light/dark theme switching | Theme button in toolbar |
| **Viewport** | Responsive testing | Viewport dropdown in toolbar |
| **Chromatic** | Visual regression testing | Run `pnpm chromatic` |
| **MSW** | API mocking | See MSW section below |

## Viewport Testing

Pre-configured viewports for responsive testing:

| Viewport | Size | Type |
|----------|------|------|
| Mobile | 375×667 | Mobile |
| Mobile Large | 414×896 | Mobile |
| Tablet | 768×1024 | Tablet |
| Laptop | 1024×768 | Desktop |
| Desktop | 1280×800 | Desktop |
| Desktop Large | 1440×900 | Desktop |

Override per-story:

*(blocco di codice rimosso)*

## API Mocking with MSW

Mock API calls in stories:

*(blocco di codice rimosso)*

## Accessibility Testing

The a11y addon runs automatic accessibility checks. View results in the Accessibility panel.

For custom a11y rules per-story:

*(blocco di codice rimosso)*

## Visual Testing with Chromatic

### Local Testing

*(blocco di codice rimosso)*

### CI Integration

Visual tests run automatically on PRs. See `.github/workflows/storybook-deploy.yml`.

### Best Practices

1. **Deterministic data**: Avoid `Math.random()`, `Date.now()`, or dynamic content
2. **Fixed viewports**: Set explicit dimensions for layout stories
3. **Mock external resources**: Use placeholder images and mock APIs
4. **Stable animations**: Disable or pause animations for consistent snapshots

## Common Patterns

### Interactive Component

*(blocco di codice rimosso)*

### Form Example

*(blocco di codice rimosso)*

### Grid/Comparison

*(blocco di codice rimosso)*

## Troubleshooting

### Story not appearing

- Ensure file ends with `.stories.tsx`
- Check `title` matches expected pattern
- Restart Storybook: `pnpm storybook`

### Styling issues

- Verify `globals.css` is imported in preview.ts
- Check Tailwind classes are valid
- Inspect with browser devtools

### TypeScript errors

- Use `satisfies Meta<typeof Component>` for type safety
- Define explicit `Story` type: `type Story = StoryObj<typeof meta>`

### Auth context errors

- MockAuthProvider is configured globally in preview.ts
- Override per-story with custom decorators if needed

## Resources

- [Storybook Docs](https://storybook.js.org/docs)
- [shadcn/ui Components](https://ui.shadcn.com)
- [Chromatic Docs](https://www.chromatic.com/docs/)
- [MSW Storybook Addon](https://github.com/mswjs/msw-storybook-addon)

---

**Last Updated**: 2026-02-01


---

