/**
 * #4692 — ripetere la creazione di una serata non deve crearne una seconda.
 *
 * La pagina ritenta la creazione fino a quattro volte (1 s, 2 s, 4 s: lo chiede la specifica del wizard)
 * e React Query ne aggiunge un'altra. Il server riconosce la ripetizione solo se tutti i tentativi di UN
 * invio portano lo stesso `id`; un invio nuovo, invece, e' una creazione nuova.
 *
 * Il wizard e gli hook di contorno sono sostituiti: qui si prova `handleSubmit`, non il wizard.
 */

import { act, fireEvent, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { NewGameNightContent } from '../_content';

const h = vi.hoisted(() => ({
  mutateAsync: vi.fn(),
  push: vi.fn(),
  toast: vi.fn(),
  timeZone: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: h.push, replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams('step=4'),
}));
vi.mock('@/hooks/useTranslation', () => ({ useTranslation: () => ({ t: (k: string) => k }) }));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: h.toast }) }));
vi.mock('@/lib/utils/browserTimeZone', () => ({ getBrowserTimeZone: () => h.timeZone() }));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: { id: 'organizer-1', displayName: 'Organizzatore' } }),
}));
vi.mock('@/hooks/queries/useGameNights', () => ({
  useCreateGameNight: () => ({ mutateAsync: h.mutateAsync, isPending: false }),
}));
vi.mock('@/lib/game-nights/hooks/useGameNightDraftPersist', () => ({
  useGameNightDraftPersist: () => ({ initialDraft: null, isPending: false, clear: vi.fn() }),
}));
vi.mock('@/lib/game-nights/hooks/usePlayerSearch', () => ({
  usePlayerSearch: () => ({ data: [], isFetching: false }),
}));
vi.mock('@/lib/game-nights/hooks/useRegularsForUser', () => ({
  useRegularsForUser: () => ({ data: [] }),
}));
vi.mock('@/lib/game-nights/hooks/useGameNightConflictCheck', () => ({
  useGameNightConflictCheck: () => ({ data: undefined, isFetching: false }),
}));
vi.mock('../_lib/useGameNightCandidates', () => ({
  useGameNightCandidates: () => ({ games: [] }),
}));
vi.mock('@/components/layout/PageContainer', () => ({
  HubPageContainer: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));
vi.mock('@/lib/game-nights/wizard-validators', () => ({
  buildSubmitPayload: () => ({
    ok: true,
    payload: {
      title: 'Serata di prova',
      scheduledAt: '2030-01-01T20:00:00.000Z',
      description: null,
      location: null,
      maxPlayers: null,
      gameIds: null,
      invitedUserIds: null,
      invitedEmails: ['ospite@example.com'],
    },
  }),
}));
vi.mock('@/components/features/game-night-create', () => ({
  GameNightCreateWizard: ({ onSubmit }: { onSubmit: () => void }) => (
    <button type="button" data-testid="wizard-submit" onClick={onSubmit}>
      invia
    </button>
  ),
}));

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** I tentativi consecutivi di un invio dormono 1 s, 2 s e 4 s: con i timer finti si saltano. */
async function submitAndLetTheRetriesRun(): Promise<void> {
  fireEvent.click(screen.getByTestId('wizard-submit'));
  await act(async () => {
    await vi.advanceTimersByTimeAsync(10_000);
  });
}

const idsOfAttempts = (): string[] =>
  h.mutateAsync.mock.calls.map(([input]) => (input as { id: string }).id);

describe('NewGameNightContent — invio con retry (#4692)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    h.mutateAsync.mockReset();
    h.push.mockReset();
    h.toast.mockReset();
    h.timeZone.mockReset();
    h.timeZone.mockReturnValue('Europe/Rome');
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("manda lo stesso id a tutti i tentativi di un invio, e l'id e' un UUID", async () => {
    h.mutateAsync
      .mockRejectedValueOnce(new Error('rete'))
      .mockRejectedValueOnce(new Error('rete'))
      .mockResolvedValueOnce('night-1');
    render(<NewGameNightContent />);

    await submitAndLetTheRetriesRun();

    const ids = idsOfAttempts();
    expect(ids).toHaveLength(3);
    expect(new Set(ids).size).toBe(1);
    expect(ids[0]).toMatch(UUID);
    expect(h.push).toHaveBeenCalledWith('/game-nights/night-1');
  });

  it("usa lo stesso id anche quando i tentativi sono esauriti, e lo dice all'utente", async () => {
    h.mutateAsync.mockRejectedValue(new Error('rete'));
    render(<NewGameNightContent />);

    await submitAndLetTheRetriesRun();

    const ids = idsOfAttempts();
    expect(ids).toHaveLength(4);
    expect(new Set(ids).size).toBe(1);
    expect(h.push).not.toHaveBeenCalled();
    expect(h.toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive' }));
  });

  it("manda il fuso del browser insieme alla serata, perche' l'email dica 21:00 e non l'ora UTC (#4754)", async () => {
    h.mutateAsync.mockResolvedValueOnce('night-1');
    render(<NewGameNightContent />);

    await submitAndLetTheRetriesRun();

    expect(h.mutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({
        timeZoneId: 'Europe/Rome',
        scheduledAt: '2030-01-01T20:00:00.000Z',
      })
    );
  });

  it('se il browser non sa dire un fuso, lascia fuori il campo e la serata si crea lo stesso (#4754)', async () => {
    h.timeZone.mockReturnValue(undefined);
    h.mutateAsync.mockResolvedValueOnce('night-1');
    render(<NewGameNightContent />);

    await submitAndLetTheRetriesRun();

    expect(h.mutateAsync).toHaveBeenCalledTimes(1);
    expect(h.mutateAsync.mock.calls[0][0]).not.toHaveProperty('timeZoneId');
    expect(h.push).toHaveBeenCalledWith('/game-nights/night-1');
  });

  it("un invio nuovo e' una creazione nuova: un altro id", async () => {
    h.mutateAsync.mockResolvedValueOnce('night-1').mockResolvedValueOnce('night-2');
    render(<NewGameNightContent />);

    await submitAndLetTheRetriesRun();
    await submitAndLetTheRetriesRun();

    const ids = idsOfAttempts();
    expect(ids).toHaveLength(2);
    expect(ids[0]).toMatch(UUID);
    expect(ids[1]).toMatch(UUID);
    expect(ids[0]).not.toBe(ids[1]);
  });
});
