/**
 * @vitest-environment jsdom
 */

/**
 * #4187 — GamePickerDialog chiedeva 200 giochi della libreria «per averli tutti», il
 * backend ne restituisce al massimo 100 senza dirlo, e il filtro lavorava sul client: un
 * gioco oltre la pagina non si trovava. Qui la libreria finta vive "sul server" e
 * Wingspan sta oltre i primi 100.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

import { GamePickerDialog } from '../GamePickerDialog';

const mockGetLibrary = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({ api: { library: { getLibrary: mockGetLibrary } } }));

vi.mock('@/components/ui/data-display/entity-list-view/hooks/use-debounce', () => ({
  useDebounce: (value: string) => value,
}));

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

// La riga del gioco carica la prontezza della KB: qui conta solo quali giochi compaiono.
vi.mock('../GamePickerItem', () => ({
  GamePickerItem: ({ game }: { game: { gameTitle: string } }) => (
    <div data-testid="picker-item">{game.gameTitle}</div>
  ),
}));

const LIBRARY = [
  ...Array.from({ length: 120 }, (_, i) => ({
    id: `entry-${i}`,
    gameId: `game-${i}`,
    gameTitle: `Agricola ${String(i).padStart(3, '0')}`,
  })),
  { id: 'entry-wingspan', gameId: 'game-wingspan', gameTitle: 'Wingspan' },
];

function serveLibrary(params?: { search?: string; pageSize?: number }) {
  const size = Math.min(params?.pageSize ?? 20, 100); // il tetto del repository
  const pool = params?.search
    ? LIBRARY.filter(e => e.gameTitle.toLowerCase().includes(params.search!.toLowerCase()))
    : LIBRARY;
  return Promise.resolve({
    items: pool.slice(0, size),
    page: 1,
    pageSize: size,
    totalCount: pool.length,
    totalPages: Math.ceil(pool.length / size),
    hasNextPage: pool.length > size,
    hasPreviousPage: false,
  });
}

function renderDialog() {
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <GamePickerDialog open onOpenChange={() => {}} gameNightEventId="night-1" />
    </QueryClientProvider>
  );
}

describe('GamePickerDialog — ricerca nella libreria (#4187)', () => {
  beforeEach(() => {
    mockGetLibrary.mockReset();
    mockGetLibrary.mockImplementation(serveLibrary);
  });

  it('scrivere «Wingspan» lo trova anche oltre i primi 100 giochi', async () => {
    const user = userEvent.setup();
    renderDialog();

    await screen.findAllByTestId('picker-item');
    await user.type(screen.getByTestId('game-picker-search'), 'Wingspan');

    expect(await screen.findByText('Wingspan')).toBeInTheDocument();
    expect(mockGetLibrary).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'Wingspan' })
    );
  });

  it('mentre arriva la risposta per un testo nuovo non dice che non ci sono giochi', async () => {
    const user = userEvent.setup();
    renderDialog();
    await screen.findAllByTestId('picker-item');

    await user.type(screen.getByTestId('game-picker-search'), 'zzz');
    expect(await screen.findByText('Nessun gioco trovato per la ricerca.')).toBeInTheDocument();

    // La risposta per «zzzq», testo mai chiesto, non arriva: a video resta la pagina vuota di
    // «zzz». Cancellare una lettera non basterebbe: «zz» e' gia' in cache, e li' la risposta vuota
    // e' quella vera.
    mockGetLibrary.mockImplementation(() => new Promise(() => {}));
    await user.type(screen.getByTestId('game-picker-search'), 'q');

    expect(screen.queryByText('Nessun gioco trovato per la ricerca.')).not.toBeInTheDocument();
    expect(screen.queryByText('Nessun gioco nella tua libreria.')).not.toBeInTheDocument();
  });

  it('non chiede più giochi di quanti il server ne restituisca', async () => {
    renderDialog();

    await screen.findAllByTestId('picker-item');
    for (const [params] of mockGetLibrary.mock.calls) {
      expect(params.pageSize).toBeLessThanOrEqual(100);
    }
  });

  it('il conteggio dice quanti giochi ha la libreria, non quanti ne ha scaricati', async () => {
    renderDialog();

    await screen.findAllByTestId('picker-item');
    expect(await screen.findByText(new RegExp(`di ${LIBRARY.length} giochi`))).toBeInTheDocument();
  });
});
