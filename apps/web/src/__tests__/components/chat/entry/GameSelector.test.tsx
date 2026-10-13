/**
 * GameSelector — Unit tests
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock api before importing component
const mockGetPrivateGames = vi.fn();
const mockGetLibrary = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getPrivateGames: (...args: unknown[]) => mockGetPrivateGames(...args),
      getLibrary: (...args: unknown[]) => mockGetLibrary(...args),
    },
  },
}));

vi.mock('@/components/ui/data-display/entity-list-view/hooks/use-debounce', () => ({
  useDebounce: (value: string) => value,
}));

import { GameSelector } from '@/components/chat/entry/GameSelector';

/** I filtri lato server: `search` sul titolo e il tetto della pagina, come i due endpoint. */
function servePrivate(all: Array<{ id: string; title: string }>) {
  return (params?: { search?: string; pageSize?: number }) => {
    const size = Math.min(params?.pageSize ?? 12, 50); // PrivateGameEndpoints: Math.Min(pageSize, 50)
    const pool = params?.search
      ? all.filter(g => g.title.toLowerCase().includes(params.search!.toLowerCase()))
      : all;
    return Promise.resolve({
      items: pool.slice(0, size).map(g => ({ ...g, createdAt: '2026-01-01T00:00:00Z' })),
      totalCount: pool.length,
    });
  };
}

function serveLibrary(all: Array<{ gameId: string; gameTitle: string; hasKb?: boolean }>) {
  return (params?: { search?: string; pageSize?: number; hasKb?: boolean }) => {
    const size = Math.min(params?.pageSize ?? 20, 100); // UserLibraryRepository: tetto a 100
    const pool = all
      .filter(
        e => !params?.search || e.gameTitle.toLowerCase().includes(params.search.toLowerCase())
      )
      // #4227: hasKb filtra nella query, come LibraryKbStates.
      .filter(e => params?.hasKb === undefined || (e.hasKb ?? true) === params.hasKb);
    return Promise.resolve({
      items: pool.slice(0, size).map(e => ({
        id: `lib-${e.gameId}`,
        userId: 'u1',
        addedAt: '2026-01-01T00:00:00Z',
        isFavorite: false,
        currentState: 'Owned',
        hasKb: true,
        ...e,
      })),
      totalCount: pool.length,
    });
  };
}

