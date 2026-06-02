/**
 * PlayerOverviewRegion unit tests — Stage 3 cluster (Issue #1113).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

import { PlayerOverviewRegion } from '../PlayerOverviewRegion';

const labels = {
  stats: {
    sessions: 'Partite',
    wins: 'Vittorie',
    winRate: 'Win rate',
    achievements: 'Achievement',
  },
  leaderboard: {
    title: 'Leaderboard',
    rank: 'Rank #{rank}',
    rankAriaLabel: 'Posizione leaderboard: {rank}',
    noRank: 'Non classificato',
  },
  favoriteAgent: {
    title: 'Agente preferito',
    none: 'Nessun agente',
    ariaLabel: 'Apri {agentName}',
  },
  topGames: {
    title: 'Top giochi',
    playsLabel: '{count} partite',
    playsLabelWithWins: '{count} partite · {wins} vittorie',
    winRateLabel: 'WIN RATE',
    rankAriaLabel: 'Posizione {rank}',
    empty: 'Nessun gioco giocato ancora',
  },
  trend: {
    title: 'Andamento ultimi 6 mesi',
    deltaUp: '↗ +{percent}%',
    deltaDown: '↘ {percent}%',
    deltaFlat: '→ 0%',
    deltaUpAriaLabel: 'Win rate in salita di {percent}%',
    deltaDownAriaLabel: 'Win rate in discesa di {percent}%',
    deltaFlatAriaLabel: 'Win rate invariato',
    empty: 'Non ci sono ancora dati sufficienti per mostrare un trend',
    trendSummaryAriaLabel: 'Andamento win rate da {from}% a {to}% negli ultimi {count} mesi',
    monthsShort: [
      'Gen',
      'Feb',
      'Mar',
      'Apr',
      'Mag',
      'Giu',
      'Lug',
      'Ago',
      'Set',
      'Ott',
      'Nov',
      'Dic',
    ],
  },
} as const;

const profile: PlayerProfileFixture = {
  playerId: 'p-test',
  displayName: 'Test Player',
  totalSessions: 23,
  totalWins: 12,
  winRate: 0.52,
  favoriteGameName: 'Azul',
  favoriteAgentName: 'Carcassonne Coach',
  achievementCount: 3,
  leaderboardRank: 7,
  topGames: [
    { gameId: null, gameName: 'Azul', playCount: 10, winCount: 6 },
    { gameId: null, gameName: 'Catan', playCount: 8, winCount: 3 },
  ],
  trendPoints: [
    { month: '2026-04', winRate: 0.4 },
    { month: '2026-05', winRate: 0.5 },
    { month: '2026-06', winRate: 0.6 },
  ],
};

describe('PlayerOverviewRegion', () => {
  it('renders the stats grid, leaderboard, favorite-agent and top-games regions', () => {
    const { container } = render(
      <PlayerOverviewRegion stats={profile} labels={labels} onFavoriteAgentClick={vi.fn()} />
    );

    expect(container.querySelector('[data-slot="player-detail-stats-grid"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-detail-leaderboard"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-detail-favorite-agent"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-detail-top-games"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-detail-trend"]')).not.toBeNull();
  });

  it('exposes a region root with data-slot="player-overview-region"', () => {
    const { container } = render(
      <PlayerOverviewRegion stats={profile} labels={labels} onFavoriteAgentClick={vi.fn()} />
    );
    expect(container.querySelector('[data-slot="player-overview-region"]')).not.toBeNull();
  });

  it('shows the favorite agent name when present in profile', () => {
    render(<PlayerOverviewRegion stats={profile} labels={labels} onFavoriteAgentClick={vi.fn()} />);
    expect(screen.getByText(/carcassonne coach/i)).toBeInTheDocument();
  });
});
