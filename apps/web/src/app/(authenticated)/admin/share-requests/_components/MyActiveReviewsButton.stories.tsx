/**
 * MyActiveReviewsButton Component Stories
 *
 * Visual testing for active reviews popover/sheet button.
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import type { ActiveReviewDto } from '@/lib/api/schemas/admin-share-requests.schemas';

import { MyActiveReviewsButton } from './MyActiveReviewsButton';

import type { Meta, StoryObj } from '@storybook/react';

// Mock active reviews data
const mockActiveReviews: ActiveReviewDto[] = [
  {
    shareRequestId: 'share-1',
    sourceGameId: 'game-1',
    gameTitle: 'Wingspan',
    contributorId: 'user-1',
    contributorName: 'Mario Rossi',
    reviewStartedAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(), // 10 minutes ago
    reviewLockExpiresAt: new Date(Date.now() + 20 * 60 * 1000).toISOString(), // 20 minutes from now
    status: 'InReview',
  },
  {
    shareRequestId: 'share-2',
    sourceGameId: 'game-2',
    gameTitle: 'Terraforming Mars: Ares Expedition',
    contributorId: 'user-2',
    contributorName: 'Giulia Bianchi',
    reviewStartedAt: new Date(Date.now() - 25 * 60 * 1000).toISOString(), // 25 minutes ago
    reviewLockExpiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(), // 5 minutes from now (expiring soon)
    status: 'InReview',
  },
  {
    shareRequestId: 'share-3',
    sourceGameId: 'game-3',
    gameTitle: 'Gloomhaven',
    contributorId: 'user-3',
    contributorName: 'Luca Verdi',
    reviewStartedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 minutes ago
    reviewLockExpiresAt: new Date(Date.now() + 25 * 60 * 1000).toISOString(), // 25 minutes from now
    status: 'InReview',
  },
];

// Create QueryClient with mocked data
const createMockQueryClient = (reviews: ActiveReviewDto[]) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: Infinity,
      },
    },
  });

  // Pre-populate cache with mock data
  queryClient.setQueryData(['admin', 'shareRequests', 'myReviews'], reviews);

  return queryClient;
};

const meta: Meta<typeof MyActiveReviewsButton> = {
  title: 'Admin/ShareRequests/MyActiveReviewsButton',
  component: MyActiveReviewsButton,
  parameters: {
    layout: 'centered',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/admin/share-requests',
      },
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof MyActiveReviewsButton>;

/**
 * No Active Reviews
 * Button with badge count = 0, empty state in sheet
 */
export const NoActiveReviews: Story = {
  decorators: [
    Story => {
      const queryClient = createMockQueryClient([]);
      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * One Active Review
 * Button with badge count = 1
 */
export const OneActiveReview: Story = {
  decorators: [
    Story => {
      const queryClient = createMockQueryClient([mockActiveReviews[0]]);
      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Multiple Active Reviews (3)
 * Button with badge count = 3, one expiring soon
 */
export const MultipleActiveReviews: Story = {
  decorators: [
    Story => {
      const queryClient = createMockQueryClient(mockActiveReviews);
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
 * Button in loading state while fetching reviews
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
};
