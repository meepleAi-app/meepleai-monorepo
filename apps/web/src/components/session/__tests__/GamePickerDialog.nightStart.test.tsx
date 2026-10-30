/**
 * @vitest-environment jsdom
 */

/**
 * #4752 — «Aggiungi partita» nel dettaglio di una serata faceva `POST /api/v1/games/{id}/sessions`,
 * una rotta che il backend non registra (405): il dialogo restava com'era, senza una parola.
 * La partita di una serata parte da `POST /api/v1/game-nights/{id}/sessions`, e un rifiuto si dice.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react';
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
    await act(async () => {
      rejectStart(new Error('boom'));
      await new Promise(resolve => setTimeout(resolve, 10)); // la richiesta si chiude a dialogo chiuso
    });
    setOpen(true);

    const catan = await screen.findByRole('button', { name: 'Catan' });
    expect(mocks.push).not.toHaveBeenCalled();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(catan).toBeEnabled(); // e il gioco si può scegliere di nuovo
  });

  // Riaperto mentre pende: se la richiesta fallisce adesso, chi guarda il dialogo deve saperlo.
  it('chiuso e riaperto mentre pende, se la richiesta fallisce l’errore si vede nel dialogo riaperto', async () => {
    let rejectStart!: (reason: unknown) => void;
    mocks.startNextGame.mockReturnValueOnce(
      new Promise((_resolve, reject) => {
        rejectStart = reject;
      })
    );
    const { setOpen } = renderDialog();
    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    setOpen(false);
    setOpen(true);
    await act(async () => {
      rejectStart(new Error('boom'));
      await new Promise(resolve => setTimeout(resolve, 10));
    });

    expect(await screen.findByRole('alert')).toHaveTextContent(/impossibile avviare la partita/i);
    expect(screen.getByRole('button', { name: 'Catan' })).toBeEnabled();
  });

  // #4752 (revisione): reset() azzera isPending. Chiuso e riaperto mentre la prima richiesta pende, il gioco
  // tornava cliccabile e partivano due POST: il backend ne respinge una con 409, e il dialogo riaperto diceva
  // «C'è già una partita in corso» mentre la prima stava per portare via la pagina.
  it('chiuso e riaperto mentre la richiesta pende, non si può avviare una seconda partita', async () => {
    mocks.startNextGame.mockReturnValue(new Promise(() => {}));
    const { setOpen } = renderDialog();
    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    setOpen(false);
    setOpen(true);

    const catan = await screen.findByRole('button', { name: 'Catan' });
    expect(catan).toBeDisabled();
    await userEvent.click(catan);
    expect(mocks.startNextGame).toHaveBeenCalledTimes(1);
  });

  // #4752 (revisione): se il genitore viene smontato mentre pende (cambio di rotta) e la richiesta poi riesce,
  // la navigazione partiva lo stesso e riportava indietro chi se n'era andato.
  it('se il dialogo viene smontato mentre pende, la riuscita non naviga né chiude', async () => {
    let resolveStart!: (value: unknown) => void;
    mocks.startNextGame.mockReturnValueOnce(
      new Promise(resolve => {
        resolveStart = resolve;
      })
    );
    const onOpenChange = vi.fn();
    const { unmount } = renderDialog(onOpenChange);
    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    unmount();
    await act(async () => {
      resolveStart({
        sessionId: 's1',
        gameNightSessionId: 'gns-1',
        sessionCode: 'ABC123',
        playOrder: 1,
      });
      await new Promise(resolve => setTimeout(resolve, 10));
    });

    expect(mocks.push).not.toHaveBeenCalled();
    expect(onOpenChange).not.toHaveBeenCalled();
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
    // #4752 (revisione): quota di sessioni, serata che non ammette partite, tetto di partite: il backend li
    // rimappa in un 409 con il proprio testo, in inglese e senza codice. Il dialogo li dice in italiano.
    [
      'la quota di sessioni è esaurita',
      new ConflictError({
        message:
          'Concurrent session limit reached (3 sessions for free tier). Close an active session or upgrade your subscription.',
      }),
      /numero massimo di sessioni attive/i,
    ],
    [
      'la serata è conclusa',
      new ConflictError({ message: 'Cannot add sessions to a Completed game night.' }),
      /serata è già conclusa/i,
    ],
    [
      'la serata è finalizzata',
      new ConflictError({
        message: 'Cannot add sessions to a finalized game night — start a new session.',
      }),
      /serata è già conclusa/i,
    ],
    [
      'la serata è annullata',
      new ConflictError({ message: 'Cannot add sessions to a Cancelled game night.' }),
      /serata è stata annullata/i,
    ],
    [
      'la serata è ancora in bozza',
      new ConflictError({ message: 'Cannot add sessions to a Draft game night.' }),
      /non è ancora pubblicata/i,
    ],
    [
      'la serata ha raggiunto il tetto di partite',
      new ConflictError({ message: 'A game night can have at most 5 sessions.' }),
      /al massimo 5 partite/i,
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

  // #4752 (revisione): il catch del backend trasforma QUALUNQUE InvalidOperationException (anche di EF o
  // Npgsql) in un 409 con `ex.Message`: un testo interno non deve arrivare all'utente. Solo i messaggi
  // noti si mappano; ogni altro 409 senza codice noto dice la frase generica, senza testo grezzo.
  it.each([
    ['un testo mai visto', new ConflictError({ message: 'Qualcosa di nuovo è successo.' })],
    [
      'un testo interno di EF o Npgsql',
      new ConflictError({
        message:
          'Npgsql.PostgresException (0x80004005): 23505: duplicate key value violates unique constraint "ix_game_night_sessions"',
      }),
    ],
    ['il solo codice macchina', new ConflictError({ message: 'conflict', code: 'conflict' })],
    [
      'il testo generico del client',
      new ConflictError({ message: 'API /api/v1/game-nights/n1/sessions failed with status 409' }),
    ],
    ['un messaggio vuoto', new ConflictError({ message: '' })],
  ])('un 409 con %s dice la frase generica e non rende il testo grezzo', async (_caso, error) => {
    mocks.startNextGame.mockRejectedValue(error);
    renderDialog();

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/non è possibile avviare la partita in questo momento/i);
    expect(alert.textContent).not.toBe(error.message);
    expect(alert).not.toHaveTextContent(
      /npgsql|duplicate key|failed with status|qualcosa di nuovo/i
    );
    expect(alert).not.toHaveTextContent(/riprova/i);
  });

  it('un 409 noto non invita a riprovare: un nuovo tentativo non può riuscire', async () => {
    mocks.startNextGame.mockRejectedValue(
      new ConflictError({ message: 'Cannot add sessions to a Completed game night.' })
    );
    renderDialog();

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    expect(await screen.findByRole('alert')).not.toHaveTextContent(/riprova/i);
  });

  it('il messaggio può andare a capo anche con una parola lunga', async () => {
    mocks.startNextGame.mockRejectedValue(new Error('boom'));
    renderDialog();

    await userEvent.click(await screen.findByRole('button', { name: 'Catan' }));

    expect(await screen.findByRole('alert')).toHaveClass('break-words');
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
