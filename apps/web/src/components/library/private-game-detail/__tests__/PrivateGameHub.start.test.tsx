/**
 * PrivateGameHub — #4770: avviando una partita a un gioco privato, il creatore collegato al suo
 * account entra per primo, perché il server fa Host il primo giocatore (#4750).
 */
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { PrivateGameHub } from '../PrivateGameHub';

const routerPush = vi.hoisted(() => vi.fn());
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: routerPush, replace: vi.fn(), prefetch: vi.fn(), back: vi.fn() }),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }));
vi.mock('@/hooks/useChatPanel', () => ({
  useChatPanel: () => ({ open: vi.fn(), close: vi.fn(), isOpen: false, gameContext: null }),
}));
vi.mock('@/hooks/queries/useLibrary', () => ({
  usePrivateGame: () => ({
    data: { id: 'pg-1', title: 'Il Mio Gioco', imageUrl: null, minPlayers: 1, maxPlayers: 6 },
    isLoading: false,
  }),
}));

const liveSessions = vi.hoisted(() => ({
  getActive: vi.fn(),
  createSession: vi.fn(),
  addPlayer: vi.fn(),
  startSession: vi.fn(),
}));
vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      getDocumentsByGame: vi.fn().mockResolvedValue([{ processingState: 'Indexed' }]),
    },
    liveSessions,
    pdf: { uploadPdf: vi.fn() },
  },
}));

// Il dialogo ha i suoi test: qui manda al hub i giocatori nell'ordine dei posti, con il creatore
// al secondo posto, per vedere in che ordine il hub li aggiunge.
vi.mock('@/components/game-night/PlayerSetupDialog', () => ({
  PlayerSetupDialog: ({ onStart }: { onStart: (p: unknown[]) => void }) => (
    <button
      type="button"
      onClick={() =>
        onStart([
          { displayName: 'Bob', color: 'Red' },
          { displayName: 'Marta', color: 'Blue', userId: 'user-marta' },
        ])
      }
    >
      conferma giocatori
    </button>
  ),
}));
vi.mock('@/components/pdf/CopyrightDisclaimerModal', () => ({
  CopyrightDisclaimerModal: () => null,
}));
vi.mock('@/components/pdf/PdfProcessingProgressBar', () => ({
  PdfProcessingProgressBar: () => null,
}));
vi.mock('@/components/pdf/progress-card', () => ({ ProgressCard: () => null }));

describe('PrivateGameHub — #4770: il creatore entra per primo', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    liveSessions.getActive.mockResolvedValue([]);
    liveSessions.createSession.mockResolvedValue('session-1');
    liveSessions.addPlayer.mockResolvedValue('player-x');
    liveSessions.startSession.mockResolvedValue(undefined);
  });

  it('aggiunge prima il giocatore collegato, con il suo account, poi gli altri', async () => {
    const user = userEvent.setup();
    render(<PrivateGameHub privateGameId="pg-1" />);

    await user.click(await screen.findByRole('button', { name: /inizia partita/i }));
    await user.click(await screen.findByRole('button', { name: 'conferma giocatori' }));
    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/sessions/session-1/live'));

    expect(liveSessions.createSession).toHaveBeenCalledWith({
      gameId: 'pg-1',
      gameName: 'Il Mio Gioco',
    });
    expect(liveSessions.addPlayer.mock.calls.map(c => c[1])).toEqual([
      { displayName: 'Marta', color: 'Blue', userId: 'user-marta' },
      { displayName: 'Bob', color: 'Red' },
    ]);
  });
});
