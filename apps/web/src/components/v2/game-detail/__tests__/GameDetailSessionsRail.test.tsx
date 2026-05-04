/**
 * Wave C.1 (Issue #581) — GameDetailSessionsRail unit tests.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  GameDetailSessionsRail,
  type GameDetailSessionEntry,
  type GameDetailSessionsRailLabels,
} from '../GameDetailSessionsRail';

const labels: GameDetailSessionsRailLabels = {
  title: 'Partite recenti',
  subtitle: 'Ultime sessioni.',
  viewAll: 'Vedi tutte',
  viewAllAriaLabel: 'Storico partite',
  empty: 'Nessuna partita',
  emptySubtitle: 'Registra una nuova sessione.',
  newSession: '+ Nuova',
  winLabel: 'Vittoria',
  lossLabel: 'Sconfitta',
};

const sessions: ReadonlyArray<GameDetailSessionEntry> = [
  {
    id: 's1',
    playedAt: '2026-04-30T19:00:00.000Z',
    durationFormatted: '1h 28m',
    didWin: true,
    playersLine: 'Marco, Sara, Luca',
  },
  {
    id: 's2',
    playedAt: '2026-04-22T20:30:00.000Z',
    durationFormatted: '1h 12m',
    didWin: false,
    playersLine: 'Sara, Andrea',
  },
  {
    id: 's3',
    playedAt: '2026-04-15T18:00:00.000Z',
    durationFormatted: '42m',
    didWin: null,
    playersLine: 'Marco',
  },
];

describe('GameDetailSessionsRail (Wave C.1)', () => {
  it('renders empty state when no sessions are provided', () => {
    render(
      <GameDetailSessionsRail sessions={[]} viewAllHref="/games/g/sessions" labels={labels} />
    );
    expect(screen.getByText('Nessuna partita')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Storico partite' })).not.toBeInTheDocument();
  });

  it('renders a session card per entry when populated', () => {
    const { container } = render(
      <GameDetailSessionsRail sessions={sessions} viewAllHref="/games/g/sessions" labels={labels} />
    );
    const cards = container.querySelectorAll('[data-slot="game-detail-session-card"]');
    expect(cards).toHaveLength(3);
  });

  it('shows win/loss outcome badges based on didWin', () => {
    const { container } = render(
      <GameDetailSessionsRail sessions={sessions} viewAllHref="/games/g/sessions" labels={labels} />
    );
    const winBadges = container.querySelectorAll('[data-outcome="win"]');
    const lossBadges = container.querySelectorAll('[data-outcome="loss"]');
    expect(winBadges).toHaveLength(1);
    expect(lossBadges).toHaveLength(1);
  });

  it('omits outcome badge when didWin is null', () => {
    render(
      <GameDetailSessionsRail
        sessions={[
          {
            id: 's-null',
            playedAt: '2026-04-15T18:00:00.000Z',
            durationFormatted: '30m',
            didWin: null,
            playersLine: 'Marco',
          },
        ]}
        viewAllHref="/games/g/sessions"
        labels={labels}
      />
    );
    expect(screen.queryByText('Vittoria')).not.toBeInTheDocument();
    expect(screen.queryByText('Sconfitta')).not.toBeInTheDocument();
  });

  it('calls onNewSession when the new-session button is clicked', () => {
    const onNew = vi.fn();
    render(
      <GameDetailSessionsRail
        sessions={sessions}
        viewAllHref="/games/g/sessions"
        labels={labels}
        onNewSession={onNew}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '+ Nuova' }));
    expect(onNew).toHaveBeenCalledTimes(1);
  });
});
