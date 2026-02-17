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
```css
/* Shadows - warm toned */
--shadow-warm-sm: 0 1px 3px rgba(180,130,80,0.06), 0 1px 2px rgba(180,130,80,0.04);
--shadow-warm-xl: 0 20px 50px rgba(180,130,80,0.16), 0 8px 16px rgba(180,130,80,0.08);

/* Entity Colors */
--color-entity-game: 25 95% 45%;      /* Orange */
--color-entity-player: 262 83% 58%;   /* Purple */
--color-entity-session: 240 60% 55%;  /* Indigo */
--color-entity-agent: 38 92% 50%;     /* Amber */
--color-entity-document: 210 40% 55%; /* Slate */
--color-entity-chat: 220 80% 55%;     /* Blue */
--color-entity-event: 350 89% 60%;    /* Rose */

/* Typography */
font-family: Quicksand (headings), Nunito (body)

/* Glassmorphism */
background: rgba(255,255,255,0.9);
backdrop-filter: blur(12px) saturate(180%);
```

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

```tsx
<MeepleCard
  entity="game"
  title="Catan"
  subtitle="Klaus Teuber · 1995"
  imageUrl="/games/catan.jpg"
  rating={7.2}
  ratingMax={10}
  metadata={[
    { icon: Users, value: "3-4" },
    { icon: Clock, value: "60-120m" },
  ]}
/>
```

**Color**: `hsl(25 95% 45%)` - Warm orange for board games

### Player (Purple)

```tsx
<MeepleCard
  entity="player"
  variant="list"
  title="Marco Rossi"
  subtitle="@marco_games"
  avatarUrl="/avatars/marco.jpg"
  metadata={[
    { label: "142 plays" },
    { label: "Top 5%" },
  ]}
/>
```

**Color**: `hsl(262 83% 58%)` - Vibrant purple for users

### Session (Indigo)

```tsx
<MeepleCard
  entity="session"
  title="Serata Azul"
  subtitle="Azul · Casa di Marco"
  metadata={[
    { icon: Users, value: "4 giocatori" },
    { icon: Clock, value: "45 min" },
  ]}
  entityQuickActions={actions.quickActions}
  showInfoButton
  infoHref={`/sessions/${session.id}`}
/>
```

**Color**: `hsl(240 60% 55%)` - Indigo for game sessions
**Actions**: Riprendi, Usa Toolkit, Condividi codice

### Agent (Amber)

```tsx
<MeepleCard
  entity="agent"
  title="Azul Rules Expert"
  subtitle="RAG Strategy · GPT-4o-mini"
  metadata={[
    { icon: MessageSquare, value: "342 invocazioni" },
  ]}
  entityQuickActions={actions.quickActions}
  showInfoButton
  infoHref={`/agents/${agent.id}`}
/>
```

**Color**: `hsl(38 92% 50%)` - Amber for AI agents
**Actions**: Chat, Statistiche

### Document (Slate)

```tsx
<MeepleCard
  entity="document"
  title="azul_rulebook.pdf"
  subtitle="Azul · Regolamento base"
  metadata={[
    { icon: FileText, value: "12 pagine" },
    { icon: Download, value: "2.4 MB" },
  ]}
  entityQuickActions={actions.quickActions}
  showInfoButton
  infoHref={`/documents/${doc.id}`}
/>
```

**Color**: `hsl(210 40% 55%)` - Slate for documents
**Actions**: Download, Chat sui contenuti

### ChatSession (Blue)

```tsx
<MeepleCard
  entity="chatSession"
  title="Come si gioca ad Azul?"
  subtitle="Azul · Azul Rules Expert"
  metadata={[
    { icon: MessageSquare, value: "12 messaggi" },
  ]}
  entityQuickActions={actions.quickActions}
  showInfoButton
  infoHref={`/chat/${chat.id}`}
/>
```

**Color**: `hsl(220 80% 55%)` - Blue for chat conversations
**Actions**: Continua Chat, Esporta

### Event (Rose)

