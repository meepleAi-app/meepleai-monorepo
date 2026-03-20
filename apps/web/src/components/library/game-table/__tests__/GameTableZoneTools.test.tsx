/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { GameTableZoneTools } from '../GameTableZoneTools';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, replace: vi.fn(), back: vi.fn(), prefetch: vi.fn() }),
}));

const mockInvalidateQueries = vi.fn();
vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: mockInvalidateQueries }),
}));

vi.mock('@/hooks/queries/useLibrary', async () => {
  const actual = await vi.importActual<typeof import('@/hooks/queries/useLibrary')>(
    '@/hooks/queries/useLibrary'
  );
  return {
    ...actual,
    useUpdateGameState: () => ({ mutateAsync: vi.fn(), isPending: false }),
  };
});

vi.mock('@/hooks/queries/useLabels', () => ({
  useGameLabels: () => ({ data: [], isLoading: false }),
  useRemoveLabelFromGame: () => ({ mutateAsync: vi.fn() }),
}));

vi.mock('@/components/library/EditNotesModal', () => ({
  EditNotesModal: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="edit-notes-modal">EditNotesModal</div> : null,
}));

vi.mock('@/components/library/RemoveGameDialog', () => ({
  RemoveGameDialog: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="remove-game-dialog">RemoveGameDialog</div> : null,
}));

vi.mock('@/components/library/DeclareOwnershipButton', () => ({
  DeclareOwnershipButton: () => <div data-testid="ownership-btn">Ownership</div>,
}));

vi.mock('@/components/library/FavoriteToggle', () => ({
  FavoriteToggle: () => <div data-testid="favorite-toggle">Favorite</div>,
}));

vi.mock('@/components/library/labels', () => ({
  LabelBadge: () => <div data-testid="label-badge">Label</div>,
  LabelSelector: () => <div data-testid="label-selector">Add Label</div>,
}));

vi.mock('@/components/library/RagAccessBadge', () => ({
  RagAccessBadge: () => <div data-testid="rag-badge">RAG</div>,
}));

vi.mock('@/components/ui/data-display/entity-link/related-entities-section', () => ({
  RelatedEntitiesSection: () => <div data-testid="related-entities">Links</div>,
}));

// ============================================================================
// Fixtures
// ============================================================================

const mockGameDetail: LibraryGameDetail = {
  libraryEntryId: 'le-1',
  userId: 'u-1',
  gameId: 'g-1',
  addedAt: '2026-01-01T00:00:00Z',
  notes: 'These are my game notes about strategy and tips for winning.',
  isFavorite: true,
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
  description: 'A classic settlement game',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 60,
  complexityRating: 2.3,
  averageRating: 7.2,
  timesPlayed: 10,
  lastPlayed: '2026-03-15T18:00:00Z',
  winRate: '40%',
  avgDuration: '55 min',
};

// ============================================================================
// Tests
// ============================================================================

describe('GameTableZoneTools', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders toolkit link with correct href', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g-1" />);

    const link = screen.getByTestId('toolkit-link');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/library/games/g-1/toolkit');
    expect(link).toHaveTextContent('Toolkit');
  });

  it('renders notes preview text', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g-1" />);

    const preview = screen.getByTestId('notes-preview');
    expect(preview).toHaveTextContent('These are my game notes');
  });

  it('renders empty notes placeholder when notes are null', () => {
    const detailWithoutNotes = { ...mockGameDetail, notes: null };
    render(<GameTableZoneTools gameDetail={detailWithoutNotes} gameId="g-1" />);

    expect(screen.getByTestId('notes-preview')).toHaveTextContent('Nessuna nota');
  });

  it('truncates long notes', () => {
    const longNotes = 'A'.repeat(200);
    const detailWithLongNotes = { ...mockGameDetail, notes: longNotes };
    render(<GameTableZoneTools gameDetail={detailWithLongNotes} gameId="g-1" />);

    const preview = screen.getByTestId('notes-preview');
    expect(preview.textContent).toContain('...');
    expect(preview.textContent!.length).toBeLessThan(200);
  });

  it('renders ownership button and RAG badge', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g-1" />);

    expect(screen.getByTestId('ownership-btn')).toBeInTheDocument();
    expect(screen.getByTestId('rag-badge')).toBeInTheDocument();
  });

  it('renders related entities section', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g-1" />);

    expect(screen.getByTestId('related-entities')).toBeInTheDocument();
  });

  it('renders remove button', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g-1" />);

    const btn = screen.getByTestId('remove-game-btn');
    expect(btn).toBeInTheDocument();
    expect(btn).toHaveTextContent('Rimuovi dalla libreria');
  });

  it('opens edit notes modal on pencil click', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g-1" />);

    expect(screen.queryByTestId('edit-notes-modal')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('edit-notes-btn'));
    expect(screen.getByTestId('edit-notes-modal')).toBeInTheDocument();
  });

  it('opens remove dialog on remove button click', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g-1" />);

    expect(screen.queryByTestId('remove-game-dialog')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('remove-game-btn'));
    expect(screen.getByTestId('remove-game-dialog')).toBeInTheDocument();
  });
});
