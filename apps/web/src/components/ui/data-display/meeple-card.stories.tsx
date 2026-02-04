/**
 * MeepleCard Storybook Stories (Issue #3329)
 *
 * Comprehensive visual documentation and interactive testing for the
 * MeepleCard universal card component system.
 *
 * @module components/ui/data-display/meeple-card.stories
 * @see Issue #3326 - Core MeepleCard Component Implementation
 */

import { useState } from 'react';
import { Users, Clock, Calendar, MapPin, Trophy, Gamepad2, BookOpen, Star, Heart, Share2 } from 'lucide-react';

import { MeepleCard, MeepleCardSkeleton } from './meeple-card';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof MeepleCard> = {
  title: 'UI/Data Display/MeepleCard',
  component: MeepleCard,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: `
# MeepleCard - Universal Card System

A polymorphic card component supporting multiple entity types and layout variants for the MeepleAI board game assistant.

## Overview
MeepleCard provides a consistent visual language for displaying games, players, collections, events, and custom entities across the application. It combines structured information display with canvas-style visuals for a distinctive MeepleAI aesthetic.

## Entity Types & Colors
| Entity | Color | HSL Value | Use Case |
|--------|-------|-----------|----------|
| **game** | Orange | \`25 95% 45%\` | Board games, rulebooks, FAQs |
| **player** | Purple | \`262 83% 58%\` | User profiles, avatars, stats |
| **collection** | Teal | \`168 76% 42%\` | Game lists, playlists, wishlists |
| **event** | Rose | \`350 89% 60%\` | Game nights, tournaments, sessions |
| **custom** | Configurable | Via \`customColor\` prop | Custom use cases |

## Layout Variants
| Variant | Dimensions | Best For |
|---------|-----------|----------|
| **grid** | 280px width, 4:3 cover | Standard card grids, search results |
| **list** | Full width, 64px thumbnail | Lists, search autocomplete |
| **compact** | Inline, 40px avatar | Mentions, tags, quick references |
| **featured** | 400px width, 16:9 cover | Featured content, promotions |
| **hero** | Full width, 320px+ height | Landing pages, event banners |

## Accessibility
- **Keyboard navigation**: Tab to focus, Enter/Space to activate
- **Screen readers**: aria-label with entity type and title
- **WCAG AA compliant**: Color contrast, focus indicators
- **No nested interactives**: Cards with actions don't receive onClick

## Usage Examples
\`\`\`tsx
// Game card with rating
<MeepleCard
  entity="game"
  title="Twilight Imperium"
  subtitle="Fantasy Flight Games"
  rating={8.7}
  ratingMax={10}
  onClick={() => router.push('/games/ti4')}
/>

// Player card in list
<MeepleCard
  entity="player"
  variant="list"
  title="Marco Rossi"
  avatarUrl="/avatars/marco.jpg"
/>

// Event hero with actions
<MeepleCard
  entity="event"
  variant="hero"
  title="MeepleAI Tournament"
  actions={[{ label: 'Register', primary: true }]}
/>
\`\`\`
        `,
      },
    },
  },
  argTypes: {
    entity: {
      control: 'select',
      options: ['game', 'player', 'collection', 'event', 'custom'],
      description: 'Entity type determines the color scheme and semantic meaning',
      table: {
        type: { summary: 'MeepleEntityType' },
        defaultValue: { summary: 'game' },
      },
    },
    variant: {
      control: 'select',
      options: ['grid', 'list', 'compact', 'featured', 'hero'],
      description: 'Layout variant determines size and arrangement',
      table: {
        type: { summary: 'MeepleCardVariant' },
        defaultValue: { summary: 'grid' },
      },
    },
    title: {
      control: 'text',
      description: 'Main title displayed prominently',
    },
    subtitle: {
      control: 'text',
      description: 'Secondary text (publisher, username, date)',
    },
    imageUrl: {
      control: 'text',
      description: 'Cover image URL (uses placeholder if not provided)',
    },
    avatarUrl: {
      control: 'text',
      description: 'Avatar URL for player entity (takes precedence over imageUrl)',
    },
    rating: {
      control: { type: 'number', min: 0, max: 10, step: 0.1 },
      description: 'Rating value (auto-converts 0-10 to 0-5 stars)',
    },
    ratingMax: {
      control: 'select',
      options: [5, 10],
      description: 'Maximum rating scale',
      table: { defaultValue: { summary: '5' } },
    },
    badge: {
      control: 'text',
      description: 'Badge text overlay (e.g., "New", "Popular")',
    },
    customColor: {
      control: 'text',
      description: 'Custom HSL color for custom entity (e.g., "142 76% 36%")',
    },
    loading: {
      control: 'boolean',
      description: 'Show loading skeleton instead of content',
      table: { defaultValue: { summary: 'false' } },
    },
  },
};

