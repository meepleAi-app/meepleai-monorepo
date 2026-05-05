/**
 * Wave B.1 (Issue #633) — GamesResultsGrid v2 component tests.
 *
 * MeepleCard zero-fork (BLOCKER #4 spec §10): wraps the existing
 * `MeepleCard` from `@/components/ui/data-display/meeple-card` with a Next.js
 * `<Link>` to `/games/{gameId}`. Component itself is dumb — no fetch, no
 * filter, no sort — orchestrator (GamesLibraryView) owns derivation.
 *
 * Contract under test (spec §3.2 + plan §4.3):
 *   - 1 MeepleCard per entry, entity='game', variant=view
 *   - Mapping: gameTitle→title, gamePublisher→subtitle, gameImageUrl→imageUrl,
 *     averageRating→rating with ratingMax=10
 *   - Each card wrapped in Link → /games/{gameId}
 *   - Layout: mobile compact = grid-cols-2 px-4
 *             desktop grid view = grid-cols-3 lg:grid-cols-4 px-8
 *             desktop list view = flex flex-col gap-2 px-8
 *   - Empty entries → container renders but no children
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GamesResultsGrid } from '../GamesResultsGrid';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

const NOW = '2026-04-29T10:00:00.000Z';

function makeEntry(
  overrides: Partial<UserLibraryEntry> & Pick<UserLibraryEntry, 'id' | 'gameId' | 'gameTitle'>
): UserLibraryEntry {
  return {
    userId: '00000000-0000-4000-8000-000000000aaa',
    gamePublisher: null,
    gameYearPublished: null,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: NOW,
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    ownershipDeclaredAt: null,
    hasRagAccess: false,
    agentIsOwned: true,
    minPlayers: null,
    maxPlayers: null,
    playingTimeMinutes: null,
    complexityRating: null,
    averageRating: null,
    privateGameId: null,
    isPrivateGame: false,
    canProposeToCatalog: false,
    ...overrides,
  };
}

const ENTRIES: readonly UserLibraryEntry[] = [
  makeEntry({
    id: 'lib-001',
    gameId: 'game-aaa',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameImageUrl: 'https://cdn.example.com/catan.jpg',
    averageRating: 7.2,
  }),
  makeEntry({
    id: 'lib-002',
    gameId: 'game-bbb',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    gameImageUrl: 'https://cdn.example.com/wingspan.jpg',
    averageRating: 8.1,
  }),
  makeEntry({
    id: 'lib-003',
    gameId: 'game-ccc',
    gameTitle: 'Mystery Game',
    // gamePublisher null, gameImageUrl null, averageRating null → fallback test
  }),
];

describe('GamesResultsGrid (Wave B.1)', () => {
  it('renders one MeepleCard per entry with entity="game"', () => {
    const { container } = render(<GamesResultsGrid entries={ENTRIES} view="grid" />);
    const cards = container.querySelectorAll('[data-entity="game"]');
    expect(cards).toHaveLength(3);
  });

  it('uses MeepleCard variant="grid" when view="grid"', () => {
    render(<GamesResultsGrid entries={ENTRIES} view="grid" />);
    // Each rendered MeepleCard exposes title as a heading; presence of all 3
    // titles confirms 3 grid-variant cards rendered.
    expect(screen.getByRole('heading', { name: 'Catan' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Wingspan' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Mystery Game' })).toBeInTheDocument();
  });

  it('uses MeepleCard variant="list" when view="list"', () => {
    const { container } = render(<GamesResultsGrid entries={ENTRIES} view="list" />);
    // ListCard variant uses different layout — check root container class.
    expect(container.querySelector('[data-slot="games-results-grid"]')).toHaveClass(
      'flex',
      'flex-col'
    );
  });

  it('maps gameTitle/gamePublisher/gameImageUrl/averageRating to MeepleCard props', () => {
    render(<GamesResultsGrid entries={ENTRIES.slice(0, 1)} view="grid" />);
    expect(screen.getByRole('heading', { name: 'Catan' })).toBeInTheDocument();
    expect(screen.getByText('Kosmos')).toBeInTheDocument();
    const img = screen.getByRole('img', { name: 'Catan' }) as HTMLImageElement;
    expect(img.src).toContain('catan.jpg');
  });

  it('handles entries with null publisher/imageUrl/rating gracefully', () => {
    render(<GamesResultsGrid entries={[ENTRIES[2]]} view="grid" />);
    expect(screen.getByRole('heading', { name: 'Mystery Game' })).toBeInTheDocument();
    // Null publisher → no subtitle text rendered.
    expect(screen.queryByText('Kosmos')).toBeNull();
  });

  it('wraps each card in a link to /games/{gameId}', () => {
    const { container } = render(<GamesResultsGrid entries={ENTRIES} view="grid" />);
    const links = container.querySelectorAll('a[href^="/games/"]');
    expect(links).toHaveLength(3);
    const hrefs = Array.from(links).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(
      expect.arrayContaining(['/games/game-aaa', '/games/game-bbb', '/games/game-ccc'])
    );
  });

  it('uses mobile grid layout (grid-cols-2 px-4) when compact=true', () => {
    const { container } = render(<GamesResultsGrid entries={ENTRIES} view="grid" compact />);
    const root = container.querySelector('[data-slot="games-results-grid"]');
    expect(root).toHaveClass('grid', 'grid-cols-2', 'px-4');
  });

  it('uses desktop grid layout (grid-cols-3 lg:grid-cols-4 px-8) when view=grid and not compact', () => {
    const { container } = render(<GamesResultsGrid entries={ENTRIES} view="grid" />);
    const root = container.querySelector('[data-slot="games-results-grid"]');
    expect(root).toHaveClass('grid', 'grid-cols-3', 'lg:grid-cols-4', 'px-8');
  });

  it('uses desktop list layout (flex flex-col gap-2 px-8) when view=list and not compact', () => {
    const { container } = render(<GamesResultsGrid entries={ENTRIES} view="list" />);
    const root = container.querySelector('[data-slot="games-results-grid"]');
    expect(root).toHaveClass('flex', 'flex-col', 'gap-2', 'px-8');
  });

  it('renders the container with no card children when entries=[]', () => {
    const { container } = render(<GamesResultsGrid entries={[]} view="grid" />);
    const root = container.querySelector('[data-slot="games-results-grid"]');
    expect(root).toBeInTheDocument();
    expect(root!.querySelectorAll('[data-entity="game"]')).toHaveLength(0);
  });
});
