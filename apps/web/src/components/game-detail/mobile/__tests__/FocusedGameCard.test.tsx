import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { FocusedGameCard } from '../FocusedGameCard';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// Stub MeepleCard — we only care about FocusedGameCard's own behavior
vi.mock('@/components/ui/data-display/meeple-card/MeepleCard', () => ({
  MeepleCard: ({ title }: { title?: string }) => (
    <div data-testid="meeple-card-stub">{title ?? 'no title'}</div>
  ),
}));

const mockGame: LibraryGameDetail = {
  libraryEntryId: 'entry-1',
  userId: 'user-1',
  gameId: 'game-1',
  addedAt: '2025-01-01T00:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: null,
  stateNotes: null,
  isAvailableForPlay: true,
  hasCustomPdf: false,
  hasRagAccess: true,
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: null,
  description: null,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.2,
  timesPlayed: 5,
  lastPlayed: null,
  winRate: null,
  avgDuration: null,
};

describe('FocusedGameCard', () => {
  it('renders MeepleCard with the game title', () => {
    render(<FocusedGameCard game={mockGame} onOpenDrawer={() => {}} />);
    expect(screen.getByTestId('meeple-card-stub')).toHaveTextContent('Catan');
  });

  it('renders a Dettagli button that calls onOpenDrawer', () => {
    const onOpenDrawer = vi.fn();
    render(<FocusedGameCard game={mockGame} onOpenDrawer={onOpenDrawer} />);
    const button = screen.getByTestId('focused-game-details-button');
    expect(button).toHaveTextContent(/dettagli/i);
    fireEvent.click(button);
    expect(onOpenDrawer).toHaveBeenCalledOnce();
  });
});
