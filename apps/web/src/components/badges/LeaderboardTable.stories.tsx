/**
 * LeaderboardTable Storybook Stories (Issue #2747)
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LeaderboardTable } from './LeaderboardTable';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Badges/LeaderboardTable',
  component: LeaderboardTable,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component: 'Leaderboard table with period filters and current user highlighting',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof LeaderboardTable>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    period: 'AllTime',
  },
  parameters: {
    docs: {
      description: {
        story: 'Default leaderboard showing all-time top contributors',
      },
    },
  },
};

export const ThisWeek: Story = {
  args: {
    period: 'ThisWeek',
  },
  parameters: {
    docs: {
      description: {
        story: 'Leaderboard filtered to this week only',
      },
    },
  },
};

export const ThisMonth: Story = {
  args: {
    period: 'ThisMonth',
  },
  parameters: {
    docs: {
      description: {
        story: 'Leaderboard filtered to this month only',
      },
    },
  },
};
