import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { FirstGameStep } from '../FirstGameStep';

const mockGetAll = vi.hoisted(() => vi.fn());
const mockAddGame = vi.hoisted(() => vi.fn());
const mockDeclareOwnership = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: mockGetAll,
    },
    library: {
      addGame: mockAddGame,
      declareOwnership: mockDeclareOwnership,
    },
  },
  ApiError: class ApiError extends Error {
    constructor(message: string) {
      super(message);
      this.name = 'ApiError';
    }
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('FirstGameStep', () => {
  const user = userEvent.setup();
  const onComplete = vi.fn();
  const onSkip = vi.fn();
  const onGameAdded = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders search input', () => {
    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    expect(screen.getByText('Add Your First Game')).toBeInTheDocument();
    expect(screen.getByLabelText(/search games/i)).toBeInTheDocument();
  });

  it('calls onSkip when skip button clicked', async () => {
    vi.useRealTimers();
    const realUser = userEvent.setup();
    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.click(screen.getByTestId('game-skip'));

    expect(onSkip).toHaveBeenCalled();
  });

  it('shows search results after typing', async () => {
    mockGetAll.mockResolvedValueOnce({
      games: [
        { id: 'g1', title: 'Catan', publisher: 'Kosmos', yearPublished: 1995 },
        { id: 'g2', title: 'Catan: Seafarers', publisher: 'Kosmos', yearPublished: 1997 },
      ],
      total: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    vi.useRealTimers();
    const realUser = userEvent.setup();

    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.type(screen.getByLabelText(/search games/i), 'Catan');

    await waitFor(
      () => {
        expect(screen.getByTestId('game-search-results')).toBeInTheDocument();
      },
      { timeout: 2000 }
    );

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
  });

  // #4158: con la ricerca sul server ogni digitazione è una richiesta, e le risposte possono
  // arrivare fuori ordine. Una risposta a una ricerca superata non deve sovrascrivere l'ultima.
  it('keeps the results of the latest search when an older response arrives later', async () => {
    const page = (titles: string[]) => ({
      games: titles.map((title, i) => ({
        id: `id-${title}-${i}`,
        title,
        publisher: null,
        yearPublished: null,
      })),
      total: titles.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    let resolveOlder!: (value: unknown) => void;
    let resolveNewer!: (value: unknown) => void;
    mockGetAll
      .mockImplementationOnce(() => new Promise(r => (resolveOlder = r)))
      .mockImplementationOnce(() => new Promise(r => (resolveNewer = r)));

    vi.useRealTimers();
    const realUser = userEvent.setup();
    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );
    const input = screen.getByLabelText(/search games/i);

    await realUser.type(input, 'Wing');
    await waitFor(() => expect(mockGetAll).toHaveBeenCalledTimes(1), { timeout: 2000 });
    await realUser.type(input, 'span');
    await waitFor(() => expect(mockGetAll).toHaveBeenCalledTimes(2), { timeout: 2000 });
    expect(mockGetAll).toHaveBeenNthCalledWith(1, { search: 'Wing' });
    expect(mockGetAll).toHaveBeenNthCalledWith(2, { search: 'Wingspan' });

    resolveNewer(page(['Wingspan']));
    await waitFor(() => expect(screen.getByText('Wingspan')).toBeInTheDocument());

    resolveOlder(page(['Wingmen']));
    // Lascia che la risposta superata venga elaborata prima di asserire che non ha effetto.
    await new Promise(r => setTimeout(r, 50));

    expect(screen.queryByText('Wingmen')).not.toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  // Dalla review di #4188: una ricerca in volo diventata superata non spegne più l'indicatore,
  // quindi se la query scende sotto i due caratteri nessuno lo spegnerebbe.
  it('stops the searching indicator when the query is cleared during a search', async () => {
    let resolveSearch!: (value: unknown) => void;
    mockGetAll.mockImplementationOnce(() => new Promise(r => (resolveSearch = r)));

    vi.useRealTimers();
    const realUser = userEvent.setup();
    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );
    const input = screen.getByLabelText(/search games/i);

    await realUser.type(input, 'ca');
    await waitFor(() => expect(mockGetAll).toHaveBeenCalledTimes(1), { timeout: 2000 });
    expect(screen.getByTestId('game-search-pending')).toBeInTheDocument();

    await realUser.clear(input);
    await waitFor(() =>
      expect(screen.queryByTestId('game-search-pending')).not.toBeInTheDocument()
    );

    resolveSearch({ games: [], total: 0, page: 1, pageSize: 20, totalPages: 0 });
    await new Promise(r => setTimeout(r, 50));
    expect(screen.queryByTestId('game-search-pending')).not.toBeInTheDocument();
  });

  it('selects a game from search results', async () => {
    mockGetAll.mockResolvedValueOnce({
      games: [{ id: 'g1', title: 'Catan', publisher: 'Kosmos', yearPublished: 1995 }],
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    vi.useRealTimers();
    const realUser = userEvent.setup();

    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.type(screen.getByLabelText(/search games/i), 'Catan');

    await waitFor(
      () => {
        expect(screen.getByTestId('game-search-results')).toBeInTheDocument();
      },
      { timeout: 2000 }
    );

    // Click on the first result
    await realUser.click(screen.getByText('Catan'));

    expect(screen.getByTestId('selected-game')).toBeInTheDocument();
    expect(screen.queryByTestId('game-search-results')).not.toBeInTheDocument();
  });

  it('adds selected game to library', async () => {
    mockGetAll.mockResolvedValueOnce({
      games: [{ id: 'g1', title: 'Catan', publisher: 'Kosmos', yearPublished: 1995 }],
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    mockAddGame.mockResolvedValueOnce({});

    vi.useRealTimers();
    const realUser = userEvent.setup();

    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );

    await realUser.type(screen.getByLabelText(/search games/i), 'Catan');

    await waitFor(
      () => {
        expect(screen.getByTestId('game-search-results')).toBeInTheDocument();
      },
      { timeout: 2000 }
    );

    await realUser.click(screen.getByText('Catan'));
    await realUser.click(screen.getByRole('button', { name: /add to library/i }));

    await waitFor(() => {
      expect(mockAddGame).toHaveBeenCalledWith('g1');
    });

    await waitFor(() => {
      expect(onGameAdded).toHaveBeenCalledWith('g1', 'Catan');
      expect(onComplete).toHaveBeenCalled();
    });
  });

  // #4155: il cancello RAG apre il manuale solo a chi dichiara di possedere il gioco. Nell'onboarding
  // la dichiarazione resta esplicita: la casella è spenta di default e spiega perché serve.
  describe('ownership declaration (#4155)', () => {
    async function selectCatan() {
      mockGetAll.mockResolvedValueOnce({
        games: [{ id: 'g1', title: 'Catan', publisher: 'Kosmos', yearPublished: 1995 }],
        total: 1,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      });
      mockAddGame.mockResolvedValueOnce({});
      vi.useRealTimers();
      const realUser = userEvent.setup();
      renderWithQuery(
        <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
      );
      await realUser.type(screen.getByLabelText(/search games/i), 'Catan');
      await waitFor(() => expect(screen.getByTestId('game-search-results')).toBeInTheDocument(), {
        timeout: 2000,
      });
      await realUser.click(screen.getByText('Catan'));
      return realUser;
    }

    const ownsCopy = () => screen.getByRole('checkbox', { name: 'I own a copy of this game' });

    it('is off by default, explains why, and adding does not declare ownership', async () => {
      const realUser = await selectCatan();

      expect(ownsCopy()).not.toBeChecked();
      expect(ownsCopy()).toHaveAccessibleDescription(/copyright/i);
      await realUser.click(screen.getByRole('button', { name: /add to library/i }));

      await waitFor(() => expect(onComplete).toHaveBeenCalled());
      expect(mockDeclareOwnership).not.toHaveBeenCalled();
    });

    it('when checked, declares ownership after adding the game', async () => {
      mockDeclareOwnership.mockResolvedValueOnce({});
      const realUser = await selectCatan();

      await realUser.click(ownsCopy());
      await realUser.click(screen.getByRole('button', { name: /add to library/i }));

      await waitFor(() => expect(onComplete).toHaveBeenCalled());
      expect(mockDeclareOwnership).toHaveBeenCalledWith('g1');
      expect(mockAddGame.mock.invocationCallOrder[0]).toBeLessThan(
        mockDeclareOwnership.mock.invocationCallOrder[0]
      );
    });

    it('keeps going when the declaration fails: the game is added, and the user is told', async () => {
      mockDeclareOwnership.mockRejectedValueOnce(new Error('boom'));
      const { toast } = await import('sonner');
      const realUser = await selectCatan();

      await realUser.click(ownsCopy());
      await realUser.click(screen.getByRole('button', { name: /add to library/i }));

      await waitFor(() => expect(onComplete).toHaveBeenCalled());
      expect(onGameAdded).toHaveBeenCalledWith('g1', 'Catan');
      expect(toast.error).toHaveBeenCalledWith(
        'Catan is in your library, but the ownership declaration failed: you can declare it from the chat.'
      );
    });
  });
});
