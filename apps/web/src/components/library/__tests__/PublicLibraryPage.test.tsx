import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, type Mock } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { useSharedGames, useGameMechanics } from '@/hooks/queries/useSharedGames';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useAddGameToLibrary, useLibrary } from '@/hooks/queries/useLibrary';

import { PublicLibraryPage } from '../PublicLibraryPage';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: mockPush })),
}));

vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: vi.fn(),
  useGameMechanics: vi.fn(),
}));
vi.mock('@/hooks/queries/useCatalogTrending', () => ({
  useCatalogTrending: vi.fn(),
}));
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(),
  useAddGameToLibrary: vi.fn(),
}));

const MOCK_GAMES = [
  {
    id: 'g1',
    title: 'Catan',
    yearPublished: 1995,
    thumbnailUrl: '',
    imageUrl: '',
    averageRating: 7.5,
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
  },
  {
    id: 'g2',
    title: 'Ticket to Ride',
    yearPublished: 0,
    thumbnailUrl: '',
    imageUrl: '',
    averageRating: null,
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 60,
  },
];

const MOCK_CATALOG = { items: MOCK_GAMES, total: 2, page: 1, pageSize: 18 };
const MOCK_LIBRARY = { items: [{ gameId: 'g1' }], totalCount: 1 };
const MOCK_ADD_MUTATE = vi.fn();

const MOCK_MECHANICS = [
  { id: 'm1', slug: 'deck-building', name: 'Deck Building' },
  { id: 'm2', slug: 'cooperative', name: 'Cooperative' },
];

function setupMocks(
  overrides: Partial<{
    catalogError: boolean;
    trendingError: boolean;
    catalogEmpty: boolean;
    withMechanics: boolean;
  }> = {}
) {
  (useSharedGames as Mock).mockReturnValue(
    overrides.catalogError
      ? { data: undefined, isLoading: false, isError: true }
      : overrides.catalogEmpty
        ? { data: { items: [], total: 0, page: 1, pageSize: 18 }, isLoading: false, isError: false }
        : { data: MOCK_CATALOG, isLoading: false, isError: false }
  );
  (useGameMechanics as Mock).mockReturnValue({
    data: overrides.withMechanics ? MOCK_MECHANICS : [],
  });
  (useCatalogTrending as Mock).mockReturnValue(
    overrides.trendingError
      ? { data: undefined, isLoading: false, isError: true }
      : { data: [], isLoading: false, isError: false }
  );
  (useLibrary as Mock).mockReturnValue({ data: MOCK_LIBRARY });
  (useAddGameToLibrary as Mock).mockReturnValue({ mutate: MOCK_ADD_MUTATE });
}

describe('PublicLibraryPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupMocks();
  });

  it('renderizza le card del catalogo con data-entity=game', async () => {
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      const cards = screen.getAllByTestId('catalog-game-card');
      expect(cards.length).toBeGreaterThan(0);
      cards.forEach(card => expect(card).toHaveAttribute('data-entity', 'game'));
    });
  });

  it('mostra "—" per yearPublished = 0', async () => {
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByText('—')).toBeInTheDocument();
    });
  });

  it('mostra il totale giochi nella sezione header', async () => {
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByTestId('catalog-total-count')).toHaveTextContent('2');
    });
  });

  it('mostra error state quando il catalogo fallisce', async () => {
    setupMocks({ catalogError: true });
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByTestId('catalog-error')).toBeInTheDocument();
    });
  });

  it('mostra error state quando il trending fallisce', async () => {
    setupMocks({ trendingError: true });
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByTestId('trending-error')).toBeInTheDocument();
    });
  });

  it('mostra empty state quando il catalogo è vuoto', async () => {
    setupMocks({ catalogEmpty: true });
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByTestId('catalog-empty')).toBeInTheDocument();
    });
  });

  it('useLibrary viene chiamato con pageSize 1000', () => {
    renderWithQuery(<PublicLibraryPage />);
    expect(useLibrary).toHaveBeenCalledWith({ pageSize: 1000 });
  });

  it('naviga al dettaglio gioco al click sulla card', async () => {
    const user = userEvent.setup();
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => screen.getAllByTestId('catalog-game-card'));
    await user.click(screen.getAllByTestId('catalog-game-card')[0]);
    expect(mockPush).toHaveBeenCalledWith('/library/games/g1');
  });

  it('chiama addToLibrary al click su Aggiungi', async () => {
    const user = userEvent.setup();
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => screen.getAllByTestId('catalog-game-card'));
    // g2 non è in libreria — deve avere il pulsante Aggiungi
    const addButtons = screen.getAllByRole('button', { name: /aggiungi/i });
    expect(addButtons.length).toBeGreaterThan(0);
    await user.click(addButtons[0]);
    expect(MOCK_ADD_MUTATE).toHaveBeenCalled();
  });

  describe('Mechanic filter Popover', () => {
    beforeEach(() => setupMocks({ withMechanics: true }));

    it('mostra il bottone Meccaniche quando ci sono meccaniche disponibili', async () => {
      renderWithQuery(<PublicLibraryPage />);
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /filtra per meccanica/i })).toBeInTheDocument();
      });
    });

    it('apre il popover e mostra le checkbox delle meccaniche', async () => {
      const user = userEvent.setup();
      renderWithQuery(<PublicLibraryPage />);
      await waitFor(() => screen.getByRole('button', { name: /filtra per meccanica/i }));
      await user.click(screen.getByRole('button', { name: /filtra per meccanica/i }));
      await waitFor(() => {
        expect(screen.getByText('Deck Building')).toBeInTheDocument();
        expect(screen.getByText('Cooperativo')).toBeInTheDocument();
      });
    });

    it('selezionare una meccanica aggiunge un chip attivo dismissibile', async () => {
      const user = userEvent.setup();
      renderWithQuery(<PublicLibraryPage />);
      await waitFor(() => screen.getByRole('button', { name: /filtra per meccanica/i }));
      await user.click(screen.getByRole('button', { name: /filtra per meccanica/i }));
      await waitFor(() => screen.getByText('Deck Building'));
      const [firstCheckbox] = screen.getAllByRole('checkbox', { hidden: true });
      await user.click(firstCheckbox);
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /rimuovi filtro/i })).toBeInTheDocument();
      });
    });

    it('clicking il chip dismissibile rimuove il filtro', async () => {
      const user = userEvent.setup();
      renderWithQuery(<PublicLibraryPage />);
      await waitFor(() => screen.getByRole('button', { name: /filtra per meccanica/i }));
      // Apri e seleziona
      await user.click(screen.getByRole('button', { name: /filtra per meccanica/i }));
      await waitFor(() => screen.getByText('Deck Building'));
      const [firstCheckbox] = screen.getAllByRole('checkbox', { hidden: true });
      await user.click(firstCheckbox);
      // Verifica chip
      await waitFor(() => screen.getByRole('button', { name: /rimuovi filtro/i }));
      // Rimuovi cliccando il chip
      await user.click(screen.getByRole('button', { name: /rimuovi filtro/i }));
      await waitFor(() => {
        expect(screen.queryByRole('button', { name: /rimuovi filtro/i })).not.toBeInTheDocument();
      });
    });
  });
});
