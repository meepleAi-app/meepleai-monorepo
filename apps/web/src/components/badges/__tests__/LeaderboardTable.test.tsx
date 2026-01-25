/**
 * LeaderboardTable Component Tests (Issue #2747)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LeaderboardTable } from '../LeaderboardTable';
import { BadgeTier, type LeaderboardEntryDto } from '@/types/badges';
import * as queries from '@/hooks/queries';

vi.mock('@/hooks/queries', () => ({
  useLeaderboard: vi.fn(),
  useCurrentUser: vi.fn(),
}));

describe('LeaderboardTable', () => {
  const mockLeaderboard: LeaderboardEntryDto[] = [
    {
      userId: 'user-1',
      userName: 'Alice',
      avatarUrl: '/avatars/alice.png',
      contributionCount: 50,
      topBadges: [
        {
          id: 'b1',
          name: 'Diamond Badge',
          description: 'Top contributor',
          tier: BadgeTier.Diamond,
          iconUrl: '/icons/diamond.png',
          earnedAt: '2026-01-20T10:00:00Z',
          isDisplayed: true,
        },
      ],
      rank: 1,
    },
    {
      userId: 'user-2',
      userName: 'Bob',
      avatarUrl: null,
      contributionCount: 30,
      topBadges: [],
      rank: 2,
    },
  ];

  const mockCurrentUser = { id: 'user-2', email: 'bob@test.com', displayName: 'Bob', role: 'User' };

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(queries.useLeaderboard).mockReturnValue({
      data: mockLeaderboard,
      isLoading: false,
    } as any);

    vi.mocked(queries.useCurrentUser).mockReturnValue({
      data: mockCurrentUser,
    } as any);
  });

  const renderWithClient = (component: React.ReactElement) => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    return render(
      <QueryClientProvider client={queryClient}>{component}</QueryClientProvider>
    );
  };

  it('should render leaderboard entries', () => {
    renderWithClient(<LeaderboardTable />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('50 contributions')).toBeInTheDocument();
    expect(screen.getByText('30 contributions')).toBeInTheDocument();
  });

  it('should highlight current user', () => {
    renderWithClient(<LeaderboardTable />);

    const bobRow = screen.getByText('Bob').closest('div')!;
    expect(bobRow).toHaveClass('border-primary');
    expect(screen.getByText('(You)')).toBeInTheDocument();
  });

  it('should display position icons for top 3', () => {
    renderWithClient(<LeaderboardTable />);

    expect(screen.getByText('🥇')).toBeInTheDocument(); // 1st place
    expect(screen.getByText('🥈')).toBeInTheDocument(); // 2nd place
  });

  it('should switch periods when tabs are clicked', async () => {
    const user = userEvent.setup();

    renderWithClient(<LeaderboardTable />);

    const weekTab = screen.getByText('This Week');
    await user.click(weekTab);

    await waitFor(() => {
      expect(queries.useLeaderboard).toHaveBeenCalledWith('ThisWeek');
    });
  });

  it('should show loading skeleton', () => {
    vi.mocked(queries.useLeaderboard).mockReturnValue({
      data: undefined,
      isLoading: true,
    } as any);

    renderWithClient(<LeaderboardTable />);

    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
  });

  it('should show empty state when no contributors', () => {
    vi.mocked(queries.useLeaderboard).mockReturnValue({
      data: [],
      isLoading: false,
    } as any);

    renderWithClient(<LeaderboardTable />);

    expect(screen.getByTestId('leaderboard-empty-message')).toBeInTheDocument();
  });
});
