/**
 * GameSourceStep Component Tests
 * Issue #4819: AddGameSheet Step 1 - Game Source
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { GameSourceStep } from '../GameSourceStep';
import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';

// Mock the api module
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: vi.fn(),
    },
    bgg: {
      search: vi.fn(),
      getGameDetails: vi.fn(),
    },
    library: {
      addPrivateGame: vi.fn(),
    },
  },
}));

// Import after mock
import { api } from '@/lib/api';

const mockSearch = vi.mocked(api.sharedGames.search);
const mockBggSearch = vi.mocked(api.bgg.search);
const mockBggDetails = vi.mocked(api.bgg.getGameDetails);
const mockAddPrivateGame = vi.mocked(api.library.addPrivateGame);

describe('GameSourceStep', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    useAddGameWizardStore.getState().reset();
    useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should render search input and heading', () => {
    render(<GameSourceStep />);
    expect(screen.getByText('Scegli Sorgente Gioco')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Cerca un gioco...')).toBeInTheDocument();
  });

  it('should render custom game button', () => {
    render(<GameSourceStep />);
    expect(screen.getByText('Crea gioco personalizzato')).toBeInTheDocument();
  });

  it('should show custom game form when button is clicked', () => {
    render(<GameSourceStep />);
    fireEvent.click(screen.getByText('Crea gioco personalizzato'));
    expect(screen.getByText('Nome gioco *', { selector: 'label' })).toBeInTheDocument();
  });

  it('should hide custom game form when cancel is clicked', () => {
    render(<GameSourceStep />);
    fireEvent.click(screen.getByText('Crea gioco personalizzato'));
    expect(screen.getByText('Nome gioco *', { selector: 'label' })).toBeInTheDocument();

    fireEvent.click(screen.getByText('Annulla'));
    expect(screen.queryByText('Nome gioco *', { selector: 'label' })).not.toBeInTheDocument();
  });

  it('should search catalog on debounced input', async () => {
    mockSearch.mockResolvedValue({
      items: [
        {
          id: 'game-1',
          title: 'Catan',
          yearPublished: 1995,
          thumbnailUrl: 'https://example.com/catan.jpg',
          minPlayers: 3,
          maxPlayers: 4,
          playingTimeMinutes: 90,
          averageRating: 7.2,
          bggId: 13,
        },
      ],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    render(<GameSourceStep />);

    await act(async () => {
      fireEvent.change(screen.getByPlaceholderText('Cerca un gioco...'), {
        target: { value: 'Catan' },
      });
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledWith(
        expect.objectContaining({ searchTerm: 'Catan' })
      );
    });
  });

  it('should show BGG button when no catalog results', async () => {
    mockSearch.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    render(<GameSourceStep />);

    await act(async () => {
      fireEvent.change(screen.getByPlaceholderText('Cerca un gioco...'), {
        target: { value: 'ObscureGame' },
      });
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByText('Cerca su BoardGameGeek')).toBeInTheDocument();
    });
  });

  it('should clear search input', async () => {
    render(<GameSourceStep />);

    fireEvent.change(screen.getByPlaceholderText('Cerca un gioco...'), {
      target: { value: 'Test' },
    });

    const clearButton = screen.getByLabelText('Cancella ricerca');
    fireEvent.click(clearButton);

    expect(screen.getByPlaceholderText('Cerca un gioco...')).toHaveValue('');
  });

  it('should show selected game indicator', () => {
    act(() => {
      useAddGameWizardStore.getState().setSelectedGame({
        gameId: 'game-1',
        title: 'Catan',
        source: 'catalog',
      });
    });

    render(<GameSourceStep />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Selezionato')).toBeInTheDocument();
  });
});
