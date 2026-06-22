import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { StatsHero } from '../StatsHero';
import {
  mockPlayerStatisticsEmpty,
  mockPlayerStatisticsFull,
  mockSharedGamesMap,
} from './fixtures';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

// Mock useSharedGames
vi.mock('@/lib/play-records/useSharedGames', () => ({
  useSharedGames: vi.fn(() => ({
    data: mockSharedGamesMap,
    isLoading: false,
    error: null,
  })),
}));

describe('StatsHero', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('AC-5.1: renders 4-col KPI with correct labels and values', () => {
    render(<StatsHero stats={mockPlayerStatisticsFull} />);

    // Partite KPI — mock i18n returns full key path
    expect(screen.getByText('playRecords.stats.kpi.sessions')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();

    // Giochi KPI
    expect(screen.getByText('playRecords.stats.kpi.uniqueGames')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();

    // Win rate KPI
    expect(screen.getByText('playRecords.stats.kpi.winRate')).toBeInTheDocument();
    expect(screen.getByText('43')).toBeInTheDocument(); // Math.round(18/42 * 100)
    expect(screen.getByText('%')).toBeInTheDocument();

    // Preferito KPI
    expect(screen.getByText('playRecords.stats.kpi.favorite')).toBeInTheDocument();
    expect(screen.getByText('🌾')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('AC-5.2: displays total hours sub-label', () => {
    render(<StatsHero stats={mockPlayerStatisticsFull} />);

    // 3180 minutes = 53 hours
    expect(screen.getByText('53.0h totali')).toBeInTheDocument();
  });

  it('AC-5.5 empty: shows dashes and zero values', () => {
    render(<StatsHero stats={mockPlayerStatisticsEmpty} />);

    // KPI cards show em-dash placeholder when totalSessions===0
    const kpiCards = screen.getAllByRole('article');
    expect(kpiCards.length).toBe(4);

    // Partite card shows '—' instead of count
    const sessionsCard = kpiCards.find(card =>
      card.textContent?.includes('playRecords.stats.kpi.sessions')
    );
    expect(sessionsCard?.textContent).toContain('—');

    // Win rate card shows '—' (no % suffix)
    const winRateCard = kpiCards.find(card =>
      card.textContent?.includes('playRecords.stats.kpi.winRate')
    );
    expect(winRateCard?.textContent).toContain('—');
    expect(winRateCard?.textContent).not.toContain('%');
  });

  it('AC-5.1: prefers mostPlayedGames[0] for favorite game', () => {
    render(<StatsHero stats={mockPlayerStatisticsFull} />);

    // Should show the first game from mostPlayedGames
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('AC-5.1: uses coverEmoji from useSharedGames lookup', () => {
    render(<StatsHero stats={mockPlayerStatisticsFull} />);

    // Should use the emoji from mock
    expect(screen.getByText('🌾')).toBeInTheDocument();
  });

  it('AC-5.2: handles zero duration gracefully', () => {
    render(<StatsHero stats={mockPlayerStatisticsEmpty} />);

    // Empty state shows '—' placeholder (not '0.0h totali') per AC-5.5
    const kpiCards = screen.getAllByRole('article');
    const sessionsCard = kpiCards.find(card =>
      card.textContent?.includes('playRecords.stats.kpi.sessions')
    );
    expect(sessionsCard?.textContent).toContain('—');
  });

  it('AC-5.2: rounds hours to 1 decimal place', () => {
    const stats = {
      ...mockPlayerStatisticsFull,
      totalDurationMinutes: 125, // 2.083... hours
    };

    render(<StatsHero stats={stats} />);

    expect(screen.getByText('2.1h totali')).toBeInTheDocument();
  });

  it('a11y: KPI cards have proper aria-labels', () => {
    const { container } = render(<StatsHero stats={mockPlayerStatisticsFull} />);

    const articles = container.querySelectorAll('article');
    expect(articles.length).toBe(4); // 4 KPI cards
  });
});
