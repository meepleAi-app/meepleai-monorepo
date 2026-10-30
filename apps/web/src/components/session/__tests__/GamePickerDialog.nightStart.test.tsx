/**
 * @vitest-environment jsdom
 */

/**
 * #4752 — «Aggiungi partita» nel dettaglio di una serata faceva `POST /api/v1/games/{id}/sessions`,
 * una rotta che il backend non registra (405): il dialogo restava com'era, senza una parola.
 * La partita di una serata parte da `POST /api/v1/game-nights/{id}/sessions`, e un rifiuto si dice.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { ApiError, ConflictError, ForbiddenError } from '@/lib/api/core/errors';

import { GamePickerDialog } from '../GamePickerDialog';

const NIGHT_ID = 'night-1';

const mocks = vi.hoisted(() => ({
  getLibrary: vi.fn(),
  startNextGame: vi.fn(),
  createSession: vi.fn(),
  push: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    library: { getLibrary: mocks.getLibrary },
    gameNights: { startNextGame: mocks.startNextGame },
    sessionFlow: { createSession: mocks.createSession },
  },
}));

vi.mock('@/components/ui/data-display/entity-list-view/hooks/use-debounce', () => ({
  useDebounce: (value: string) => value,
}));

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mocks.push }) }));

// La riga del gioco carica la prontezza della KB: qui conta solo che scegliere il gioco lo passi al dialogo.
vi.mock('../GamePickerItem', () => ({
  GamePickerItem: ({
    game,
    onSelect,
    isSelecting,
  }: {
    game: { gameId: string; gameTitle: string };
    onSelect: (gameId: string) => void;
    isSelecting: boolean;
  }) => (
    <button type="button" disabled={isSelecting} onClick={() => onSelect(game.gameId)}>
      {game.gameTitle}
    </button>
  ),
}));

function renderDialog(onOpenChange: (open: boolean) => void = () => {}, open = true) {
  const client = createTestQueryClient();
  const ui = (isOpen: boolean) => (
    <QueryClientProvider client={client}>
      <GamePickerDialog open={isOpen} onOpenChange={onOpenChange} gameNightEventId={NIGHT_ID} />
    </QueryClientProvider>
  );
  const view = render(ui(open));
  return { ...view, setOpen: (isOpen: boolean) => view.rerender(ui(isOpen)) };
}

describe('GamePickerDialog — avvio di una partita della serata (#4752)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getLibrary.mockResolvedValue({
      items: [{ id: 'entry-catan', gameId: 'game-catan', gameTitle: 'Catan' }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    });
    mocks.startNextGame.mockResolvedValue({
      sessionId: 's1',
      gameNightSessionId: 'gns-1',
      sessionCode: 'ABC123',
      playOrder: 1,
    });
  });

  it('avvia la partita sulla rotta della serata e porta alla pagina live', async () => {
    const onOpenChange = vi.fn();
    renderDialog(onOpenChange);

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    await waitFor(() => expect(mocks.push).toHaveBeenCalledWith(`/game-nights/${NIGHT_ID}/live`));
    expect(mocks.startNextGame).toHaveBeenCalledWith(NIGHT_ID, 'game-catan', 'Catan');
    expect(onOpenChange).toHaveBeenCalledWith(false);
    // La rotta che il backend non ha non si chiama più.
    expect(mocks.createSession).not.toHaveBeenCalled();
  });

  it.each([
    [
      'un’altra partita è già live',
      new ConflictError({ message: 'blocked', code: 'MAX_LIVE_SESSIONS_EXCEEDED' }),
      /già una partita in corso/i,
    ],
    [
      'il gioco non ha un regolamento pronto',
      new ApiError({ message: 'kb', statusCode: 422, code: 'kb_not_ready' }),
      /non ha ancora un regolamento pronto/i,
    ],
    ['non sei l’organizzatore', new ForbiddenError({ message: 'no' }), /solo l.organizzatore/i],
    ['il server risponde in modo imprevisto', new Error('boom'), /impossibile avviare la partita/i],
  ])('se %s lo dice nel dialogo, che resta aperto', async (_caso, error, expected) => {
    mocks.startNextGame.mockRejectedValue(error);
    const onOpenChange = vi.fn();
    renderDialog(onOpenChange);

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(expected);
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
    expect(mocks.push).not.toHaveBeenCalled();
  });

  it('un nuovo tentativo toglie il messaggio del precedente', async () => {
    mocks.startNextGame.mockRejectedValueOnce(new Error('boom'));
    renderDialog();
    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));
    await screen.findByRole('alert');

    mocks.startNextGame.mockReturnValueOnce(new Promise(() => {}));
    await userEvent.click(screen.getByRole('button', { name: 'Catan' }));

    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });

  it('riaprire il dialogo non rimostra l’errore di prima', async () => {
    mocks.startNextGame.mockRejectedValue(new Error('boom'));
    const onOpenChange = vi.fn();
    const { setOpen } = renderDialog(onOpenChange);
    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));
    await screen.findByRole('alert');

    setOpen(false);
    setOpen(true);

    await screen.findByRole('button', { name: 'Catan' });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