describe('GameSelector', () => {
  const onSelect = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPrivateGames.mockResolvedValue({
      items: [
        { id: 'g1', title: 'Catan', createdAt: '2026-01-01T00:00:00Z' },
        { id: 'g2', title: 'Wingspan', createdAt: '2026-01-02T00:00:00Z' },
      ],
      totalCount: 2,
    });
    mockGetLibrary.mockResolvedValue({
      items: [
        {
          id: 'lib1',
          userId: 'u1',
          gameId: 's1',
          gameTitle: 'Shared Game',
          addedAt: '2026-01-01T00:00:00Z',
          isFavorite: false,
          currentState: 'Owned',
          hasKb: true,
        },
      ],
      totalCount: 1,
    });
  });

  it('renders loading skeleton initially', () => {
    mockGetPrivateGames.mockReturnValue(new Promise(() => {})); // never resolves
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    // Skeleton divs should be rendered while loading
    expect(screen.getByTestId('game-selection-section')).toBeInTheDocument();
  });

  it('renders private games after loading', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });
    expect(screen.getByTestId('game-card-g2')).toBeInTheDocument();
  });

  it('calls onSelect when a game card is clicked', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('game-card-g1'));
    expect(onSelect).toHaveBeenCalledWith('g1');
  });

  it('calls onSelect with empty string for skip-game button', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('skip-game-btn')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('skip-game-btn'));
    expect(onSelect).toHaveBeenCalledWith('');
  });

  it('filters games by search text', async () => {
    mockGetPrivateGames.mockImplementation(
      servePrivate([
        { id: 'g1', title: 'Catan' },
        { id: 'g2', title: 'Wingspan' },
      ])
    );
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });

    const searchInput = screen.getByTestId('game-search-input');
    fireEvent.change(searchInput, { target: { value: 'wing' } });

    await waitFor(() => {
      expect(screen.queryByTestId('game-card-g1')).not.toBeInTheDocument();
    });
    expect(screen.getByTestId('game-card-g2')).toBeInTheDocument();
  });

  it('renders tabs and defaults to private', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('tab-private-games')).toBeInTheDocument();
    });
    expect(screen.getByTestId('tab-private-games')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('tab-shared-games')).toHaveAttribute('aria-selected', 'false');
  });

  it('highlights selected game card', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId="g1" />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });
    expect(screen.getByTestId('game-card-g1')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('game-card-g2')).toHaveAttribute('aria-pressed', 'false');
  });

  it('shows error state on API failure', async () => {
    mockGetPrivateGames.mockRejectedValue(new Error('Network error'));
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  describe('#4187: la ricerca va al server', () => {
    const MANY_PRIVATE = [
      ...Array.from({ length: 60 }, (_, i) => ({ id: `p${i}`, title: `Agricola ${i}` })),
      { id: 'p-wingspan', title: 'Wingspan' },
    ];
    const MANY_LIBRARY = [
      ...Array.from({ length: 120 }, (_, i) => ({ gameId: `s${i}`, gameTitle: `Agricola ${i}` })),
      { gameId: 's-wingspan', gameTitle: 'Wingspan' },
    ];

    beforeEach(() => {
      mockGetPrivateGames.mockImplementation(servePrivate(MANY_PRIVATE));
      mockGetLibrary.mockImplementation(serveLibrary(MANY_LIBRARY));
    });

    it('i miei giochi: «Wingspan», oltre i primi 50, si trova', async () => {
      render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByTestId('game-card-p0');

      fireEvent.change(screen.getByTestId('game-search-input'), { target: { value: 'Wingspan' } });

      expect(await screen.findByTestId('game-card-p-wingspan')).toBeInTheDocument();
      expect(mockGetPrivateGames).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: 'Wingspan' })
      );
    });

    it('i miei giochi: non chiede più di 50, quanti il server ne restituisce', async () => {
      render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByTestId('game-card-p0');

      for (const [params] of mockGetPrivateGames.mock.calls) {
        expect(params.pageSize).toBeLessThanOrEqual(50);
      }
    });

    it('libreria condivisa: «Wingspan», oltre i primi 100, si trova, in ordine di titolo', async () => {
      render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByTestId('game-card-p0');
      fireEvent.click(screen.getByTestId('tab-shared-games'));
      await screen.findByTestId('game-card-s0');

      fireEvent.change(screen.getByTestId('game-search-input'), { target: { value: 'Wingspan' } });

      expect(await screen.findByTestId('game-card-s-wingspan')).toBeInTheDocument();
      expect(mockGetLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: 'Wingspan', sortBy: 'title', sortDescending: false })
      );
      for (const [params] of mockGetLibrary.mock.calls) {
        expect(params.pageSize).toBeLessThanOrEqual(100);
      }
    });

    it('libreria condivisa: i giochi con KB li filtra il server, anche oltre i primi 100 (#4227)', async () => {
      mockGetLibrary.mockImplementation(
        serveLibrary([
          ...Array.from({ length: 120 }, (_, i) => ({
            gameId: `n${i}`,
            gameTitle: `Agricola ${i}`,
            hasKb: false,
          })),
          { gameId: 's-zombicide', gameTitle: 'Zombicide', hasKb: true },
        ])
      );
      render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByTestId('game-card-p0');

      fireEvent.click(screen.getByTestId('tab-shared-games'));

      expect(await screen.findByTestId('game-card-s-zombicide')).toBeInTheDocument();
      expect(mockGetLibrary).toHaveBeenLastCalledWith(expect.objectContaining({ hasKb: true }));
    });

    it('libreria condivisa: se cambia showOnlyWithKb rifà la richiesta, anche a testo invariato (#4227)', async () => {
      mockGetLibrary.mockImplementation(
        serveLibrary([
          { gameId: 's-kb', gameTitle: 'Con KB', hasKb: true },
          { gameId: 's-nokb', gameTitle: 'Senza KB', hasKb: false },
        ])
      );
      const { rerender } = render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByTestId('game-card-p0');
      fireEvent.click(screen.getByTestId('tab-shared-games'));
      await screen.findByTestId('game-card-s-kb');
      expect(screen.queryByTestId('game-card-s-nokb')).not.toBeInTheDocument();

      rerender(<GameSelector onSelect={onSelect} selectedGameId={null} showOnlyWithKb={false} />);

      expect(await screen.findByTestId('game-card-s-nokb')).toBeInTheDocument();
      expect(mockGetLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ hasKb: undefined })
      );
    });

    it("dopo un errore, una ricerca riuscita toglie l'avviso", async () => {
      mockGetPrivateGames.mockRejectedValueOnce(new Error('Network error'));
      render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByRole('alert');

      fireEvent.change(screen.getByTestId('game-search-input'), {
        target: { value: 'Agricola 1' },
      });

      await screen.findByTestId('game-card-p1');
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('scrivendo sulla scheda libreria non interroga i giochi privati', async () => {
      render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByTestId('game-card-p0');
      fireEvent.click(screen.getByTestId('tab-shared-games'));
      await screen.findByTestId('game-card-s0');
      const privateCalls = mockGetPrivateGames.mock.calls.length;

      fireEvent.change(screen.getByTestId('game-search-input'), { target: { value: 'Wingspan' } });
      await screen.findByTestId('game-card-s-wingspan');

      expect(mockGetPrivateGames.mock.calls.length).toBe(privateCalls);
    });

    it('mentre la ricerca carica il campo resta al suo posto, con il testo', async () => {
      render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
      await screen.findByTestId('game-card-p0');

      mockGetPrivateGames.mockImplementation(() => new Promise(() => {}));
      fireEvent.change(screen.getByTestId('game-search-input'), { target: { value: 'Wing' } });

      expect(screen.getByTestId('game-search-input')).toHaveValue('Wing');
    });
  });
});