export default meta;
type Story = StoryObj<typeof MeepleCard>;

// ============================================================================
// Entity Type Stories
// ============================================================================

export const GameCard: Story = {
  args: {
    entity: 'game',
    variant: 'grid',
    title: 'Twilight Imperium',
    subtitle: 'Fantasy Flight Games · 2017',
    imageUrl: 'https://cf.geekdo-images.com/5CFwjd8zTcGYVUnkXh04hw__opengraph_letterbox/img/cwZqKBblz6FBLv98sLANmJNLkJ8=/fit-in/1200x630/filters:fill(auto)/pic1176894.jpg',
    rating: 8.7,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '3-6' },
      { icon: Clock, value: '4-8h' },
    ],
  },
};

export const PlayerCard: Story = {
  args: {
    entity: 'player',
    variant: 'grid',
    title: 'Marco Rossi',
    subtitle: '@marco_games · Since 2019',
    avatarUrl: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=400&h=300&fit=crop',
    metadata: [
      { icon: Gamepad2, label: '142 plays' },
      { icon: BookOpen, label: '89 games' },
      { icon: Trophy, label: 'Top 5%' },
    ],
  },
};

export const CollectionCard: Story = {
  args: {
    entity: 'collection',
    variant: 'grid',
    title: 'Euro Games Essentials',
    subtitle: 'Curated by @boardgame_guru',
    imageUrl: 'https://images.unsplash.com/photo-1611371805429-8b5c1b2c34ba?w=400&h=300&fit=crop',
    metadata: [
      { label: '24 games' },
      { label: '1.2K views' },
      { icon: Star, label: '89' },
    ],
  },
};

export const EventCard: Story = {
  args: {
    entity: 'event',
    variant: 'grid',
    title: 'Saturday Game Night',
    subtitle: 'Feb 15, 2026 · 7:00 PM',
    imageUrl: 'https://images.unsplash.com/photo-1606092195730-5d7b9af1efc5?w=400&h=300&fit=crop',
    metadata: [
      { icon: MapPin, label: 'Milano, IT' },
      { icon: Users, label: '4/6 spots' },
      { icon: Gamepad2, label: 'TI4' },
    ],
  },
};

// ============================================================================
// Layout Variant Stories
// ============================================================================

export const GridLayout: Story = {
  args: {
    ...GameCard.args,
  },
  decorators: [
    Story => (
      <div className="w-[280px]">
        <Story />
      </div>
    ),
  ],
};

export const ListLayout: Story = {
  args: {
    entity: 'game',
    variant: 'list',
    title: 'Gloomhaven',
    subtitle: 'Cephalofair Games · Dungeon Crawl',
    imageUrl: 'https://cf.geekdo-images.com/x3zxjr-Vw5iU4yDPg70Jgw__opengraph_letterbox/img/EiJABZjDfwKnXlNd1pjl00_p-hg=/fit-in/1200x630/filters:fill(auto)/pic3490053.jpg',
    rating: 8.8,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '2-3h' },
    ],
  },
  decorators: [
    Story => (
      <div className="w-[500px]">
        <Story />
      </div>
    ),
  ],
};

export const CompactLayout: Story = {
  args: {
    entity: 'player',
    variant: 'compact',
    title: 'Elena Bianchi',
    subtitle: 'Online now',
    avatarUrl: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=100&h=100&fit=crop',
  },
  decorators: [
    Story => (
      <div className="w-[300px]">
        <Story />
      </div>
    ),
  ],
};

export const FeaturedLayout: Story = {
  args: {
    entity: 'collection',
    variant: 'featured',
    title: 'Party Games Night',
    subtitle: 'Perfect for 6+ players · 18 games',
    imageUrl: 'https://images.unsplash.com/photo-1632501641765-e568d28b0015?w=600&h=400&fit=crop',
    rating: 4.5,
    metadata: [
      { label: '18 games' },
      { label: 'Public' },
    ],
    actions: [
      { label: 'Browse', primary: true },
      { label: 'Clone' },
    ],
  },
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
};

