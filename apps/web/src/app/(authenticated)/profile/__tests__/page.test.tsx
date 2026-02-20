/**
 * Profile Landing Page Tests (Issue #4893)
 */

import { screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import ProfilePage from '../page';

const mockGetStats = vi.hoisted(() => vi.fn());
const mockUseAuth = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/useAuth', () => ({
  useAuth: mockUseAuth,
}));

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getStats: mockGetStats,
    },
  },
}));

const mockUser = {
  id: 'user-1',
  email: 'alice@example.com',
  displayName: 'Alice Smith',
  role: 'User',
};

const mockStats = {
  totalGames: 24,
  favoriteGames: 8,
  oldestAddedAt: null,
  newestAddedAt: null,
};

describe('ProfilePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuth.mockReturnValue({ user: mockUser });
    mockGetStats.mockResolvedValue(mockStats);
  });

  it('shows user display name in header', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Alice Smith')).toBeInTheDocument();
    });
  });

  it('shows user email in header', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });
  });

  it('shows user initials in avatar', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      // "Alice Smith" → initials "AS"
      expect(screen.getByText('AS')).toBeInTheDocument();
    });
  });

  it('renders tab bar with three tabs', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /Overview/i })).toBeInTheDocument();
    });

    expect(screen.getByRole('tab', { name: /Achievements/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /Activity/i })).toBeInTheDocument();
  });

  it('shows library stats on Overview tab', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Total Games')).toBeInTheDocument();
    });

    expect(screen.getByText('24')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
  });

  it('shows quick action links on Overview tab', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Quick Actions')).toBeInTheDocument();
    });

    expect(screen.getByText('My Library')).toBeInTheDocument();
    expect(screen.getByText('Game Sessions')).toBeInTheDocument();
  });

  it('switches to Achievements tab on click', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /Achievements/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('tab', { name: /Achievements/i }));

    expect(screen.getByText('Open Achievements')).toBeInTheDocument();
  });

  it('switches to Activity tab and shows coming soon', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /Activity/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('tab', { name: /Activity/i }));

    expect(screen.getByText('Activity Feed Coming Soon')).toBeInTheDocument();
  });

  it('shows loading skeleton while fetching stats', () => {
    mockGetStats.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<ProfilePage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('works without a logged-in user', () => {
    mockUseAuth.mockReturnValue({ user: null });

    renderWithQuery(<ProfilePage />);

    expect(screen.getByText('Player')).toBeInTheDocument();
  });
});
