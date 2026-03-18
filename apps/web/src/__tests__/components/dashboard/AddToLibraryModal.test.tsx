import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import { AddToLibraryModal } from '@/components/dashboard/AddToLibraryModal';

// Mock the api module
const mockAddGame = vi.fn();
const mockCreateThread = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      addGame: (...args: unknown[]) => mockAddGame(...args),
    },
    chat: {
      createThread: (...args: unknown[]) => mockCreateThread(...args),
    },
  },
}));

// Mock useAddGameToLibrary — we bypass the hook and call api directly in component,
// but let's mock the hook as the component uses it
const mockMutateAsync = vi.fn();
const mockMutationState = {
  mutateAsync: mockMutateAsync,
  isPending: false,
};
vi.mock('@/hooks/queries/useLibrary', () => ({
  useAddGameToLibrary: () => mockMutationState,
  libraryKeys: {
    all: ['library'],
    lists: () => ['library', 'list'],
    stats: () => ['library', 'stats'],
    quota: () => ['library', 'quota'],
    gameStatus: (id: string) => ['library', 'status', id],
  },
}));

// Mock @tanstack/react-query for useQueryClient
const mockInvalidateQueries = vi.fn();
vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return {
    ...actual,
    useQueryClient: () => ({
      invalidateQueries: mockInvalidateQueries,
    }),
  };
});

describe('AddToLibraryModal', () => {
  const mockGame = {
    id: 'game-123',
    name: 'Catan',
    imageUrl: '/catan.jpg',
    playerCount: '3-4',
  };

  const mockOnClose = vi.fn();
  const mockOnSuccess = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockMutationState.isPending = false;
  });

  function renderModal(props: Partial<React.ComponentProps<typeof AddToLibraryModal>> = {}) {
    return render(
      <AddToLibraryModal
        game={mockGame}
        isOpen={true}
        onClose={mockOnClose}
        onSuccess={mockOnSuccess}
        {...props}
      />
    );
  }

  // =========================================================================
  // 1. Renders game info and CTA when open
  // =========================================================================
  it('renders game info and CTA when open', () => {
    renderModal();

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('3-4 giocatori')).toBeInTheDocument();
    expect(
      screen.getByText("Per chattare con l'AI su questo gioco, aggiungilo alla tua libreria.")
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /aggiungi e chiedi/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  // =========================================================================
  // 2. Does not render when closed (isOpen=false)
  // =========================================================================
  it('does not render content when isOpen is false', () => {
    renderModal({ isOpen: false });

    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /aggiungi e chiedi/i })).not.toBeInTheDocument();
  });

  // =========================================================================
  // 3. Does not render when game is null
  // =========================================================================
  it('does not render content when game is null', () => {
    renderModal({ game: null });

    expect(screen.queryByRole('button', { name: /aggiungi e chiedi/i })).not.toBeInTheDocument();
  });

  // =========================================================================
  // 4. Orchestrates add + create thread on confirm, calls onSuccess
  // =========================================================================
  it('orchestrates add game + create thread on confirm', async () => {
    const mockLibraryEntry = { gameId: 'game-123', id: 'entry-1' };
    const mockThread = { id: 'thread-456', agentId: 'agent-789', gameId: 'game-123' };

    mockMutateAsync.mockResolvedValueOnce(mockLibraryEntry);
    mockCreateThread.mockResolvedValueOnce(mockThread);

    renderModal();

    fireEvent.click(screen.getByRole('button', { name: /aggiungi e chiedi/i }));

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({ gameId: 'game-123' });
    });

    await waitFor(() => {
      expect(mockCreateThread).toHaveBeenCalledWith({
        gameId: 'game-123',
        title: 'Catan',
      });
    });

    await waitFor(() => {
      expect(mockOnSuccess).toHaveBeenCalledWith({
        gameId: 'game-123',
        threadId: 'thread-456',
        agentId: 'agent-789',
        gameName: 'Catan',
      });
    });
  });

  // =========================================================================
  // 5. Shows error when addGame fails
  // =========================================================================
  it('shows error message when addGame fails', async () => {
    mockMutateAsync.mockRejectedValueOnce(new Error('Quota exceeded'));

    renderModal();

    fireEvent.click(screen.getByRole('button', { name: /aggiungi e chiedi/i }));

    await waitFor(() => {
      expect(screen.getByText(/non è stato possibile aggiungere il gioco/i)).toBeInTheDocument();
    });

    // onSuccess should NOT have been called
    expect(mockOnSuccess).not.toHaveBeenCalled();
    // createThread should NOT have been called
    expect(mockCreateThread).not.toHaveBeenCalled();
  });

  // =========================================================================
  // 6. Shows "Riprova" when createThread fails
  // =========================================================================
  it('shows retry button when createThread fails after addGame succeeds', async () => {
    const mockLibraryEntry = { gameId: 'game-123', id: 'entry-1' };
    mockMutateAsync.mockResolvedValueOnce(mockLibraryEntry);
    mockCreateThread.mockRejectedValueOnce(new Error('Thread creation failed'));

    renderModal();

    fireEvent.click(screen.getByRole('button', { name: /aggiungi e chiedi/i }));

    await waitFor(() => {
      expect(screen.getByText(/gioco aggiunto.*riprova/i)).toBeInTheDocument();
    });

    // Retry button should be visible
    const retryButton = screen.getByRole('button', { name: /riprova/i });
    expect(retryButton).toBeInTheDocument();

    // Now retry succeeds
    const mockThread = { id: 'thread-456', agentId: 'agent-789', gameId: 'game-123' };
    mockCreateThread.mockResolvedValueOnce(mockThread);

    fireEvent.click(retryButton);

    await waitFor(() => {
      expect(mockOnSuccess).toHaveBeenCalledWith({
        gameId: 'game-123',
        threadId: 'thread-456',
        agentId: 'agent-789',
        gameName: 'Catan',
      });
    });
  });

  // =========================================================================
  // 7. Calls onClose on cancel
  // =========================================================================
  it('calls onClose when cancel button is clicked', () => {
    renderModal();

    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));

    expect(mockOnClose).toHaveBeenCalledTimes(1);
  });

  // =========================================================================
  // 8. Button is disabled during loading
  // =========================================================================
  it('disables confirm button during loading', async () => {
    // Make the mutation hang
    mockMutateAsync.mockImplementation(() => new Promise(() => {}));

    renderModal();

    fireEvent.click(screen.getByRole('button', { name: /aggiungi e chiedi/i }));

    await waitFor(() => {
      const button = screen.getByRole('button', { name: /aggiungendo/i });
      expect(button).toBeDisabled();
    });
  });

  // =========================================================================
  // 9. Renders game image when imageUrl is provided
  // =========================================================================
  it('renders game thumbnail when imageUrl is provided', () => {
    renderModal();

    const img = screen.getByRole('img', { name: 'Catan' });
    expect(img).toBeInTheDocument();
  });

  // =========================================================================
  // 10. Shows placeholder when imageUrl is null
  // =========================================================================
  it('shows placeholder when imageUrl is null', () => {
    renderModal({ game: { ...mockGame, imageUrl: null } });

    expect(screen.queryByRole('img', { name: 'Catan' })).not.toBeInTheDocument();
    // Placeholder icon should exist
    expect(screen.getByTestId('game-placeholder-icon')).toBeInTheDocument();
  });
});
