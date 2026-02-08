# MeepleCard - Riferimento Completo Funzionalità

> Componente universale per visualizzazione entità con 5 varianti layout e 6 feature estese

**Issue**: #3326 | **Epic**: #3325 | **ADR**: ADR-041
**Ultima revisione**: 2026-02-08

---

## 📊 Overview

MeepleCard è il **componente canonico** per visualizzare tutte le entità in MeepleAI:
- **5 tipi entità**: Game, Player, Collection, Event, Custom
- **5 varianti layout**: Grid, List, Compact, Featured, Hero
- **6 feature estese**: Wishlist, QuickActions, Status, HoverPreview, Drag&Drop, BulkSelect
- **Accessibilità**: WCAG 2.1 AA compliant
- **Performance**: React.memo + lazy loading

---

## 🎨 Entity Types & Colors

Ogni tipo di entità ha un colore semantico distintivo:

| Entity | HSL | Hex | Visual | Use Case |
|--------|-----|-----|--------|----------|
| **game** | `25 95% 45%` | `#E07B00` | 🟠 Orange | Board games, video games |
| **player** | `262 83% 58%` | `#8B5CF6` | 🟣 Purple | User profiles, participants |
| **collection** | `168 76% 42%` | `#14B8A6` | 🟢 Teal | Game collections, wishlists |
| **event** | `350 89% 60%` | `#F43F5E` | 🔴 Rose | Tournaments, meetups |
| **custom** | `220 70% 50%` | `#3B82F6` | 🔵 Blue | Custom entities |

**Applicazione colore**:
- Border sinistro (4px accent)
- Badge "GAME/PLAYER/etc." in alto a sinistra
- Gradient overlay su immagine cover
- Primary button background
- Ribbon corner (variant hero)

---

## 🖼️ Layout Variants

### Grid (Default) - Griglia Multi-Colonna

```tsx
<MeepleCard
  entity="game"
  variant="grid"
  title="Twilight Imperium"
  subtitle="Fantasy Flight Games"
  imageUrl="/games/ti4.jpg"
  rating={8.7}
  ratingMax={10}
  metadata={[
    { icon: Users, value: "3-6" },
    { icon: Clock, value: "4-8h" },
  ]}
/>
```

**Caratteristiche**:
- Cover image 4:3 aspect ratio
- Glass morphism background (light mode)
- Padding: 16px
- Hover: -translate-y-1 + shadow-lg
- **Best for**: Cataloghi, griglie prodotti, dashboard

---

### List - Lista Compatta

```tsx
<MeepleCard
  entity="game"
  variant="list"
  title="Gloomhaven"
  subtitle="Cephalofair Games"
  imageUrl="/games/gloomhaven.jpg"
  metadata={[
    { icon: Users, value: "1-4" },
    { icon: Clock, value: "120m" },
  ]}
/>
```

**Caratteristiche**:
- Layout orizzontale
- Thumbnail 64x64px
- Padding: 12px
- Hover: translate-x-1 + shadow-md
- **Best for**: Search results, liste verticali, sidebar

---

### Compact - Minimalista

```tsx
<MeepleCard
  entity="player"
  variant="compact"
  title="Marco Rossi"
  subtitle="@marco_games"
/>
```

**Caratteristiche**:
- Nessuna immagine
- Dot indicator colorato
- Padding: 8px
- Hover: bg-card
- **Best for**: Dropdown, widget, sidebar stretti

---

### Featured - In Evidenza

```tsx
<MeepleCard
  entity="event"
  variant="featured"
  title="MeepleAI Tournament"
  subtitle="Grand Finals"
  imageUrl="/events/tournament.jpg"
  actions={[
    { label: "Join Now", primary: true, onClick: handleJoin },
    { label: "Details", onClick: handleDetails },
  ]}
/>
```

**Caratteristiche**:
- Cover image 16:9 aspect ratio
- Action buttons support
- Padding: 20px
- Hover: -translate-y-2 + shadow-xl
- **Best for**: Featured content, promotional cards

---

### Hero - Sfondo Full-Bleed

```tsx
<MeepleCard
  entity="game"
  variant="hero"
  title="Game of the Month"
  subtitle="Editors' Choice"
  imageUrl="/games/featured.jpg"
  metadata={[
    { icon: Star, value: "Editor's Pick" },
    { icon: TrendingUp, value: "10K+ plays" },
  ]}
/>
```

