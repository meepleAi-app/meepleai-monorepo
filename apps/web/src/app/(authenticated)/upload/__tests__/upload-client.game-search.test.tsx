/**
 * UploadClient — la scelta del gioco cerca sul server (#4187)
 *
 * Il wizard offriva la prima pagina del catalogo senza ricerca. Ora `useGames` segue il testo
 * del `GamePicker`. Due cose devono reggere quando la ricerca cambia: il gioco già scelto resta
 * nel selettore, e il gioco confermato resta nominato, altrimenti il modulo di upload, che
 * dipende da lui, sparisce.
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const CATAN = {
  id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  title: 'Catan',
  createdAt: '2026-01-01',
};
const WINGSPAN = {
  id: '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
  title: 'Wingspan',
  createdAt: '2026-01-01',
};

vi.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => ({ user: { id: 'u1', email: 'tester@meepleai.app' }, loading: false }),
}));
vi.mock('@/hooks/useAdminRole', () => ({ useAdminRole: () => ({ isAdminOrAbove: false }) }));

/** Il catalogo «sul server»: senza testo Catan, con un testo solo Wingspan. Catan resta fra i visti. */
const mockUseGames = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/wizard/useGames', () => ({ useGames: mockUseGames }));

vi.mock('@/hooks/wizard/usePdfs', () => ({
  usePdfs: () => ({ pdfs: [], loading: false, error: null, refetch: vi.fn() }),
}));

vi.mock('@/components/game/GamePicker', () => ({
  GamePicker: ({
    games,
    onSearchChange,
  }: {
    games: Array<{ id: string; title: string }>;
    onSearchChange?: (v: string) => void;
  }) => (
    <div data-testid="game-picker">
      <input aria-label="cerca nel finto picker" onChange={e => onSearchChange?.(e.target.value)} />
      <ul aria-label="giochi offerti">
        {games.map(g => (
          <li key={g.id}>{g.title}</li>
        ))}
      </ul>
    </div>
  ),
}));

vi.mock('@/components/pdf/PdfUploadForm', () => ({
  PdfUploadForm: () => <div data-testid="pdf-upload-form" />,
}));
vi.mock('@/components/progress/ProcessingProgress', () => ({ ProcessingProgress: () => null }));
vi.mock('@/components/pdf/PdfTable', () => ({ PdfTable: () => <div data-testid="pdf-table" /> }));
vi.mock('@/components/upload', () => ({ MultiFileUpload: () => <div data-testid="multi-file" /> }));
vi.mock('@/components/documents', () => ({
  MultiDocumentCollectionUpload: () => <div data-testid="collection-upload" />,
}));
vi.mock('@/components/ui/admin/upload-progress-tracker', () => ({
  UploadProgressTracker: () => <div data-testid="upload-progress-tracker" />,
}));

beforeEach(() => {
  mockUseGames.mockReset();
  mockUseGames.mockImplementation((_user: unknown, search?: string) =>
    search
      ? {
          games: [WINGSPAN],
          gamesById: { [CATAN.id]: CATAN, [WINGSPAN.id]: WINGSPAN },
          loading: false,
          createGame: vi.fn(),
        }
      : { games: [CATAN], gamesById: { [CATAN.id]: CATAN }, loading: false, createGame: vi.fn() }
  );
});

async function renderUpload() {
  const { UploadClient } = await import('../upload-client');
  const user = userEvent.setup();
  renderWithQuery(<UploadClient />);
  await screen.findByTestId('game-picker');
  return user;
}

describe('UploadClient — ricerca del gioco (#4187)', () => {
  it('quello che si scrive nel selettore arriva a useGames', async () => {
    const user = await renderUpload();

    await user.type(screen.getByLabelText('cerca nel finto picker'), 'Wing');

    expect(mockUseGames).toHaveBeenLastCalledWith(expect.anything(), 'Wing');
  });

  it('il gioco già scelto resta nel selettore anche se la ricerca non lo mostra', async () => {
    const user = await renderUpload();

    await user.type(screen.getByLabelText('cerca nel finto picker'), 'Wing');

    const offered = screen.getByRole('list', { name: 'giochi offerti' });
    expect(offered).toHaveTextContent('Catan');
    expect(offered).toHaveTextContent('Wingspan');
  });

  it('il gioco confermato resta nominato, e il modulo di upload resta, quando la ricerca cambia', async () => {
    const user = await renderUpload();
    await user.click(await screen.findByRole('button', { name: /confirm game selection/i }));
    expect(await screen.findByTestId('pdf-upload-form')).toBeInTheDocument();

    await user.type(screen.getByLabelText('cerca nel finto picker'), 'Wing');

    expect(screen.getByTestId('pdf-upload-form')).toBeInTheDocument();
  });
});
