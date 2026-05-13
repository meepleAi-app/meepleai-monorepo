import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

import { SessionsTabPanel } from '../SessionsTabPanel';

const labels = {
  title: 'Sessioni recenti',
  viewAll: 'Vedi tutte',
  empty: 'Nessuna partita registrata',
  totalLabel: 'Totale partite',
};

const profile: PlayerProfileFixture = {
  playerId: 'p-test',
  displayName: 'Test Player',
  totalSessions: 23,
  totalWins: 12,
  winRate: 0.52,
  favoriteGameName: 'Azul',
  favoriteAgentName: null,
  achievementCount: 0,
  leaderboardRank: null,
};

describe('SessionsTabPanel', () => {
  it('renders the panel root with data-slot="sessions-tab-panel"', () => {
    const { container } = render(<SessionsTabPanel stats={profile} labels={labels} />);
    expect(container.querySelector('[data-slot="sessions-tab-panel"]')).not.toBeNull();
  });

  it('shows the total sessions count', () => {
    render(<SessionsTabPanel stats={profile} labels={labels} />);
    expect(screen.getByText('23')).toBeInTheDocument();
    expect(screen.getByText(/totale partite/i)).toBeInTheDocument();
  });

  it('renders a CTA link to /players/<id>/sessions', () => {
    render(<SessionsTabPanel stats={profile} labels={labels} />);
    const cta = screen.getByRole('link', { name: /vedi tutte/i });
    expect(cta).toHaveAttribute('href', '/players/p-test/sessions');
  });

  it('shows the empty label when totalSessions is zero', () => {
    render(<SessionsTabPanel stats={{ ...profile, totalSessions: 0 }} labels={labels} />);
    expect(screen.getByText(/nessuna partita registrata/i)).toBeInTheDocument();
  });
});
