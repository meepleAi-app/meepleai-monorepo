/**
 * PlayerTopGamesCard unit tests — /players/[id] v2 (#1546, #1485 follow-up)
 *
 * Coverage (mirror PlayerLeaderboardCard.test pattern):
 *   S1. Renders items with winRate %
 *   S2. Renders empty state when items is []
 *   S3. Gracefully hides winRate when winCount is null (no "0%")
 *   S4. Caps the list to maxItems (default 5; explicit override respected)
 *   S5. Rank-1 shows trophy 🏆 (decorative aria-hidden); rank-2+ shows "#N"
 *   S6. Passes axe a11y scan in populated and empty states; exposes win-rate
 *       via sr-only span.
 */

import { render, screen, within } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, it, expect } from 'vitest';

import { PlayerTopGamesCard } from '../PlayerTopGamesCard';
import type { PlayerTopGamesCardLabels, TopGameItem } from '../PlayerTopGamesCard';

const labels: PlayerTopGamesCardLabels = {
  title: 'Top giochi',
  playsLabel: '{count} partite',
  playsLabelWithWins: '{count} partite · {wins} vittorie',
  winRateLabel: 'WIN RATE',
  rankAriaLabel: 'Posizione {rank}',
  empty: 'Nessun gioco giocato ancora',
};

const baseItems: ReadonlyArray<TopGameItem> = [
  { gameId: null, gameName: 'Wingspan', playCount: 10, winCount: 6 },
  { gameId: null, gameName: 'Catan', playCount: 8, winCount: 3 },
];

