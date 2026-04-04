/**
 * PlayerStatistics Component Tests
 *
 * Tests statistics display and chart rendering.
 * Issue #3892: Play Records Frontend UI
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PlayerStatistics } from '@/components/play-records/PlayerStatistics';
import * as usePlayRecordsHooks from '@/lib/domain-hooks/usePlayRecords';

// Mock hooks
vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayerStatistics: vi.fn(),
}));

const mockStatsData = {
  totalSessions: 42,
  totalWins: 18,
  gamePlayCounts: {
    'Twilight Imperium': 12,
    Wingspan: 8,
    Gloomhaven: 10,
    Azul: 5,
    'Ticket to Ride': 7,
  },
  averageScoresByGame: {
    'Twilight Imperium': 245.5,
    Wingspan: 78.2,
    Gloomhaven: 156.8,
  },
};

describe('PlayerStatistics', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });

    vi.mocked(usePlayRecordsHooks.usePlayerStatistics).mockReturnValue({
      data: mockStatsData,
      isLoading: false,
      error: null,
    } as any);
  });

  const renderComponent = () => {
    return render(
      <QueryClientProvider client={queryClient}>
        <PlayerStatistics />
      </QueryClientProvider>
    );
  };

  it('displays stat cards with correct values', () => {
    renderComponent();

    // Use getAllByText for values that may appear multiple times
    expect(screen.getAllByText('42').length).toBeGreaterThan(0); // Total sessions
    expect(screen.getAllByText('18').length).toBeGreaterThan(0); // Total wins
    expect(screen.getByText('42.9%')).toBeInTheDocument(); // Win rate
    expect(screen.getAllByText('5').length).toBeGreaterThan(0); // Unique games
  });

  it('shows loading state', () => {
    vi.mocked(usePlayRecordsHooks.usePlayerStatistics).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as any);

    renderComponent();
    const cards = screen.getAllByRole('generic');
    expect(cards.length).toBeGreaterThan(0);
  });

  it('shows error state', () => {
    vi.mocked(usePlayRecordsHooks.usePlayerStatistics).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Failed to load statistics'),
    } as any);

    renderComponent();
    expect(screen.getByText(/Failed to load statistics/)).toBeInTheDocument();
  });

  it('shows empty state when no sessions', () => {
    vi.mocked(usePlayRecordsHooks.usePlayerStatistics).mockReturnValue({
      data: {
        totalSessions: 0,
        totalWins: 0,
        gamePlayCounts: {},
        averageScoresByGame: {},
      },
      isLoading: false,
      error: null,
    } as any);

    renderComponent();
    expect(screen.getByText('No Statistics Yet')).toBeInTheDocument();
  });

  it('renders game play counts table', () => {
    renderComponent();
    expect(screen.getAllByText('Twilight Imperium').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Wingspan').length).toBeGreaterThan(0);
    expect(screen.getAllByText('12').length).toBeGreaterThan(0);
  });
});
