import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { PrivateGameHub } from '../PrivateGameHub';

/**
 * Issue #4137 — l'ingresso per fare una domanda su un gioco privato.
 *
 * Il backend concede al proprietario l'accesso al KB del proprio PrivateGame, ma
 * nessuna superficie poteva chiedere: questo hub non aveva né pannello di chat né
 * un ingresso, quindi il permesso era inesercitabile. Questo file copre solo quel
 * cablaggio — la resa della CTA è già coperta da `ActivationChecklist.test.tsx`.
 *
 * Il pannello è globale e scopato per gioco (#4139 ha spostato lo stream da un id
 * agente a `/agents/qa/stream`), quindi ciò che conta è con quale id viene aperto.
 */

const openChatPanel = vi.fn();

vi.mock('@/hooks/useChatPanel', () => ({
  useChatPanel: () => ({ open: openChatPanel, close: vi.fn(), isOpen: false, gameContext: null }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn(), back: vi.fn() }),
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }));

vi.mock('@/hooks/queries/useLibrary', () => ({
  usePrivateGame: () => ({
    data: { id: 'pg-1', title: 'Il Mio Gioco', imageUrl: null },
    isLoading: false,
  }),
}));

// Il PDF e' indicizzato: e' la condizione su cui la CTA e' gated, perche' e' anche
// quella che decide se una risposta e' possibile.
vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      getDocumentsByGame: vi.fn().mockResolvedValue([{ processingState: 'Indexed' }]),
    },
    liveSessions: { getActive: vi.fn().mockResolvedValue([]) },
    pdf: { uploadPdf: vi.fn() },
  },
}));

vi.mock('@/components/game-night/PlayerSetupDialog', () => ({
  PlayerSetupDialog: () => null,
}));
vi.mock('@/components/pdf/CopyrightDisclaimerModal', () => ({
  CopyrightDisclaimerModal: () => null,
}));
vi.mock('@/components/pdf/PdfProcessingProgressBar', () => ({
  PdfProcessingProgressBar: () => null,
}));
vi.mock('@/components/pdf/progress-card', () => ({ ProgressCard: () => null }));

describe('PrivateGameHub — ingresso alla domanda (#4137)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('apre il pannello di chat scopato sul GIOCO PRIVATO, non su un agente', async () => {
    const user = userEvent.setup();
    render(<PrivateGameHub privateGameId="pg-1" />);

    const cta = await screen.findByRole('button', { name: /Prova una domanda/i });
    await user.click(cta);

    await waitFor(() => expect(openChatPanel).toHaveBeenCalledTimes(1));
    expect(openChatPanel).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'pg-1', name: 'Il Mio Gioco', kbStatus: 'ready' })
    );
  });

  it('non offre la domanda finche il KB non e indicizzato', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.documents.getDocumentsByGame).mockResolvedValueOnce([
      { processingState: 'Embedding' },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    ] as any);

    render(<PrivateGameHub privateGameId="pg-1" />);

    // Il passo KB resta attivo, quindi la CTA non compare: senza indice la chat
    // non potrebbe rispondere, ed e' la stessa condizione del backend.
    await waitFor(() => expect(screen.getByTestId('step-pdf')).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: /Prova una domanda/i })).not.toBeInTheDocument();
    expect(openChatPanel).not.toHaveBeenCalled();
  });
});