export const HeroLayout: Story = {
  args: {
    entity: 'event',
    variant: 'hero',
    title: 'MeepleAI Tournament',
    subtitle: 'Grand Finals · Live Streamed',
    imageUrl: 'https://images.unsplash.com/photo-1511882150382-421056c89033?w=1200&h=600&fit=crop',
    metadata: [
      { icon: Calendar, label: 'Mar 8, 2026' },
      { icon: MapPin, label: 'Online' },
      { icon: Trophy, label: '$500 Prize' },
    ],
    actions: [
      { label: 'Register Now', primary: true },
      { label: 'Learn More' },
    ],
  },
  decorators: [
    Story => (
      <div className="w-[600px]">
        <Story />
      </div>
    ),
  ],
};

// ============================================================================
// State Stories
// ============================================================================

export const Loading: Story = {
  args: {
    entity: 'game',
    variant: 'grid',
    title: '',
    loading: true,
  },
  decorators: [
    Story => (
      <div className="w-[280px]">
        <Story />
      </div>
    ),
  ],
};

export const WithBadge: Story = {
  args: {
    ...GameCard.args,
    badge: 'New',
  },
  decorators: [
    Story => (
      <div className="w-[280px]">
        <Story />
      </div>
    ),
  ],
};

export const CustomColor: Story = {
  args: {
    entity: 'custom',
    variant: 'grid',
    title: 'Custom Entity',
    subtitle: 'With custom color',
    customColor: '142 76% 36%', // Green
    imageUrl: 'https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=400&h=300&fit=crop',
    metadata: [{ label: 'Custom metadata' }],
  },
  decorators: [
    Story => (
      <div className="w-[280px]">
        <Story />
      </div>
    ),
  ],
};

// ============================================================================
// Grid Showcase
// ============================================================================

export const AllEntitiesGrid: Story = {
  render: () => (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-4 max-w-4xl">
      <MeepleCard
        entity="game"
        title="Twilight Imperium"
        subtitle="Fantasy Flight Games"
        imageUrl="https://cf.geekdo-images.com/5CFwjd8zTcGYVUnkXh04hw__opengraph_letterbox/img/cwZqKBblz6FBLv98sLANmJNLkJ8=/fit-in/1200x630/filters:fill(auto)/pic1176894.jpg"
        rating={8.7}
        ratingMax={10}
        metadata={[
          { icon: Users, value: '3-6' },
          { icon: Clock, value: '4-8h' },
        ]}
      />
      <MeepleCard
        entity="player"
        title="Marco Rossi"
        subtitle="@marco_games"
        avatarUrl="https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=400&h=300&fit=crop"
        metadata={[
          { label: '142 plays' },
          { label: 'Top 5%' },
        ]}
      />
      <MeepleCard
        entity="collection"
        title="Euro Games"
        subtitle="24 games"
        imageUrl="https://images.unsplash.com/photo-1611371805429-8b5c1b2c34ba?w=400&h=300&fit=crop"
        metadata={[
          { label: '1.2K views' },
        ]}
      />
      <MeepleCard
        entity="event"
        title="Game Night"
        subtitle="Feb 15 · 7 PM"
        imageUrl="https://images.unsplash.com/photo-1606092195730-5d7b9af1efc5?w=400&h=300&fit=crop"
        metadata={[
          { icon: MapPin, label: 'Milano' },
          { label: '4/6' },
        ]}
      />
    </div>
  ),
  parameters: {
    layout: 'padded',
  },
};

export const AllEntitiesList: Story = {
  render: () => (
    <div className="flex flex-col gap-3 max-w-xl">
      <MeepleCard
        entity="game"
        variant="list"
        title="Twilight Imperium"
        subtitle="Fantasy Flight Games · 2017"
        imageUrl="https://cf.geekdo-images.com/5CFwjd8zTcGYVUnkXh04hw__opengraph_letterbox/img/cwZqKBblz6FBLv98sLANmJNLkJ8=/fit-in/1200x630/filters:fill(auto)/pic1176894.jpg"
        metadata={[
          { icon: Users, value: '3-6' },
          { icon: Clock, value: '4-8h' },
        ]}
      />
      <MeepleCard
        entity="player"
        variant="list"
        title="Marco Rossi"
        subtitle="@marco_games · Since 2019"
        avatarUrl="https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=100&h=100&fit=crop"
        metadata={[
          { label: '142 plays' },
          { label: 'Top 5%' },
        ]}
      />
      <MeepleCard
        entity="collection"
        variant="list"
        title="Euro Games Essentials"
        subtitle="Curated by @boardgame_guru"
        imageUrl="https://images.unsplash.com/photo-1611371805429-8b5c1b2c34ba?w=100&h=100&fit=crop"
        metadata={[
          { label: '24 games' },
          { label: '1.2K views' },
        ]}
      />
      <MeepleCard
        entity="event"
        variant="list"
        title="Saturday Game Night"
        subtitle="Feb 15, 2026 · 7:00 PM"
        imageUrl="https://images.unsplash.com/photo-1606092195730-5d7b9af1efc5?w=100&h=100&fit=crop"
        metadata={[
          { icon: MapPin, label: 'Milano' },
          { label: '4/6 spots' },
        ]}
      />
    </div>
  ),
  parameters: {
    layout: 'padded',
  },
};

