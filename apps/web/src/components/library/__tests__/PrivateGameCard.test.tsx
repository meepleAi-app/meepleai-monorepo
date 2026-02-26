/**
 * PrivateGameCard Tests
 * Issue #4857: MeepleCard migration
 *
 * Verifies that PrivateGameCard renders via MeepleCard with correct props.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';

import { PrivateGameCard } from '../PrivateGameCard';

// Mock MeepleCard to inspect props passed
const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return (
      <div data-testid={props['data-testid'] as string}>
        <span>{props.title as string}</span>
        <span>{props.subtitle as string}</span>
        <span>{props.badge as string}</span>
        {(props.quickActions as Array<{ label: string; onClick: () => void }> | undefined)?.map(
          (action) => (
            <button key={action.label} onClick={action.onClick}>
              {action.label}
            </button>
          )
        )}
      </div>
    );
  },
}));

function createMockGame(overrides?: Partial<PrivateGameDto>): PrivateGameDto {
  return {
    id: 'game-1',
    ownerId: 'user-1',
    source: 'Manual',
    bggId: null,
    title: 'Test Game',
    minPlayers: 2,
    maxPlayers: 4,
    yearPublished: 2024,
    description: 'A test game',
    playingTimeMinutes: 60,
    minAge: 10,
    complexityRating: 2.5,
    imageUrl: 'https://example.com/image.jpg',
    thumbnailUrl: null,
    createdAt: '2026-01-15T10:00:00Z',
    updatedAt: '2026-01-15T10:00:00Z',
    ...overrides,
  };
}

describe('PrivateGameCard', () => {
  beforeEach(() => {
    mockMeepleCard.mockClear();
  });

  it('should render with MeepleCard entity="game" and variant="grid"', () => {
    const game = createMockGame();
    render(<PrivateGameCard game={game} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        entity: 'game',
        variant: 'grid',
        title: 'Test Game',
        badge: 'Private',
      })
    );
  });

  it('should pass correct subtitle with player count, year, and playtime', () => {
    const game = createMockGame({
      minPlayers: 2,
      maxPlayers: 4,
      yearPublished: 2024,
      playingTimeMinutes: 60,
    });
    render(<PrivateGameCard game={game} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        subtitle: '2-4 players \u00b7 (2024) \u00b7 60 min',
      })
    );
  });

  it('should handle missing optional fields in subtitle', () => {
    const game = createMockGame({
      yearPublished: null as unknown as number,
      playingTimeMinutes: null as unknown as number,
    });
    render(<PrivateGameCard game={game} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        subtitle: '2-4 players',
      })
    );
  });

  it('should pass imageUrl when available', () => {
    const game = createMockGame({ imageUrl: 'https://example.com/img.jpg' });
    render(<PrivateGameCard game={game} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        imageUrl: 'https://example.com/img.jpg',
      })
    );
  });

  it('should pass undefined imageUrl when null', () => {
    const game = createMockGame({ imageUrl: null as unknown as string });
    render(<PrivateGameCard game={game} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        imageUrl: undefined,
      })
    );
  });

  it('should pass metadata with player count, playtime, and year', () => {
    const game = createMockGame({
      minPlayers: 1,
      maxPlayers: 6,
      playingTimeMinutes: 90,
      yearPublished: 2023,
    });
    render(<PrivateGameCard game={game} />);

    const calledProps = mockMeepleCard.mock.calls[0][0];
    expect(calledProps.metadata).toHaveLength(3);
    expect(calledProps.metadata[0]).toEqual(expect.objectContaining({ label: '1-6' }));
    expect(calledProps.metadata[1]).toEqual(expect.objectContaining({ label: '90 min' }));
    expect(calledProps.metadata[2]).toEqual(expect.objectContaining({ label: '2023' }));
  });

  it('should pass "Private" badge', () => {
    const game = createMockGame();
    render(<PrivateGameCard game={game} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ badge: 'Private' })
    );
  });

  it('should set correct data-testid', () => {
    const game = createMockGame({ id: 'game-42' });
    render(<PrivateGameCard game={game} />);

    expect(screen.getByTestId('game-card-game-42')).toBeInTheDocument();
  });

  describe('Quick Actions', () => {
    it('should include Edit action when onEdit is provided', async () => {
      const onEdit = vi.fn();
      const game = createMockGame();
      const user = userEvent.setup();

      render(<PrivateGameCard game={game} onEdit={onEdit} />);
      await user.click(screen.getByText('Edit'));

      expect(onEdit).toHaveBeenCalledWith(game);
    });

    it('should include Propose action when onPropose is provided', async () => {
      const onPropose = vi.fn();
      const game = createMockGame();
      const user = userEvent.setup();

      render(<PrivateGameCard game={game} onPropose={onPropose} />);
      await user.click(screen.getByText('Propose'));

      expect(onPropose).toHaveBeenCalledWith(game);
    });

    it('should include Delete action when onDelete is provided', async () => {
      const onDelete = vi.fn();
      const game = createMockGame({ id: 'game-99' });
      const user = userEvent.setup();

      render(<PrivateGameCard game={game} onDelete={onDelete} />);
      await user.click(screen.getByText('Delete'));

      expect(onDelete).toHaveBeenCalledWith('game-99');
    });

    it('should not include quickActions when no handlers provided', () => {
      const game = createMockGame();
      render(<PrivateGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ quickActions: undefined })
      );
    });

    it('should mark Delete action as destructive', () => {
      const game = createMockGame();
      render(<PrivateGameCard game={game} onDelete={vi.fn()} />);

      const calledProps = mockMeepleCard.mock.calls[0][0];
      const deleteAction = calledProps.quickActions?.find(
        (a: { label: string }) => a.label === 'Delete'
      );
      expect(deleteAction).toEqual(expect.objectContaining({ destructive: true }));
    });
  });
});
