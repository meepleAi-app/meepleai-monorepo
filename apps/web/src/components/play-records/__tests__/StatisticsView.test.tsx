/**
 * StatisticsView integration tests (#2438)
 *
 * Covers the date-range preset filter (aria-pressed state + switch) and the
 * TrendChart mount. Data hooks are mocked (PlayHistory-style harness) so no
 * QueryClient/MSW provider is needed and the assertions are deterministic.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { StatisticsView } from '../StatisticsView';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';

// i18n: return the key path (deterministic, no catalog dependency)
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

// next/navigation: router.push is a no-op spy
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Child bars depend on useSharedGames (React Query) — mock it to an empty map
vi.mock('@/lib/play-records/useSharedGames', () => ({
  useSharedGames: vi.fn(() => ({ data: new Map(), isLoading: false, error: null })),
}));

// Controlled stats hook
const mockUsePlayerStatistics = vi.fn();
vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayerStatistics: (...args: unknown[]) => mockUsePlayerStatistics(...args),
}));

const statsWithTrend: PlayerStatistics = {
  totalSessions: 5,
  totalWins: 3,
  gamePlayCounts: {},
  averageScoresByGame: {},
  mostPlayedGames: [],
  winByGame: [],
  winRateTrend: [
    { month: '2026-04', winRate: 0.5 },
    { month: '2026-05', winRate: 1 },
  ],
};

describe('StatisticsView (#2438)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePlayerStatistics.mockReturnValue({
      data: statsWithTrend,
      isLoading: false,
      error: null,
    });
  });

  it('renders the range filter and the trend section on success', () => {
    render(<StatisticsView />);
    expect(screen.getByTestId('stats-range-filter')).toBeInTheDocument();
    // "all" preset is selected by default
    expect(screen.getByTestId('range-all')).toHaveAttribute('aria-pressed', 'true');
    // TrendChart renders its chart section (fixture has trend data)
    expect(screen.getByTestId('trend-section')).toBeInTheDocument();
  });

  it('switching preset updates aria-pressed', async () => {
    const user = userEvent.setup();
    render(<StatisticsView />);

    const btn30 = screen.getByTestId('range-30d');
    expect(btn30).toHaveAttribute('aria-pressed', 'false');

    await user.click(btn30);

    expect(btn30).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('range-all')).toHaveAttribute('aria-pressed', 'false');
  });

  it('passes a date range to the stats hook when a non-all preset is active', async () => {
    const user = userEvent.setup();
    render(<StatisticsView />);

    // default render: called with undefined (all)
    expect(mockUsePlayerStatistics).toHaveBeenCalledWith(undefined);

    await user.click(screen.getByTestId('range-90d'));

    // after switch: called with a { startDate } range
    const lastCall = mockUsePlayerStatistics.mock.calls.at(-1)?.[0];
    expect(lastCall).toMatchObject({ startDate: expect.any(String) });
  });
});