// ============================================================================
// Additional State Stories
// ============================================================================

export const NoImage: Story = {
  args: {
    entity: 'game',
    variant: 'grid',
    title: 'Game Without Image',
    subtitle: 'Uses placeholder automatically',
    rating: 7.5,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '2-4' },
      { icon: Clock, value: '60 min' },
    ],
  },
  decorators: [
    Story => (
      <div className="w-[280px]">
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'Cards without an image URL automatically display a themed placeholder with a dice emoji.',
      },
    },
  },
};

export const WithActions: Story = {
  args: {
    entity: 'game',
    variant: 'featured',
    title: 'Spirit Island',
    subtitle: 'Greater Than Games · 2017',
    imageUrl: 'https://cf.geekdo-images.com/kjCm4ZvPjIZxS-mYgSPy1g__opengraph_letterbox/img/73pGheHuxHPjxDlCLOBkQ_n7upc=/fit-in/1200x630/filters:fill(auto)/pic3615739.png',
    rating: 8.3,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '90-120 min' },
    ],
    actions: [
      { label: 'Add to Library', primary: true },
      { label: 'View Details' },
    ],
  },
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'Featured and hero variants support action buttons. Note: Cards with actions do not receive onClick to prevent nested interactive elements.',
      },
    },
  },
};

// ============================================================================
// Interactive Stories
// ============================================================================

export const Playground: Story = {
  args: {
    entity: 'game',
    variant: 'grid',
    title: 'Customize Me!',
    subtitle: 'Use the controls panel →',
    imageUrl: 'https://images.unsplash.com/photo-1632501641765-e568d28b0015?w=400&h=300&fit=crop',
    rating: 4.2,
    ratingMax: 5,
    metadata: [
      { icon: Users, value: '2-6' },
      { icon: Clock, value: '45 min' },
    ],
    badge: 'Demo',
    loading: false,
  },
  decorators: [
    Story => (
      <div className="w-[320px]">
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'Interactive playground with all controls exposed. Modify any prop using the controls panel to see live changes.',
      },
    },
  },
};

export const ClickableCard: Story = {
  render: function ClickableCardStory() {
    const [clickCount, setClickCount] = useState(0);
    const [lastAction, setLastAction] = useState<string | null>(null);

    return (
      <div className="flex flex-col gap-4 items-center">
        <div className="w-[280px]">
          <MeepleCard
            entity="game"
            variant="grid"
            title="Click Me!"
            subtitle="Interactive card demo"
            imageUrl="https://images.unsplash.com/photo-1632501641765-e568d28b0015?w=400&h=300&fit=crop"
            rating={8.5}
            ratingMax={10}
            metadata={[
              { icon: Users, value: '2-4' },
            ]}
            onClick={() => {
              setClickCount(c => c + 1);
              setLastAction('Card clicked');
            }}
          />
        </div>
        <div className="text-center p-4 bg-muted rounded-lg w-[280px]">
          <p className="text-sm font-medium">Click count: {clickCount}</p>
          {lastAction && (
            <p className="text-xs text-muted-foreground mt-1">{lastAction}</p>
          )}
        </div>
      </div>
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Demonstrates interactive behavior. Click or press Enter/Space to trigger the onClick handler.',
      },
    },
  },
};

