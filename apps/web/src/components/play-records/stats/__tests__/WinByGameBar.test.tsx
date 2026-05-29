import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { WinByGameBar } from '../WinByGameBar';
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

describe('WinByGameBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('AC-5.4: renders all games from winByGame', () => {
    render(<WinByGameBar stats={mockPlayerStatisticsFull} />);

    // All 4 games with stats
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Carcassonne')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Splendor')).toBeInTheDocument();
  });

  it('AC-5.4: orders by win-rate descending', () => {
    const { container } = render(<WinByGameBar stats={mockPlayerStatisticsFull} />);

    // Expected win rates (Math.round half-up): Catan 8/15=53, Carcassonne 6/12=50, TTR 3/8=38, Splendor 2/10=20
    const rates = container.querySelectorAll('[data-testid="win-rate"]');
    const values = Array.from(rates).map(el => parseInt(el.textContent || '0', 10));

    expect(values).toEqual([53, 50, 38, 20]);
  });

  it('AC-5.4: proportional bars scale to max win-rate (100%)', () => {
    const { container } = render(<WinByGameBar stats={mockPlayerStatisticsFull} />);

    const bars = container.querySelectorAll('[data-testid="win-bar"]');

    // Catan 53% = 53% width, Carcassonne 50% = 50% width
    const catnPercent = bars[0]?.getAttribute('data-percent');
    expect(catnPercent).toBe('53');

    const carcPercent = bars[1]?.getAttribute('data-percent');
    expect(carcPercent).toBe('50');
  });

  it('AC-5.4: displays win/played ratio', () => {
    render(<WinByGameBar stats={mockPlayerStatisticsFull} />);

    // Catan: 8/15, Carcassonne: 6/12, etc.
    expect(screen.getByText('8/15')).toBeInTheDocument();
    expect(screen.getByText('6/12')).toBeInTheDocument();
    expect(screen.getByText('3/8')).toBeInTheDocument();
  });

  it('AC-5.5 empty: shows EmptySection with CTA', () => {
    render(<WinByGameBar stats={mockPlayerStatisticsEmpty} />);

    expect(screen.getByText('playRecords.stats.empty.noWins')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /playRecords.stats.cta.newRecord/i })).toHaveAttribute(
      'href',
      '/play-records/new'
    );
  });

  it('a11y: bars have role="progressbar" with aria-valuenow/max', () => {
    const { container } = render(<WinByGameBar stats={mockPlayerStatisticsFull} />);

    const bars = container.querySelectorAll('[role="progressbar"]');
    expect(bars.length).toBe(4);

    bars.forEach((bar, i) => {
      const expectedRate = Math.round(
        (mockPlayerStatisticsFull.winByGame![i].won /
          mockPlayerStatisticsFull.winByGame![i].played) *
          100
      );
      expect(bar.getAttribute('aria-valuenow')).toBe(String(expectedRate));
      expect(bar.getAttribute('aria-valuemax')).toBe('100');
    });
  });

  it('a11y: game labels include win rate percentage', () => {
    const { container } = render(<WinByGameBar stats={mockPlayerStatisticsFull} />);

    const labels = container.querySelectorAll('[data-testid="game-label"]');
    expect(labels[0]?.textContent).toContain('Catan');
    expect(labels[0]?.textContent).toContain('53%');
  });

  it('handles zero wins gracefully (0/X)', () => {
    const statsWithZeroWins = {
      ...mockPlayerStatisticsFull,
      winByGame: [
        { gameId: '550e8400-e29b-41d4-a716-446655440001', gameName: 'Catan', played: 10, won: 0 },
      ],
    };

    render(<WinByGameBar stats={statsWithZeroWins} />);

    expect(screen.getByText('0/10')).toBeInTheDocument();
    expect(screen.getByText('0%')).toBeInTheDocument();
  });
});
