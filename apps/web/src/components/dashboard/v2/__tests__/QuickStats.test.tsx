/**
 * QuickStats — Unit Tests
 * Dashboard v2 "Il Tavolo"
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { QuickStats } from '../QuickStats';
import type { QuickStatsData } from '../QuickStats';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const MOCK_STATS: QuickStatsData = {
  totalGames: 42,
  monthlyPlays: 8,
  weeklyPlaytime: '3h 20m',
  favorites: 5,
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('QuickStats', () => {
  it('renders 4 stat cards with correct values', () => {
    render(<QuickStats stats={MOCK_STATS} />);

    const root = screen.getByTestId('quick-stats');
    expect(root).toBeInTheDocument();

    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('3h 20m')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();

    expect(screen.getByText('Giochi')).toBeInTheDocument();
    expect(screen.getByText('Partite / Mese')).toBeInTheDocument();
    expect(screen.getByText('Tempo / Sett.')).toBeInTheDocument();
    expect(screen.getByText('Preferiti')).toBeInTheDocument();

    expect(screen.queryByTestId('stat-skeleton')).not.toBeInTheDocument();
  });

  it('renders 4 skeletons when loading', () => {
    render(<QuickStats stats={null} loading />);

    const skeletons = screen.getAllByTestId('stat-skeleton');
    expect(skeletons).toHaveLength(4);

    skeletons.forEach(el => {
      expect(el).toHaveClass('animate-pulse');
    });

    expect(screen.queryByText('Giochi')).not.toBeInTheDocument();
  });

  it('renders "—" values when error is true', () => {
    render(<QuickStats stats={MOCK_STATS} error />);

    const dashes = screen.getAllByText('—');
    expect(dashes).toHaveLength(4);

    expect(screen.queryByText('42')).not.toBeInTheDocument();
  });

  it('renders "—" values when stats is null', () => {
    render(<QuickStats stats={null} />);

    const dashes = screen.getAllByText('—');
    expect(dashes).toHaveLength(4);
  });
});