export const ActionButtonsDemo: Story = {
  render: function ActionButtonsDemoStory() {
    const [lastAction, setLastAction] = useState<string | null>(null);

    return (
      <div className="flex flex-col gap-4 items-center">
        <div className="w-[400px]">
          <MeepleCard
            entity="event"
            variant="featured"
            title="Game Night"
            subtitle="Saturday, Feb 15 · 7:00 PM"
            imageUrl="https://images.unsplash.com/photo-1606092195730-5d7b9af1efc5?w=600&h=400&fit=crop"
            metadata={[
              { icon: MapPin, label: 'Milano, IT' },
              { icon: Users, label: '4/6 spots' },
            ]}
            actions={[
              {
                label: 'Join Event',
                primary: true,
                onClick: () => setLastAction('Joined event!'),
              },
              {
                label: 'Share',
                onClick: () => setLastAction('Shared event'),
              },
            ]}
          />
        </div>
        {lastAction && (
          <div className="text-center p-4 bg-muted rounded-lg w-[400px]">
            <p className="text-sm font-medium">{lastAction}</p>
          </div>
        )}
      </div>
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Action buttons demo. Each button triggers its own onClick handler independently from the card.',
      },
    },
  },
};

// ============================================================================
// Loading State Variants
// ============================================================================

export const AllLoadingVariants: Story = {
  render: () => (
    <div className="flex flex-col gap-6">
      <div>
        <h3 className="text-sm font-semibold mb-2 text-muted-foreground">Grid Skeleton</h3>
        <div className="w-[280px]">
          <MeepleCardSkeleton variant="grid" />
        </div>
      </div>
      <div>
        <h3 className="text-sm font-semibold mb-2 text-muted-foreground">List Skeleton</h3>
        <div className="w-[500px]">
          <MeepleCardSkeleton variant="list" />
        </div>
      </div>
      <div>
        <h3 className="text-sm font-semibold mb-2 text-muted-foreground">Compact Skeleton</h3>
        <div className="w-[300px]">
          <MeepleCardSkeleton variant="compact" />
        </div>
      </div>
      <div>
        <h3 className="text-sm font-semibold mb-2 text-muted-foreground">Featured Skeleton</h3>
        <div className="w-[400px]">
          <MeepleCardSkeleton variant="featured" />
        </div>
      </div>
      <div>
        <h3 className="text-sm font-semibold mb-2 text-muted-foreground">Hero Skeleton</h3>
        <div className="w-[600px]">
          <MeepleCardSkeleton variant="hero" />
        </div>
      </div>
    </div>
  ),
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        story: 'All skeleton variants for loading states. Use the `loading` prop or `MeepleCardSkeleton` component directly.',
      },
    },
  },
};

// ============================================================================
// Responsive Demo
// ============================================================================

export const ResponsiveDemo: Story = {
  render: () => (
    <div className="flex flex-col gap-8">
      <div>
        <h3 className="text-sm font-semibold mb-3 text-muted-foreground">Mobile (375px)</h3>
        <div className="w-[375px] bg-background p-4 rounded-lg border">
          <div className="grid grid-cols-2 gap-3">
            <MeepleCard
              entity="game"
              title="Azul"
              subtitle="Plan B Games"
              imageUrl="https://cf.geekdo-images.com/tz19PfklMdAdjxV9WArraA__opengraph_letterbox/img/A93i5hJz-A5GH_aZQTdcgF1GNPo=/fit-in/1200x630/filters:fill(auto)/pic3718275.jpg"
              rating={7.8}
              ratingMax={10}
            />
            <MeepleCard
              entity="player"
              title="Anna"
              subtitle="Online"
              avatarUrl="https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=200&h=200&fit=crop"
            />
          </div>
        </div>
      </div>
      <div>
        <h3 className="text-sm font-semibold mb-3 text-muted-foreground">Tablet (768px)</h3>
        <div className="w-[768px] bg-background p-4 rounded-lg border">
          <div className="grid grid-cols-3 gap-4">
            <MeepleCard
              entity="game"
              title="Wingspan"
              subtitle="Stonemaier Games"
              imageUrl="https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__opengraph_letterbox/img/hNYPYYOJFYcYvDW3p6v-mYbVpxk=/fit-in/1200x630/filters:fill(auto)/pic4458123.jpg"
              rating={8.1}
              ratingMax={10}
            />
            <MeepleCard
              entity="collection"
              title="Favorites"
              subtitle="12 games"
              imageUrl="https://images.unsplash.com/photo-1611371805429-8b5c1b2c34ba?w=400&h=300&fit=crop"
            />
            <MeepleCard
              entity="event"
              title="Weekend Game"
              subtitle="Sunday 3 PM"
              imageUrl="https://images.unsplash.com/photo-1606092195730-5d7b9af1efc5?w=400&h=300&fit=crop"
            />
          </div>
        </div>
      </div>
    </div>
  ),
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        story: 'Demonstrates responsive grid layouts at different viewport sizes.',
      },
    },
  },
};