**Caratteristiche**:
- Background image full-bleed
- Ribbon corner indicator
- Rich gradient overlay (bottom 70%)
- Padding: 24px
- Min height: 320px
- Hover: scale-[1.01] + shadow-2xl
- **Best for**: Hero sections, spotlight content

---

## ⚙️ Core Props

### Required

| Prop | Type | Description |
|------|------|-------------|
| `entity` | `MeepleEntityType` | `'game' \| 'player' \| 'collection' \| 'event' \| 'custom'` |
| `title` | `string` | Main card title (required for a11y) |

### Layout & Content

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `variant` | `MeepleCardVariant` | `'grid'` | `'grid' \| 'list' \| 'compact' \| 'featured' \| 'hero'` |
| `subtitle` | `string` | - | Secondary text (publisher, username, etc.) |
| `imageUrl` | `string` | - | Cover image URL |
| `avatarUrl` | `string` | - | Avatar URL (player entity, fallback to imageUrl) |
| `metadata` | `MeepleCardMetadata[]` | `[]` | Metadata chips (icons + values) |
| `rating` | `number` | - | Rating value (0-5 or 0-10) |
| `ratingMax` | `number` | `5` | Max rating scale (5 for stars, 10 for BGG) |
| `badge` | `string` | - | Badge text overlay |
| `customColor` | `string` | - | Custom HSL color (`"262 83% 58%"`) |
| `onClick` | `() => void` | - | Card click handler |
| `loading` | `boolean` | `false` | Show skeleton loader |
| `className` | `string` | - | Additional CSS classes |
| `data-testid` | `string` | - | Test identifier |

### Actions (Featured/Hero only)

| Prop | Type | Description |
|------|------|-------------|
| `actions` | `MeepleCardAction[]` | Action buttons array |

```typescript
interface MeepleCardAction {
  label: string;      // Button text
  primary?: boolean;  // Primary styling (colored background)
  onClick?: () => void;
  disabled?: boolean;
}
```

---

## 🎯 Feature Extensions (Issue #3820)

### 1. Wishlist Button (#3824)

```tsx
<MeepleCard
  entity="game"
  title="Wingspan"

  // Wishlist feature
  showWishlist={true}
  isWishlisted={false}
  onWishlistToggle={(id, isWishlisted) => {
    console.log(`Game ${id} wishlisted: ${isWishlisted}`);
  }}
/>
```

**Posizione**: Top-right corner
**Icon**: Heart (outline/filled)
**Behavior**: Toggle wishlist status
**Precedence**: Hidden se `quickActions` presente

---

### 2. Quick Actions Menu (#3825)

```tsx
import { Edit, Trash, Share, Copy } from 'lucide-react';

<MeepleCard
  entity="game"
  title="Azul"

  // Quick actions
  quickActions={[
    {
      icon: Edit,
      label: 'Edit',
      onClick: () => handleEdit(game.id),
    },
    {
      icon: Share,
      label: 'Share',
      onClick: () => handleShare(game.id),
    },
    { separator: true },  // Visual separator
    {
      icon: Trash,
      label: 'Delete',
      onClick: () => handleDelete(game.id),
      destructive: true,    // Red color
      adminOnly: true,      // Requires admin role
    },
  ]}
  userRole="admin"  // 'user' | 'editor' | 'admin'
/>
```

**Posizione**: Top-right corner (priorità su Wishlist)
**UI**: Kebab menu (⋮) con dropdown
**Features**:
- Icons da lucide-react
- Separator visuale tra gruppi
- Destructive actions (red)
- Role-based visibility (adminOnly)
- Hidden items (conditional)
- Disabled state

---

### 3. Status Badge (#3826)

```tsx
<MeepleCard
  entity="game"
  title="Scythe"

  // Status badge
  status="owned"  // 'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade'
  showStatusIcon={true}  // Show icon in badge
/>

// Multiple statuses
<MeepleCard
  entity="game"
  title="Everdell"
  status={['owned', 'wishlisted', 'played']}
  showStatusIcon={true}
/>
```

