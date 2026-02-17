/**
 * GameCarousel Storybook Stories (Issue #3589, Epic #4604 v2)
 *
 * Comprehensive visual documentation and interactive testing for the
 * GameCarousel immersive 3D carousel component.
 *
 * v2 Updates (Epic #4604):
 * - Center card: scale 1.1x (enhanced prominence)
 * - Side cards: scale 0.85x, opacity 0.6 (consistent fade)
 * - Warm shadows: var(--shadow-warm-2xl) on center card
 * - Entity glow inherited from MeepleCard hover
 *
 * @module components/ui/data-display/game-carousel.stories
 * @see Issue #3585 - Epic: GameCarousel Integration & Production Readiness
 * @see Issue #4612 - Enhanced Carousel v2
 */

import { useState } from 'react';

import { Clock, Users } from 'lucide-react';

import {
  GameCarousel,
  GameCarouselSkeleton,
  type CarouselGame,
  type CarouselSortValue,
} from './game-carousel';

import type { Meta, StoryObj } from '@storybook/react';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_GAMES: CarouselGame[] = [
  {
    id: '1',
    title: 'Gloomhaven',
    subtitle: 'Cephalofair Games',
    imageUrl:
      'https://cf.geekdo-images.com/sZYp_3BTDGjh2unaZfZmuA__imagepage/img/pBaOL7vV402ZGRoKKDOGBmCLdUs=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2437871.jpg',
    rating: 8.7,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '60-120 min' },
    ],
    badge: 'Top Rated',
  },
  {
    id: '2',
    title: 'Brass: Birmingham',
    subtitle: 'Roxley Games',
    imageUrl:
      'https://cf.geekdo-images.com/x3zxjr-Vw5iU4yDPg70Jgw__imagepage/img/giNUMut4HAl-zWyQkGG0YchmuLI=/fit-in/900x600/filters:no_upscale():strip_icc()/pic3490053.jpg',
    rating: 8.6,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '2-4' },
      { icon: Clock, value: '60-120 min' },
    ],
    badge: 'Strategy',
  },
  {
    id: '3',
    title: 'Ark Nova',
    subtitle: 'Capstone Games',
    imageUrl:
      'https://cf.geekdo-images.com/SoU8p28Sk1s8MSvoM4N8pQ__imagepage/img/qR1EvTSNPjDa-pNPGxU9HY2oKfs=/fit-in/900x600/filters:no_upscale():strip_icc()/pic6293412.jpg',
    rating: 8.5,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '90-150 min' },
    ],
    badge: 'New',
  },
  {
    id: '4',
    title: 'Terraforming Mars',
    subtitle: 'Stronghold Games',
    imageUrl:
      'https://cf.geekdo-images.com/wg9oOLcsKvDesSUdZQ4rxw__imagepage/img/FS1RE8Ue6nk1pNbPI3l-OSapQGc=/fit-in/900x600/filters:no_upscale():strip_icc()/pic3536616.jpg',
    rating: 8.4,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-5' },
      { icon: Clock, value: '120 min' },
    ],
  },
  {
    id: '5',
    title: 'Spirit Island',
    subtitle: 'Greater Than Games',
    imageUrl:
      'https://cf.geekdo-images.com/kjCm4ZvPjIZxS-mYgSPy1g__imagepage/img/h9D-SfnpjfTGXbsZM85CYRjS0oo=/fit-in/900x600/filters:no_upscale():strip_icc()/pic7013651.jpg',
    rating: 8.3,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '90-120 min' },
    ],
    badge: 'Co-op',
  },
  {
    id: '6',
    title: 'Dune: Imperium',
    subtitle: 'Dire Wolf Digital',
    imageUrl:
      'https://cf.geekdo-images.com/PhjygpWSo-0labGrPBMyyg__imagepage/img/BjM3LyahJ4IQ2ov5MkzkHatbmUc=/fit-in/900x600/filters:no_upscale():strip_icc()/pic5666597.jpg',
    rating: 8.3,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '60-120 min' },
    ],
  },
  {
    id: '7',
    title: 'Wingspan',
    subtitle: 'Stonemaier Games',
    imageUrl:
      'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__imagepage/img/uIjeoKgHMcRtzRSR4MoUYl3nXxs=/fit-in/900x600/filters:no_upscale():strip_icc()/pic4458123.jpg',
    rating: 8.1,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-5' },
      { icon: Clock, value: '40-70 min' },
    ],
    badge: 'Family',
  },
];

