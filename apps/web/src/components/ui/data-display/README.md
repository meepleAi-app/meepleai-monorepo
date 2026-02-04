# MeepleCard - Universal Card Component System

> Issue #3326 | MeepleCard Epic #3325

A polymorphic card component supporting multiple entity types and layout variants for the MeepleAI application.

## Installation

```tsx
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
```

## Quick Start

```tsx
// Basic game card
<MeepleCard
  entity="game"
  title="Catan"
  subtitle="Klaus Teuber"
/>

// Player card with avatar
<MeepleCard
  entity="player"
  variant="list"
  title="Marco Rossi"
  avatarUrl="/avatars/marco.jpg"
/>

// Featured event with actions
<MeepleCard
  entity="event"
  variant="featured"
  title="Tournament"
  actions={[{ label: "Join", primary: true }]}
/>
```

## Entity Types

| Entity | Color | Use Case |
|--------|-------|----------|
| `game` | Orange (`25 95% 45%`) | Board games, video games |
| `player` | Purple (`262 83% 58%`) | User profiles, participants |
| `collection` | Teal (`168 76% 42%`) | Game collections, playlists |
| `event` | Rose (`350 89% 60%`) | Tournaments, meetups |
| `custom` | Blue (`220 70% 50%`) | Custom entities |

## Layout Variants

| Variant | Description | Best For |
|---------|-------------|----------|
| `grid` | Card with 4:3 cover image | Catalog displays, grids |
| `list` | Horizontal with thumbnail | Lists, search results |
| `compact` | Minimal, no image | Sidebar widgets, dropdowns |
| `featured` | 16:9 cover with actions | Featured content |
| `hero` | Full-bleed background | Hero sections, spotlights |

## Props API

### Required Props

| Prop | Type | Description |
|------|------|-------------|
| `entity` | `'game' \| 'player' \| 'collection' \| 'event' \| 'custom'` | Entity type (determines color scheme) |
| `title` | `string` | Main card title |

### Optional Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `variant` | `'grid' \| 'list' \| 'compact' \| 'featured' \| 'hero'` | `'grid'` | Layout variant |
| `subtitle` | `string` | - | Secondary text |
| `imageUrl` | `string` | - | Cover image URL |
| `avatarUrl` | `string` | - | Avatar URL (player entity) |
| `metadata` | `MeepleCardMetadata[]` | `[]` | Metadata chips |
| `actions` | `MeepleCardAction[]` | `[]` | Action buttons (featured/hero) |
| `rating` | `number` | - | Rating value |
| `ratingMax` | `number` | `5` | Max rating scale (5 or 10) |
| `badge` | `string` | - | Badge text overlay |
| `customColor` | `string` | - | Custom HSL color |
| `onClick` | `() => void` | - | Click handler |
| `loading` | `boolean` | `false` | Show skeleton |
| `className` | `string` | - | Additional CSS classes |
| `data-testid` | `string` | - | Test identifier |

### TypeScript Interfaces

```typescript
interface MeepleCardMetadata {
  icon?: LucideIcon;  // Lucide React icon
  label?: string;     // Display text
  value?: string;     // Alternative to label
}

interface MeepleCardAction {
  label: string;      // Button text
  primary?: boolean;  // Primary styling
  onClick?: () => void;
  disabled?: boolean;
}
```

## Examples

### With Full Options

```tsx
import { Users, Clock } from 'lucide-react';

<MeepleCard
  entity="game"
  variant="featured"
  title="Twilight Imperium"
  subtitle="Fantasy Flight Games · 2017"
  imageUrl="/games/ti4.jpg"
  rating={8.7}
  ratingMax={10}
  metadata={[
    { icon: Users, value: "3-6" },
    { icon: Clock, value: "4-8h" },
  ]}
  badge="New"
  actions={[
    { label: "Play Now", primary: true, onClick: handlePlay },
    { label: "Details", onClick: handleDetails },
  ]}
  onClick={handleCardClick}
/>
```

### Custom Entity Color

```tsx
<MeepleCard
  entity="custom"
  customColor="142 76% 36%"  // Green HSL
  title="Custom Card"
/>
```

### Loading State

```tsx
<MeepleCard
  entity="game"
  title=""
  loading
/>
```

## Accessibility

- WCAG AA compliant
- Keyboard navigation (Enter/Space to activate)
- Screen reader labels (`aria-label`)
- Focus visible states
- Semantic HTML structure

## Performance

- `React.memo` optimized
- Lazy-loaded images with blur placeholder
- Efficient CVA variant system
- Minimal re-renders

## Migration

If migrating from `GameCard`:

```tsx
// Before (deprecated)
import { GameCard } from '@/components/games/GameCard';
<GameCard game={game} variant="grid" />

// After
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
<MeepleCard
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={game.publisher}
  imageUrl={game.imageUrl}
  rating={game.averageRating}
  ratingMax={10}
/>
```

## Related Components

- `MeepleGameCatalogCard` - Catalog adapter (`@/components/catalog`)
- `MeepleGameWidget` - Dashboard adapter (`@/components/dashboard`)
- `GameCard` - Legacy wrapper (deprecated)

## Storybook

View interactive examples:

```bash
cd apps/web && pnpm storybook
# Navigate to: Data Display / MeepleCard
```
