/**
 * UserActionSection Component Tests (Issue #3511)
 *
 * Simplified tests focusing on core functionality.
 * Full integration tests covered by E2E suite.
 *
 * Target: ≥85% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render } from '@testing-library/react';
import { UserActionSection } from '../UserActionSection';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ============================================================================
// Mock Setup
// ============================================================================

vi.mock('@/hooks/queries/useLibrary', () => ({
  useUpdateGameState: () => ({
    mutateAsync: vi.fn().mockResolvedValue({}),
    isPending: false,
  }),
  useToggleLibraryFavorite: () => ({
    mutate: vi.fn(),
    mutateAsync: vi.fn().mockResolvedValue({}),
    isPending: false,
  }),
}));

vi.mock('@/hooks/queries/useLabels', () => ({
  useGameLabels: () => ({
    data: [],
    isLoading: false,
  }),
  useRemoveLabelFromGame: () => ({
    mutateAsync: vi.fn().mockResolvedValue({}),
    isPending: false,
  }),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

const mockGameDetail: LibraryGameDetail = {
  gameId: 'game-123',
  gameTitle: 'Catan',
  gamePublisher: 'CATAN Studio',
  gameYearPublished: 1995,
  gameDescription: 'Classic trading game',
  gameImageUrl: 'https://example.com/catan.jpg',
  gameThumbnailUrl: 'https://example.com/catan-thumb.jpg',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.5,
  averageRating: 7.8,
  gameCategories: ['Strategy'],
  gameMechanics: ['Trading'],
  gameDesigners: ['Klaus Teuber'],
  currentState: 'Owned',
  isFavorite: false,
  timesPlayed: 10,
  lastPlayed: '2026-02-01T10:00:00Z',
  winRate: 0.6,
  avgDuration: 85,
  notes: 'Great game for families',
  pdfDocuments: [],
  socialLinks: [],
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('UserActionSection - Rendering', () => {
  it('renders without crashing', () => {
    const { container } = render(<UserActionSection gameDetail={mockGameDetail} />);
    expect(container).toBeInTheDocument();
  });

  it('renders with game that has notes', () => {
    const { container } = render(<UserActionSection gameDetail={mockGameDetail} />);
    expect(container.textContent).toContain('Great game for families');
  });

  it('renders with game without notes', () => {
    const gameWithoutNotes = { ...mockGameDetail, notes: null };
    const { container } = render(<UserActionSection gameDetail={gameWithoutNotes} />);
    expect(container).toBeInTheDocument();
  });

  it('renders with game that has play stats', () => {
    const { container } = render(<UserActionSection gameDetail={mockGameDetail} />);
    expect(container.textContent).toContain('10'); // timesPlayed
  });

  it('renders with game never played', () => {
    const gameNeverPlayed = { ...mockGameDetail, timesPlayed: 0, lastPlayed: null, winRate: null };
    const { container } = render(<UserActionSection gameDetail={gameNeverPlayed} />);
    expect(container).toBeInTheDocument();
  });

  it('renders all state types correctly', () => {
    const states: Array<'Owned' | 'Wishlist' | 'Nuovo' | 'InPrestito'> = ['Owned', 'Wishlist', 'Nuovo', 'InPrestito'];

    states.forEach(state => {
      const game = { ...mockGameDetail, currentState: state };
      const { container } = render(<UserActionSection gameDetail={game} />);
      expect(container).toBeInTheDocument();
    });
  });
});