// ============================================================================
// Meta
// ============================================================================

const meta: Meta<typeof GameCarousel> = {
  title: 'UI/Data Display/GameCarousel',
  component: GameCarousel,
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component: `
# GameCarousel - Immersive 3D Game Browser

An immersive carousel component with depth perspective for browsing board games.
Features a prominent center card with fading side cards, creating a "Netflix-style" browsing experience.

## Features
- **3D Perspective**: Depth illusion with scale and blur effects
- **Responsive**: 1 card on mobile, 3 on tablet, 5 on desktop
- **Touch Support**: Swipe gestures for mobile navigation
- **Keyboard Navigation**: Arrow keys and Enter for accessibility
- **Infinite Loop**: Seamless navigation from last to first card
- **Auto-play**: Optional automatic rotation with pause on hover
- **Sorting**: Built-in sort dropdown with persistence support

## Accessibility
- WCAG 2.1 AA compliant
- Screen reader announcements for current card
- Keyboard navigation (Arrow Left/Right)
- Focus indicators on all interactive elements
- Mobile-friendly touch targets (≥44px)

## Usage
\`\`\`tsx
import { GameCarousel, useCarouselGames } from '@/components/ui/data-display';
import { useFeaturedGames } from '@/hooks/queries';

function FeaturedSection() {
  const { data, isLoading } = useFeaturedGames(10);

  if (isLoading) return <GameCarouselSkeleton />;

  return (
    <GameCarousel
      games={data.games}
      title="Featured Games"
      subtitle="Top-rated by the community"
      onGameSelect={(game) => router.push(\`/games/\${game.id}\`)}
      showDots
      autoPlay
    />
  );
}
\`\`\`
        `,
      },
    },
  },
  argTypes: {
    games: {
      control: 'object',
      description: 'Array of games to display in the carousel',
      table: {
        type: { summary: 'CarouselGame[]' },
      },
    },
    title: {
      control: 'text',
      description: 'Title displayed above the carousel',
      table: {
        type: { summary: 'string' },
      },
    },
    subtitle: {
      control: 'text',
      description: 'Subtitle/description below the title',
      table: {
        type: { summary: 'string' },
      },
    },
    autoPlay: {
      control: 'boolean',
      description: 'Enable automatic rotation',
      table: {
        type: { summary: 'boolean' },
        defaultValue: { summary: 'false' },
      },
    },
    autoPlayInterval: {
      control: { type: 'range', min: 2000, max: 10000, step: 500 },
      description: 'Auto-play interval in milliseconds',
      table: {
        type: { summary: 'number' },
        defaultValue: { summary: '5000' },
      },
    },
    showDots: {
      control: 'boolean',
      description: 'Show navigation dots indicator',
      table: {
        type: { summary: 'boolean' },
        defaultValue: { summary: 'true' },
      },
    },
    sortable: {
      control: 'boolean',
      description: 'Enable sorting dropdown',
      table: {
        type: { summary: 'boolean' },
        defaultValue: { summary: 'false' },
      },
    },
    defaultSort: {
      control: 'select',
      options: ['rating', 'popularity', 'name', 'date'],
      description: 'Default sort value',
      table: {
        type: { summary: 'CarouselSortValue' },
        defaultValue: { summary: 'rating' },
      },
    },
    onGameSelect: {
      action: 'gameSelected',
      description: 'Callback when a game card is clicked',
      table: {
        type: { summary: '(game: CarouselGame) => void' },
      },
    },
    onSortChange: {
      action: 'sortChanged',
      description: 'Callback when sort option changes',
      table: {
        type: { summary: '(sort: CarouselSortValue) => void' },
      },
    },
  },
};

export default meta;
type Story = StoryObj<typeof GameCarousel>;

// ============================================================================
// Stories
// ============================================================================

/**
 * Default carousel with mock data
 */
