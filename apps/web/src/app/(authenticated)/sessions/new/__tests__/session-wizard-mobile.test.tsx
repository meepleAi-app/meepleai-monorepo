/**
 * SessionWizardMobile — S1 prefill + S2 turn order tests
 */
import { render as rtlRender, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { SessionWizardMobile } from '../session-wizard-mobile';

function render(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return rtlRender(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// ── Mocks ──────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// #4227: the game step uses the canonical GamePicker, which asks the library itself.
vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: (value: string) => value,
}));

const WINGSPAN = { gameId: 'game-wingspan', gameTitle: 'Wingspan' };
const AZUL = { gameId: 'game-azul', gameTitle: 'Azul' };

function libraryPage(items: ReadonlyArray<{ gameId: string; gameTitle: string }>) {
  return {
    items,
    page: 1,
    pageSize: 30,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}

const mockApi = vi.hoisted(() => ({
  library: { getLibrary: vi.fn() },
  games: { getPhaseTemplates: vi.fn().mockResolvedValue([]) },
  liveSessions: {
    createSession: vi.fn().mockResolvedValue('session-123'),
    addPlayer: vi
      .fn()
      .mockResolvedValueOnce('player-uuid-1')
      .mockResolvedValueOnce('player-uuid-2')
      .mockResolvedValueOnce('player-uuid-3'),
    updateTurnOrder: vi.fn().mockResolvedValue(undefined),
    configurePhases: vi.fn().mockResolvedValue(undefined),
  },
}));

vi.mock('@/lib/api', () => ({ api: mockApi }));

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

beforeEach(() => {
  vi.clearAllMocks();
  currentUser.value = undefined;
  // La libreria come si comporta: filtra per titolo.
  mockApi.library.getLibrary.mockImplementation((params?: { search?: string }) => {
    const term = params?.search?.toLowerCase();
    return Promise.resolve(
      libraryPage([AZUL, WINGSPAN].filter(g => !term || g.gameTitle.toLowerCase().includes(term)))
    );
  });
  // clearAllMocks non svuota la coda dei valori «Once»: senza questo reset un test che
  // aggiunge un giocatore sposta gli id attesi da quelli dopo.
  mockApi.liveSessions.addPlayer.mockReset();
  mockApi.liveSessions.addPlayer
    .mockResolvedValueOnce('player-uuid-1')
    .mockResolvedValueOnce('player-uuid-2')
    .mockResolvedValueOnce('player-uuid-3');
});

// ── #4187 / #4227: la scelta del gioco, col selettore canonico ───────────────

describe('SessionWizardMobile — scelta del gioco (#4187, #4227)', () => {
  it('a campo vuoto elenca la libreria, in ordine di titolo', async () => {
    render(<SessionWizardMobile />);

    expect(await screen.findByTestId('session-wizard-game-result-game-azul')).toBeInTheDocument();
    expect(screen.getByTestId('session-wizard-game-result-game-wingspan')).toBeInTheDocument();
    expect(mockApi.library.getLibrary).toHaveBeenCalledWith(
      expect.objectContaining({ search: undefined, sortBy: 'title', sortDescending: false })
    );
  });

  it('scrivere nella ricerca manda il testo al server', async () => {
    const user = userEvent.setup();
    render(<SessionWizardMobile />);
    await screen.findByTestId('session-wizard-game-result-game-azul');

    await user.type(screen.getByRole('combobox', { name: 'Cerca un gioco' }), 'Wing');

    await waitFor(() =>
      expect(mockApi.library.getLibrary).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: 'Wing' })
      )
    );
    await waitFor(() =>
      expect(screen.queryByTestId('session-wizard-game-result-game-azul')).not.toBeInTheDocument()
    );
    expect(screen.getByTestId('session-wizard-game-result-game-wingspan')).toBeInTheDocument();
  });

  it('mentre la risposta per il testo nuovo non è arrivata non dice che non c’è nulla', async () => {
    const user = userEvent.setup();
    mockApi.library.getLibrary.mockImplementation((params?: { search?: string }) =>
      params?.search ? new Promise(() => {}) : Promise.resolve(libraryPage([AZUL, WINGSPAN]))
    );
    render(<SessionWizardMobile />);
    await screen.findByTestId('session-wizard-game-result-game-azul');

    await user.type(screen.getByRole('combobox', { name: 'Cerca un gioco' }), 'Zz');

    expect(screen.queryByTestId('session-wizard-game-empty')).not.toBeInTheDocument();
    // La lista di prima resta a schermo finché non arriva quella nuova.
    expect(screen.getByTestId('session-wizard-game-result-game-azul')).toBeInTheDocument();
  });

  it('«Avanti» resta spento finché non c’è un gioco', async () => {
    render(<SessionWizardMobile />);
    await screen.findByTestId('session-wizard-game-result-game-azul');

    expect(screen.getByRole('button', { name: 'Avanti' })).toBeDisabled();
  });

  it('un gioco scelto dalla libreria fa nascere la sessione col suo id', async () => {
    render(<SessionWizardMobile />);
    fireEvent.click(await screen.findByTestId('session-wizard-game-result-game-wingspan'));
    expect(screen.getByTestId('session-wizard-game-selected')).toHaveTextContent('Wingspan');

    fireEvent.click(screen.getByText('Avanti')); // → step 2
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    fireEvent.click(screen.getByText('Avanti')); // → step 4
    fireEvent.click(screen.getByText('Salta')); // → step 5
    fireEvent.click(screen.getByText('Inizia a Giocare'));

    await waitFor(() =>
      expect(mockApi.liveSessions.createSession).toHaveBeenCalledWith({
        gameName: 'Wingspan',
        gameId: 'game-wingspan',
      })
    );
  });

  it('un nome scritto a mano fa nascere la sessione senza id di gioco', async () => {
    render(<SessionWizardMobile />);
    await screen.findByTestId('session-wizard-game-result-game-azul');

    fireEvent.change(screen.getByTestId('session-wizard-game-manual-input'), {
      target: { value: 'Gioco di casa' },
    });
    fireEvent.click(screen.getByTestId('session-wizard-game-manual-confirm'));
    expect(screen.getByTestId('session-wizard-game-selected')).toHaveTextContent('Gioco di casa');

    fireEvent.click(screen.getByText('Avanti')); // → step 2
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    fireEvent.click(screen.getByText('Avanti')); // → step 4
    fireEvent.click(screen.getByText('Salta')); // → step 5
    fireEvent.click(screen.getByText('Inizia a Giocare'));

    await waitFor(() =>
      expect(mockApi.liveSessions.createSession).toHaveBeenCalledWith({
        gameName: 'Gioco di casa',
        gameId: undefined,
      })
    );
  });

  it('la «×» toglie il gioco scelto e «Avanti» si rispegne', async () => {
    render(<SessionWizardMobile />);
    fireEvent.click(await screen.findByTestId('session-wizard-game-result-game-wingspan'));

    fireEvent.click(screen.getByTestId('session-wizard-game-clear'));

    expect(screen.queryByTestId('session-wizard-game-selected')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Avanti' })).toBeDisabled();
  });
});

