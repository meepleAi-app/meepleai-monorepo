/**
 * StatsZone Tests
 *
 * Tests:
 * 1. Renders 4 stat cards with correct values
 * 2. Shows skeleton when loading
 * 3. Has correct data-testid
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { DashboardData } from '@/types/dashboard';

const MOCK_DATA: DashboardData = {
  user: {
    id: 'user-1',
    username: 'TestUser',
    email: 'test@example.com',
  },
  stats: {
    libraryCount: 42,
    playedLast30Days: 8,
    chatCount: 5,
    wishlistCount: 10,
    currentStreak: 3,
  },
  activeSessions: [],
  librarySnapshot: { quota: { used: 42, total: 200 }, topGames: [] },
  recentActivity: [],
  chatHistory: [],
};

// Mock the useDashboardData hook
const mockUseDashboardData = vi.fn();

vi.mock('@/hooks/useDashboardData', () => ({
  useDashboardData: () => mockUseDashboardData(),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

// Import after mock setup
const { StatsZone } = await import('../StatsZone');

describe('StatsZone', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('has correct data-testid', () => {
    mockUseDashboardData.mockReturnValue({ data: MOCK_DATA, isLoading: false });
    render(<StatsZone />, { wrapper: createWrapper() });

    expect(screen.getByTestId('stats-zone')).toBeInTheDocument();
  });

  it('renders 4 stat cards with correct values', () => {
    mockUseDashboardData.mockReturnValue({ data: MOCK_DATA, isLoading: false });
    render(<StatsZone />, { wrapper: createWrapper() });

    // Values
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();

    // Labels
    expect(screen.getByText('Games in library')).toBeInTheDocument();
    expect(screen.getByText('Played last 30 days')).toBeInTheDocument();
    expect(screen.getByText('Active chats')).toBeInTheDocument();
    expect(screen.getByText('Current streak')).toBeInTheDocument();
  });

  it('shows skeleton when loading', () => {
    mockUseDashboardData.mockReturnValue({ data: undefined, isLoading: true });
    render(<StatsZone />, { wrapper: createWrapper() });

    const zone = screen.getByTestId('stats-zone');
    expect(zone).toBeInTheDocument();

    const skeletons = screen.getAllByLabelText('Loading stats');
    expect(skeletons).toHaveLength(4);
  });

  it('renders nothing when data is null and not loading', () => {
    mockUseDashboardData.mockReturnValue({ data: null, isLoading: false });
    const { container } = render(<StatsZone />, { wrapper: createWrapper() });

    expect(container.innerHTML).toBe('');
  });
});
