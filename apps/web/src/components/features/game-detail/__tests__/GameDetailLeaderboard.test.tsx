/**
 * GameDetailLeaderboard - Unit tests (Issue #1468).
 *
 * Pure presentational leaderboard. Test matrix (Crispin):
 *   T1. Renders one row per entry.
 *   T2. data-slot attributes (card + rows).
 *   T3. Rank: 🏆 for #1, "#N" for the rest.
 *   T4. avgScore null → "—".
 *   T5. Empty state (no entries) → labels.empty, no rows.
 *   T6. maxItems caps rows.
 *   T7. hueFor applied to avatar when provided; neutral otherwise.
 *   T8. DS-15 entity tokens.
 *   T9. className composition.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameDetailLeaderboard } from '../GameDetailLeaderboard';

import type { GameLeaderboardEntry } from '@/lib/api/schemas';

const labels = {
  plays: 'partite',
  avgScore: 'avg',
  wins: 'vittorie',
  empty: 'Nessuna classifica disponibile',
};

function entry(over: Partial<GameLeaderboardEntry> & { playerId: string }): GameLeaderboardEntry {
  return {
    playerId: over.playerId,
    displayName: over.displayName ?? 'Player',
    initials: over.initials ?? 'PL',
    wins: over.wins ?? 0,
    plays: over.plays ?? 0,
    avgScore: over.avgScore ?? null,
    lastPlayedAt: over.lastPlayedAt ?? '2026-05-20T12:00:00.000Z',
  };
}

const threeEntries: ReadonlyArray<GameLeaderboardEntry> = [
  entry({
    playerId: 'p1',
    displayName: 'Marco Rossi',
    initials: 'MR',
    wins: 8,
    plays: 14,
    avgScore: 87.5,
  }),
  entry({
    playerId: 'p2',
    displayName: 'Lucia Bianchi',
    initials: 'LB',
    wins: 5,
    plays: 12,
    avgScore: 80,
  }),
  entry({
    playerId: 'p3',
    displayName: 'Guest One',
    initials: 'GO',
    wins: 2,
    plays: 9,
    avgScore: null,
  }),
];

describe('GameDetailLeaderboard (Issue #1468)', () => {
  // T1
  it('renders one row per entry', () => {
    render(<GameDetailLeaderboard entries={threeEntries} title="Classifica" labels={labels} />);
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    expect(screen.getByText('Marco Rossi')).toBeInTheDocument();
    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  // T2
  it('exposes data-slot for card and rows', () => {
    const { container } = render(
      <GameDetailLeaderboard entries={threeEntries} title="Classifica" labels={labels} />
    );
    expect(container.querySelector('[data-slot="game-detail-leaderboard"]')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="game-detail-leaderboard-row"]')).toHaveLength(3);
  });

  // T3
  it('shows a trophy for rank #1 and #N for the rest', () => {
    render(<GameDetailLeaderboard entries={threeEntries} title="Classifica" labels={labels} />);
    expect(screen.getByText('🏆')).toBeInTheDocument();
    expect(screen.getByText('#2')).toBeInTheDocument();
    expect(screen.getByText('#3')).toBeInTheDocument();
  });

  // T4
  it('renders an em-dash when avgScore is null', () => {
    render(
      <GameDetailLeaderboard
        entries={[
          entry({ playerId: 'p3', displayName: 'No Score', initials: 'NS', avgScore: null }),
        ]}
        title="Classifica"
        labels={labels}
      />
    );
    expect(screen.getByText(/—/)).toBeInTheDocument();
  });

  // T5
  it('renders the empty label and no rows when entries is empty', () => {
    render(<GameDetailLeaderboard entries={[]} title="Classifica" labels={labels} />);
    expect(screen.getByText('Nessuna classifica disponibile')).toBeInTheDocument();
    expect(screen.queryAllByRole('listitem')).toHaveLength(0);
  });

  // T6
  it('caps rows at maxItems', () => {
    render(
      <GameDetailLeaderboard
        entries={threeEntries}
        title="Classifica"
        labels={labels}
        maxItems={2}
      />
    );
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
  });

  // T7
  it('applies an hsl background to the avatar when hueFor is provided', () => {
    const { container } = render(
      <GameDetailLeaderboard
        entries={threeEntries}
        title="Classifica"
        labels={labels}
        hueFor={() => 200}
      />
    );
    const avatar = container.querySelector(
      '[data-slot="game-detail-leaderboard-avatar"]'
    ) as HTMLElement;
    // jsdom normalizes hsl(...) to rgb(...); assert a background is applied (not neutral).
    expect(avatar.style.background).not.toBe('');
    expect(avatar.className).not.toContain('bg-muted');
  });

  it('uses a neutral avatar when hueFor is omitted', () => {
    const { container } = render(
      <GameDetailLeaderboard entries={threeEntries} title="Classifica" labels={labels} />
    );
    const avatar = container.querySelector(
      '[data-slot="game-detail-leaderboard-avatar"]'
    ) as HTMLElement;
    expect(avatar.getAttribute('style') ?? '').not.toContain('hsl(');
    expect(avatar.className).toContain('bg-muted');
  });

  // T8
  it('uses DS-15 entity tokens for wins and rank-1', () => {
    const { container } = render(
      <GameDetailLeaderboard entries={threeEntries} title="Classifica" labels={labels} />
    );
    expect(container.querySelector('.text-entity-player')).toBeInTheDocument();
    expect(container.querySelector('.text-entity-agent')).toBeInTheDocument();
  });

  // T9
  it('composes custom className with base classes', () => {
    const { container } = render(
      <GameDetailLeaderboard
        entries={threeEntries}
        title="Classifica"
        labels={labels}
        className="extra"
      />
    );
    const card = container.querySelector('[data-slot="game-detail-leaderboard"]');
    expect(card).toHaveClass('extra');
    expect(card).toHaveClass('bg-card');
    expect(card).toHaveClass('border-border');
  });

  it('renders title as heading and wins/plays meta', () => {
    render(<GameDetailLeaderboard entries={threeEntries} title="Classifica" labels={labels} />);
    expect(screen.getByRole('heading', { name: 'Classifica' })).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument(); // wins of first player
  });
});
