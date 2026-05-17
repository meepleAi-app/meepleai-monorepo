import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { DashboardStatsRow } from '@/components/dashboard/DashboardStatsRow';

const baseStats = {
  games: { value: 5, isLoading: false, isError: false, isFetching: false },
  sessions: { value: 2, isLoading: false, isError: false, isFetching: false },
  agents: { value: 1, isLoading: false, isError: false, isFetching: false },
  events: { value: 3, isLoading: false, isError: false, isFetching: false },
};

describe('DashboardStatsRow', () => {
  it('renders 4 stat cards in correct order: game, session, agent, event', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    const links = screen.getAllByRole('link');
    expect(links).toHaveLength(4);
    expect(links[0]).toHaveAttribute('data-entity', 'game');
    expect(links[1]).toHaveAttribute('data-entity', 'session');
    expect(links[2]).toHaveAttribute('data-entity', 'agent');
    expect(links[3]).toHaveAttribute('data-entity', 'event');
  });

  it('wrapper has nav role with aria-label', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    expect(
      screen.getByRole('navigation', { name: 'Statistiche personali' })
    ).toBeInTheDocument();
  });

  it('renders the Italian labels', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('Agenti')).toBeInTheDocument();
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
          agents: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: vi.fn(), sessions: vi.fn(), agents: vi.fn() }}
      />
    );
    expect(screen.getByText(/Connessione instabile/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Riprova tutto/i })).toBeInTheDocument();
  });

  it('"Riprova tutto" calls all error onRetry callbacks', () => {
    const gameRetry = vi.fn();
    const sessionRetry = vi.fn();
    const agentRetry = vi.fn();
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          games: { value: 0, isLoading: false, isError: true, isFetching: false },
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
          agents: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: gameRetry, sessions: sessionRetry, agents: agentRetry }}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Riprova tutto/i }));
    expect(gameRetry).toHaveBeenCalledOnce();
    expect(sessionRetry).toHaveBeenCalledOnce();
    expect(agentRetry).toHaveBeenCalledOnce();
  });
});
