/**
 * PlayerConnectionBar unit tests — Stage 3 cluster (Issue #1113).
 *
 * Verifies the 6-pip layout and isEmpty fallback contract for pips 3-6.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

import { PlayerConnectionBar } from '../PlayerConnectionBar';

const labels = {
  topGames: 'Top giochi',
  sessions: 'Partite',
  gameNights: 'Game Nights',
  agents: 'Agenti usati',
  toolkits: 'Toolkit',
  chats: 'Chat',
};

function makeProfile(overrides: Partial<PlayerProfileFixture> = {}): PlayerProfileFixture {
  return {
    playerId: 'p-test',
    displayName: 'Test Player',
    totalSessions: 23,
    totalWins: 12,
    winRate: 0.52,
    favoriteGameName: 'Azul',
    favoriteAgentName: null,
    achievementCount: 0,
    leaderboardRank: null,
    ...overrides,
  };
}

describe('PlayerConnectionBar', () => {
  it('renders six pips in canonical order', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={5} labels={labels} />);

    expect(screen.getByTestId('connection-pip-game')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-session')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-event')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-agent')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-toolkit')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-chat')).toBeInTheDocument();
  });

  it('renders game pip with real count from gameCount prop', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={5} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-game');
    expect(pip).toHaveAttribute('aria-label', 'Top giochi: 5');
  });

  it('renders session pip with real count from stats.totalSessions', () => {
    render(<PlayerConnectionBar stats={makeProfile({ totalSessions: 23 })} gameCount={5} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-session');
    expect(pip).toHaveAttribute('aria-label', 'Partite: 23');
  });

  it('renders pips 3-6 with empty fallback aria-label (no count suffix)', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={5} labels={labels} />);

    expect(screen.getByTestId('connection-pip-event')).toHaveAttribute('aria-label', 'Game Nights');
    expect(screen.getByTestId('connection-pip-agent')).toHaveAttribute('aria-label', 'Agenti usati');
    expect(screen.getByTestId('connection-pip-toolkit')).toHaveAttribute('aria-label', 'Toolkit');
    expect(screen.getByTestId('connection-pip-chat')).toHaveAttribute('aria-label', 'Chat');
  });

  it('renders the game pip as empty when gameCount is zero', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={0} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-game');
    expect(pip).toHaveAttribute('aria-label', 'Top giochi');
  });

  it('renders the session pip as empty when totalSessions is zero', () => {
    render(<PlayerConnectionBar stats={makeProfile({ totalSessions: 0 })} gameCount={5} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-session');
    expect(pip).toHaveAttribute('aria-label', 'Partite');
  });
});
