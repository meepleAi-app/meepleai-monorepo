/**
 * GamesRecentRail — pure component tests
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { GamesRecentRail } from '../GamesRecentRail';

const labels = {
  sectionTitle: 'Giochi recenti',
  emptyHint: 'Inizia a giocare per vedere qui i tuoi titoli recenti.',
};

describe('GamesRecentRail', () => {
  it('renders skeleton when isLoading', () => {
    render(<GamesRecentRail items={[]} labels={labels} isLoading onSelect={vi.fn()} />);
    expect(screen.getAllByTestId('games-recent-rail-skeleton')).toHaveLength(3);
  });

  it('renders empty hint when no items and not loading', () => {
    render(<GamesRecentRail items={[]} labels={labels} onSelect={vi.fn()} />);
    expect(screen.getByText(labels.emptyHint)).toBeInTheDocument();
  });

  it('renders single card when items has length 1', () => {
    const items = [{ id: 'solo', title: 'Solo Game', kbBadge: 'ready' as const }];
    render(<GamesRecentRail items={items} labels={labels} onSelect={vi.fn()} />);
    const cards = screen.getAllByRole('button', { name: /Solo Game/ });
    expect(cards).toHaveLength(1);
  });

  it('renders 5 cards in order', () => {
    const items = ['a', 'b', 'c', 'd', 'e'].map(id => ({
      id,
      title: `Game ${id}`,
      kbBadge: 'ready' as const,
    }));
    render(<GamesRecentRail items={items} labels={labels} onSelect={vi.fn()} />);
    const cards = screen.getAllByRole('button', { name: /Game [a-e]/ });
    expect(cards).toHaveLength(5);
    expect(cards[0].textContent).toContain('Game a');
    expect(cards[4].textContent).toContain('Game e');
  });

  it('calls onSelect with game id when card clicked', () => {
    const onSelect = vi.fn();
    render(
      <GamesRecentRail
        items={[{ id: 'wingspan', title: 'Wingspan', kbBadge: 'ready' }]}
        labels={labels}
        onSelect={onSelect}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Wingspan/ }));
    expect(onSelect).toHaveBeenCalledWith('wingspan');
  });

  it('section has accessible name from sectionTitle', () => {
    render(<GamesRecentRail items={[]} labels={labels} onSelect={vi.fn()} />);
    expect(screen.getByRole('region', { name: labels.sectionTitle })).toBeInTheDocument();
  });
});
