/**
 * LibraryPublicHome — DS-17-10 #2208 smoke tests.
 *
 * DEC-5: manual + smoke test only (no exhaustive unit suite). Text-based
 * assertions resilient to layout iteration.
 *
 * Fixture values for stats are chosen under 1000 to avoid `it-IT`
 * thousand-separator ambiguity in jsdom's toLocaleString implementation.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { LibraryPublicHome } from '../LibraryPublicHome';
import type { CommunityStats } from '../CommunityStatsRow';
import type { FeaturedGame } from '../FeaturedGamesCarousel';

// MeepleCard is a complex composite — mock to keep this test focused on the
// LibraryPublicHome shell (hero / stats / sections / CTA), not nested card
// rendering. Render the title as plain text so `getByText('Wingspan')` works.
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({ title }: { title: string }) => <div data-testid="meeple-card">{title}</div>,
}));

const FEATURED: FeaturedGame[] = [
  { gameId: 'g-1', title: 'Wingspan', publisher: 'Stonemaier', averageRating: 8.1 },
];
const STATS: CommunityStats = {
  totalGames: 100,
  totalPlayers: 200,
  totalSessions: 300,
  totalCommunityContent: 50,
};

describe('LibraryPublicHome — DS-17-10 smoke', () => {
  it('renders the hero headline + sub copy', () => {
    render(<LibraryPublicHome featured={FEATURED} stats={STATS} />);
    expect(
      screen.getByRole('heading', { name: /scopri la community board game/i })
    ).toBeInTheDocument();
  });

  it('renders stats numbers from props', () => {
    render(<LibraryPublicHome featured={FEATURED} stats={STATS} />);
    // Values under 1000 render plain (no separator) under it-IT.
    expect(screen.getByText('100')).toBeInTheDocument();
    expect(screen.getByText('200')).toBeInTheDocument();
    expect(screen.getByText('300')).toBeInTheDocument();
    expect(screen.getByText('50')).toBeInTheDocument();
  });

  it('renders featured games + empty state copy when list is empty', () => {
    const { rerender } = render(<LibraryPublicHome featured={FEATURED} stats={STATS} />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    rerender(<LibraryPublicHome featured={[]} stats={STATS} />);
    expect(screen.getByText(/nessun gioco in evidenza/i)).toBeInTheDocument();
  });
});