export const Default: Story = {
  args: {
    games: MOCK_GAMES,
    title: 'Featured Games',
    subtitle: 'Top-rated games loved by our community',
    showDots: true,
  },
};

/**
 * Carousel with auto-play enabled
 */
export const AutoPlay: Story = {
  args: {
    games: MOCK_GAMES,
    title: 'Featured Games',
    subtitle: 'Watch the games rotate automatically',
    showDots: true,
    autoPlay: true,
    autoPlayInterval: 3000,
  },
};

/**
 * Carousel with sorting controls
 */
export const WithSorting: Story = {
  args: {
    games: MOCK_GAMES,
    title: 'Browse Games',
    subtitle: 'Sort by rating, popularity, name, or date',
    showDots: true,
    sortable: true,
    defaultSort: 'rating',
  },
};

/**
 * Interactive story with controlled sort state
 */
export const ControlledSort: Story = {
  render: function ControlledSortStory() {
    const [sort, setSort] = useState<CarouselSortValue>('rating');

    return (
      <div className="space-y-4 p-4">
        <div className="text-center text-sm text-muted-foreground">
          Current sort: <strong>{sort}</strong>
        </div>
        <GameCarousel
          games={MOCK_GAMES}
          title="Controlled Sorting"
          subtitle="Sort state is managed externally"
          showDots
          sortable
          sort={sort}
          onSortChange={setSort}
        />
      </div>
    );
  },
};

/**
 * Loading state with skeleton
 */
export const Loading: Story = {
  render: () => <GameCarouselSkeleton />,
};

/**
 * Empty state with no games
 */
export const Empty: Story = {
  args: {
    games: [],
    title: 'No Games Available',
  },
};

/**
 * Minimal cards (just 3)
 */
export const FewCards: Story = {
  args: {
    games: MOCK_GAMES.slice(0, 3),
    title: 'Just Three Games',
    subtitle: 'Carousel adapts to small datasets',
    showDots: true,
  },
};

/**
 * No title or subtitle
 */
export const NoHeader: Story = {
  args: {
    games: MOCK_GAMES,
    showDots: true,
  },
};

/**
 * All features enabled
 */
export const FullFeatured: Story = {
  args: {
    games: MOCK_GAMES,
    title: 'Complete Experience',
    subtitle: 'All features enabled: auto-play, sorting, dots',
    showDots: true,
    autoPlay: true,
    autoPlayInterval: 5000,
    sortable: true,
    defaultSort: 'rating',
  },
};

/**
 * Responsive preview
 */
export const Responsive: Story = {
  args: {
    games: MOCK_GAMES,
    title: 'Responsive Design',
    subtitle: 'Resize the viewport to see adaptive layout',
    showDots: true,
  },
  parameters: {
    viewport: {
      viewports: {
        mobile: { name: 'Mobile', styles: { width: '375px', height: '667px' } },
        tablet: { name: 'Tablet', styles: { width: '768px', height: '1024px' } },
        desktop: { name: 'Desktop', styles: { width: '1280px', height: '800px' } },
      },
      defaultViewport: 'desktop',
    },
  },
};

/**
 * Dark mode preview
 */
export const DarkMode: Story = {
  args: {
    games: MOCK_GAMES,
    title: 'Dark Mode',
    subtitle: 'Carousel with dark theme active',
    showDots: true,
  },
  parameters: {
    backgrounds: {
      default: 'dark',
    },
  },
  decorators: [
    Story => (
      <div className="dark bg-background min-h-screen p-8">
        <Story />
      </div>
    ),
  ],
};

/**
 * Games without images (placeholder fallback)
 */
export const NoImages: Story = {
  args: {
    games: MOCK_GAMES.map(game => ({
      ...game,
      imageUrl: undefined,
    })),
    title: 'Games Without Images',
    subtitle: 'Placeholder images are shown when imageUrl is missing',
    showDots: true,
  },
};

/**
 * Games without ratings
 */
export const NoRatings: Story = {
  args: {
    games: MOCK_GAMES.map(game => ({
      ...game,
      rating: undefined,
      ratingMax: undefined,
    })),
    title: 'Games Without Ratings',
    subtitle: 'Rating display is hidden when not provided',
    showDots: true,
  },
};
