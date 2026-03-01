/**
 * Tests for GameInfoStep
 * Issue #4821: Step 3 Info & Save
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      addGame: vi.fn(),
      updatePrivateGame: vi.fn(),
    },
    documents: { getDocumentsByGame: vi.fn().mockResolvedValue([]) },
    pdf: { uploadPdf: vi.fn() },
  },
}));

import { api } from '@/lib/api';
import { GameInfoStep } from '../GameInfoStep';

const mockApi = api as {
  library: {
    addGame: ReturnType<typeof vi.fn>;
    updatePrivateGame: ReturnType<typeof vi.fn>;
  };
};

const mockCatalogGame = {
  gameId: 'game-abc',
  title: 'Catan',
  source: 'catalog' as const,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.3,
  yearPublished: 1995,
  description: 'A trading game',
  imageUrl: 'https://example.com/catan.jpg',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  categories: ['Strategy', 'Negotiation'],
  mechanics: ['Trading', 'Dice Rolling'],
};

const mockBggGame = {
  gameId: 'bgg-456',
  title: 'Wingspan',
  source: 'bgg' as const,
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 70,
  complexityRating: 2.4,
  yearPublished: 2019,
};

const mockCustomGame = {
  gameId: 'private-123',
  title: 'My Custom Game',
  source: 'custom' as const,
  minPlayers: 2,
  maxPlayers: 6,
};

const defaultProps = {
  onSuccess: vi.fn(),
  onAddAnother: vi.fn(),
  onClose: vi.fn(),
};

function initializeWithGame(game: typeof mockCatalogGame | typeof mockCustomGame) {
  useAddGameWizardStore.getState().initialize(
    { type: 'fromGameCard', sharedGameId: game.gameId },
    game,
  );
}

describe('GameInfoStep', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAddGameWizardStore.getState().reset();
  });

  // --- Rendering ---

  it('renders step header and form', () => {
    initializeWithGame(mockCatalogGame);
    render(<GameInfoStep {...defaultProps} />);

    expect(screen.getByTestId('game-info-step')).toBeInTheDocument();
    expect(screen.getByText('Rivedi informazioni')).toBeInTheDocument();
  });

  it('pre-fills form from selected catalog game', () => {
    initializeWithGame(mockCatalogGame);
    render(<GameInfoStep {...defaultProps} />);

    expect(screen.getByTestId('info-title')).toHaveValue('Catan');
    expect(screen.getByTestId('info-min-players')).toHaveValue(3);
    expect(screen.getByTestId('info-max-players')).toHaveValue(4);
    expect(screen.getByTestId('info-time')).toHaveValue(90);
    expect(screen.getByTestId('info-complexity')).toHaveValue(2.3);
    expect(screen.getByTestId('info-year')).toHaveValue(1995);
  });

  it('shows categories and mechanics tags', () => {
    initializeWithGame(mockCatalogGame);
    render(<GameInfoStep {...defaultProps} />);

    expect(screen.getByTestId('categories-tags')).toBeInTheDocument();
    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.getByText('Negotiation')).toBeInTheDocument();

    expect(screen.getByTestId('mechanics-tags')).toBeInTheDocument();
    expect(screen.getByText('Trading')).toBeInTheDocument();
    expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
  });

  it('disables fields for catalog source (read-only)', () => {
    initializeWithGame(mockCatalogGame);
    render(<GameInfoStep {...defaultProps} />);

    expect(screen.getByTestId('info-title')).toBeDisabled();
    expect(screen.getByTestId('info-min-players')).toBeDisabled();
    expect(screen.getByTestId('info-max-players')).toBeDisabled();
  });

  it('disables fields for BGG source (read-only)', () => {
    initializeWithGame(mockBggGame);
    render(<GameInfoStep {...defaultProps} />);

    expect(screen.getByTestId('info-title')).toBeDisabled();
    expect(screen.getByTestId('info-min-players')).toBeDisabled();
    expect(screen.getByTestId('info-max-players')).toBeDisabled();
  });

  it('saves BGG game directly without updatePrivateGame', async () => {
    initializeWithGame(mockBggGame);
    mockApi.library.addGame.mockResolvedValueOnce({ id: 'entry-bgg' });

    render(<GameInfoStep {...defaultProps} />);
    fireEvent.click(screen.getByTestId('save-button'));

    await waitFor(() => {
      expect(mockApi.library.addGame).toHaveBeenCalledWith('bgg-456');
    });

    expect(mockApi.library.updatePrivateGame).not.toHaveBeenCalled();
    expect(screen.getByTestId('success-state')).toBeInTheDocument();
  });

  it('enables fields for custom source', () => {
    initializeWithGame(mockCustomGame);
    render(<GameInfoStep {...defaultProps} />);

    expect(screen.getByTestId('info-title')).not.toBeDisabled();
    expect(screen.getByTestId('info-min-players')).not.toBeDisabled();
  });

  it('shows save button', () => {
    initializeWithGame(mockCatalogGame);
    render(<GameInfoStep {...defaultProps} />);

    expect(screen.getByTestId('save-button')).toBeInTheDocument();
    expect(screen.getByText('Salva in Collezione')).toBeInTheDocument();
  });

  // --- Validation ---

  it('shows validation error for empty title', async () => {
    initializeWithGame(mockCustomGame);
    render(<GameInfoStep {...defaultProps} />);

    // Clear the title
    fireEvent.change(screen.getByTestId('info-title'), { target: { value: '' } });
    fireEvent.click(screen.getByTestId('save-button'));

    await waitFor(() => {
      expect(screen.getByText('Il nome è obbligatorio')).toBeInTheDocument();
    });

    expect(mockApi.library.addGame).not.toHaveBeenCalled();
  });

  it('shows validation error for invalid player count', async () => {
    initializeWithGame(mockCustomGame);
    render(<GameInfoStep {...defaultProps} />);

    // Set min > max
    fireEvent.change(screen.getByTestId('info-min-players'), { target: { value: '5' } });
    fireEvent.change(screen.getByTestId('info-max-players'), { target: { value: '2' } });
    fireEvent.click(screen.getByTestId('save-button'));

    await waitFor(() => {
      expect(screen.getByText('Deve essere ≥ min giocatori')).toBeInTheDocument();
    });
  });

  it('shows validation error for invalid complexity', async () => {
    initializeWithGame(mockCustomGame);
    render(<GameInfoStep {...defaultProps} />);

    fireEvent.change(screen.getByTestId('info-complexity'), { target: { value: '6' } });
    fireEvent.click(screen.getByTestId('save-button'));

    await waitFor(() => {
      expect(screen.getByText('Da 1.0 a 5.0')).toBeInTheDocument();
    });
  });

  // --- Save: Catalog game ---

  it('saves catalog game to library', async () => {
    initializeWithGame(mockCatalogGame);
    mockApi.library.addGame.mockResolvedValueOnce({ id: 'entry-new' });

    render(<GameInfoStep {...defaultProps} />);
    fireEvent.click(screen.getByTestId('save-button'));

    await waitFor(() => {
      expect(mockApi.library.addGame).toHaveBeenCalledWith('game-abc');
    });

    // Should show success state
    expect(screen.getByTestId('success-state')).toBeInTheDocument();
    expect(defaultProps.onSuccess).toHaveBeenCalledWith('entry-new');
  });

  // --- Save: Custom game ---

  it('updates private game then adds to library for custom source', async () => {
    initializeWithGame(mockCustomGame);
    mockApi.library.updatePrivateGame.mockResolvedValueOnce({});
    mockApi.library.addGame.mockResolvedValueOnce({ id: 'entry-custom' });

    render(<GameInfoStep {...defaultProps} />);
    fireEvent.click(screen.getByTestId('save-button'));

    await waitFor(() => {
      expect(mockApi.library.updatePrivateGame).toHaveBeenCalledWith(
        'private-123',
        expect.objectContaining({
          title: 'My Custom Game',
          minPlayers: 2,
          maxPlayers: 6,
        }),
      );
      expect(mockApi.library.addGame).toHaveBeenCalledWith('private-123');
    });

    expect(screen.getByTestId('success-state')).toBeInTheDocument();
  });

  // --- Error handling ---

  it('shows error when save fails', async () => {
    initializeWithGame(mockCatalogGame);
    mockApi.library.addGame.mockRejectedValueOnce(new Error('Quota exceeded'));

    render(<GameInfoStep {...defaultProps} />);
    fireEvent.click(screen.getByTestId('save-button'));

    await waitFor(() => {
      expect(screen.getByTestId('save-error')).toBeInTheDocument();
      expect(screen.getByText('Quota exceeded')).toBeInTheDocument();
    });

    // Should NOT show success state
    expect(screen.queryByTestId('success-state')).not.toBeInTheDocument();
  });

  // --- No game selected ---

  it('renders without crashing when no game selected', () => {
    render(<GameInfoStep {...defaultProps} />);
    expect(screen.getByTestId('game-info-step')).toBeInTheDocument();
  });
});
