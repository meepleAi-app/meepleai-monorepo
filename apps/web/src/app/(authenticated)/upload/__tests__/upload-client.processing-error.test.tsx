/**
 * UploadClient — il fallimento dell'elaborazione deve restare leggibile (#3878)
 *
 * Il componente `ProcessingProgress` ha 27 test propri e li supera: segnala il
 * fallimento chiamando `onError`. Il difetto vive nel confine con la pagina, che
 * quel callback lo usa per SMONTARE il pannello e scrivere la causa in
 * `wizardState.processingError` — un campo che nessun ramo di render legge.
 *
 * Questi test montano la pagina, non il componente: sono l'unico punto da cui
 * quel difetto è visibile.
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

// ── Mocks ─────────────────────────────────────────────────────────────────────
// `useWizard` NON è mockato: il reducer reale è ciò che stiamo verificando.

const GAME_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const DOCUMENT_ID = '7d0f2b1a-1111-2222-3333-444455556666';
const BACKEND_ERROR = 'Extraction failed: page 3 is corrupted';

vi.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => ({ user: { id: 'u1', email: 'tester@meepleai.app' }, loading: false }),
}));

vi.mock('@/hooks/useAdminRole', () => ({
  useAdminRole: () => ({ isAdminOrAbove: false }),
}));

vi.mock('@/hooks/wizard/useGames', () => ({
  useGames: () => ({
    games: [{ id: GAME_ID, title: 'Catan' }],
    loading: false,
    createGame: vi.fn(),
  }),
}));

vi.mock('@/hooks/wizard/usePdfs', () => ({
  usePdfs: () => ({ pdfs: [], loading: false, error: null, refetch: vi.fn() }),
}));

vi.mock('@/components/game/GamePicker', () => ({
  GamePicker: () => <div data-testid="game-picker" />,
}));

/** Sostituisce il form di upload con un bottone che simula l'esito riuscito. */
vi.mock('@/components/pdf/PdfUploadForm', () => ({
  PdfUploadForm: ({ onUploadSuccess }: { onUploadSuccess: (id: string) => void }) => (
    <button type="button" onClick={() => onUploadSuccess(DOCUMENT_ID)}>
      simula upload riuscito
    </button>
  ),
}));

/**
 * Sostituisce il pannello di progresso con un bottone che segnala il fallimento.
 * Mockarlo è deliberato: costringe il messaggio a venire dalla PAGINA, che è ciò
 * che la DoD chiede («resta leggibile dopo la fine del polling»).
 */
vi.mock('@/components/progress/ProcessingProgress', () => ({
  ProcessingProgress: ({ onError }: { onError: (e: string) => void }) => (
    <button type="button" onClick={() => onError(BACKEND_ERROR)}>
      simula fallimento elaborazione
    </button>
  ),
}));

vi.mock('@/components/pdf/PdfTable', () => ({ PdfTable: () => <div data-testid="pdf-table" /> }));
vi.mock('@/components/upload', () => ({ MultiFileUpload: () => <div data-testid="multi-file" /> }));
vi.mock('@/components/documents', () => ({
  MultiDocumentCollectionUpload: () => <div data-testid="collection-upload" />,
}));
vi.mock('@/components/ui/admin/upload-progress-tracker', () => ({
  UploadProgressTracker: () => <div data-testid="upload-progress-tracker" />,
}));

// ── Helper ────────────────────────────────────────────────────────────────────

/**
 * Il flag è letto a livello di modulo (`upload-client.tsx:50`), quindi va
 * impostato PRIMA dell'import: da qui `resetModules` + import dinamico.
 */
async function renderUploadAtParseStep() {
  const { UploadClient } = await import('../upload-client');
  const user = userEvent.setup();
  renderWithQuery(<UploadClient />);

  await user.click(await screen.findByRole('button', { name: /confirm game selection/i }));
  await user.click(await screen.findByRole('button', { name: /simula upload riuscito/i }));

  return user;
}

describe("UploadClient — fallimento dell'elaborazione (#3878)", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('NEXT_PUBLIC_ENABLE_PROGRESS_UI', 'true');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('lascia sullo schermo la causa del fallimento riportata dal backend', async () => {
    const user = await renderUploadAtParseStep();

    await user.click(await screen.findByRole('button', { name: /simula fallimento/i }));

    expect(await screen.findByText(new RegExp(BACKEND_ERROR, 'i'))).toBeInTheDocument();
  });

  it('spiega perché il passo successivo è bloccato, invece di lasciare un bottone grigio', async () => {
    const user = await renderUploadAtParseStep();

    await user.click(await screen.findByRole('button', { name: /simula fallimento/i }));

    const parseButton = await screen.findByRole('button', { name: /parse pdf/i });
    await waitFor(() => expect(parseButton).toBeDisabled());
    expect(parseButton).toHaveAccessibleDescription(/fallita|failed/i);
  });
});
