/**
 * CreateSessionStep — #4750: chi crea la partita è il primo giocatore, collegato al suo account.
 *
 * Qui il collegamento non è uno stato a parte: un posto è «dell'utente» finché porta il suo nome.
 * Cambiare il nome lo scollega, e «Io non gioco» svuota il posto.
 */
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { CreateSessionStep } from '../steps/CreateSessionStep';

const { createSession, addPlayer, startSession } = vi.hoisted(() => ({
  createSession: vi.fn(),
  addPlayer: vi.fn(),
  startSession: vi.fn(),
}));

vi.mock('@/lib/api', () => ({ api: { liveSessions: { createSession, addPlayer, startSession } } }));

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

function renderStep(onSessionCreated = vi.fn()) {
  const ui = () => (
    <CreateSessionStep gameId="g1" gameTitle="Catan" onSessionCreated={onSessionCreated} />
  );
  return { ...render(ui()), ui, onSessionCreated };
}

function addPlayerPayloads(): Array<Record<string, unknown>> {
  return addPlayer.mock.calls.map(call => call[1]);
}

describe('CreateSessionStep — #4750: «Aggiungi me»', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentUser.value = undefined;
    createSession.mockResolvedValue('sess-1');
    addPlayer.mockResolvedValue('player-1');
    startSession.mockResolvedValue(undefined);
  });

  it('con l’utente noto il primo posto è suo, con la targhetta «Tu»', () => {
    currentUser.value = MARCO;
    renderStep();

    expect(screen.getByTestId('player-input-0')).toHaveValue('Marco Rossi');
    expect(screen.getByTestId('player-input-1')).toHaveValue('');
    expect(screen.getAllByText('Tu')).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
  });

  it('il posto dell’utente non ha il cestino: se ne esce con «Io non gioco»', async () => {
    const user = userEvent.setup();
    currentUser.value = MARCO;
    renderStep();

    await user.click(screen.getByTestId('add-player-button')); // tre posti: il cestino compare

    expect(screen.queryByRole('button', { name: 'Rimuovi giocatore 1' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rimuovi giocatore 2' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Io non gioco' })).toBeInTheDocument();
  });

  it('crea la sessione col suo userId, e l’altro giocatore senza', async () => {
    const user = userEvent.setup();
    currentUser.value = MARCO;
    const { onSessionCreated } = renderStep();

    await user.type(screen.getByTestId('player-input-1'), 'Luca');
    await user.click(screen.getByTestId('create-session-button'));

    await waitFor(() => expect(onSessionCreated).toHaveBeenCalledWith('sess-1'));
    const [marco, luca] = addPlayerPayloads();
    expect(marco).toMatchObject({ displayName: 'Marco Rossi', userId: 'user-marco' });
    expect(luca).toMatchObject({ displayName: 'Luca' });
    expect(luca).not.toHaveProperty('userId');
  });

  it('«Io non gioco» svuota il posto: servono comunque due giocatori, e nessuno parte collegato', async () => {
    const user = userEvent.setup();
    currentUser.value = MARCO;
    const { onSessionCreated } = renderStep();

    await user.click(screen.getByRole('button', { name: 'Io non gioco' }));

    expect(screen.getByTestId('player-input-0')).toHaveValue('');
    expect(screen.queryByText('Tu')).not.toBeInTheDocument();
    expect(screen.getByTestId('create-session-button')).toBeDisabled();
    await user.type(screen.getByTestId('player-input-0'), 'Anna');
    await user.type(screen.getByTestId('player-input-1'), 'Luca');
    await user.click(screen.getByTestId('create-session-button'));

    await waitFor(() => expect(onSessionCreated).toHaveBeenCalled());
    expect(addPlayerPayloads().every(p => !('userId' in p))).toBe(true);
  });

  it('scrivere un altro nome nel posto dell’utente lo scollega', async () => {
    const user = userEvent.setup();
    currentUser.value = MARCO;
    renderStep();

    await user.clear(screen.getByTestId('player-input-0'));
    await user.type(screen.getByTestId('player-input-0'), 'Anna');

    expect(screen.queryByText('Tu')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aggiungi me' })).toBeInTheDocument();
  });

  it('«Aggiungi me» riempie il primo posto vuoto', async () => {
    const user = userEvent.setup();
    currentUser.value = MARCO;
    renderStep();
    await user.click(screen.getByRole('button', { name: 'Io non gioco' }));

    await user.click(screen.getByRole('button', { name: 'Aggiungi me' }));

    expect(screen.getByTestId('player-input-0')).toHaveValue('Marco Rossi');
    expect(screen.getAllByText('Tu')).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
  });

  it('con tutti i posti pieni «Aggiungi me» ne aggiunge uno, finché c’è spazio', async () => {
    const user = userEvent.setup();
    currentUser.value = MARCO;
    renderStep();
    await user.click(screen.getByRole('button', { name: 'Io non gioco' }));
    await user.type(screen.getByTestId('player-input-0'), 'Anna');
    await user.type(screen.getByTestId('player-input-1'), 'Luca');

    await user.click(screen.getByRole('button', { name: 'Aggiungi me' }));

    expect(screen.getByTestId('player-input-2')).toHaveValue('Marco Rossi');
  });

  it('è aggiunto per primo anche se il suo nome sta nel secondo posto', async () => {
    // Il server dà il ruolo Host a chi entra per primo, e il riepilogo lo indica come
    // proprietario: il creatore collegato deve esserlo. I colori seguono invece i posti.
    const user = userEvent.setup();
    currentUser.value = MARCO;
    const { onSessionCreated } = renderStep();
    await user.click(screen.getByRole('button', { name: 'Io non gioco' }));
    await user.type(screen.getByTestId('player-input-0'), 'Luca');
    await user.type(screen.getByTestId('player-input-1'), 'Marco Rossi');

    await user.click(screen.getByTestId('create-session-button'));

    await waitFor(() => expect(onSessionCreated).toHaveBeenCalled());
    const [first, second] = addPlayerPayloads();
    expect(first).toMatchObject({
      displayName: 'Marco Rossi',
      userId: 'user-marco',
      color: 'Blue',
    });
    expect(second).toMatchObject({ displayName: 'Luca', color: 'Red' });
  });

  it('senza un utente noto i posti restano vuoti, come prima', () => {
    renderStep();

    expect(screen.getByTestId('player-input-0')).toHaveValue('');
    expect(screen.queryByText('Tu')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
  });

  it('se l’utente arriva dopo il montaggio prende il primo posto vuoto', () => {
    const { rerender, ui } = renderStep();
    expect(screen.getByTestId('player-input-0')).toHaveValue('');

    currentUser.value = MARCO;
    rerender(ui());

    expect(screen.getByTestId('player-input-0')).toHaveValue('Marco Rossi');
  });
});
