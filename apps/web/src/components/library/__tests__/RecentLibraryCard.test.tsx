/**
 * RecentLibraryCard Component Tests (Issue #2612)
 * Issue #4858: Updated for MeepleCard migration
 *
 * Verifies MeepleCard props mapping: entity, variant, title,
 * subtitle (relative date), imageUrl, badge (favorite), info button.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { UserLibraryEntry } from '@/lib/api';

import { RecentLibraryCard } from '../RecentLibraryCard';

// Mock MeepleCard to inspect props
const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return (
      <div data-testid="meeple-card">
        <span>{props.title as string}</span>
        <span>{props.subtitle as string}</span>
        {props.badge && <span data-testid="badge">{props.badge as string}</span>}
      </div>
    );
  },
}));

// ============================================================================
// Mock Data
// ============================================================================

const mockGameComplete: UserLibraryEntry = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  userId: 'user-123',
  gameId: 'game-456',
  gameTitle: 'Azul',
  gamePublisher: 'Plan B Games',
  gameYearPublished: 2017,
  gameIconUrl: null,
  gameImageUrl: 'https://example.com/azul.png',
  addedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
  notes: 'Great family game',
  isFavorite: true,
};

const mockGameMinimal: UserLibraryEntry = {
  id: '223e4567-e89b-12d3-a456-426614174001',
  userId: 'user-123',
  gameId: 'game-789',
  gameTitle: 'Mystery Game',
  gamePublisher: null,
  gameYearPublished: null,
  gameIconUrl: null,
  gameImageUrl: null,
  addedAt: new Date().toISOString(),
  notes: null,
  isFavorite: false,
};

const mockGameLongTitle: UserLibraryEntry = {
  ...mockGameComplete,
  id: '323e4567-e89b-12d3-a456-426614174002',
  gameTitle: 'Twilight Imperium: Fourth Edition - Prophecy of Kings Expansion Pack',
  isFavorite: false,
};

describe('RecentLibraryCard', () => {
  beforeEach(() => {
    mockMeepleCard.mockClear();
  });

  describe('MeepleCard Props', () => {
    it('renders with entity="game" and variant="compact"', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          entity: 'game',
          variant: 'compact',
        })
      );
    });

    it('passes game title', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Azul' })
      );
    });

    it('passes imageUrl when available', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ imageUrl: 'https://example.com/azul.png' })
      );
    });

    it('passes undefined imageUrl when null', () => {
      render(<RecentLibraryCard game={mockGameMinimal} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ imageUrl: undefined })
      );
    });

    it('passes info button props for library navigation', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({
          showInfoButton: true,
          infoHref: '/library',
          infoTooltip: 'Gestisci',
        })
      );
    });
  });

  describe('Favorite Badge', () => {
    it('shows "Preferito" badge when isFavorite is true', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ badge: 'Preferito' })
      );
    });

    it('does not show badge when isFavorite is false', () => {
      render(<RecentLibraryCard game={mockGameMinimal} />);

      expect(mockMeepleCard).toHaveBeenCalledWith(
        expect.objectContaining({ badge: undefined })
      );
    });
  });

  describe('Relative Time', () => {
    it('includes "Aggiunto" in subtitle for relative time', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      const calledProps = mockMeepleCard.mock.calls[0][0];
      expect(calledProps.subtitle).toMatch(/^Aggiunto/);
    });

    it('includes Italian relative time suffix', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      const calledProps = mockMeepleCard.mock.calls[0][0];
      expect(calledProps.subtitle).toMatch(/fa$/);
    });
  });

  describe('Test ID and Structure', () => {
    it('has data-testid="recent-library-card" wrapper', () => {
      render(<RecentLibraryCard game={mockGameComplete} />);

      expect(screen.getByTestId('recent-library-card')).toBeInTheDocument();
    });

    it('renders long titles without error', () => {
      expect(() => {
        render(<RecentLibraryCard game={mockGameLongTitle} />);
      }).not.toThrow();
    });
  });

  describe('Edge Cases', () => {
    it('handles future dates gracefully', () => {
      const futureGame: UserLibraryEntry = {
        ...mockGameComplete,
        addedAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
      };

      expect(() => {
        render(<RecentLibraryCard game={futureGame} />);
      }).not.toThrow();
    });

    it('renders minimal data without errors', () => {
      expect(() => {
        render(<RecentLibraryCard game={mockGameMinimal} />);
      }).not.toThrow();

      expect(screen.getByText('Mystery Game')).toBeInTheDocument();
    });
  });
});
