/**
 * SessionCreationWizard tests.
 * - The icon-only "add dimension" button in the scoring step must have an accessible
 *   name (axe button-name / WCAG 4.1.2).
 * - #4156: after "Crea Sessione" the wizard lands on the live page of the session it created.
 */
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { beforeEach, describe, it, expect, vi } from 'vitest';

import { SessionCreationWizard } from '../SessionCreationWizard';

const { push, createSession, addPlayer, startSession } = vi.hoisted(() => ({
  push: vi.fn(),
  createSession: vi.fn(),
  addPlayer: vi.fn(),
  startSession: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, back: vi.fn() }),
}));

vi.mock('@/lib/api', () => ({ api: { liveSessions: { createSession, addPlayer, startSession } } }));

// #4750: chi crea la sessione è il primo giocatore. L'utente corrente arriva da questo hook.
type TestUser = { id: string; email: string; displayName?: string | null; role: string };
const currentUser = vi.hoisted(() => ({ value: undefined as TestUser | undefined }));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: currentUser.value }),
}));

const MARCO: TestUser = {
  id: 'user-marco',
  email: 'marco@example.com',
  displayName: 'Marco Rossi',
  role: 'User',
};

// GamePicker fetches the library catalogue; replace it with a button that selects a game
// so the wizard can advance from the game step to the scoring step.
vi.mock('@/components/features/game-picker', () => ({
  GamePicker: ({
    onChange,
  }: {
    onChange: (game: { id: string; title: string; manual: boolean }) => void;
  }) => (
    <button type="button" onClick={() => onChange({ id: 'g1', title: 'Test Game', manual: true })}>
      mock-select-game
    </button>
  ),
}));

describe('SessionCreationWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentUser.value = undefined;
  });

  function advanceToScoringStep() {
    render(<SessionCreationWizard />);
    // Step 1 (game) → pick a game so "Avanti" enables → Step 2 (scoring).
    fireEvent.click(screen.getByText('mock-select-game'));
    fireEvent.click(screen.getByText('Avanti'));
  }

  it('exposes an accessible name on the add-dimension icon button', () => {
    advanceToScoringStep();
    expect(screen.getByRole('button', { name: /aggiungi dimensione/i })).toBeInTheDocument();
  });

  it('lands on the live page of the session it created (#4156)', async () => {
    // L'id che il wizard riceve è quello di una LiveGameSession, e le pagine che la leggono
    // stanno sotto /live. Prima navigava su /sessions/{id}, che leggeva la GameSession: 404.
    createSession.mockResolvedValue('live-1');
    addPlayer.mockResolvedValue('player-1');
    startSession.mockResolvedValue(undefined);

    advanceToScoringStep();
    fireEvent.click(screen.getByText('Avanti')); // scoring → players
    fireEvent.change(screen.getByPlaceholderText('Nome giocatore'), {
      target: { value: 'Marco' },
    });
    fireEvent.click(screen.getByText('Aggiungi'));
    fireEvent.click(screen.getByText('Avanti')); // players → review
    fireEvent.click(screen.getByText('Crea Sessione'));

    await waitFor(() => expect(push).toHaveBeenCalledWith('/sessions/live-1/live'));
    expect(startSession).toHaveBeenCalledWith('live-1');
  });
});

// ── #4750: il creatore è il primo giocatore, collegato al suo account ─────────────

describe('SessionCreationWizard — #4750: «Aggiungi me»', () => {
  function goToPlayersStep() {
    render(<SessionCreationWizard />);
    fireEvent.click(screen.getByText('mock-select-game'));
    fireEvent.click(screen.getByText('Avanti')); // game → scoring
    fireEvent.click(screen.getByText('Avanti')); // scoring → players
  }

  function addBob() {
    fireEvent.change(screen.getByPlaceholderText('Nome giocatore'), { target: { value: 'Bob' } });
    fireEvent.click(screen.getByText('Aggiungi'));
  }

  async function create() {
    fireEvent.click(screen.getByText('Avanti')); // players → review
    fireEvent.click(screen.getByText('Crea Sessione'));
    await waitFor(() => expect(startSession).toHaveBeenCalled());
  }

  function addPlayerPayloads(): Array<Record<string, unknown>> {
    return addPlayer.mock.calls.map(call => call[1]);
  }

  // Il describe è un fratello del primo: il suo beforeEach non arriva fin qui.
  beforeEach(() => {
    vi.clearAllMocks();
    currentUser.value = undefined;
    createSession.mockResolvedValue('live-1');
    addPlayer.mockResolvedValue('player-1');
    startSession.mockResolvedValue(undefined);
  });

  it('con l’utente noto la lista parte da lui, con la targhetta «Tu»', () => {
    currentUser.value = MARCO;
    goToPlayersStep();

    addBob();

    // La targhetta è una sola, sulla riga dell'utente: la riga di Bob non la ha.
    expect(screen.getByText('Marco Rossi')).toBeInTheDocument();
    expect(screen.getAllByText('Tu')).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
    // «Io non gioco» è la sua uscita: accanto non c'è anche il cestino, che per Bob c'è.
    expect(screen.getByRole('button', { name: 'Io non gioco' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rimuovi Marco Rossi' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rimuovi Bob' })).toBeInTheDocument();
    // Un giocatore c'è già: si può andare avanti senza scrivere niente.
    expect(screen.getByRole('button', { name: 'Avanti' })).toBeEnabled();
  });

  it('crea la sessione col suo userId, e gli altri giocatori senza', async () => {
    currentUser.value = MARCO;
    goToPlayersStep();
    addBob();

    await create();

    const [marco, bob] = addPlayerPayloads();
    expect(marco).toMatchObject({ displayName: 'Marco Rossi', userId: 'user-marco' });
    expect(bob).toMatchObject({ displayName: 'Bob' });
    expect(bob).not.toHaveProperty('userId');
  });

  it('«Io non gioco» lo toglie: senza giocatori «Avanti» si spegne e nessuno parte collegato', async () => {
    currentUser.value = MARCO;
    goToPlayersStep();

    fireEvent.click(screen.getByRole('button', { name: 'Io non gioco' }));

    expect(screen.queryByText('Marco Rossi')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Avanti' })).toBeDisabled();
    addBob();
    await create();

    expect(addPlayerPayloads().every(p => !('userId' in p))).toBe(true);
  });

  it('tolto, «Aggiungi me» lo rimette per primo e collegato', async () => {
    currentUser.value = MARCO;
    goToPlayersStep();
    fireEvent.click(screen.getByRole('button', { name: 'Io non gioco' }));
    addBob();

    fireEvent.click(screen.getByRole('button', { name: 'Aggiungi me' }));
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
    await create();

    const [first, second] = addPlayerPayloads();
    expect(first).toMatchObject({ displayName: 'Marco Rossi', userId: 'user-marco' });
    expect(second).toMatchObject({ displayName: 'Bob' });
  });

  it('senza un utente noto la lista parte vuota, come prima', () => {
    goToPlayersStep();

    expect(screen.queryByText('Tu')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Avanti' })).toBeDisabled();
  });

  it('se l’utente arriva dopo il montaggio la lista vuota parte da lui', () => {
    const ui = () => <SessionCreationWizard />;
    const { rerender } = render(ui());
    fireEvent.click(screen.getByText('mock-select-game'));
    fireEvent.click(screen.getByText('Avanti'));
    fireEvent.click(screen.getByText('Avanti'));
    expect(screen.queryByText('Marco Rossi')).not.toBeInTheDocument();

    currentUser.value = MARCO;
    rerender(ui());

    expect(screen.getByText('Marco Rossi')).toBeInTheDocument();
  });
});