```tsx
<MeepleCard
  entity="event"
  variant="featured"
  title="MeepleAI Tournament"
  subtitle="Grand Finals · March 15"
  imageUrl="/events/tournament.jpg"
  actions={[
    { label: "Register", primary: true, onClick: handleRegister },
    { label: "Details", onClick: handleDetails },
  ]}
/>
```

**Color**: `hsl(350 89% 60%)` - Energetic rose for events

### Custom

```tsx
<MeepleCard
  entity="custom"
  customColor="142 76% 36%"  // Green HSL
  title="Custom Entity"
  subtitle="With custom color"
/>
```

**Color**: Customizable via `customColor` prop (HSL format)

## Layout Variants

### Grid (Default)

Best for catalog displays and card grids.

```tsx
<div className="grid grid-cols-2 md:grid-cols-4 gap-4">
  {games.map(game => (
    <MeepleCard
      key={game.id}
      entity="game"
      variant="grid"
      title={game.title}
      imageUrl={game.imageUrl}
    />
  ))}
</div>
```

**Features**:
- 4:3 aspect ratio cover image
- Glass morphism background
- Hover: lifts up (-translate-y-1)
- Left border color accent

### List

Best for search results and sidebar lists.

```tsx
<div className="space-y-2">
  {results.map(game => (
    <MeepleCard
      key={game.id}
      entity="game"
      variant="list"
      title={game.title}
      subtitle={game.publisher}
      imageUrl={game.thumbnailUrl}
    />
  ))}
</div>
```

**Features**:
- Horizontal layout with 64px thumbnail
- Compact metadata display
- Hover: slides right (translate-x-1)
- Dot color indicator

### Compact

Best for sidebars, dropdowns, and space-constrained areas.

```tsx
<MeepleCard
  entity="game"
  variant="compact"
  title="Quick Pick"
  subtitle="2 players"
/>
```

**Features**:
- Minimal footprint, no image
- Small dot indicator
- Subtle hover effect
- Text-only display

### Featured

Best for featured content and promotional areas.

```tsx
<MeepleCard
  entity="event"
  variant="featured"
  title="Featured Tournament"
  subtitle="Join the competition"
  imageUrl="/events/featured.jpg"
  actions={[
    { label: "Join Now", primary: true },
    { label: "Learn More" },
  ]}
/>
```

**Features**:
- 16:9 aspect ratio cover
- Action buttons support
- Enhanced gradient overlay
- Stronger hover lift

### Hero

Best for hero sections and spotlight content.

```tsx
<MeepleCard
  entity="game"
  variant="hero"
  title="Game of the Month"
  subtitle="Explore the featured selection"
  imageUrl="/games/featured-hero.jpg"
  metadata={[
    { icon: Star, value: "Editor's Pick" },
    { icon: Users, value: "10K+ plays" },
  ]}
/>
```

**Features**:
- Full-bleed background image
- Ribbon entity indicator
- Rich gradient overlay
- 320px minimum height

## Customization

### Custom Colors

Override the entity color with HSL values:

```tsx
<MeepleCard
  entity="custom"
  customColor="280 70% 50%"  // Custom purple
  title="Custom Styled"
/>
```

### Metadata Icons

Use Lucide React icons for metadata:

```tsx
import { Users, Clock, Star, Calendar } from 'lucide-react';

<MeepleCard
  entity="game"
  title="Game Title"
  metadata={[
    { icon: Users, value: "2-4" },
    { icon: Clock, value: "45m" },
    { icon: Star, value: "4.5" },
    { icon: Calendar, value: "2024" },
  ]}
/>
```

### Action Buttons

Available in featured and hero variants:

```tsx
<MeepleCard
  entity="event"
  variant="featured"
  title="Event"
  actions={[
    { label: "Primary", primary: true, onClick: () => {} },
    { label: "Secondary", onClick: () => {} },
    { label: "Disabled", disabled: true },
  ]}
/>
```

## Accessibility

MeepleCard is built with accessibility in mind:

### Keyboard Navigation

- **Tab**: Focus the card
- **Enter/Space**: Activate click handler
- **Focus visible**: Clear ring indicator