**Stati disponibili**:
- `owned`: ✓ Posseduto (green)
- `wishlisted`: ♥ In wishlist (rose)
- `played`: 🎮 Giocato (blue)
- `borrowed`: 🔄 In prestito (amber)
- `for-trade`: 🔁 In scambio (purple)

**Posizione**: Below entity badge (top-left area)
**Behavior**: Stacked quando multipli

---

### 4. Hover Preview (#3827)

```tsx
<MeepleCard
  id="game-123"  // Required for preview
  entity="game"
  title="Brass: Birmingham"

  // Hover preview
  showPreview={true}
  previewData={{
    description: "Build industries, transport goods, sell to markets...",
    designer: "Martin Wallace",
    complexity: 3.9,
    weight: 'Heavy',
    categories: ['Economic', 'Industry'],
    mechanics: ['Network Building', 'Route Building'],
  }}
  onFetchPreview={async (id) => {
    // Async fetch preview data
    return await fetchGamePreview(id);
  }}
/>
```

**Caratteristiche**:
- Popover con 500ms delay
- Async data loading on demand
- Preview content: Description, Designer, Complexity, Weight, Categories, Mechanics
- Wraps card content (no layout shift)

---

### 5. Drag & Drop (#3828)

```tsx
<MeepleCard
  entity="game"
  variant="list"
  title="Spirit Island"

  // Drag & Drop
  draggable={true}
  dragData={{
    id: game.id,
    type: 'game',
    index: 0,
  }}
  onDragStart={(data) => console.log('Drag started:', data)}
  onDragEnd={(data) => console.log('Drag ended:', data)}
/>
```

**UI**: Drag handle (⋮⋮) su variante list (left side)
**Use case**: Riordinamento liste, collection management
**Events**: onDragStart, onDragEnd

---

### 6. Bulk Selection (#3829)

```tsx
<MeepleCard
  entity="game"
  title="Terraforming Mars"

  // Bulk selection
  selectable={true}
  selected={false}
  onSelect={(id, selected) => {
    console.log(`Game ${id} selected: ${selected}`);
  }}
/>
```

**UI**: Checkbox top-left corner (highest z-index)
**Visual feedback**: Ring-2 + offset quando selected
**Color**: Entity color per ring

---

## 📐 Responsive Behavior

### Image Sizes

MeepleCard usa `sizes` ottimizzati per Next.js Image:

```tsx
sizes={
  variant === 'hero'
    ? '100vw'
    : variant === 'featured'
      ? '(max-width: 768px) 100vw, 50vw'
      : variant === 'grid'
        ? '(max-width: 640px) 50vw, (max-width: 1024px) 33vw, 25vw'
        : '64px'  // list/compact
}
```

### Grid Breakpoints

```tsx
// Recommended responsive grid usage
<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
  {games.map(game => (
    <MeepleCard entity="game" variant="grid" {...game} />
  ))}
</div>
```

### Variant Switching

```tsx
// Adaptive variant per device
function ResponsiveCard({ game }) {
  const isMobile = useMediaQuery('(max-width: 640px)');

  return (
    <MeepleCard
      entity="game"
      variant={isMobile ? 'list' : 'grid'}
      title={game.title}
    />
  );
}
```

---

## 🎯 Esempi Completi

### 1. Game Card - Catalog Grid

```tsx
import { Users, Clock, Calendar, HelpCircle } from 'lucide-react';

<MeepleCard
  id={game.id}
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={`${game.publisher} · ${game.yearPublished}`}
  imageUrl={game.coverImageUrl}
  rating={game.averageRating}
  ratingMax={10}
  metadata={[
    { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
    { icon: Clock, value: `${game.estimatedPlaytime}m` },
    { icon: Calendar, value: String(game.yearPublished) },
    game.faqCount > 0 && { icon: HelpCircle, value: String(game.faqCount) },
  ].filter(Boolean)}
  badge={game.bggId ? 'BGG' : undefined}
  onClick={() => router.push(`/games/${game.id}`)}

  // Extended features
  showWishlist
  isWishlisted={game.isWishlisted}
  onWishlistToggle={handleWishlistToggle}

  status={game.isOwned ? 'owned' : undefined}
  showStatusIcon

  showPreview
  onFetchPreview={fetchGamePreview}
/>
```

---

### 2. Player Card - List View

