/**
 * GamePicker — Unit tests for the 5 canonical states.
 *
 * Issue #2089 — Unified GamePicker shared component.
 *
 * Canonical states tested:
 *   1. loading       — input shows aria-busy + loader visible during search
 *   2. error         — alert on the page, with a retry, when the API fails (replaces silent catch{})
 *   3. empty         — "Nessun risultato" status when search has no matches
 *   4. manual-entry  — allowManualEntry=true surfaces a manual-input + "Usa" button
 *   5. manual-unknown-warning — selecting a manual entry surfaces the AI-not-available alert
 *
 * Plus: source dispatch contract (library | catalog | both) using mocked api.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { useState, type ReactElement } from 'react';
import { toast } from 'sonner';

// Mock the api module BEFORE importing the component
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: vi.fn(),
    },
    sharedGames: {
      search: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

import { api } from '@/lib/api';
import { GamePicker, type GameOption } from '../GamePicker';

const mockGetLibrary = vi.mocked(api.library.getLibrary);
const mockSharedSearch = vi.mocked(api.sharedGames.search);
const mockToastError = vi.mocked(toast.error);

/** #4227: the picker loads through TanStack Query, so it needs a client. */
function renderPicker(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return { client, ...render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>) };
}

function libraryPage(items: ReadonlyArray<Record<string, unknown>>, totalCount = items.length) {
  return {
    items,
    page: 1,
    pageSize: 10,
    totalCount,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  } as never;
}

function catalogPage(items: ReadonlyArray<Record<string, unknown>>) {
  return { items, total: items.length, page: 1, pageSize: 10 } as never;
}

async function type(value: string, testId = 'game-picker-input') {
  fireEvent.change(screen.getByTestId(testId), { target: { value } });
  await act(async () => {
    vi.advanceTimersByTime(350);
  });
}

const sampleLibraryItem = {
  id: 'entry-1',
  userId: 'user-1',
  gameId: 'game-wingspan',
  gameTitle: 'Wingspan',
  gamePublisher: null,
  gameYearPublished: null,
  gameIconUrl: null,
  gameImageUrl: 'https://example.com/wingspan.png',
  addedAt: '2026-01-01T00:00:00.000Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: '2026-01-01T00:00:00.000Z',
  stateNotes: null,
  hasKb: false,
  kbCardCount: 0,
  kbIndexedCount: 0,
  kbProcessingCount: 0,
  agentIsOwned: true,
  minPlayers: null,
  maxPlayers: null,
  playingTimeMinutes: null,
  complexityRating: null,
  averageRating: null,
  ownershipDeclaredAt: null,
  hasRagAccess: false,
  timesPlayed: 0,
  lastPlayed: null,
  coverUrl: null,
} as const;

const sampleCatalogItem = {
  id: 'cat-catan',
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: 'https://example.com/catan.png',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  averageRating: 7.2,
  bggId: 13,
} as const;