// ── S1: Pre-fill ───────────────────────────────────────────────────────────────

describe('SessionWizardMobile — S1: pre-fill da game card', () => {
  it('mostra step 1 (scegli gioco) quando nessun prefilledGameId', () => {
    render(<SessionWizardMobile />);
    expect(screen.getByText('Scegli un gioco')).toBeInTheDocument();
  });

  it('salta a step 2 quando prefilledGameId è fornito', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    expect(screen.queryByText('Scegli un gioco')).not.toBeInTheDocument();
    expect(screen.getByText('Aggiungi giocatori')).toBeInTheDocument();
  });

  it('mostra context pill del gioco pre-selezionato nello step 2', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('mostra 5 dot con il primo marcato come done (pre-filled)', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    const dots = screen.getAllByRole('button', { name: /Passo/i });
    expect(dots).toHaveLength(5);
  });
});

// ── S2: Turn Order ─────────────────────────────────────────────────────────────

describe('SessionWizardMobile — S2: step ordine turni', () => {
  it('mostra lo step "Ordine turni" dopo step giocatori', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    // step 2 → Avanti → step 3
    fireEvent.click(screen.getByText('Avanti'));
    expect(screen.getByText('Ordine turni')).toBeInTheDocument();
  });

  it('mostra badge posizione "1°" per il primo giocatore', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    expect(screen.getByText('1°')).toBeInTheDocument();
  });

  it('sposta giocatore in basso cliccando ↓', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    // Aggiungi secondo giocatore in step 2
    const nameInput = screen.getByLabelText('Nome nuovo giocatore');
    fireEvent.change(nameInput, { target: { value: 'Bob' } });
    fireEvent.click(screen.getByText('Aggiungi'));

    // → step 3 (ordine turni)
    fireEvent.click(screen.getByText('Avanti'));

    // "Giocatore 1" è in posizione 1 — clicca ↓ per spostarlo
    const downButtons = screen.getAllByRole('button', { name: /Sposta in basso/i });
    fireEvent.click(downButtons[0]);

    // Ora Bob dovrebbe avere badge "1°"
    const firstBadge = screen.getByText('1°');
    const firstRow = firstBadge.closest('[role="listitem"]');
    expect(firstRow).toHaveTextContent('Bob');
  });

  it('disabilita freccia ▲ per il primo giocatore', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    const upButtons = screen.getAllByRole('button', { name: /Sposta in alto/i });
    expect(upButtons[0]).toBeDisabled();
  });

  it('la summary mostra la sezione ordine turni con badge numerati', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    // Aggiungi secondo giocatore (la sezione ordine turni in summary richiede players.length > 1)
    const nameInput = screen.getByLabelText('Nome nuovo giocatore');
    fireEvent.change(nameInput, { target: { value: 'Bob' } });
    fireEvent.click(screen.getByText('Aggiungi'));

    // step 2 → Avanti (step 3)
    fireEvent.click(screen.getByText('Avanti'));
    // step 3 → Avanti (step 4 — fasi)
    fireEvent.click(screen.getByText('Avanti'));
    // step 4 → Salta (step 5 — summary)
    fireEvent.click(screen.getByText('Salta'));
    // Summary dovrebbe mostrare "Ordine turni"
    expect(screen.getByText('Ordine turni')).toBeInTheDocument();
  });
});