```tsx
import { TrendingUp, Star, Award } from 'lucide-react';

<MeepleCard
  id={player.id}
  entity="player"
  variant="list"
  title={player.name}
  subtitle={player.username}
  avatarUrl={player.avatarUrl}
  metadata={[
    { icon: TrendingUp, value: `${player.playCount} plays` },
    { icon: Star, value: `${player.rating.toFixed(1)} rating` },
    { icon: Award, value: player.rank },
  ]}
  onClick={() => router.push(`/players/${player.id}`)}
/>
```

---

### 3. Collection Card - Admin Mode

```tsx
import { Edit, Trash, Users, Eye } from 'lucide-react';

<MeepleCard
  id={collection.id}
  entity="collection"
  variant="grid"
  title={collection.name}
  subtitle={collection.description}
  imageUrl={collection.coverImageUrl}
  metadata={[
    { icon: Gamepad2, value: `${collection.gameCount} games` },
    { icon: Users, value: `${collection.followers} followers` },
  ]}

  // Quick actions per admin
  quickActions={[
    { icon: Eye, label: 'View', onClick: () => handleView(collection.id) },
    { icon: Edit, label: 'Edit', onClick: () => handleEdit(collection.id), adminOnly: true },
    { separator: true },
    { icon: Trash, label: 'Delete', onClick: () => handleDelete(collection.id), destructive: true, adminOnly: true },
  ]}
  userRole="admin"
/>
```

---

### 4. Event Card - Featured with Actions

```tsx
<MeepleCard
  entity="event"
  variant="featured"
  title="MeepleAI Championship 2026"
  subtitle="Grand Finals · March 15, 2026"
  imageUrl="/events/championship.jpg"
  metadata={[
    { icon: Users, value: "256 participants" },
    { icon: MapPin, value: "Milano, IT" },
    { icon: Calendar, value: "Mar 15" },
  ]}
  actions={[
    { label: "Register Now", primary: true, onClick: handleRegister },
    { label: "View Details", onClick: handleDetails },
  ]}
  badge="Early Bird"
/>
```

---

### 5. Hero Section - Full Bleed

```tsx
<MeepleCard
  entity="game"
  variant="hero"
  title="Featured: Dune Imperium"
  subtitle="The epic strategy game of intrigue and warfare"
  imageUrl="/games/dune-hero.jpg"
  metadata={[
    { icon: Star, value: "Editor's Choice" },
    { icon: TrendingUp, value: "10K+ plays this month" },
    { icon: Award, value: "#1 Strategy Game" },
  ]}
  onClick={() => router.push('/games/dune-imperium')}
/>
```

---

### 6. Bulk Selection - Admin Batch Operations

```tsx
const [selectedGames, setSelectedGames] = useState<Set<string>>(new Set());

{games.map(game => (
  <MeepleCard
    key={game.id}
    id={game.id}
    entity="game"
    variant="grid"
    title={game.title}

    // Bulk selection
    selectable
    selected={selectedGames.has(game.id)}
    onSelect={(id, selected) => {
      setSelectedGames(prev => {
        const next = new Set(prev);
        selected ? next.add(id) : next.delete(id);
        return next;
      });
    }}

    // Quick actions per batch ops
    quickActions={[
      { icon: Archive, label: 'Archive Selected', onClick: () => handleBulkArchive(selectedGames) },
      { icon: Trash, label: 'Delete Selected', onClick: () => handleBulkDelete(selectedGames), destructive: true },
    ]}
    userRole="admin"
  />
))}
```

---

### 7. Drag & Drop Reordering

```tsx
const [games, setGames] = useState(initialGames);

{games.map((game, index) => (
  <MeepleCard
    key={game.id}
    entity="game"
    variant="list"
    title={game.title}

    // Drag & Drop
    draggable
    dragData={{ id: game.id, type: 'game', index }}
    onDragStart={(data) => console.log('Start drag:', data)}
    onDragEnd={(data) => {
      // Reorder logic
      const newOrder = reorderArray(games, data.index, dropTargetIndex);
      setGames(newOrder);
    }}
  />
))}
```

---

## 🎨 TypeScript Types

