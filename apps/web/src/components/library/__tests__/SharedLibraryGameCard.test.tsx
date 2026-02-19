/**
 * SharedLibraryGameCard Component Tests (Issue #3026)
 * Issue #4858: Updated for MeepleCard migration
 *
 * Verifies MeepleCard props mapping: entity, variant, title,
 * subtitle (publisher), imageUrl, badge (favorite), metadata (year),
 * notes via previewData.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';

import { SharedLibraryGameCard } from '../SharedLibraryGameCard';
import { createMockSharedLibraryGame } from '@/__tests__/fixtures/common-fixtures';

// Mock MeepleCard to inspect props
const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return <div data-testid="meeple-card">{props.title as string}</div>;
  },
}));

describe('SharedLibraryGameCard', () => {
  beforeEach(() => {
    mockMeepleCard.mockClear();
  });

  describe('MeepleCard Props', () => {
    it('renders with entity="game" and variant="grid"', () => {
      const game = createMockSharedLibraryGame();
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          entity: 'game',
          variant: 'grid',
        })
      );
    });

    it('passes game title', () => {
      const game = createMockSharedLibraryGame({ title: 'Settlers of Catan' });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Settlers of Catan' })
      );
    });

    it('passes publisher as subtitle', () => {
      const game = createMockSharedLibraryGame({ publisher: 'Kosmos Games' });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ subtitle: 'Kosmos Games' })
      );
    });

    it('passes undefined subtitle when no publisher', () => {
      const game = createMockSharedLibraryGame({ publisher: null });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ subtitle: undefined })
      );
    });

    it('passes imageUrl when available', () => {
      const game = createMockSharedLibraryGame({ imageUrl: 'https://example.com/game.jpg' });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ imageUrl: 'https://example.com/game.jpg' })
      );
    });

    it('passes undefined imageUrl when null', () => {
      const game = createMockSharedLibraryGame({ imageUrl: null });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ imageUrl: undefined })
      );
    });
  });

  describe('Year Metadata', () => {
    it('includes year in metadata when available', () => {
      const game = createMockSharedLibraryGame({ yearPublished: 1995 });
      render(<SharedLibraryGameCard game={game} />);

      const calledProps = mockMeepleCard.mock.calls[0][0];
      expect(calledProps.metadata).toEqual(
        expect.arrayContaining([expect.objectContaining({ label: '1995' })])
      );
    });

    it('passes undefined metadata when no year', () => {
      const game = createMockSharedLibraryGame({ yearPublished: null });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ metadata: undefined })
      );
    });
  });

  describe('Favorite Badge', () => {
    it('shows "Preferito" badge when isFavorite is true', () => {
      const game = createMockSharedLibraryGame({ isFavorite: true });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ badge: 'Preferito' })
      );
    });

    it('does not show badge when isFavorite is false', () => {
      const game = createMockSharedLibraryGame({ isFavorite: false });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ badge: undefined })
      );
    });
  });

  describe('Notes Display', () => {
    it('enables preview with notes when showNotes is true and notes exist', () => {
      const game = createMockSharedLibraryGame({ notes: 'Great family game' });
      render(<SharedLibraryGameCard game={game} showNotes={true} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          showPreview: true,
          previewData: { description: 'Great family game' },
        })
      );
    });

    it('does not enable preview when showNotes is false', () => {
      const game = createMockSharedLibraryGame({ notes: 'Great family game' });
      render(<SharedLibraryGameCard game={game} showNotes={false} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          showPreview: false,
          previewData: undefined,
        })
      );
    });

    it('does not enable preview when notes are null', () => {
      const game = createMockSharedLibraryGame({ notes: null });
      render(<SharedLibraryGameCard game={game} showNotes={true} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          showPreview: false,
          previewData: undefined,
        })
      );
    });

    it('defaults showNotes to false', () => {
      const game = createMockSharedLibraryGame({ notes: 'Some notes' });
      render(<SharedLibraryGameCard game={game} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          showPreview: false,
          previewData: undefined,
        })
      );
    });
  });

  describe('Complete Card', () => {
    it('renders all elements for a complete game', () => {
      const game = createMockSharedLibraryGame({
        title: 'Ticket to Ride',
        publisher: 'Days of Wonder',
        yearPublished: 2004,
        imageUrl: 'https://example.com/ttr.jpg',
        isFavorite: true,
        notes: 'Best travel-themed game!',
      });
      render(<SharedLibraryGameCard game={game} showNotes={true} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          entity: 'game',
          variant: 'grid',
          title: 'Ticket to Ride',
          subtitle: 'Days of Wonder',
          imageUrl: 'https://example.com/ttr.jpg',
          badge: 'Preferito',
          showPreview: true,
          previewData: { description: 'Best travel-themed game!' },
        })
      );
    });
  });
});
