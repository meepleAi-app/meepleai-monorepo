import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MostPlayedBar } from '../MostPlayedBar';
import {
  mockPlayerStatisticsEmpty,
  mockPlayerStatisticsFull,
  mockSharedGamesMap,
} from './fixtures';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('@/lib/play-records/useSharedGames', () => ({
  useSharedGames: vi.fn(() => ({
    data: mockSharedGamesMap,
    isLoading: false,
    error: null,
  })),
}));

describe('MostPlayedBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('AC-5.3: renders top 5 games from mostPlayedGames', () => {
    render(<MostPlayedBar stats={mockPlayerStatisticsFull} />);

    // All 5 games should be visible
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Carcassonne')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Splendor')).toBeInTheDocument();
    expect(screen.getByText('Puerto Rico')).toBeInTheDocument();
  });

  it('AC-5.3: shows play counts in correct order (descending)', () => {
    const { container } = render(<MostPlayedBar stats={mockPlayerStatisticsFull} />);

    const counts = container.querySelectorAll('[data-testid="play-count"]');
    const values = Array.from(counts).map(el => parseInt(el.textContent || '0', 10));

    // Should be in descending order: 15, 12, 8, 5, 2
    expect(values).toEqual([15, 12, 8, 5, 2]);
  });

  it('AC-5.3: proportional bars scale to maximum play count', () => {
    const { container } = render(<MostPlayedBar stats={mockPlayerStatisticsFull} />);

    const bars = container.querySelectorAll('[data-testid="play-bar"]');

    // Max play count is 15 (Catan)
    // Catan should be 100%, Carcassonne 80%, Ticket to Ride 53%, etc.
    const catnBarPercent = bars[0]?.getAttribute('data-percent');
    expect(catnBarPercent).toBe('100');

    const carcPercent = bars[1]?.getAttribute('data-percent');
    expect(carcPercent).toBe('80'); // 12/15 * 100
  });

  it('AC-5.5 empty: shows EmptySection with CTA', () => {
    render(<MostPlayedBar stats={mockPlayerStatisticsEmpty} />);

    // Mock i18n returns full key path
    expect(screen.getByText('playRecords.stats.empty.noGames')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /playRecords.stats.cta.newRecord/i })).toHaveAttribute(
      'href',
      '/play-records/new'
    );
  });

  it('a11y: bars have role="progressbar" with aria-valuenow/max', () => {
    const { container } = render(<MostPlayedBar stats={mockPlayerStatisticsFull} />);

    const bars = container.querySelectorAll('[role="progressbar"]');
    expect(bars.length).toBe(5);

    bars.forEach((bar, i) => {
      const expected = mockPlayerStatisticsFull.mostPlayedGames![i].plays;
      expect(bar.getAttribute('aria-valuenow')).toBe(String(expected));
      expect(bar.getAttribute('aria-valuemax')).toBe('15'); // Max from fixture
    });
  });

  it('a11y: game titles have aria-label with rank and play count', () => {
    const { container } = render(<MostPlayedBar stats={mockPlayerStatisticsFull} />);

    const labels = container.querySelectorAll('[data-testid="game-label"]');
    expect(labels[0]).toHaveAttribute('aria-label', expect.stringContaining('1.'));
    expect(labels[0]).toHaveAttribute('aria-label', expect.stringContaining('Catan'));
    expect(labels[0]).toHaveAttribute('aria-label', expect.stringContaining('15'));
  });
});
