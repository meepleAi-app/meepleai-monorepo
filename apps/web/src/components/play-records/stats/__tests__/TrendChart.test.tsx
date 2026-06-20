import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { TrendChart } from '../TrendChart';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const base: PlayerStatistics = {
  totalSessions: 3,
  totalWins: 2,
  gamePlayCounts: {},
  averageScoresByGame: {},
};

describe('TrendChart', () => {
  it('renders the empty state when winRateTrend is missing/empty', () => {
    render(<TrendChart stats={base} />);
    expect(screen.getByTestId('trend-empty')).toBeInTheDocument();
  });

  it('renders the chart section + sr-only data table when trend has data', () => {
    const stats: PlayerStatistics = {
      ...base,
      winRateTrend: [
        { month: '2026-04', winRate: 0.5 },
        { month: '2026-05', winRate: 1 },
      ],
    };
    render(<TrendChart stats={stats} />);
    expect(screen.getByTestId('trend-section')).toBeInTheDocument();
    // sr-only table mirrors the data points as percentages
    expect(screen.getByText('50%')).toBeInTheDocument();
    expect(screen.getByText('100%')).toBeInTheDocument();
  });
});
