/**
 * MeepleCard Storybook Stories
 *
 * Visual documentation and testing for the universal card component system.
 *
 * @module components/ui/data-display/meeple-card.stories
 */

import type { Meta, StoryObj } from '@storybook/react';
import { Users, Clock, Calendar, MapPin, Trophy, Gamepad2, BookOpen, Star } from 'lucide-react';

import { MeepleCard } from './meeple-card';

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

A polymorphic card component supporting multiple entity types and layout variants.

## Entity Types
- **game**: Board games (orange accent)
- **player**: User profiles (purple accent)
- **collection**: Game lists/playlists (teal accent)
- **event**: Game sessions/tournaments (rose accent)
- **custom**: Custom entities with configurable color

## Layout Variants
- **grid**: Standard card for grid layouts (default)
- **list**: Horizontal compact layout for lists
- **compact**: Minimal inline display
- **featured**: Larger card with action buttons
- **hero**: Full-width cinematic display
        `,
      },
    },
  },
  argTypes: {
    entity: {
      control: 'select',
      options: ['game', 'player', 'collection', 'event', 'custom'],
      description: 'Entity type determines the color scheme',
    },
    variant: {
      control: 'select',
      options: ['grid', 'list', 'compact', 'featured', 'hero'],
      description: 'Layout variant',
    },
    loading: {
      control: 'boolean',
      description: 'Show loading skeleton',
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
