/**
 * StartSessionSheet — #4781: «Avvia Partita» deve avviare la partita, non solo crearla.
 */
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { StartSessionSheet } from '../StartSessionSheet';

const routerPush = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: routerPush }),
}));

// Il contenitore non è l'oggetto della prova: basta che mostri i figli quando è aperto.
vi.mock('@/components/ui/overlays/BottomSheet', () => ({
  BottomSheet: ({ open, children }: { open: boolean; children: React.ReactNode }) =>
    open ? <div>{children}</div> : null,
}));

const mockApi = vi.hoisted(() => ({
  liveSessions: {
    createSession: vi.fn(),
    addPlayer: vi.fn(),
    updateTurnOrder: vi.fn(),
    startSession: vi.fn(),
  },
}));

vi.mock('@/lib/api', () => ({ api: mockApi }));

beforeEach(() => {
  vi.clearAllMocks();
  mockApi.liveSessions.createSession.mockResolvedValue('session-9');
  mockApi.liveSessions.addPlayer.mockReset();
  mockApi.liveSessions.addPlayer
    .mockResolvedValueOnce('player-1')
    .mockResolvedValueOnce('player-2');
  mockApi.liveSessions.updateTurnOrder.mockResolvedValue(undefined);
  mockApi.liveSessions.startSession.mockReset();
  mockApi.liveSessions.startSession.mockResolvedValue(undefined);
});

function openSheetWithTwoPlayers() {
  render(<StartSessionSheet open onOpenChange={vi.fn()} gameId="game-1" gameName="Catan" />);
  fireEvent.change(screen.getByTestId('new-player-input'), { target: { value: 'Bob' } });
  fireEvent.click(screen.getByTestId('add-player-btn'));
  fireEvent.click(screen.getByTestId('next-step-btn')); // → ordine dei turni
}

// NaN when the call never happened: any comparison with it is false, so a missing call fails the
// test instead of sorting before everything.
const lastCallOrder = (fn: { mock: { invocationCallOrder: number[] } }) =>
  fn.mock.invocationCallOrder.at(-1) ?? Number.NaN;

describe('StartSessionSheet — #4781: «Avvia Partita» avvia la partita', () => {
  it('avvia la sessione creata, dopo giocatori e ordine dei turni e prima di aprire la pagina live', async () => {
    openSheetWithTwoPlayers();
    fireEvent.click(screen.getByTestId('start-session-btn'));
    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/sessions/session-9/live'));

    const { addPlayer, updateTurnOrder, startSession } = mockApi.liveSessions;
    expect(startSession).toHaveBeenCalledTimes(1);
    expect(startSession).toHaveBeenCalledWith('session-9');
    expect(lastCallOrder(addPlayer)).toBeLessThan(lastCallOrder(startSession));
    expect(lastCallOrder(updateTurnOrder)).toBeLessThan(lastCallOrder(startSession));
    expect(lastCallOrder(startSession)).toBeLessThan(lastCallOrder(routerPush));
  });

  it('se l’avvio fallisce lo dice, non apre la pagina live e il secondo tentativo non crea un’altra sessione', async () => {
    mockApi.liveSessions.startSession.mockRejectedValueOnce(new Error('Rete non raggiungibile'));
    openSheetWithTwoPlayers();

    fireEvent.click(screen.getByTestId('start-session-btn'));
    expect(await screen.findByTestId('session-error')).toHaveTextContent('Rete non raggiungibile');
    expect(routerPush).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('start-session-btn'));
    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/sessions/session-9/live'));

    expect(mockApi.liveSessions.createSession).toHaveBeenCalledTimes(1);
    expect(mockApi.liveSessions.addPlayer).toHaveBeenCalledTimes(2);
    expect(mockApi.liveSessions.startSession).toHaveBeenCalledTimes(2);
  });

  it('se dopo l’errore i giocatori cambiano, il tentativo successivo apre una sessione nuova', async () => {
    mockApi.liveSessions.startSession.mockRejectedValueOnce(new Error('Rete non raggiungibile'));
    mockApi.liveSessions.createSession
      .mockResolvedValueOnce('session-9')
      .mockResolvedValueOnce('session-10');
    mockApi.liveSessions.addPlayer.mockReset();
    mockApi.liveSessions.addPlayer.mockImplementation(
      async (_sessionId: string, p: { displayName: string }) => `id-${p.displayName}`
    );
    openSheetWithTwoPlayers();

    fireEvent.click(screen.getByTestId('start-session-btn'));
    await screen.findByTestId('session-error');

    fireEvent.click(screen.getByTestId('back-btn'));
    fireEvent.change(screen.getByTestId('new-player-input'), { target: { value: 'Carla' } });
    fireEvent.click(screen.getByTestId('add-player-btn'));
    fireEvent.click(screen.getByTestId('next-step-btn'));
    fireEvent.click(screen.getByTestId('start-session-btn'));
    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/sessions/session-10/live'));

    // La sessione del primo tentativo non aveva Carla: non va avviata al suo posto.
    expect(mockApi.liveSessions.createSession).toHaveBeenCalledTimes(2);
    expect(mockApi.liveSessions.startSession).toHaveBeenLastCalledWith('session-10');
  });

  it('se il server risponde 409 all’avvio (partita già partita, risposta persa) apre la pagina live', async () => {
    // Il client ritenta i POST su 502/503: l’avvio può essere riuscito e il ritentativo trovare 409.
    mockApi.liveSessions.startSession.mockRejectedValueOnce(
      Object.assign(new Error('Cannot start session from InProgress status.'), { statusCode: 409 })
    );
    openSheetWithTwoPlayers();

    fireEvent.click(screen.getByTestId('start-session-btn'));
    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/sessions/session-9/live'));

    expect(screen.queryByTestId('session-error')).toBeNull();
    expect(mockApi.liveSessions.createSession).toHaveBeenCalledTimes(1);
  });

  it('se un giocatore non è stato aggiunto, un avvio fallito non fa ripartire quella sessione monca', async () => {
    mockApi.liveSessions.addPlayer.mockReset();
    mockApi.liveSessions.addPlayer
      .mockResolvedValueOnce('player-1')
      .mockRejectedValueOnce(new Error('Nome già usato'))
      .mockResolvedValue('player-x');
    mockApi.liveSessions.createSession
      .mockResolvedValueOnce('session-9')
      .mockResolvedValueOnce('session-10');
    mockApi.liveSessions.startSession.mockRejectedValueOnce(new Error('Rete non raggiungibile'));
    openSheetWithTwoPlayers();

    fireEvent.click(screen.getByTestId('start-session-btn'));
    // Prima compare l’avviso sul giocatore mancante, poi il foglio aspetta un attimo e avvia.
    await waitFor(
      () => expect(screen.getByTestId('session-error')).toHaveTextContent('Rete non raggiungibile'),
      { timeout: 4000 }
    );

    // Il secondo tentativo rifà la sessione con tutti e due i giocatori, invece di avviarne una
    // con un giocatore solo mentre l’elenco ne mostra due.
    fireEvent.click(screen.getByTestId('start-session-btn'));
    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/sessions/session-10/live'), {
      timeout: 4000,
    });
    expect(mockApi.liveSessions.createSession).toHaveBeenCalledTimes(2);
  });
});
