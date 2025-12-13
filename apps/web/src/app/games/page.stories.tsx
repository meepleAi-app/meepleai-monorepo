/* eslint-disable security/detect-object-injection -- Safe mock game data array access */
/**
 * Game Catalog Page Stories (Issue #1838: PAGE-003)
 *
 * Chromatic visual regression testing for Games Page.
 * Uses dynamic imports for Next.js compatibility.
 *
 * Stories:
 * - DefaultGrid: Grid view with 20 mock games
 * - ListView: List view variant
 * - MobileFlow: 375px mobile viewport
 * - TabletFlow: 768px tablet viewport
 * - DesktopFlow: 1440px desktop viewport
 * - EmptyState: No games found
 * - LoadingState: Skeleton state
 * - DarkTheme: Dark mode
 */

import type { Meta, StoryObj } from '@storybook/react';
import { Game } from '@/lib/api';
import { ViewToggle } from './components/ViewToggle';
import { SearchBar } from './components/SearchBar';
import { GameGrid } from './components/GameGrid';
import { Pagination } from './components/Pagination';

// ============================================================================
// Mock Component (Simulates Server Component for Storybook)
// ============================================================================

function GamesPageStory({
  games,
  view = 'grid',
  search = '',
  currentPage = 1,
  totalPages = 5,
  total = 100,
  loading = false,
}: {
  games: Game[];
  view?: 'grid' | 'list';
  search?: string;
  currentPage?: number;
  totalPages?: number;
  total?: number;
  loading?: boolean;
}) {
  return (
    <div className="container mx-auto px-4 py-8">
      {/* Header */}
      <div className="mb-8">
        <h1 className="font-heading text-3xl md:text-4xl font-bold mb-2">Catalogo Giochi</h1>
        <p className="text-muted-foreground">
          Esplora {total} giochi da tavolo. Cerca, filtra e scopri le regole.
        </p>
      </div>

      {/* Toolbar: Search + View Toggle */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between mb-6">
        <div className="w-full sm:w-96">
          <SearchBar currentSearch={search} />
        </div>
        <ViewToggle currentView={view} />
      </div>

      {/* Games Grid/List */}
      <GameGrid games={games} variant={view} loading={loading} />

      {/* Pagination */}
      {totalPages > 1 && (
        <Pagination currentPage={currentPage} totalPages={totalPages} totalItems={total} />
      )}
    </div>
  );
}

// ============================================================================
// Mock Data
// ============================================================================

const mockGames: Game[] = Array.from({ length: 20 }, (_, i) => ({
  id: `game-${i + 1}`,
  title:
    [
      'Catan',
      'Ticket to Ride',
      'Wingspan',
      'Azul',
      'Carcassonne',
      'Pandemic',
      '7 Wonders',
      'Splendor',
      'King of Tokyo',
      'Codenames',
      'Dixit',
      'Dominion',
      'Terraforming Mars',
      'Scythe',
      'Gloomhaven',
      'Brass: Birmingham',
      'Spirit Island',
      'Root',
      'Everdell',
      'Cascadia',
    ][i] || `Game ${i + 1}`,
  imageUrl: `https://picsum.photos/seed/${i}/160/220`,
  publisher: ['CMON', 'Stonemaier', 'Days of Wonder', 'Next Move Games'][i % 4],
  minPlayers: [2, 2, 1, 2][i % 4],
  maxPlayers: [4, 5, 5, 4][i % 4],
  minPlayTimeMinutes: [30, 45, 60, 90][i % 4],
  maxPlayTimeMinutes: [60, 90, 90, 120][i % 4],
  yearPublished: 2015 + (i % 10),
  averageRating: 7.0 + (i % 3),
  bggId: 1000 + i,
  faqCount: i % 3 === 0 ? i + 5 : 0,
  createdAt: new Date(2024, 0, i + 1).toISOString(),
}));

// ============================================================================
// Story Meta
// ============================================================================

const meta: Meta<typeof GamesPageStory> = {
  title: 'Pages/GameCatalog',
  component: GamesPageStory,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/games',
        query: {},
      },
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof GamesPageStory>;

// ============================================================================
// Stories
// ============================================================================

/**
 * Default Grid View
 * Standard catalog with 20 games in grid layout
 */
export const DefaultGrid: Story = {
  args: {
    games: mockGames,
    view: 'grid',
    currentPage: 1,
    totalPages: 5,
    total: 100,
  },
};

/**
 * List View
 * Alternative layout showing full-width game cards
 */
export const ListView: Story = {
  args: {
    games: mockGames,
    view: 'list',
    currentPage: 1,
    totalPages: 5,
    total: 100,
  },
};

/**
 * Mobile Flow (375px)
 * 2-column grid on mobile devices
 */
export const MobileFlow: Story = {
  args: {
    games: mockGames.slice(0, 8),
    view: 'grid',
    currentPage: 1,
    totalPages: 3,
    total: 50,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet Flow (768px)
 * 3-column grid on tablet devices
 */
export const TabletFlow: Story = {
  args: {
    games: mockGames.slice(0, 12),
    view: 'grid',
    currentPage: 1,
    totalPages: 4,
    total: 75,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Desktop Flow (1440px)
 * 4-column grid on desktop
 */
export const DesktopFlow: Story = {
  args: {
    games: mockGames,
    view: 'grid',
    currentPage: 2,
    totalPages: 5,
    total: 100,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1440],
    },
  },
};

/**
 * Empty State
 * No games found after search/filter
 */
export const EmptyState: Story = {
  args: {
    games: [],
    view: 'grid',
    search: 'nonexistent',
    currentPage: 1,
    totalPages: 0,
    total: 0,
  },
};

/**
 * Loading State
 * Skeleton loaders while fetching
 */
export const LoadingState: Story = {
  args: {
    games: [],
    view: 'grid',
    currentPage: 1,
    totalPages: 0,
    total: 0,
    loading: true,
  },
};

/**
 * Dark Theme
 * Dark mode variant
 */
export const DarkTheme: Story = {
  args: {
    games: mockGames.slice(0, 12),
    view: 'grid',
    currentPage: 1,
    totalPages: 3,
    total: 60,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
};
