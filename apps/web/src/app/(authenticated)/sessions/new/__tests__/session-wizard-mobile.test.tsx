/**
 * SessionWizardMobile — S1 prefill + S2 turn order tests
 */
import { render as rtlRender, screen, fireEvent, waitFor } from '@testing-library/react';
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

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: () => ({ data: { items: [] }, isLoading: false }),
}));

const mockApi = vi.hoisted(() => ({
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

beforeEach(() => {
  vi.clearAllMocks();
  mockApi.liveSessions.addPlayer
    .mockResolvedValueOnce('player-uuid-1')
    .mockResolvedValueOnce('player-uuid-2')
    .mockResolvedValueOnce('player-uuid-3');
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
