import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { DashboardStatsRow } from '@/components/dashboard/DashboardStatsRow';

const baseStats = {
  games: { value: 5, isLoading: false, isError: false, isFetching: false },
  sessions: { value: 2, isLoading: false, isError: false, isFetching: false },
  events: { value: 3, isLoading: false, isError: false, isFetching: false },
};

describe('DashboardStatsRow', () => {
  // Issue #4138: the agent tile is gone with the /agents section it linked to.
  // With one system-wide agent the count was 1 for everyone — a tile that
  // reported nothing.
  it('renders 3 stat cards in correct order: game, session, event', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    const links = screen.getAllByRole('link');
    expect(links).toHaveLength(3);
    expect(links[0]).toHaveAttribute('data-entity', 'game');
    expect(links[1]).toHaveAttribute('data-entity', 'session');
    expect(links[2]).toHaveAttribute('data-entity', 'event');
    expect(screen.queryByText('Agenti')).not.toBeInTheDocument();
  });

  it('wrapper has nav role with aria-label', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    expect(screen.getByRole('navigation', { name: 'Statistiche personali' })).toBeInTheDocument();
  });

  it('renders the Italian labels', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('Eventi')).toBeInTheDocument();
  });

  it('per-key isError shows "—" only for that card', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ sessions: vi.fn() }}
      />
    );
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('does NOT show "Riprova tutto" banner when only 1 card errored', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ sessions: vi.fn() }}
      />
    );
    expect(screen.queryByRole('button', { name: /Riprova tutto/i })).not.toBeInTheDocument();
  });

  it('does NOT show "Riprova tutto" banner when 2 cards errored', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          games: { value: 0, isLoading: false, isError: true, isFetching: false },
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: vi.fn(), sessions: vi.fn() }}
      />
    );
    expect(screen.queryByText(/Connessione instabile/i)).not.toBeInTheDocument();
  });

  it('shows "Riprova tutto" banner when 3 or more cards errored', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          games: { value: 0, isLoading: false, isError: true, isFetching: false },
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
          events: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: vi.fn(), sessions: vi.fn(), events: vi.fn() }}
      />
    );
    expect(screen.getByText(/Connessione instabile/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Riprova tutto/i })).toBeInTheDocument();
  });

  it('"Riprova tutto" calls all error onRetry callbacks', () => {
    const gameRetry = vi.fn();
    const sessionRetry = vi.fn();
    const eventRetry = vi.fn();
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          games: { value: 0, isLoading: false, isError: true, isFetching: false },
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
          events: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: gameRetry, sessions: sessionRetry, events: eventRetry }}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Riprova tutto/i }));
    expect(gameRetry).toHaveBeenCalledOnce();
    expect(sessionRetry).toHaveBeenCalledOnce();
    expect(eventRetry).toHaveBeenCalledOnce();
  });
});
