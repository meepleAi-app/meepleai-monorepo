/**
 * CreateSessionStep — #4750: chi crea la partita è il primo giocatore, collegato al suo account.
 *
 * Il collegamento è uno stato del posto, non una deduzione dal nome: modificare il nome del posto
 * non lo toglie, e scrivere lo stesso nome in un altro posto non lo crea. Lo tolgono «Io non
 * gioco» (che svuota anche il posto) e il nome svuotato.
 */
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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

const ANNA: TestUser = {
  id: 'user-anna',
  email: 'anna@example.com',
  displayName: 'Anna',
  role: 'User',
};

const NO_NAME: TestUser = {
  id: 'user-x',
  email: 'segreto@example.com',
  displayName: null,
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

function setSeatName(index: number, value: string) {
  fireEvent.change(screen.getByTestId(`player-input-${index}`), { target: { value } });
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

  describe('il collegamento è uno stato del posto, non una deduzione dal nome', () => {
    it('accorciare il nome del posto dell’utente non lo scollega: la stessa persona non compare due volte', async () => {
      // Profilo «Marco Rossi», il posto diventa «Marco»: dedotto dal nome il collegamento spariva
      // in silenzio, e «Aggiungi me» rimetteva il profilo in un altro posto.
      const user = userEvent.setup();
      currentUser.value = MARCO;
      const { onSessionCreated } = renderStep();

      setSeatName(0, 'Marco');

      expect(screen.getAllByText('Tu')).toHaveLength(1);
      expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
      await user.type(screen.getByTestId('player-input-1'), 'Luca');
      await user.click(screen.getByTestId('create-session-button'));

      await waitFor(() => expect(onSessionCreated).toHaveBeenCalled());
      expect(addPlayer).toHaveBeenCalledTimes(2);
      const [first, second] = addPlayerPayloads();
      expect(first).toMatchObject({ displayName: 'Marco', userId: 'user-marco' });
      expect(second).toMatchObject({ displayName: 'Luca' });
      expect(second).not.toHaveProperty('userId');
    });

    it('scrivere il nome del profilo per un altro giocatore non lo collega, dopo «Io non gioco»', async () => {
      // Profilo «Anna», «Io non gioco», poi «Anna» per un'amica: dedotto dal nome partiva
      // con lo userId dell'utente, contro «Io non gioco = nessun giocatore collegato».
      const user = userEvent.setup();
      currentUser.value = ANNA;
      const { onSessionCreated } = renderStep();
      await user.click(screen.getByRole('button', { name: 'Io non gioco' }));

      setSeatName(0, 'Anna');
      setSeatName(1, 'Luca');

      expect(screen.queryByText('Tu')).not.toBeInTheDocument();
      await user.click(screen.getByTestId('create-session-button'));
      await waitFor(() => expect(onSessionCreated).toHaveBeenCalled());
      expect(addPlayerPayloads().every(p => !('userId' in p))).toBe(true);
    });

    it('svuotare il nome del posto dell’utente lo scollega, e «Aggiungi me» riappare', () => {
      currentUser.value = MARCO;
      renderStep();

      setSeatName(0, '');

      expect(screen.queryByText('Tu')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Aggiungi me' })).toBeInTheDocument();
    });
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

  it('è aggiunto per primo anche se siede in un posto dopo il primo', async () => {
    // Il server dà il ruolo Host a chi entra per primo, e il riepilogo lo indica come
    // proprietario: il creatore collegato deve esserlo. I colori seguono invece i posti.
    const user = userEvent.setup();
    currentUser.value = MARCO;
    const { onSessionCreated } = renderStep();
    await user.click(screen.getByRole('button', { name: 'Io non gioco' }));
    setSeatName(0, 'Luca');
    await user.click(screen.getByRole('button', { name: 'Aggiungi me' })); // il primo vuoto: il secondo

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

describe('CreateSessionStep — #4750: seme, nomi e posti', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentUser.value = undefined;
    createSession.mockResolvedValue('sess-1');
    addPlayer.mockResolvedValue('player-1');
    startSession.mockResolvedValue(undefined);
  });

  describe('il seme parte una volta, e solo se il primo posto è vuoto', () => {
    it('un nome già scritto nel primo posto prima che l’utente arrivi non si cancella', async () => {
      const user = userEvent.setup();
      const { rerender, ui } = renderStep();
      setSeatName(0, 'Anna');

      currentUser.value = MARCO;
      rerender(ui());

      expect(screen.getByTestId('player-input-0')).toHaveValue('Anna');
      expect(screen.getByTestId('player-input-1')).toHaveValue('');
      expect(screen.queryByText('Tu')).not.toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: 'Aggiungi me' }));
      expect(screen.getByTestId('player-input-1')).toHaveValue('Marco Rossi');
    });

    it('tolto con «Io non gioco», un aggiornamento dell’utente non riempie di nuovo il posto', async () => {
      const user = userEvent.setup();
      currentUser.value = MARCO;
      const { rerender, ui } = renderStep();
      await user.click(screen.getByRole('button', { name: 'Io non gioco' }));

      currentUser.value = { ...MARCO, displayName: 'Marco R.' };
      rerender(ui());

      expect(screen.getByTestId('player-input-0')).toHaveValue('');
      expect(screen.queryByText('Tu')).not.toBeInTheDocument();
    });
  });

  describe('utente cambiato a metà', () => {
    it('se l’utente cambia dopo il seme, il posto non parte più col vecchio userId', async () => {
      // Un altro account dopo un refetch: il posto perde la targhetta e «Io non gioco» (non è più
      // suo), e non deve partire con lo userId di prima.
      const user = userEvent.setup();
      currentUser.value = MARCO;
      const { rerender, ui, onSessionCreated } = renderStep();

      currentUser.value = ANNA;
      rerender(ui());

      expect(screen.getByTestId('player-input-0')).toHaveValue('Marco Rossi');
      expect(screen.queryByText('Tu')).not.toBeInTheDocument();
      await user.type(screen.getByTestId('player-input-1'), 'Luca');
      await user.click(screen.getByTestId('create-session-button'));

      await waitFor(() => expect(onSessionCreated).toHaveBeenCalled());
      expect(addPlayerPayloads()[0]).toMatchObject({ displayName: 'Marco Rossi' });
      expect(addPlayerPayloads().every(p => !('userId' in p))).toBe(true);
    });
  });

  describe('nomi', () => {
    it('due giocatori con lo stesso nome: messaggio accanto ai campi, «Inizia» spento, nessuna sessione', async () => {
      const user = userEvent.setup();
      currentUser.value = MARCO;
      renderStep();

      setSeatName(1, ' marco rossi ');

      expect(screen.getByRole('status')).toHaveTextContent(
        'Due giocatori si chiamano «marco rossi»'
      );
      expect(screen.getByTestId('create-session-button')).toBeDisabled();
      await user.click(screen.getByTestId('create-session-button'));
      expect(createSession).not.toHaveBeenCalled();

      setSeatName(1, 'Luca');

      expect(screen.queryByRole('status')).not.toBeInTheDocument();
      expect(screen.getByTestId('create-session-button')).toBeEnabled();
    });

    it('i campi in conflitto sono non validi e puntano al messaggio; gli altri no', () => {
      renderStep();
      fireEvent.click(screen.getByTestId('add-player-button')); // tre posti
      setSeatName(0, 'Anna');
      setSeatName(1, ' anna');
      setSeatName(2, 'Luca');

      const message = screen.getByRole('status').textContent ?? '';
      expect(message).toContain('Due giocatori si chiamano');
      for (const index of [0, 1]) {
        const input = screen.getByTestId(`player-input-${index}`);
        expect(input).toHaveAttribute('aria-invalid', 'true');
        expect(input).toHaveAccessibleDescription(message);
      }
      const luca = screen.getByTestId('player-input-2');
      expect(luca).not.toHaveAttribute('aria-invalid');
      expect(luca).not.toHaveAttribute('aria-describedby');

      setSeatName(1, 'Marta');

      for (const index of [0, 1, 2]) {
        const input = screen.getByTestId(`player-input-${index}`);
        expect(input).not.toHaveAttribute('aria-invalid');
        expect(input).not.toHaveAttribute('aria-describedby');
      }
    });

    it('con «Luca» in un posto, scrivere «Luca Rossi» nell’altro non dà mai un avviso assertivo', async () => {
      // Per i nomi intermedi («Luca», «Luca ») il conflitto c'è davvero, ma non deve
      // interrompere chi scrive: il messaggio è una regione di stato, non un avviso.
      const user = userEvent.setup();
      renderStep();
      setSeatName(0, 'Luca');
      const second = screen.getByTestId('player-input-1');
      await user.click(second);

      for (const key of 'Luca Rossi') {
        await user.keyboard(key);
        expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      }

      expect(second).toHaveValue('Luca Rossi');
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('senza nome del profilo il creatore è «Giocatore 1», non l’email', async () => {
      const user = userEvent.setup();
      currentUser.value = NO_NAME;
      const { onSessionCreated } = renderStep();

      expect(screen.getByTestId('player-input-0')).toHaveValue('Giocatore 1');
      expect(screen.queryByDisplayValue(/segreto/i)).not.toBeInTheDocument();
      await user.type(screen.getByTestId('player-input-1'), 'Luca');
      await user.click(screen.getByTestId('create-session-button'));

      await waitFor(() => expect(onSessionCreated).toHaveBeenCalled());
      expect(addPlayerPayloads()[0]).toMatchObject({
        displayName: 'Giocatore 1',
        userId: 'user-x',
      });
    });
  });

  describe('posti', () => {
    function fillEightSeats() {
      for (let i = 0; i < 6; i++) {
        fireEvent.click(screen.getByTestId('add-player-button')); // da due a otto posti
      }
      for (let i = 0; i < 8; i++) setSeatName(i, `Giocatore ${i + 10}`);
    }

    it('con tutti i posti pieni «Aggiungi me» è spento e dice perché', async () => {
      const user = userEvent.setup();
      currentUser.value = MARCO;
      renderStep();
      await user.click(screen.getByRole('button', { name: 'Io non gioco' }));
      fillEightSeats();

      const addSelf = screen.getByRole('button', { name: 'Aggiungi me' });
      expect(addSelf).toBeDisabled();
      expect(screen.getByText(/posti sono pieni/i)).toBeInTheDocument();
      expect(addSelf).toHaveAccessibleDescription(/posti sono pieni/i);

      await user.click(screen.getByRole('button', { name: 'Rimuovi giocatore 8' })); // un posto libero

      expect(screen.getByRole('button', { name: 'Aggiungi me' })).toBeEnabled();
      expect(screen.queryByText(/posti sono pieni/i)).not.toBeInTheDocument();
    });
  });
});