describe('GamePicker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // -------------------------------------------------------------------------
  // State 1: Loading
  // -------------------------------------------------------------------------
  it('shows loading state (aria-busy + loader) while a search is in flight', async () => {
    mockGetLibrary.mockImplementation(
      () => new Promise(() => {}) // never resolves — hold loading state
    );

    renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    const input = screen.getByTestId('game-picker-input');
    fireEvent.change(input, { target: { value: 'wing' } });

    // Advance debounce timer (300ms default)
    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    expect(input).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByTestId('game-picker-loader')).toBeInTheDocument();
  });

  // -------------------------------------------------------------------------
  // State 2: Error
  // -------------------------------------------------------------------------
  it('shows the error on the page when the search API rejects (no silent catch)', async () => {
    mockGetLibrary.mockRejectedValue(new Error('Network down'));

    renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    const input = screen.getByTestId('game-picker-input');
    fireEvent.change(input, { target: { value: 'wing' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    expect(await screen.findByTestId('game-picker-error')).toHaveTextContent(
      'Impossibile cercare i giochi: Network down'
    );
    // #4227: one announcement, on the page; the toast repeated it.
    expect(mockToastError).not.toHaveBeenCalled();
  });

  // -------------------------------------------------------------------------
  // State 3: Empty
  // -------------------------------------------------------------------------
  it('shows empty state when results are []', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    const input = screen.getByTestId('game-picker-input');
    fireEvent.change(input, { target: { value: 'nope' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('game-picker-empty')).toBeInTheDocument();
    });
    expect(screen.getByTestId('game-picker-empty')).toHaveTextContent(/nessun risultato/i);
  });

  // -------------------------------------------------------------------------
  // State 4: Manual entry
  // -------------------------------------------------------------------------
  it('renders manual-entry input when allowManualEntry=true', () => {
    renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} allowManualEntry />);

    expect(screen.getByTestId('game-picker-manual-input')).toBeInTheDocument();
    expect(screen.getByTestId('game-picker-manual-confirm')).toBeInTheDocument();
  });

  it('does NOT render manual-entry input when allowManualEntry=false (default)', () => {
    renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    expect(screen.queryByTestId('game-picker-manual-input')).not.toBeInTheDocument();
  });

  it('emits a manual GameOption when the user confirms a manual entry', () => {
    const onChange = vi.fn();
    renderPicker(<GamePicker source="library" value={null} onChange={onChange} allowManualEntry />);

    fireEvent.change(screen.getByTestId('game-picker-manual-input'), {
      target: { value: 'Homebrew Game' },
    });
    fireEvent.click(screen.getByTestId('game-picker-manual-confirm'));

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Homebrew Game',
        manual: true,
      })
    );
  });

  // -------------------------------------------------------------------------
  // State 5: Manual-unknown warning
  // -------------------------------------------------------------------------
  it('shows the manual-unknown warning when the selected value is manual', () => {
    const value: GameOption = { id: 'manual-foo', title: 'Foo', manual: true };

    renderPicker(<GamePicker source="library" value={value} onChange={vi.fn()} />);

    expect(screen.getByTestId('game-picker-manual-warning')).toBeInTheDocument();
    expect(screen.getByTestId('game-picker-manual-warning')).toHaveTextContent(
      /gioco non riconosciuto/i
    );
  });

  it('does NOT show the manual-unknown warning for a non-manual (library/catalog) selection', () => {
    const value: GameOption = { id: 'game-1', title: 'Wingspan' };

    renderPicker(<GamePicker source="library" value={value} onChange={vi.fn()} />);

    expect(screen.queryByTestId('game-picker-manual-warning')).not.toBeInTheDocument();
  });

  // -------------------------------------------------------------------------
  // Source dispatch contract
  // -------------------------------------------------------------------------
  it('dispatches to api.library.getLibrary when source="library"', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [sampleLibraryItem],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'wing' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      // #4227: sorted by title, like every other library picker (the endpoint sorts
      // descending by date unless told otherwise).
      expect(mockGetLibrary).toHaveBeenCalledWith({
        search: 'wing',
        page: 1,
        pageSize: 10,
        sortBy: 'title',
        sortDescending: false,
      });
    });
    expect(mockSharedSearch).not.toHaveBeenCalled();
  });

  it('dispatches to api.sharedGames.search when source="catalog"', async () => {
    mockSharedSearch.mockResolvedValue({
      items: [sampleCatalogItem],
      total: 1,
      page: 1,
      pageSize: 10,
    });

    renderPicker(<GamePicker source="catalog" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'catan' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      // #4227: no `status`. The public endpoint returns published games only and ignores
      // the parameter; the 1 that was sent meant PendingApproval, not Published.
      expect(mockSharedSearch).toHaveBeenCalledWith({
        searchTerm: 'catan',
        page: 1,
        pageSize: 10,
      });
    });
    expect(mockGetLibrary).not.toHaveBeenCalled();
  });

  it('dispatches to BOTH library AND catalog when source="both"', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [sampleLibraryItem],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });
    mockSharedSearch.mockResolvedValue({
      items: [sampleCatalogItem],
      total: 1,
      page: 1,
      pageSize: 10,
    });

    renderPicker(<GamePicker source="both" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'game' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(mockGetLibrary).toHaveBeenCalled();
      expect(mockSharedSearch).toHaveBeenCalled();
    });
  });

  // -------------------------------------------------------------------------
  // Selection + clear
  // -------------------------------------------------------------------------
  it('selects an option via the click handler', async () => {
    mockGetLibrary.mockResolvedValue({
      items: [sampleLibraryItem],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });

    const onChange = vi.fn();
    renderPicker(<GamePicker source="library" value={null} onChange={onChange} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'wing' } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    await waitFor(() => {
      expect(screen.getByTestId('game-picker-result-game-wingspan')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('game-picker-result-game-wingspan'));

    expect(onChange).toHaveBeenCalledWith({
      id: 'game-wingspan',
      title: 'Wingspan',
      imageUrl: 'https://example.com/wingspan.png',
    });
  });

  it('clears the selection when the user clicks the × button', () => {
    const onChange = vi.fn();
    const value: GameOption = { id: 'game-1', title: 'Wingspan' };

    renderPicker(<GamePicker source="library" value={value} onChange={onChange} />);

    fireEvent.click(screen.getByTestId('game-picker-clear'));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('does NOT trigger search for queries shorter than 2 chars', async () => {
    renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

    fireEvent.change(screen.getByTestId('game-picker-input'), { target: { value: 'a' } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    expect(mockGetLibrary).not.toHaveBeenCalled();
  });

  // -------------------------------------------------------------------------
  // #4227 — data layer, library browsing, errors
  // -------------------------------------------------------------------------
  describe('#4227 — dati, libreria a campo vuoto, errori', () => {
    const wingspan = { ...sampleLibraryItem };
    const azul = { ...sampleLibraryItem, id: 'entry-2', gameId: 'game-azul', gameTitle: 'Azul' };

    it('catalogo: la copertina viene da coverUrl, non da thumbnailUrl (#2123)', async () => {
      mockSharedSearch.mockResolvedValue(
        catalogPage([{ ...sampleCatalogItem, coverUrl: 'https://cdn.example.com/catan-cover.png' }])
      );
      const onChange = vi.fn();
      renderPicker(<GamePicker source="catalog" value={null} onChange={onChange} />);

      await type('catan');
      fireEvent.click(await screen.findByTestId('game-picker-result-cat-catan'));

      expect(onChange).toHaveBeenCalledWith(
        expect.objectContaining({ imageUrl: 'https://cdn.example.com/catan-cover.png' })
      );
    });

    it('senza showLibraryWhenEmpty il campo vuoto non interroga il server', async () => {
      renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);
      await act(async () => {
        vi.advanceTimersByTime(500);
      });
      expect(mockGetLibrary).not.toHaveBeenCalled();
    });

    it('con showLibraryWhenEmpty mostra la libreria a campo vuoto, senza testo di ricerca', async () => {
      mockGetLibrary.mockResolvedValue(libraryPage([azul, wingspan]));
      renderPicker(
        <GamePicker source="library" value={null} onChange={vi.fn()} showLibraryWhenEmpty />
      );

      expect(await screen.findByTestId('game-picker-result-game-azul')).toBeInTheDocument();
      expect(screen.getByTestId('game-picker-result-game-wingspan')).toBeInTheDocument();
      expect(mockGetLibrary).toHaveBeenCalledWith(
        expect.objectContaining({ search: undefined, sortBy: 'title', sortDescending: false })
      );
    });

    it('con showLibraryWhenEmpty cerca già dalla prima lettera', async () => {
      mockGetLibrary.mockResolvedValue(libraryPage([azul]));
      renderPicker(
        <GamePicker source="library" value={null} onChange={vi.fn()} showLibraryWhenEmpty />
      );
      await screen.findByTestId('game-picker-result-game-azul');

      await type('a');

      await waitFor(() =>
        expect(mockGetLibrary).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'a' }))
      );
    });

    it('showLibraryWhenEmpty non riguarda il catalogo: a campo vuoto non lo interroga', async () => {
      renderPicker(
        <GamePicker source="catalog" value={null} onChange={vi.fn()} showLibraryWhenEmpty />
      );
      await act(async () => {
        vi.advanceTimersByTime(500);
      });
      expect(mockSharedSearch).not.toHaveBeenCalled();
    });

    it('libreria vuota a campo vuoto: lo dice, invece di non mostrare nulla', async () => {
      mockGetLibrary.mockResolvedValue(libraryPage([]));
      renderPicker(
        <GamePicker source="library" value={null} onChange={vi.fn()} showLibraryWhenEmpty />
      );

      expect(await screen.findByTestId('game-picker-empty')).toHaveTextContent(/libreria è vuota/i);
    });

    it('se la libreria ha più giochi di quelli mostrati, invita a restringere', async () => {
      mockGetLibrary.mockResolvedValue(libraryPage([azul, wingspan], 42));
      renderPicker(
        <GamePicker source="library" value={null} onChange={vi.fn()} showLibraryWhenEmpty />
      );

      expect(await screen.findByTestId('game-picker-more')).toHaveTextContent(/2 di 42/);
    });

    it('una risposta arrivata in ritardo per un testo vecchio non sostituisce quella nuova', async () => {
      let resolveOld: (value: never) => void = () => {};
      mockGetLibrary.mockImplementation(params => {
        if (params?.search === 'wi') {
          return new Promise(resolve => {
            resolveOld = resolve as (value: never) => void;
          });
        }
        return Promise.resolve(libraryPage([azul]));
      });
      renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

      await type('wi');
      await type('az');
      await screen.findByTestId('game-picker-result-game-azul');
      await act(async () => {
        resolveOld(libraryPage([wingspan]));
      });

      expect(screen.getByTestId('game-picker-result-game-azul')).toBeInTheDocument();
      expect(screen.queryByTestId('game-picker-result-game-wingspan')).not.toBeInTheDocument();
    });

    it('un errore resta in pagina, e «Riprova» rifà la ricerca', async () => {
      mockGetLibrary.mockRejectedValueOnce(new Error('Network down'));
      mockGetLibrary.mockResolvedValue(libraryPage([wingspan]));
      renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

      await type('wing');
      const alert = await screen.findByTestId('game-picker-error');
      expect(alert).toHaveTextContent(/Network down/);

      fireEvent.click(screen.getByTestId('game-picker-retry'));

      expect(await screen.findByTestId('game-picker-result-game-wingspan')).toBeInTheDocument();
      expect(screen.queryByTestId('game-picker-error')).not.toBeInTheDocument();
    });

    it('both: se una fonte fallisce mostra i risultati della seconda e dice che sono parziali', async () => {
      mockGetLibrary.mockRejectedValue(new Error('library down'));
      mockSharedSearch.mockResolvedValue(catalogPage([sampleCatalogItem]));
      renderPicker(<GamePicker source="both" value={null} onChange={vi.fn()} />);

      await type('cat');

      expect(await screen.findByTestId('game-picker-result-cat-catan')).toBeInTheDocument();
      expect(screen.getByTestId('game-picker-partial')).toBeInTheDocument();
      expect(screen.queryByTestId('game-picker-error')).not.toBeInTheDocument();
    });

    it('una copertina su un host BGG non viene mai richiesta: resta il segnaposto (#2123)', async () => {
      mockGetLibrary.mockResolvedValue(
        libraryPage([{ ...wingspan, gameImageUrl: 'https://cf.geekdo-images.com/wingspan.png' }])
      );
      renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

      await type('wing');
      const row = await screen.findByTestId('game-picker-result-game-wingspan');

      expect(row.querySelector('img')).toBeNull();
    });

    it('una copertina che non si carica lascia il posto al segnaposto, non a un\u2019immagine rotta', async () => {
      mockGetLibrary.mockResolvedValue(libraryPage([wingspan]));
      renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);

      await type('wing');
      const row = await screen.findByTestId('game-picker-result-game-wingspan');
      const img = row.querySelector('img');
      expect(img).not.toBeNull();

      fireEvent.error(img as HTMLImageElement);

      expect(row.querySelector('img')).toBeNull();
    });

    it('anche la scheda del gioco scelto non richiede una copertina BGG', () => {
      renderPicker(
        <GamePicker
          source="library"
          value={{ id: 'g1', title: 'Wingspan', imageUrl: 'https://cf.geekdo-images.com/w.png' }}
          onChange={vi.fn()}
        />
      );

      expect(screen.getByTestId('game-picker-selected').querySelector('img')).toBeNull();
    });
    it('se un aggiornamento in background fallisce, la lista già mostrata resta', async () => {
      mockGetLibrary.mockResolvedValueOnce(libraryPage([wingspan]));
      mockGetLibrary.mockRejectedValue(new Error('rete assente'));
      const { client } = renderPicker(
        <GamePicker source="library" value={null} onChange={vi.fn()} />
      );
      await type('wing');
      await screen.findByTestId('game-picker-result-game-wingspan');

      await act(async () => {
        await client.refetchQueries();
      });

      // La notifica di TanStack arriva un giro dopo: si aspetta l'avviso, poi si guarda la lista.
      expect(await screen.findByTestId('game-picker-error')).toHaveTextContent(/rete assente/);
      expect(screen.getByTestId('game-picker-result-game-wingspan')).toBeInTheDocument();
    });

    it('dopo la «×» non riappare la lista del testo precedente', async () => {
      mockGetLibrary.mockResolvedValue(libraryPage([wingspan]));
      function Controlled() {
        const [value, setValue] = useState<GameOption | null>(null);
        return <GamePicker source="library" value={value} onChange={setValue} />;
      }
      renderPicker(<Controlled />);
      await type('wing');
      fireEvent.click(await screen.findByTestId('game-picker-result-game-wingspan'));
      expect(screen.getByTestId('game-picker-selected')).toBeInTheDocument();

      fireEvent.click(screen.getByTestId('game-picker-clear'));

      // Subito dopo, a campo vuoto e prima che il debounce si assesti.
      expect(screen.getByTestId('game-picker-input')).toHaveValue('');
      expect(screen.queryByTestId('game-picker-result-game-wingspan')).not.toBeInTheDocument();
    });
    it('both: se falliscono entrambe è un errore, non una lista vuota', async () => {
      mockGetLibrary.mockRejectedValue(new Error('library down'));
      mockSharedSearch.mockRejectedValue(new Error('catalog down'));
      renderPicker(<GamePicker source="both" value={null} onChange={vi.fn()} />);

      await type('cat');

      expect(await screen.findByTestId('game-picker-error')).toBeInTheDocument();
      expect(screen.queryByTestId('game-picker-empty')).not.toBeInTheDocument();
    });
  });

  // -------------------------------------------------------------------------
  // #4227 — keyboard and ARIA (combobox pattern)
  // -------------------------------------------------------------------------
  describe('#4227 — tastiera e ARIA da combobox', () => {
    const azul = { ...sampleLibraryItem, id: 'entry-2', gameId: 'game-azul', gameTitle: 'Azul' };
    const brass = { ...sampleLibraryItem, id: 'entry-3', gameId: 'game-brass', gameTitle: 'Brass' };

    async function openWithTwoResults(onChange = vi.fn()) {
      mockGetLibrary.mockResolvedValue(libraryPage([azul, brass]));
      renderPicker(<GamePicker source="library" value={null} onChange={onChange} />);
      await type('as');
      await screen.findByTestId('game-picker-result-game-azul');
      return { input: screen.getByTestId('game-picker-input'), onChange };
    }

    it('il campo è un combobox che controlla la lista, chiuso finché non ci sono risultati', async () => {
      renderPicker(<GamePicker source="library" value={null} onChange={vi.fn()} />);
      const input = screen.getByRole('combobox', { name: 'Cerca un gioco' });

      expect(input).toHaveAttribute('aria-expanded', 'false');
      expect(input).toHaveAttribute('aria-autocomplete', 'list');
      expect(input).not.toHaveAttribute('aria-activedescendant');

      mockGetLibrary.mockResolvedValue(libraryPage([azul]));
      await type('az');
      await screen.findByTestId('game-picker-result-game-azul');

      expect(input).toHaveAttribute('aria-expanded', 'true');
      expect(document.getElementById(input.getAttribute('aria-controls') as string)).toBe(
        screen.getByRole('listbox')
      );
    });

    it('le opzioni non sono nel giro del Tab e stanno in voci di lista senza ruolo proprio', async () => {
      await openWithTwoResults();

      const options = screen.getAllByRole('option');
      expect(options).toHaveLength(2);
      for (const option of options) {
        expect(option).toHaveAttribute('tabindex', '-1');
        expect(option.parentElement).toHaveAttribute('role', 'presentation');
      }
    });

    it('freccia giù e freccia su spostano l’opzione attiva, senza uscire dalla lista', async () => {
      const { input } = await openWithTwoResults();
      const [first, second] = screen.getAllByRole('option');

      fireEvent.keyDown(input, { key: 'ArrowDown' });
      expect(input).toHaveAttribute('aria-activedescendant', first.id);
      expect(first).toHaveAttribute('aria-selected', 'true');
      expect(second).toHaveAttribute('aria-selected', 'false');

      fireEvent.keyDown(input, { key: 'ArrowDown' });
      expect(input).toHaveAttribute('aria-activedescendant', second.id);

      fireEvent.keyDown(input, { key: 'ArrowDown' });
      expect(input).toHaveAttribute('aria-activedescendant', second.id);

      fireEvent.keyDown(input, { key: 'ArrowUp' });
      expect(input).toHaveAttribute('aria-activedescendant', first.id);

      fireEvent.keyDown(input, { key: 'ArrowUp' });
      expect(input).toHaveAttribute('aria-activedescendant', first.id);
    });

    it('freccia su senza opzione attiva parte dall’ultima', async () => {
      const { input } = await openWithTwoResults();
      const options = screen.getAllByRole('option');

      fireEvent.keyDown(input, { key: 'ArrowUp' });

      expect(input).toHaveAttribute('aria-activedescendant', options[1].id);
    });

    it('Invio sceglie l’opzione attiva', async () => {
      const { input, onChange } = await openWithTwoResults();

      fireEvent.keyDown(input, { key: 'ArrowDown' });
      fireEvent.keyDown(input, { key: 'ArrowDown' });
      fireEvent.keyDown(input, { key: 'Enter' });

      expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ id: 'game-brass' }));
    });

    it('Invio senza opzione attiva non sceglie nulla', async () => {
      const { input, onChange } = await openWithTwoResults();

      fireEvent.keyDown(input, { key: 'Enter' });

      expect(onChange).not.toHaveBeenCalled();
    });

    it('Esc toglie l’opzione attiva', async () => {
      const { input } = await openWithTwoResults();
      fireEvent.keyDown(input, { key: 'ArrowDown' });

      fireEvent.keyDown(input, { key: 'Escape' });

      expect(input).not.toHaveAttribute('aria-activedescendant');
      for (const option of screen.getAllByRole('option')) {
        expect(option).toHaveAttribute('aria-selected', 'false');
      }
    });

    it('mentre la lista appartiene a un testo vecchio la tastiera non sceglie', async () => {
      const { input, onChange } = await openWithTwoResults();

      // Nuovo testo, debounce non ancora passato: a schermo c’è ancora la lista di prima.
      fireEvent.change(input, { target: { value: 'asx' } });
      fireEvent.keyDown(input, { key: 'ArrowDown' });
      fireEvent.keyDown(input, { key: 'Enter' });

      expect(input).not.toHaveAttribute('aria-activedescendant');
      expect(onChange).not.toHaveBeenCalled();
    });

    it('quando la lista cambia l’opzione attiva si azzera', async () => {
      const { input } = await openWithTwoResults();
      fireEvent.keyDown(input, { key: 'ArrowDown' });
      expect(input).toHaveAttribute('aria-activedescendant');

      mockGetLibrary.mockResolvedValue(libraryPage([brass]));
      await type('br');
      await waitFor(() =>
        expect(screen.queryByTestId('game-picker-result-game-azul')).not.toBeInTheDocument()
      );

      expect(input).not.toHaveAttribute('aria-activedescendant');
    });
  });
});