```typescript
import type {
  MeepleCardProps,
  MeepleEntityType,
  MeepleCardVariant,
  MeepleCardMetadata,
  MeepleCardAction,
} from '@/components/ui/data-display/meeple-card';

// Entity type
type EntityType = 'game' | 'player' | 'collection' | 'event' | 'custom';

// Metadata
interface Metadata {
  icon?: LucideIcon;
  label?: string;
  value?: string;
}

// Action
interface Action {
  label: string;
  primary?: boolean;
  onClick?: () => void;
  disabled?: boolean;
}

// Quick Action (extended)
interface QuickAction extends Action {
  icon: LucideIcon;
  hidden?: boolean;
  adminOnly?: boolean;
  destructive?: boolean;
  separator?: boolean;
}

// Status
type Status = 'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade';
```

---

## ♿ Accessibility

### Keyboard Navigation

| Key | Action |
|-----|--------|
| **Tab** | Focus card |
| **Enter / Space** | Activate onClick |
| **Shift + Tab** | Focus previous |

### ARIA Attributes

```tsx
// Automatic ARIA labels
<MeepleCard entity="game" title="Catan" />
// → aria-label="Game: Catan"

// Interactive cards
onClick={handleClick}
// → role="button"
// → tabIndex={0}

// Non-interactive
// → <article> semantic element
```

### Screen Reader Support

- Entity type announced
- Rating announced with scale
- Metadata announced with labels
- Loading state announced ("Loading...")
- Empty states with `role="status"`

### Color Contrast

- **Text**: Minimum 4.5:1 contrast ratio
- **UI Components**: Minimum 3:1 contrast
- **Focus indicators**: 2px ring with offset
- **Tested**: Passes WCAG 2.1 AA automated checks

---

## 🚀 Performance

### React.memo

```tsx
export const MeepleCard = React.memo(function MeepleCard(props) {
  // Only re-renders when props change
});
```

### Image Optimization

```tsx
// Next.js Image component with optimizations
<Image
  src={imageUrl || placeholder}
  fill
  sizes="(max-width: 640px) 50vw, 33vw"
  loading="lazy"
  placeholder="blur"
  blurDataURL={placeholder}
/>
```

### Performance Tips

1. **Memoize metadata**: Extract to stable reference
   ```tsx
   const metadata = useMemo(() => [
     { icon: Users, value: `${minPlayers}-${maxPlayers}` },
   ], [minPlayers, maxPlayers]);
   ```

2. **Memoize callbacks**: Use useCallback
   ```tsx
   const handleClick = useCallback(() => {
     router.push(`/games/${id}`);
   }, [id, router]);
   ```

3. **Virtualize large lists**: For 100+ cards
   ```tsx
   import { useVirtualizer } from '@tanstack/react-virtual';
   ```

4. **Optimize images**: Use Next.js image optimization
   - WebP format auto-conversion
   - Responsive sizes
   - Lazy loading

---

## 🧪 Testing

### Unit Tests

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MeepleCard } from './meeple-card';

it('should render game card with rating', () => {
  render(
    <MeepleCard
      entity="game"
      title="Test Game"
      rating={8.5}
      ratingMax={10}
    />
  );

  expect(screen.getByText('Test Game')).toBeInTheDocument();
  expect(screen.getByLabelText(/rating.*8\.5/i)).toBeInTheDocument();
});

it('should call onClick when card is clicked', async () => {
  const mockClick = jest.fn();

  render(
    <MeepleCard
      entity="game"
      title="Test"
      onClick={mockClick}
    />
  );

  await userEvent.click(screen.getByTestId('meeple-card'));
  expect(mockClick).toHaveBeenCalledTimes(1);
});
```

### Accessibility Tests

```typescript
import { axe, toHaveNoViolations } from 'jest-axe';

expect.extend(toHaveNoViolations);

it('should have no a11y violations', async () => {
  const { container } = render(
    <MeepleCard entity="game" title="Test" />
  );

  const results = await axe(container);
  expect(results).toHaveNoViolations();
});
```

---

## 📚 Storybook

Tutte le varianti e feature disponibili in Storybook:

```bash
cd apps/web && pnpm storybook
# Navigate to: UI/Data Display/MeepleCard
```

**Stories disponibili**:
- Default (Grid)
- All Entities (Game, Player, Collection, Event, Custom)
- All Variants (Grid, List, Compact, Featured, Hero)
- With Rating
- With Actions
- With Metadata
- Loading State
- Custom Color
- With Wishlist
- With Quick Actions
- With Status Badge
- With Hover Preview
- With Drag & Drop
- With Bulk Selection
- Dark Mode

---

## 🔄 Migration da GameCard

### Prima (Deprecated)

```tsx
import { GameCard } from '@/components/games/GameCard';