// ── S2: handleStart chiama updateTurnOrder ─────────────────────────────────────

describe('SessionWizardMobile — S2: handleStart chiama updateTurnOrder', () => {
  it('chiama updateTurnOrder con i playerIds in ordine dopo addPlayer', async () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    // Aggiungi secondo giocatore
    const nameInput = screen.getByLabelText('Nome nuovo giocatore');
    fireEvent.change(nameInput, { target: { value: 'Bob' } });
    fireEvent.click(screen.getByText('Aggiungi'));

    // step 2 → 3 → 4 → 5
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    fireEvent.click(screen.getByText('Avanti')); // → step 4
    fireEvent.click(screen.getByText('Salta')); // → step 5

    // Avvia partita
    fireEvent.click(screen.getByText('Inizia a Giocare'));

    await waitFor(() => {
      expect(mockApi.liveSessions.updateTurnOrder).toHaveBeenCalledWith('session-123', {
        playerIds: ['player-uuid-1', 'player-uuid-2'],
      });
    });
  });
});

// ── #4750: il creatore è il primo giocatore, collegato al suo account ─────────────

describe('SessionWizardMobile — #4750: «Aggiungi me»', () => {
  function addBob() {
    fireEvent.change(screen.getByLabelText('Nome nuovo giocatore'), { target: { value: 'Bob' } });
    fireEvent.click(screen.getByText('Aggiungi'));
  }

  function startFromPlayersStep() {
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    fireEvent.click(screen.getByText('Avanti')); // → step 4
    fireEvent.click(screen.getByText('Salta')); // → step 5
    fireEvent.click(screen.getByText('Inizia a Giocare'));
  }

  function addPlayerPayloads(): Array<Record<string, unknown>> {
    return mockApi.liveSessions.addPlayer.mock.calls.map(call => call[1]);
  }

  it('con l’utente noto il primo giocatore è lui, con la targhetta «Tu»', () => {
    currentUser.value = MARCO;
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    const row = screen.getByText('Marco Rossi').closest('[role="listitem"]');
    expect(row).toHaveTextContent('Tu');
    expect(screen.queryByText('Giocatore 1')).not.toBeInTheDocument();
    // Già in lista: non serve offrire di aggiungerlo.
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
    // «Io non gioco» è la sua uscita: accanto non c'è anche il cestino.
    expect(screen.getByRole('button', { name: 'Io non gioco' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rimuovi Marco Rossi' })).not.toBeInTheDocument();
  });

  it('alla partenza il primo giocatore porta il suo userId e gli altri no', async () => {
    currentUser.value = MARCO;
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    addBob();

    startFromPlayersStep();

    await waitFor(() => expect(mockApi.liveSessions.addPlayer).toHaveBeenCalledTimes(2));
    const [marco, bob] = addPlayerPayloads();
    expect(marco).toMatchObject({ displayName: 'Marco Rossi', userId: 'user-marco' });
    expect(bob).toMatchObject({ displayName: 'Bob' });
    expect(bob).not.toHaveProperty('userId');
  });

  it('«Io non gioco» lo toglie e nessun giocatore parte collegato', async () => {
    currentUser.value = MARCO;
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    fireEvent.click(screen.getByRole('button', { name: 'Io non gioco' }));
    expect(screen.queryByText('Marco Rossi')).not.toBeInTheDocument();
    addBob();
    startFromPlayersStep();

    await waitFor(() => expect(mockApi.liveSessions.addPlayer).toHaveBeenCalledTimes(1));
    expect(addPlayerPayloads().every(p => !('userId' in p))).toBe(true);
  });

  it('tolto, «Aggiungi me» lo rimette collegato', async () => {
    currentUser.value = MARCO;
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    fireEvent.click(screen.getByRole('button', { name: 'Io non gioco' }));
    addBob();

    fireEvent.click(screen.getByRole('button', { name: 'Aggiungi me' }));
    expect(screen.getByText('Marco Rossi').closest('[role="listitem"]')).toHaveTextContent('Tu');
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
    startFromPlayersStep();

    await waitFor(() => expect(mockApi.liveSessions.addPlayer).toHaveBeenCalledTimes(2));
    expect(addPlayerPayloads()[0]).toMatchObject({ userId: 'user-marco' });
  });

  it('senza un utente noto resta il giocatore segnaposto, senza collegamento', async () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    expect(screen.getByText('Giocatore 1')).toBeInTheDocument();
    expect(screen.queryByText('Tu')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aggiungi me' })).not.toBeInTheDocument();
    startFromPlayersStep();

    await waitFor(() => expect(mockApi.liveSessions.addPlayer).toHaveBeenCalledTimes(1));
    expect(addPlayerPayloads()[0]).not.toHaveProperty('userId');
  });

  it('se l’utente arriva dopo il montaggio prende il posto del segnaposto', () => {
    const client = new QueryClient();
    const ui = () => (
      <QueryClientProvider client={client}>
        <SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />
      </QueryClientProvider>
    );
    const { rerender } = rtlRender(ui());
    expect(screen.getByText('Giocatore 1')).toBeInTheDocument();

    currentUser.value = MARCO;
    rerender(ui());

    expect(screen.queryByText('Giocatore 1')).not.toBeInTheDocument();
    expect(screen.getByText('Marco Rossi').closest('[role="listitem"]')).toHaveTextContent('Tu');
  });

  it('spostato dopo un altro giocatore è comunque aggiunto per primo, e i turni restano come scelti', async () => {
    // Il server dà il ruolo Host a chi entra per primo, e il riepilogo lo indica come
    // proprietario: il creatore collegato deve esserlo anche se in tavola siede secondo.
    currentUser.value = MARCO;
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    addBob();
    fireEvent.click(screen.getByText('Avanti')); // → step 3 (ordine turni)
    fireEvent.click(screen.getAllByRole('button', { name: /Sposta in basso/i })[0]); // Bob primo
    fireEvent.click(screen.getByText('Avanti')); // → step 4
    fireEvent.click(screen.getByText('Salta')); // → step 5
    fireEvent.click(screen.getByText('Inizia a Giocare'));

    await waitFor(() => expect(mockApi.liveSessions.updateTurnOrder).toHaveBeenCalled());
    const [first, second] = addPlayerPayloads();
    expect(first).toMatchObject({ displayName: 'Marco Rossi', userId: 'user-marco' });
    expect(second).toMatchObject({ displayName: 'Bob' });
    // I mock rispondono 'player-uuid-1' al primo addPlayer (Marco) e '-2' al secondo (Bob):
    // l'ordine dei turni è quello della lista, cioè Bob poi Marco.
    expect(mockApi.liveSessions.updateTurnOrder).toHaveBeenCalledWith('session-123', {
      playerIds: ['player-uuid-2', 'player-uuid-1'],
    });
  });
});
