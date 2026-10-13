import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { FirstGameStep } from '../FirstGameStep';

const mockSearch = vi.hoisted(() => vi.fn());
const mockAddGame = vi.hoisted(() => vi.fn());
const mockDeclareOwnership = vi.hoisted(() => vi.fn());

// #4227: the step mounts the canonical GamePicker, real here. The catalog is the only
// source it may ask: `api.games` and `api.library.getLibrary` are deliberately absent, so
// a call to either would break the test instead of passing unnoticed.
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: mockSearch,
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

vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: (value: string) => value,
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const CATAN = { id: 'g1', title: 'Catan' };
const SEAFARERS = { id: 'g2', title: 'Catan: Seafarers' };
const WINGSPAN = { id: 'g3', title: 'Wingspan' };

function catalogPage(games: ReadonlyArray<{ id: string; title: string }>) {
  return {
    items: games.map(game => ({ ...game, coverUrl: null })),
    total: games.length,
    page: 1,
    pageSize: 8,
  };
}

describe('FirstGameStep', () => {
  const onComplete = vi.fn();
  const onSkip = vi.fn();
  const onGameAdded = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockSearch.mockReset();
    mockAddGame.mockReset();
    mockDeclareOwnership.mockReset();
    // The catalog as it behaves: it answers the text it is asked.
    mockSearch.mockImplementation(({ searchTerm }: { searchTerm: string }) =>
      Promise.resolve(
        catalogPage(
          [CATAN, SEAFARERS, WINGSPAN].filter(game =>
            game.title.toLowerCase().includes(searchTerm.toLowerCase())
          )
        )
      )
    );
  });

  function renderStep() {
    const user = userEvent.setup();
    renderWithQuery(
      <FirstGameStep onComplete={onComplete} onSkip={onSkip} onGameAdded={onGameAdded} />
    );
    return user;
  }

  const searchField = () => screen.getByRole('combobox', { name: 'Cerca un gioco' });
  const addButton = () => screen.getByRole('button', { name: 'Add to Library' });

  async function pick(
    user: ReturnType<typeof userEvent.setup>,
    game: { id: string },
    text: string
  ) {
    await user.type(searchField(), text);
    await user.click(await screen.findByTestId(`first-game-result-${game.id}`));
  }

  it('starts with the search field and asks nothing before the user types', () => {
    renderStep();

    expect(searchField()).toBeInTheDocument();
    expect(mockSearch).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Add to Library' })).not.toBeInTheDocument();
  });

  it('calls onSkip when the skip button is clicked', async () => {
    const user = renderStep();

    await user.click(screen.getByTestId('game-skip'));

    expect(onSkip).toHaveBeenCalled();
  });

  it('searches the public catalog and lists what it finds', async () => {
    const user = renderStep();

    await user.type(searchField(), 'Catan');

    expect(await screen.findByTestId('first-game-result-g1')).toHaveTextContent('Catan');
    expect(screen.getByTestId('first-game-result-g2')).toHaveTextContent('Catan: Seafarers');
    expect(screen.queryByTestId('first-game-result-g3')).not.toBeInTheDocument();
    expect(mockSearch).toHaveBeenLastCalledWith({ searchTerm: 'Catan', page: 1, pageSize: 8 });
  });

  it('shows the chosen game and offers to add it', async () => {
    const user = renderStep();

    await pick(user, CATAN, 'Catan');

    expect(screen.getByTestId('first-game-selected')).toHaveTextContent('Catan');
    expect(screen.queryByTestId('first-game-results')).not.toBeInTheDocument();
    expect(addButton()).toBeInTheDocument();
  });

  it('adds the chosen game to the library and moves on', async () => {
    mockAddGame.mockResolvedValueOnce({});
    const user = renderStep();

    await pick(user, CATAN, 'Catan');
    await user.click(addButton());

    await waitFor(() => expect(onComplete).toHaveBeenCalled());
    expect(mockAddGame).toHaveBeenCalledWith('g1');
    expect(onGameAdded).toHaveBeenCalledWith('g1', 'Catan');
  });

  it('removing the chosen game brings the search back and takes the add button away', async () => {
    const user = renderStep();
    await pick(user, CATAN, 'Catan');

    await user.click(screen.getByTestId('first-game-clear'));

    expect(searchField()).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add to Library' })).not.toBeInTheDocument();
  });

  it('while the game is being added it cannot be changed', async () => {
    mockAddGame.mockImplementationOnce(() => new Promise(() => {}));
    const user = renderStep();
    await pick(user, CATAN, 'Catan');

    await user.click(addButton());

    await waitFor(() => expect(mockAddGame).toHaveBeenCalled());
    expect(screen.getByTestId('first-game-selected')).toHaveTextContent('Catan');
    expect(screen.queryByTestId('first-game-clear')).not.toBeInTheDocument();
  });

  it('when adding fails it says so, keeps the game chosen and does not move on', async () => {
    mockAddGame.mockRejectedValueOnce(new Error('Library is full'));
    const user = renderStep();
    await pick(user, CATAN, 'Catan');

    await user.click(addButton());

    expect(await screen.findByRole('alert')).toHaveTextContent('Library is full');
    expect(screen.getByTestId('first-game-selected')).toHaveTextContent('Catan');
    expect(onComplete).not.toHaveBeenCalled();
    expect(onGameAdded).not.toHaveBeenCalled();
  });

  // #4155: il cancello RAG apre il manuale solo a chi dichiara di possedere il gioco. Nell'onboarding
  // la dichiarazione resta esplicita: la casella è spenta di default e spiega perché serve.
  describe('ownership declaration (#4155)', () => {
    const ownsCopy = () => screen.getByRole('checkbox', { name: 'I own a copy of this game' });

    it('is not offered before a game is chosen', () => {
      renderStep();

      expect(
        screen.queryByRole('checkbox', { name: 'I own a copy of this game' })
      ).not.toBeInTheDocument();
    });

    it('is off by default, explains why, and adding does not declare ownership', async () => {
      mockAddGame.mockResolvedValueOnce({});
      const user = renderStep();
      await pick(user, CATAN, 'Catan');

      expect(ownsCopy()).not.toBeChecked();
      expect(ownsCopy()).toHaveAccessibleDescription(/copyright/i);
      await user.click(addButton());

      await waitFor(() => expect(onComplete).toHaveBeenCalled());
      expect(mockDeclareOwnership).not.toHaveBeenCalled();
    });

    it('when checked, declares ownership after adding the game', async () => {
      mockAddGame.mockResolvedValueOnce({});
      mockDeclareOwnership.mockResolvedValueOnce({});
      const user = renderStep();
      await pick(user, CATAN, 'Catan');

      await user.click(ownsCopy());
      await user.click(addButton());

      await waitFor(() => expect(onComplete).toHaveBeenCalled());
      expect(mockDeclareOwnership).toHaveBeenCalledWith('g1');
      expect(mockAddGame.mock.invocationCallOrder[0]).toBeLessThan(
        mockDeclareOwnership.mock.invocationCallOrder[0]
      );
    });

    it('keeps going when the declaration fails: the game is added, and the user is told', async () => {
      mockAddGame.mockResolvedValueOnce({});
      mockDeclareOwnership.mockRejectedValueOnce(new Error('boom'));
      const { toast } = await import('sonner');
      const user = renderStep();
      await pick(user, CATAN, 'Catan');

      await user.click(ownsCopy());
      await user.click(addButton());

      await waitFor(() => expect(onComplete).toHaveBeenCalled());
      expect(onGameAdded).toHaveBeenCalledWith('g1', 'Catan');
      expect(toast.error).toHaveBeenCalledWith(
        'Catan is in your library, but the ownership declaration failed: you can declare it from the chat.'
      );
    });

    // The declaration is about one game: ticked for Catan, it must not ride along to the
    // game chosen next and be sent for a copy the user never said they own.
    it('is cleared when the chosen game changes', async () => {
      mockAddGame.mockResolvedValueOnce({});
      const user = renderStep();
      await pick(user, CATAN, 'Catan');
      await user.click(ownsCopy());

      await user.click(screen.getByTestId('first-game-clear'));
      await pick(user, WINGSPAN, 'Wing');

      expect(ownsCopy()).not.toBeChecked();
      await user.click(addButton());
      await waitFor(() => expect(onComplete).toHaveBeenCalled());
      expect(mockAddGame).toHaveBeenCalledWith('g3');
      expect(mockDeclareOwnership).not.toHaveBeenCalled();
    });
  });
});