<GameCard
  game={game}
  variant="grid"
  showRating
  onClick={handleClick}
/>
```

### Dopo (Recommended)

```tsx
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

<MeepleCard
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={game.publisher}
  imageUrl={game.coverImageUrl}
  rating={game.averageRating}
  ratingMax={10}
  metadata={[
    { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
    { icon: Clock, value: `${game.estimatedPlaytime}m` },
  ]}
  onClick={handleClick}
/>
```

---

## 🎯 Quando Usare Quale Variante

| Context | Recommended Variant | Rationale |
|---------|---------------------|-----------|
| **Catalog browse** | Grid | Optimal image showcase + metadata |
| **Search results** | List | Scannable with compact layout |
| **Sidebar widget** | Compact | Space-efficient |
| **Homepage featured** | Featured | Prominent with actions |
| **Hero banner** | Hero | Maximum visual impact |
| **Mobile list** | List | Better for narrow screens |
| **Desktop grid** | Grid | Better image utilization |

---

## 🚫 Anti-Patterns

### DON'T: Create Custom Card Components

```tsx
// ❌ BAD
<div className="rounded-lg bg-card p-4">
  <h3>{game.title}</h3>
</div>

// ✅ GOOD
<MeepleCard entity="game" title={game.title} />
```

### DON'T: Override Core Styles

```tsx
// ❌ BAD
<MeepleCard className="!bg-red-500 !p-8" />

// ✅ GOOD
<MeepleCard entity="custom" customColor="0 84% 60%" />
```

### DON'T: Mix Entity Types

```tsx
// ❌ BAD (semantic confusion)
<MeepleCard entity="player" title={game.title} />

// ✅ GOOD
<MeepleCard entity="game" title={game.title} />
```

### DON'T: Ignore Accessibility

```tsx
// ❌ BAD (missing title)
<MeepleCard entity="game" imageUrl="/cover.jpg" />

// ✅ GOOD
<MeepleCard entity="game" title="Game Title" imageUrl="/cover.jpg" />
```

---

## 🔗 Related Components

| Component | Location | Purpose |
|-----------|----------|---------|
| **GameCarousel** | `ui/data-display/game-carousel.tsx` | 3D carousel per games |
| **EntityListView** | `ui/data-display/entity-list-view/` | Multi-view list (Grid/List/Carousel) |
| **MeepleGameCatalogCard** | `catalog/` | Adapter per SharedGameCatalog |
| **MeepleGameWidget** | `dashboard/` | Adapter per dashboard widgets |

---

## 📚 References

- **Codice**: `apps/web/src/components/ui/data-display/meeple-card.tsx`
- **Tests**: `apps/web/src/components/ui/data-display/__tests__/meeple-card.*.tsx`
- **Stories**: `apps/web/src/components/ui/data-display/meeple-card.stories.tsx`
- **Docs**:
  - `docs/frontend/components/meeple-card.md`
  - `docs/design-system/cards.md`
  - `docs/01-architecture/adr/adr-041-meeple-card-universal-system.md`
- **Issues**:
  - #3325 - MeepleCard Epic
  - #3326 - Core Implementation
  - #3820 - Feature Extensions Epic
  - #3824-#3829 - Individual features

---

## 💡 Quick Reference

### Import

```tsx
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps, MeepleEntityType } from '@/components/ui/data-display/meeple-card';
```

### Minimal Usage

```tsx
<MeepleCard
  entity="game"
  title="Catan"
/>
```

### Full Featured

```tsx
<MeepleCard
  id="game-123"
  entity="game"
  variant="grid"
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
  onClick={() => router.push('/games/ti4')}
  showWishlist
  isWishlisted={false}
  onWishlistToggle={handleWishlist}
  status="owned"
  showStatusIcon
  showPreview
  onFetchPreview={fetchPreview}
  selectable
  selected={false}
  onSelect={handleSelect}
/>
```

---

**Ultima revisione**: 2026-02-08
**Autore**: MeepleAI Team + Claude
**Status**: Production Ready ✅