### Screen Readers

```tsx
// Automatically generates aria-label
<MeepleCard entity="game" title="Catan" />
// aria-label="Game: Catan"
```

### Best Practices

1. **Always provide `title`**: Required for screen reader announcement
2. **Use semantic metadata**: Icons should have descriptive values
3. **Test with keyboard**: Ensure all interactions work without mouse
4. **Provide alt text**: Image alt defaults to title

## Performance

### React.memo

MeepleCard uses `React.memo` to prevent unnecessary re-renders:

```tsx
// Only re-renders if props change
export const MeepleCard = React.memo(function MeepleCard(props) {
  // ...
});
```

### Lazy Loading

Images are lazy-loaded with blur placeholders:

```tsx
<Image
  src={imageUrl}
  loading="lazy"
  placeholder="blur"
  blurDataURL={placeholder}
/>
```

### Optimization Tips

1. **Memoize callbacks**: Use `useCallback` for onClick handlers
2. **Virtualize lists**: For 50+ cards, use react-window or similar
3. **Optimize images**: Use Next.js Image optimization
4. **Avoid inline objects**: Extract metadata arrays to stable references

## Migration Guide

### From GameCard

```tsx
// Before (deprecated)
import { GameCard } from '@/components/games/GameCard';

<GameCard
  game={game}
  variant="grid"
  onClick={handleClick}
  showRating
/>

// After
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Users, Clock, Calendar, HelpCircle } from 'lucide-react';

<MeepleCard
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={`${game.publisher} · ${game.yearPublished}`}
  imageUrl={game.imageUrl}
  rating={game.averageRating}
  ratingMax={10}
  metadata={[
    { icon: Users, value: formatPlayers(game.minPlayers, game.maxPlayers) },
    { icon: Clock, value: formatPlayTime(game.minPlayTimeMinutes, game.maxPlayTimeMinutes) },
    { icon: Calendar, value: String(game.yearPublished) },
    game.faqCount && { icon: HelpCircle, value: String(game.faqCount) },
  ].filter(Boolean)}
  badge={game.sharedGameId ? 'Catalogo' : game.bggId ? 'BGG' : undefined}
  onClick={handleClick}
/>
```

### Using Adapter Components

For catalog and dashboard contexts, use the pre-built adapters:

```tsx
// SharedGameCatalog
import { MeepleGameCatalogCard } from '@/components/catalog';

<MeepleGameCatalogCard
  game={sharedGame}
  variant="grid"
  onClick={(id) => router.push(`/catalog/${id}`)}
/>

// Dashboard widgets
import { MeepleGameWidget } from '@/components/dashboard';

<MeepleGameWidget
  game={recentGame}
  variant="compact"
  subtitle="Giocato 2 giorni fa"
/>
```

## Troubleshooting

### Card not clickable

Ensure `onClick` is provided and the card doesn't have action buttons:

```tsx
// Works
<MeepleCard entity="game" title="Catan" onClick={() => {}} />

// Won't be clickable (has actions)
<MeepleCard
  entity="game"
  variant="featured"
  title="Catan"
  onClick={() => {}}  // Ignored because actions exist
  actions={[{ label: "Details" }]}
/>
```

### Rating shows wrong stars

Check `ratingMax` matches your scale:

```tsx
// BGG scale (0-10)
<MeepleCard rating={7.5} ratingMax={10} />

// 5-star scale
<MeepleCard rating={4.2} ratingMax={5} />
```

### Custom color not working

Ensure HSL format without `hsl()` wrapper:

```tsx
// Correct
<MeepleCard customColor="262 83% 58%" />

// Wrong
<MeepleCard customColor="hsl(262 83% 58%)" />
<MeepleCard customColor="#8B5CF6" />
```

## Related Documentation

- [Component README](../../../apps/web/src/components/ui/data-display/README.md)
- [ADR-041: MeepleCard Universal System](../01-architecture/adr/adr-041-meeple-card-universal-system.md)
- [Design System: Cards](../design-system/cards.md)
