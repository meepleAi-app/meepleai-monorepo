/**
 * Dashboard Page Storybook Stories (Issue #1836)
 *
 * Full page composition for visual regression testing with Chromatic.
 * Tests complete dashboard with all sections and states.
 *
 * Mocked Dependencies:
 * - TanStack Query (useCurrentUser, useGames)
 * - Authentication (assumes authenticated user)
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import DashboardPage from './page';
import type { AuthUser, Game } from '@/lib/api';

// Mock data for stories
const mockUser: AuthUser = {
  id: 'user-123',
  email: 'mario.rossi@example.com',
  displayName: 'Mario Rossi',
  role: 'user',
};

const mockGames: Game[] = [
  {
    id: 'game-1',
    title: 'Catan',
    publisher: 'Catan Studio',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    imageUrl: 'https://via.placeholder.com/160x220?text=Catan',
    averageRating: 7.5,
    bggId: 13,
    faqCount: 45,
  },
  {
    id: 'game-2',
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 5,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 60,
    imageUrl: 'https://via.placeholder.com/160x220?text=Ticket+to+Ride',
    averageRating: 7.8,
    bggId: 9209,
    faqCount: 23,
  },
  {
    id: 'game-3',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
    minPlayTimeMinutes: 40,
    maxPlayTimeMinutes: 70,
    imageUrl: 'https://via.placeholder.com/160x220?text=Wingspan',
    averageRating: 8.2,
    bggId: 266192,
    faqCount: 67,
  },
  {
    id: 'game-4',
    title: '7 Wonders',
    publisher: 'Repos Production',
    yearPublished: 2010,
    minPlayers: 2,
    maxPlayers: 7,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 30,
    imageUrl: 'https://via.placeholder.com/160x220?text=7+Wonders',
    averageRating: 7.7,
    bggId: 68448,
    faqCount: 34,
  },
  {
    id: 'game-5',
    title: 'Azul',
    publisher: 'Plan B Games',
    yearPublished: 2017,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 45,
    imageUrl: 'https://via.placeholder.com/160x220?text=Azul',
    averageRating: 7.9,
    bggId: 230802,
    faqCount: 12,
  },
  {
    id: 'game-6',
    title: 'Splendor',
    publisher: 'Space Cowboys',
    yearPublished: 2014,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 30,
    imageUrl: 'https://via.placeholder.com/160x220?text=Splendor',
    averageRating: 7.4,
    bggId: 148228,
    faqCount: 8,
  },
];

// Create QueryClient with mocked data
const createMockQueryClient = (userData: AuthUser, gamesData: Game[]) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: Infinity,
      },
    },
  });

  // Pre-populate cache with mock data
  queryClient.setQueryData(['user', 'current'], userData);
  queryClient.setQueryData(
    ['games', 'list', { filters: undefined, sort: undefined, page: 1, pageSize: 6 }],
    {
      games: gamesData,
      total: gamesData.length,
      page: 1,
      pageSize: 6,
      totalPages: 1,
    }
  );

  return queryClient;
};

const meta: Meta<typeof DashboardPage> = {
  title: 'Pages/Dashboard',
  component: DashboardPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/dashboard',
      },
    },
    chromatic: {
      viewports: [375, 768, 1440],
      delay: 500, // Allow TanStack Query and animations to settle
    },
  },
  decorators: [
    Story => {
      const queryClient = createMockQueryClient(mockUser, mockGames);
      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof DashboardPage>;

/**
 * Default Dashboard View
 * Complete authenticated dashboard with all sections populated
 */
export const Default: Story = {};

/**
 * Mobile Experience (375px)
 * Mobile flow with BottomNav visible, 2-column game grid
 */
export const MobileFlow: Story = {
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
 * Tablet Experience (768px)
 * Tablet responsive layout
 */
export const TabletFlow: Story = {
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
 * Desktop Experience (1440px)
 * Full desktop layout with 3-column game grid
 */
export const DesktopFlow: Story = {
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
 * Empty State - No Games
 * Dashboard with no games yet, showing empty state
 */
export const EmptyGames: Story = {
  decorators: [
    Story => {
      const queryClient = createMockQueryClient(mockUser, []);
      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Loading State
 * Dashboard while fetching user and games data
 */
export const Loading: Story = {
  decorators: [
    Story => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            enabled: false, // Prevent actual fetching
          },
        },
      });
      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
  parameters: {
    chromatic: {
      delay: 100, // Catch loading state quickly
    },
  },
};

/**
 * Dark Theme - Full Dashboard
 * Complete dark mode experience
 */
export const DarkTheme: Story = {
  decorators: [
    Story => {
      const queryClient = createMockQueryClient(mockUser, mockGames);
      return (
        <div className="dark">
          <QueryClientProvider client={queryClient}>
            <Story />
          </QueryClientProvider>
        </div>
      );
    },
  ],
};