describe('PlayerTopGamesCard', () => {
  it('S1: renders items with win rate %', () => {
    render(<PlayerTopGamesCard items={baseItems} labels={labels} />);

    expect(screen.getByText('Top giochi')).toBeInTheDocument();
    expect(screen.getByText('10 partite · 6 vittorie')).toBeInTheDocument();
    expect(screen.getByText('60%')).toBeInTheDocument();
    expect(screen.getByText('8 partite · 3 vittorie')).toBeInTheDocument();
    // 3/8 → 37.5 → rounded 38%
    expect(screen.getByText('38%')).toBeInTheDocument();
  });

  it('S2: renders empty state when items is []', () => {
    render(<PlayerTopGamesCard items={[]} labels={labels} />);

    const emptyEl = screen.getByText('Nessun gioco giocato ancora');
    expect(emptyEl).toBeInTheDocument();
    expect(emptyEl).toHaveAttribute('role', 'status');
    // Title still rendered even in empty state.
    expect(screen.getByText('Top giochi')).toBeInTheDocument();
  });

  it('S3: gracefully hides win rate when winCount is null (no data)', () => {
    const itemsNoWins: ReadonlyArray<TopGameItem> = [
      { gameId: null, gameName: 'Azul', playCount: 5, winCount: null },
    ];
    const { container } = render(<PlayerTopGamesCard items={itemsNoWins} labels={labels} />);

    // Subtitle uses "X partite" template (no "· wins").
    expect(screen.getByText('5 partite')).toBeInTheDocument();
    // No "wins" suffix from the with-wins template.
    expect(screen.queryByText(/vittorie/)).not.toBeInTheDocument();
    // No win-rate badge in the DOM at all.
    expect(container.querySelector('[data-slot="player-detail-top-games-win-rate"]')).toBeNull();
    // Crucial: never display a misleading "0%" when there is no data.
    expect(screen.queryByText('0%')).not.toBeInTheDocument();
  });

  it('S3b: renders "0%" when winCount === 0 (played but never won — valid datum, NOT "no data")', () => {
    // DEC-9 distinction: winCount === null ⇒ "no data" (badge hidden);
    // winCount === 0 ⇒ valid datum "played 5, won 0" (badge renders "0%").
    const itemsZeroWins: ReadonlyArray<TopGameItem> = [
      { gameId: null, gameName: 'Carcassonne', playCount: 5, winCount: 0 },
    ];
    const { container } = render(<PlayerTopGamesCard items={itemsZeroWins} labels={labels} />);

    // Subtitle uses "X partite · Y vittorie" template (with wins).
    expect(screen.getByText('5 partite · 0 vittorie')).toBeInTheDocument();
    // Win-rate badge present with "0%" value.
    expect(
      container.querySelector('[data-slot="player-detail-top-games-win-rate"]')
    ).not.toBeNull();
    expect(screen.getByText('0%')).toBeInTheDocument();
    // sr-only accessible name still announces the rate.
    expect(screen.getByText('WIN RATE: 0%')).toHaveClass('sr-only');
  });

  it('S4: caps list to maxItems (default 5; explicit override respected)', () => {
    const many: ReadonlyArray<TopGameItem> = Array.from({ length: 10 }, (_, i) => ({
      gameId: null,
      gameName: `Game ${i}`,
      playCount: 100 - i, // already sorted desc
      winCount: 50 - i,
    }));

    const { rerender, container } = render(<PlayerTopGamesCard items={many} labels={labels} />);
    let list = container.querySelector('[data-slot="player-detail-top-games-list"]');
    expect(list).not.toBeNull();
    expect(within(list as HTMLElement).getAllByRole('listitem')).toHaveLength(5);

    rerender(<PlayerTopGamesCard items={many} maxItems={3} labels={labels} />);
    list = container.querySelector('[data-slot="player-detail-top-games-list"]');
    expect(within(list as HTMLElement).getAllByRole('listitem')).toHaveLength(3);

    // Defense-in-depth: items shuffled but card still renders top-N desc.
    const shuffled: ReadonlyArray<TopGameItem> = [...many].reverse();
    rerender(<PlayerTopGamesCard items={shuffled} maxItems={2} labels={labels} />);
    // The highest playCount is 100 (originally Game 0). After sort desc, top is Game 0.
    expect(screen.getByText('Game 0')).toBeInTheDocument();
    expect(screen.getByText('Game 1')).toBeInTheDocument();
    expect(screen.queryByText('Game 2')).not.toBeInTheDocument();
  });

  it('S5: rank-1 shows trophy; rank-2+ shows #N', () => {
    const items: ReadonlyArray<TopGameItem> = [
      { gameId: 'a', gameName: 'A', playCount: 10, winCount: 5 },
      { gameId: 'b', gameName: 'B', playCount: 8, winCount: 4 },
      { gameId: 'c', gameName: 'C', playCount: 6, winCount: 3 },
    ];
    const { container } = render(<PlayerTopGamesCard items={items} labels={labels} />);

    // Rank-1 uses the trophy slot.
    const trophy = container.querySelector('[data-slot="player-detail-top-games-rank-trophy"]');
    expect(trophy).not.toBeNull();
    expect(trophy?.textContent).toBe('🏆');
    expect(trophy).toHaveAttribute('aria-hidden', 'true');

    // Rank-2 and rank-3 use the numeric slot.
    const numericRanks = container.querySelectorAll('[data-slot="player-detail-top-games-rank"]');
    expect(numericRanks).toHaveLength(2);
    expect(numericRanks[0]?.textContent).toBe('#2');
    expect(numericRanks[1]?.textContent).toBe('#3');

    // Accessible names available via sr-only.
    expect(screen.getByText('Posizione 1')).toHaveClass('sr-only');
    expect(screen.getByText('Posizione 2')).toHaveClass('sr-only');
    expect(screen.getByText('Posizione 3')).toHaveClass('sr-only');
  });

  it('S6: passes axe a11y scan in populated and empty states', async () => {
    const populated = render(<PlayerTopGamesCard items={baseItems} labels={labels} />);
    const populatedResults = await axe(populated.container);
    expect(populatedResults).toHaveNoViolations();
    // Win-rate accessible name via sr-only ("WIN RATE: 60%").
    expect(screen.getByText('WIN RATE: 60%')).toHaveClass('sr-only');
    populated.unmount();

    const empty = render(<PlayerTopGamesCard items={[]} labels={labels} />);
    const emptyResults = await axe(empty.container);
    expect(emptyResults).toHaveNoViolations();
  });
});
