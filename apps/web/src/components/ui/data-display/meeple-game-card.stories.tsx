/**
 * MeepleGameCard Stories
 *
 * PRIMARY game card component for MeepleAI.
 * Frosted glass design with 3D flip animation.
 */

import { MeepleGameCard, MeepleGameCardSkeleton } from './meeple-game-card';

import type { Meta, StoryObj } from '@storybook/react';


const meta: Meta<typeof MeepleGameCard> = {
  title: 'Data Display/MeepleGameCard',
  component: MeepleGameCard,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
    backgrounds: {
      default: 'dark',
      values: [
        { name: 'dark', value: '#0f172a' },
        { name: 'darker', value: '#030712' },
      ],
    },
    docs: {
      description: {
        component: `
**MeepleGameCard** is the PRIMARY game card component for MeepleAI.

## Features
- Frosted glass effect with backdrop-blur
- 3D flip animation (Framer Motion)
- Front: Cover image, title, rating stars, metadata chips
- Back: Description, categories, mechanics, designers
- Status indicators: Owned, Wishlist, Favorite
- Accessible: Keyboard navigation, ARIA labels

## Usage
\`\`\`tsx
import { MeepleGameCard } from '@/components/ui/data-display';

<MeepleGameCard
  id="1"
  title="Gloomhaven"
  publisher="Cephalofair Games"
  year={2017}
  imageUrl="/games/gloomhaven.jpg"
  rating={8.7}
  minPlayers={1}
  maxPlayers={4}
  playingTimeMinutes={120}
  complexity={3.86}
  onAddToLibrary={() => console.log('Add to library')}
  onViewDetails={() => router.push('/games/1')}
/>
\`\`\`
        `,
      },
    },
  },
  decorators: [
    (Story) => (
      <div className="min-h-[500px] p-8 bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    rating: {
      control: { type: 'range', min: 0, max: 10, step: 0.1 },
    },
    complexity: {
      control: { type: 'range', min: 1, max: 5, step: 0.1 },
    },
    minPlayers: {
      control: { type: 'number', min: 1, max: 12 },
    },
    maxPlayers: {
      control: { type: 'number', min: 1, max: 12 },
    },
    playingTimeMinutes: {
      control: { type: 'number', min: 5, max: 600 },
    },
    isOwned: { control: 'boolean' },
    isWishlist: { control: 'boolean' },
    isFavorite: { control: 'boolean' },
  },
};

export default meta;
type Story = StoryObj<typeof MeepleGameCard>;

// ============================================================================
// Mock Data
// ============================================================================

const gloomhavenData = {
  id: '1',
  title: 'Gloomhaven',
  publisher: 'Cephalofair Games',
  year: 2017,
  imageUrl: 'https://cf.geekdo-images.com/sZYp_3BTDGjh2unaZfZmuA__imagepage/img/pBaOL7vV402ZGRoKKDOGBmCLdUs=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2437871.jpg',
  rating: 8.7,
  minPlayers: 1,
  maxPlayers: 4,
  playingTimeMinutes: 120,
  complexity: 3.86,
  description:
    'Gloomhaven is a game of Euro-inspired tactical combat in a persistent world of shifting motives. Players will take on the role of a wandering adventurer with their own special set of skills.',
  categories: [
    { id: '1', name: 'Adventure' },
    { id: '2', name: 'Fantasy' },
    { id: '3', name: 'Fighting' },
  ],
  mechanics: [
    { id: '1', name: 'Campaign' },
    { id: '2', name: 'Cooperative' },
    { id: '3', name: 'Hand Management' },
  ],
  designers: [{ id: '1', name: 'Isaac Childres' }],
};

const wingspanData = {
  id: '2',
  title: 'Wingspan',
  publisher: 'Stonemaier Games',
  year: 2019,
  imageUrl: 'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__imagepage/img/uIjeoKgHMcRtzRSR4MoUYl3nXxs=/fit-in/900x600/filters:no_upscale():strip_icc()/pic4458123.jpg',
  rating: 8.1,
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 70,
  complexity: 2.44,
  description:
    'Wingspan is a competitive, medium-weight, card-driven, engine-building board game about birds. Players seek to attract birds to their wildlife preserves.',
  categories: [
    { id: '1', name: 'Animals' },
    { id: '2', name: 'Card Game' },
  ],
  mechanics: [
    { id: '1', name: 'Engine Building' },
    { id: '2', name: 'Hand Management' },
  ],
  designers: [{ id: '1', name: 'Elizabeth Hargrave' }],
};

// ============================================================================
// Stories
// ============================================================================

/**
 * Default state showing a complete game card with all data.
 */
export const Default: Story = {
  args: {
    ...gloomhavenData,
    badge: 'Top Rated',
  },
};

/**
 * Card marked as owned by the user.
 */
export const Owned: Story = {
  args: {
    ...gloomhavenData,
    isOwned: true,
    isFavorite: true,
  },
};

/**
 * Card in user's wishlist.
 */
export const Wishlist: Story = {
  args: {
    ...wingspanData,
    isWishlist: true,
  },
};

/**
 * Card with a custom badge.
 */
export const WithBadge: Story = {
  args: {
    ...gloomhavenData,
    badge: 'Strategy',
    isOwned: true,
  },
};

/**
 * Card without an image, showing placeholder.
 */
export const NoImage: Story = {
  args: {
    id: '3',
    title: 'Mystery Game',
    publisher: 'Unknown Publisher',
    year: 2024,
    rating: 7.5,
    minPlayers: 2,
    maxPlayers: 6,
    playingTimeMinutes: 90,
    complexity: 2.5,
    description: 'A mysterious game with no cover image yet.',
    categories: [{ id: '1', name: 'Mystery' }],
    mechanics: [{ id: '1', name: 'Deduction' }],
    designers: [{ id: '1', name: 'Anonymous' }],
  },
};

/**
 * Card with minimal data.
 */
export const MinimalData: Story = {
  args: {
    id: '4',
    title: 'Simple Game',
    publisher: 'Small Publisher',
  },
};

/**
 * Loading skeleton state.
 */
export const Skeleton: Story = {
  render: () => <MeepleGameCardSkeleton />,
};

/**
 * Multiple cards in a grid layout.
 */
export const GridLayout: Story = {
  render: () => (
    <div className="flex flex-wrap justify-center gap-8">
      <MeepleGameCard
        {...gloomhavenData}
        badge="Top Rated"
        isOwned
        isFavorite
      />
      <MeepleGameCard
        {...wingspanData}
        isWishlist
      />
      <MeepleGameCard
        id="3"
        title="Brass: Birmingham"
        publisher="Roxley Games"
        year={2018}
        rating={8.6}
        minPlayers={2}
        maxPlayers={4}
        playingTimeMinutes={120}
        complexity={3.91}
        badge="Strategy"
        description="An economic strategy game during the industrial revolution."
        categories={[
          { id: '1', name: 'Economic' },
          { id: '2', name: 'Industry' },
        ]}
        mechanics={[
          { id: '1', name: 'Network Building' },
          { id: '2', name: 'Hand Management' },
        ]}
        designers={[
          { id: '1', name: 'Gavan Brown' },
          { id: '2', name: 'Matt Tolman' },
        ]}
      />
    </div>
  ),
};

/**
 * Interactive example with action callbacks.
 */
export const Interactive: Story = {
  args: {
    ...gloomhavenData,
    badge: 'Featured',
    onAddToLibrary: () => alert('Added to library!'),
    onViewDetails: () => alert('Viewing details!'),
  },
};
