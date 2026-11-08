/**
 * SessionCreateForm — the game field, with the real GamePicker (#4250).
 *
 * The old `GameCombobox` looked the chosen game up among the results of the CURRENT
 * search, then cleared the search on select: after the debounce the trigger fell back to
 * its placeholder, and a game that arrived preset (edit page, game-night prefill) never
 * showed its name. Its tests could not see it: the search mock answered the same results
 * whatever the text. Here the mocked server filters by the text it is asked for.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { playRecordsNewMessages } from '@/__tests__/fixtures/i18n-test-messages';
import { api } from '@/lib/api';

import { SessionCreateForm } from '../SessionCreateForm';

vi.mock('@/lib/api', () => ({
  api: {
    library: { getLibrary: vi.fn() },
    sharedGames: { search: vi.fn() },
  },
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) =>
      (playRecordsNewMessages as Record<string, string>)[
        key.replace('playRecords.new.', '').replace('playRecords.edit.', '')
      ] ?? key,
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/lib/hooks/useMediaQuery', () => ({
  useMediaQuery: () => true,
}));

vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: 0 },
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: vi.fn(),
    setSessionField: vi.fn(),
  }),
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: undefined }),
}));

const CATAN_ID = '11111111-1111-4111-8111-111111111111';
const AZUL_ID = '22222222-2222-4222-8222-222222222222';
const LIBRARY = [
  { gameId: AZUL_ID, gameTitle: 'Azul' },
  { gameId: CATAN_ID, gameTitle: 'Catan' },
];

const mockGetLibrary = vi.mocked(api.library.getLibrary);
const mockSharedSearch = vi.mocked(api.sharedGames.search);

/** The library endpoint as it behaves: it filters by title. */
function serveLibrary(params?: { search?: string }) {
  const term = params?.search?.toLowerCase();
  const items = LIBRARY.filter(g => !term || g.gameTitle.toLowerCase().includes(term));
  return Promise.resolve({
    items,
    page: 1,
    pageSize: 10,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  } as never);
}

function renderForm(props: Partial<Parameters<typeof SessionCreateForm>[0]> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <SessionCreateForm onSubmit={vi.fn()} onCancel={vi.fn()} isSubmitting={false} {...props} />
    </QueryClientProvider>
  );
}

async function settle(ms = 400) {
  await act(async () => {
    vi.advanceTimersByTime(ms);
  });
}

describe('SessionCreateForm — campo del gioco (#4250)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockGetLibrary.mockImplementation(serveLibrary as never);
    mockSharedSearch.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 } as never);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('il gioco scelto resta mostrato, anche dopo che la ricerca si è assestata', async () => {
    renderForm();

    fireEvent.change(screen.getByTestId('play-record-game-input'), { target: { value: 'cat' } });
    await settle();
    fireEvent.click(await screen.findByTestId(`play-record-game-result-${CATAN_ID}`));

    expect(screen.getByTestId('play-record-game-selected')).toHaveTextContent('Catan');

    // Il difetto compariva qui: passato il debounce il pulsante tornava al segnaposto.
    await settle(1500);

    expect(screen.getByTestId('play-record-game-selected')).toHaveTextContent('Catan');
  });

  it('un gioco già impostato mostra il suo nome senza alcuna ricerca', async () => {
    renderForm({
      initialValues: { gameType: 'catalog', gameId: CATAN_ID, gameName: 'Catan' },
    });
    await settle();

    expect(screen.getByTestId('play-record-game-selected')).toHaveTextContent('Catan');
    expect(mockGetLibrary).not.toHaveBeenCalled();
  });

  it('a campo vuoto elenca la libreria, in ordine di titolo', async () => {
    renderForm();

    expect(await screen.findByTestId(`play-record-game-result-${AZUL_ID}`)).toBeInTheDocument();
    expect(screen.getByTestId(`play-record-game-result-${CATAN_ID}`)).toBeInTheDocument();
    expect(mockGetLibrary).toHaveBeenCalledWith(
      expect.objectContaining({ sortBy: 'title', sortDescending: false })
    );
  });

  it('la «×» toglie il gioco e riporta al campo di ricerca', async () => {
    renderForm({
      initialValues: { gameType: 'catalog', gameId: CATAN_ID, gameName: 'Catan' },
    });
    await settle();

    fireEvent.click(screen.getByTestId('play-record-game-clear'));

    expect(screen.queryByTestId('play-record-game-selected')).not.toBeInTheDocument();
    expect(screen.getByTestId('play-record-game-input')).toHaveValue('');
  });

  it('in modifica il gioco è mostrato ma non si può togliere', async () => {
    renderForm({
      mode: 'edit',
      initialValues: { gameType: 'catalog', gameId: CATAN_ID, gameName: 'Catan' },
    });
    await settle();

    expect(screen.getByTestId('play-record-game-selected')).toHaveTextContent('Catan');
    expect(screen.queryByTestId('play-record-game-clear')).not.toBeInTheDocument();
  });
});
