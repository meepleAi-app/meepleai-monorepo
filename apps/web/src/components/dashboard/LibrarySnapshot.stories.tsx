/**
 * LibrarySnapshot Storybook Stories
 * Issue #3912 - Library Snapshot Component
 *
 * Visual regression stories for:
 * - Default state with quota and top games
 * - Loading skeleton
 * - Empty state
 * - Quota variants (low/medium/high)
 * - Different game counts
 * - Dark mode variants
 */

import type { Meta, StoryObj } from '@storybook/react';
import { LibrarySnapshot, type TopGame, type LibraryQuota } from './LibrarySnapshot';

const meta = {
  title: 'Dashboard/LibrarySnapshot',
  component: LibrarySnapshot,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Compact library overview widget showing collection quota and top 3 most played games. Part of Dashboard Hub (Epic #3901).',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[400px] min-h-[400px] p-4 bg-background">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof LibrarySnapshot>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Mock Data
// ============================================================================

const lowQuota: LibraryQuota = {
  used: 35,
  total: 200,
}; // 17.5% - green

const mediumQuota: LibraryQuota = {
  used: 127,
  total: 200,
}; // 63.5% - amber

const highQuota: LibraryQuota = {
  used: 185,
  total: 200,
}; // 92.5% - red

const topGames: TopGame[] = [
  {
    id: 'game-1',
    title: 'Catan',
    coverUrl: '/images/games/catan.jpg',
    rating: 5,
    playCount: 45,
  },
  {
    id: 'game-2',
    title: 'Ticket to Ride',
    coverUrl: '/images/games/ticket.jpg',
    rating: 4,
    playCount: 32,
  },
  {
    id: 'game-3',
    title: 'Azul',
    coverUrl: '/images/games/azul.jpg',
    rating: 4,
    playCount: 28,
  },
];

const gamesWithoutCovers: TopGame[] = topGames.map((game) => ({
  ...game,
  coverUrl: undefined,
}));

const fewGames: TopGame[] = [topGames[0], topGames[1]];

const singleGame: TopGame[] = [topGames[0]];

// ============================================================================
// Stories
// ============================================================================

/**
 * Default state with medium quota (amber) and 3 games
 */
export const Default: Story = {
  args: {
    quota: mediumQuota,
    topGames,
    isLoading: false,
  },
};

/**
 * Loading skeleton state
 */
export const Loading: Story = {
  args: {
    isLoading: true,
  },
};

/**
 * Empty state when no games in collection
 */
export const Empty: Story = {
  args: {
    quota: { used: 0, total: 200 },
    topGames: [],
    isLoading: false,
  },
};

/**
 * Low quota (< 50%) - green progress bar
 */
export const LowQuota: Story = {
  args: {
    quota: lowQuota,
    topGames,
    isLoading: false,
  },
};

/**
 * Medium quota (50-80%) - amber progress bar
 */
export const MediumQuota: Story = {
  args: {
    quota: mediumQuota,
    topGames,
    isLoading: false,
  },
};

/**
 * High quota (> 80%) - red progress bar
 */
export const HighQuota: Story = {
  args: {
    quota: highQuota,
    topGames,
    isLoading: false,
  },
};

/**
 * Only 2 games in library
 */
export const FewGames: Story = {
  args: {
    quota: lowQuota,
    topGames: fewGames,
    isLoading: false,
  },
};

/**
 * Single game only
 */
export const SingleGame: Story = {
  args: {
    quota: { used: 1, total: 200 },
    topGames: singleGame,
    isLoading: false,
  },
};

/**
 * Games without cover images (fallback icons)
 */
export const NoCoverImages: Story = {
  args: {
    quota: mediumQuota,
    topGames: gamesWithoutCovers,
    isLoading: false,
  },
};

/**
 * All rating variants (1-5 stars)
 */
export const RatingVariants: Story = {
  args: {
    quota: mediumQuota,
    topGames: [
      { id: '1', title: 'Rating 5', rating: 5, playCount: 50 },
      { id: '2', title: 'Rating 3', rating: 3, playCount: 30 },
      { id: '3', title: 'Rating 1', rating: 1, playCount: 10 },
    ],
    isLoading: false,
  },
};

/**
 * Full quota (100%) - red progress bar
 */
export const FullQuota: Story = {
  args: {
    quota: { used: 200, total: 200 },
    topGames,
    isLoading: false,
  },
};

/**
 * Dark mode variant (requires Storybook dark background)
 */
export const DarkMode: Story = {
  args: {
    quota: mediumQuota,
    topGames,
    isLoading: false,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
};
