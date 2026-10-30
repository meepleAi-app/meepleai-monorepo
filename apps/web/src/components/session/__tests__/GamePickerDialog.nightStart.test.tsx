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
  push: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    library: { getLibrary: mocks.getLibrary },
    gameNights: { startNextGame: mocks.startNextGame },
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
  });

  // #4752 (revisione): chiudere il dialogo (Esc, overlay, X) mentre la richiesta pende azzerava la mutazione, e
  // dopo il reset il `onSuccess` PER-CHIAMATA di `mutate` non scatta: la partita partiva sul server e nel
  // browser non succedeva nulla. La navigazione sta nel `onSuccess` dell'hook, che sopravvive al reset.
  it('se lo chiudo mentre la richiesta pende e poi riesce, la partita avviata porta alla pagina live', async () => {
    let resolveStart!: (value: unknown) => void;
    mocks.startNextGame.mockReturnValueOnce(
      new Promise(resolve => {
        resolveStart = resolve;
      })
    );
    const onOpenChange = vi.fn();
    const { setOpen } = renderDialog(onOpenChange);
    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    setOpen(false); // il genitore chiude il dialogo: Esc, overlay o X
    resolveStart({
      sessionId: 's1',
      gameNightSessionId: 'gns-1',
      sessionCode: 'ABC123',
      playOrder: 1,
    });

    await waitFor(() => expect(mocks.push).toHaveBeenCalledWith(`/game-nights/${NIGHT_ID}/live`));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('se lo chiudo mentre pende e poi la richiesta fallisce, non naviga né mostra errori alla riapertura', async () => {
    let rejectStart!: (reason: unknown) => void;
    mocks.startNextGame.mockReturnValueOnce(
      new Promise((_resolve, reject) => {
        rejectStart = reject;
      })
    );
    const { setOpen } = renderDialog();
    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    setOpen(false);
    rejectStart(new Error('boom'));
    await waitFor(() => expect(mocks.startNextGame).toHaveBeenCalledTimes(1));
    setOpen(true);

    await screen.findByRole('button', { name: 'Catan' });
    expect(mocks.push).not.toHaveBeenCalled();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
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
    // #4752 (revisione): quota di sessioni, serata già conclusa, tetto di partite: il backend li rimappa in
    // un 409 con il suo messaggio e senza codice. Dire «Riprova» sarebbe falso (non può riuscire) e
    // scartare il messaggio nasconderebbe il motivo.
    [
      'il server rifiuta con un 409 senza codice',
      new ConflictError({ message: 'Cannot add sessions to a Completed game night.' }),
      /cannot add sessions to a completed game night/i,
    ],
    [
      'il server rifiuta con un 409 il cui codice non è noto',
      new ConflictError({ message: 'Hai raggiunto il limite di sessioni.', code: 'conflict' }),
      /hai raggiunto il limite di sessioni/i,
    ],
  ])('se %s lo dice nel dialogo, che resta aperto', async (_caso, error, expected) => {
    mocks.startNextGame.mockRejectedValue(error);
    const onOpenChange = vi.fn();
    renderDialog(onOpenChange);

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(expected);
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
    expect(mocks.push).not.toHaveBeenCalled();
  });

  it('un 409 senza codice noto non invita a riprovare: un nuovo tentativo non può riuscire', async () => {
    mocks.startNextGame.mockRejectedValue(
      new ConflictError({ message: 'Cannot add sessions to a Completed game night.' })
    );
    renderDialog();

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    expect(await screen.findByRole('alert')).not.toHaveTextContent(/riprova/i);
  });

  it('un 409 il cui messaggio è solo il codice macchina non lo mostra come motivo', async () => {
    mocks.startNextGame.mockRejectedValue(
      new ConflictError({ message: 'conflict', code: 'conflict' })
    );
    renderDialog();

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    const alert = await screen.findByRole('alert');
    expect(alert).not.toHaveTextContent(/^conflict$/i);
    expect(alert).toHaveTextContent(/non è possibile avviare la partita/i);
  });

  it('un 409 il cui messaggio è solo quello generico del client non lo mostra come motivo', async () => {
    mocks.startNextGame.mockRejectedValue(
      new ConflictError({ message: 'API /api/v1/game-nights/n1/sessions failed with status 409' })
    );
    renderDialog();

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    const alert = await screen.findByRole('alert');
    expect(alert).not.toHaveTextContent(/failed with status/i);
    expect(alert).not.toHaveTextContent(/riprova/i);
    expect(alert).toHaveTextContent(/non è possibile avviare la partita/i);
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
