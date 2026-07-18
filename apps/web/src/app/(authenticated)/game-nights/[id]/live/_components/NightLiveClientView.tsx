'use client';

/**
 * NightLiveClientView — #2633 Slice B.
 *
 * Backend-driven read-only projection of the night-live hub. The header + planned
 * line-up come from `useGameNightLive` (GET /game-nights/{id}/live → mapped
 * NightLiveViewModel); no fixtures remain. currentGame + the diary arrives in
 * Slice C (the mapper emits null/[] today); the LIVE badge + blocked modal is
 * Slice D. Drive controls are hidden (read-only, LD-13).
 *
 * States: loading skeleton · error taxonomy (LD-10) · terminal-night routing
 * (LD-14) · empty-published happy path (LD-11).
 */

import { useCallback, useEffect, useState, type ReactNode } from 'react';

import { useRouter } from 'next/navigation';

import {
  BlockedLiveSessionModal,
  NightLiveHub,
  WinnerPickerModal,
} from '@/components/features/game-nights/live';
import { useCompleteGameNight } from '@/hooks/queries/useSessionFlow';
import { useResponsive } from '@/hooks/useResponsive';
import {
  ConflictError,
  ForbiddenError,
  NotFoundError,
  UnauthorizedError,
} from '@/lib/api/core/errors';
import { useCompleteGameNightSession } from '@/lib/game-nights/hooks/useCompleteGameNightSession';
import { useGameNightLive } from '@/lib/game-nights/hooks/useGameNightLive';
import { useNightLiveDiary } from '@/lib/game-nights/hooks/useNightLiveDiary';
import { isMaxLiveBlockedError, useStartNextGame } from '@/lib/game-nights/hooks/useStartNextGame';
import { mapDiary } from '@/lib/game-nights/mapDiary';

export interface NightLiveClientViewProps {
  readonly nightId: string;
}

// WS-A (#3150): below lg the hub renders an in-flow 3-tab bar (~4rem: py-2.5 + border + icon/label)
// at the bottom; lift the fixed organizer CTAs above it + iOS safe-area. 4rem errs slightly tall so
// the CTA never overlaps the bar (a small gap is harmless). At lg there is no tab bar → reset to
// bottom-0. Fine-tune via visual QA at 390px if needed.
const CTA_BAR_POSITION =
  'fixed inset-x-0 z-40 p-4 bottom-[calc(4rem_+_env(safe-area-inset-bottom))] lg:bottom-0';

export function NightLiveClientView({ nightId }: NightLiveClientViewProps) {
  const router = useRouter();
  // WS-A (#3150): NightLiveHub already ships a mobile layout (3-tab bottom nav) — activate it
  // below lg. useResponsive is SSR-safe (isDesktop=false until hydrated); the data-loading
  // skeleton masks the pre-hydration mobile→desktop settle so there is no visible flash.
  const { isDesktop } = useResponsive();
  const { data: vm, isLoading, isError, error } = useGameNightLive(nightId);
  // Slice C2: the diary is a SEPARATE participant-guarded read; composed with the live VM
  // below via mapDiary (panel D2). Its own error/loading is non-fatal — the hub renders an
  // empty diary rather than blocking the live view.
  const { data: diaryDto } = useNightLiveDiary(nightId);

  const handleBack = useCallback(() => {
    router.push(`/game-nights/${nightId}`);
  }, [router, nightId]);

  const handleJumpToSession = useCallback(
    (sessionId: string) => {
      router.push(`/sessions/${sessionId}`);
    },
    [router]
  );

  const handleLogin = useCallback(() => {
    router.push('/login');
  }, [router]);

  // WS1 DEC-10: organizer-only interactive start. The mutation is server-authoritative —
  // on success we invalidate the read model (no optimistic flip); a max-1-live 409 surfaces
  // the blocked modal instead of a generic toast.
  const startNextGame = useStartNextGame(nightId);
  const [blockedModalOpen, setBlockedModalOpen] = useState(false);
  const handleStartNext = useCallback(
    (gameId: string, gameTitle: string) => {
      startNextGame.mutate(
        { gameId, gameTitle },
        {
          onError: err => {
            if (isMaxLiveBlockedError(err)) setBlockedModalOpen(true);
          },
        }
      );
    },
    [startNextGame]
  );

  // #2634 C4: organizer completes the live game, optionally picking a winner from the guarded
  // roster. Complementary to the WS1 start CTA (live → Completa → transition → Avvia prossimo).
  const completeGame = useCompleteGameNightSession(nightId);
  const [winnerPickerOpen, setWinnerPickerOpen] = useState(false);
  const handleCompleteConfirm = useCallback(
    (winnerId?: string) => {
      completeGame.mutate({ winnerId }, { onSuccess: () => setWinnerPickerOpen(false) });
    },
    [completeGame]
  );

  // #2634 SI-3: the organizer concludes the WHOLE night (in-progress → completed) once every game
  // is done and none is live. POST /complete (useCompleteGameNight) is the single finalize path; on
  // success the read model refetches and the LD-14 effect below routes to /summary. Complementary
  // to — and mutually exclusive with — the per-session start/Completa CTAs (this one is night-level,
  // live-only CTAs are per-session).
  const finalizeNight = useCompleteGameNight();

  // LD-14: a Completed night must not render a fake-live hub over stale data —
  // send the user to the summary they actually want. The effect fires only once
  // the read model confirms the terminal status.
  const isCompleted = vm?.nightStatus === 'Completed';
  useEffect(() => {
    if (isCompleted) {
      router.replace(`/game-nights/${nightId}/summary`);
    }
  }, [isCompleted, router, nightId]);

  if (isLoading) {
    return <NightLiveSkeleton />;
  }

  if (isError) {
    return <NightLiveError error={error} onBack={handleBack} onLogin={handleLogin} />;
  }

  if (!vm) {
    return null;
  }

  // LD-14: terminal / non-viewable lifecycle states.
  if (vm.nightStatus === 'Completed') {
    // router.replace has been scheduled; render a neutral placeholder meanwhile.
    return (
      <NightLiveNotice
        title="Serata conclusa"
        body="Ti stiamo portando al riepilogo…"
        onBack={handleBack}
      />
    );
  }
  if (vm.nightStatus === 'Cancelled') {
    return (
      <NightLiveNotice
        title="Serata annullata"
        body="Questa serata è stata annullata e non ha una modalità live."
        onBack={handleBack}
      />
    );
  }
  if (vm.nightStatus === 'Draft') {
    return (
      <NightLiveNotice
        title="Serata non ancora avviata"
        body="Pubblica la serata per aprirne la modalità live."
        onBack={handleBack}
      />
    );
  }

  // Published → the read-only live hub + the organizer's interactive start CTA (WS1).
  const nextGame = vm.nextGame;
  const liveSessionId = vm.currentGame?.sessionId ?? null;
  // DEC-10: hidden when a game is already live (status==='live') — you can only start the
  // next game when the current one is over. The 409 modal is the backstop for races.
  const showStartCta = vm.isViewerOrganizer && nextGame !== null && vm.status !== 'live';
  // #2634 C4: the organizer completes the live game. Mutually exclusive with the start CTA above.
  const showCompleteCta = vm.isViewerOrganizer && vm.status === 'live';
  const completeErrorMessage = completeGame.isError
    ? completeGame.error instanceof ForbiddenError
      ? 'Solo l’organizzatore può completare la partita.'
      : completeGame.error instanceof ConflictError
        ? 'Non è possibile completare ora (nessuna partita live o vincitore non valido).'
        : 'Impossibile completare la partita. Riprova.'
    : null;

  // #2634 SI-3: the night-level finalize CTA. Gated on nightStatus==='InProgress' because POST
  // /complete is InProgress-only (409 on a still-Published night not yet promoted by its first
  // session). Also requires no live game (status!=='live') and nothing left to start (nextGame
  // null) — so it never overlaps the per-session start CTA (needs nextGame) or Completa CTA
  // (live-only). plannedGames>0 is a sanity floor (a night with a lineup).
  const showFinalizeCta =
    vm.isViewerOrganizer &&
    vm.status !== 'live' &&
    nextGame === null &&
    vm.nightStatus === 'InProgress' &&
    vm.plannedGames.length > 0;
  const finalizeErrorMessage = finalizeNight.isError
    ? finalizeNight.error instanceof ForbiddenError
      ? 'Solo l’organizzatore può concludere la serata.'
      : finalizeNight.error instanceof ConflictError
        ? 'Non è possibile concludere ora.'
        : 'Impossibile concludere la serata. Riprova.'
    : null;

  // Slice C2 (panel D2): compose the diary here, keyed off the live sessionId→game lookup.
  // A pending/errored diary read yields empty arrays — the hub degrades to an empty diary
  // rather than blocking the live view (AC6).
  const diary = diaryDto
    ? mapDiary(diaryDto, vm.sessions)
    : { diaryEvents: [], diaryGames: [], diaryPlayers: [] };

  return (
    <>
      <NightLiveHub
        readOnly
        mobile={!isDesktop}
        night={vm.night}
        status={vm.status}
        current={vm.current}
        total={vm.total}
        elapsed={vm.elapsed}
        confirmedPlayers={vm.confirmedPlayers}
        totalPlayers={vm.totalPlayers}
        plannedGames={vm.plannedGames}
        currentGame={vm.currentGame}
        diaryEvents={diary.diaryEvents}
        diaryGames={diary.diaryGames}
        diaryPlayers={diary.diaryPlayers}
        onBack={handleBack}
        onJumpToSession={handleJumpToSession}
      />

      {showStartCta && nextGame ? (
        <div className={`${CTA_BAR_POSITION} flex justify-center`}>
          <button
            type="button"
            onClick={() => handleStartNext(nextGame.gameId, nextGame.gameTitle)}
            disabled={startNextGame.isPending}
            className="flex items-center gap-2 rounded-full border border-entity-session/40 bg-entity-session px-5 py-2.5 font-display text-sm font-extrabold text-white shadow-lg transition hover:bg-entity-session/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {startNextGame.isPending ? 'Avvio…' : `▶ Avvia: ${nextGame.gameTitle}`}
          </button>
        </div>
      ) : null}

      {showCompleteCta ? (
        <div className={`${CTA_BAR_POSITION} flex justify-center`}>
          <button
            type="button"
            onClick={() => {
              completeGame.reset(); // clear a stale 409/403 from a previous attempt before re-opening
              setWinnerPickerOpen(true);
            }}
            disabled={completeGame.isPending}
            className="flex items-center gap-2 rounded-full border border-entity-session/40 bg-entity-session px-5 py-2.5 font-display text-sm font-extrabold text-white shadow-lg transition hover:bg-entity-session/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            🏁 Completa partita
          </button>
        </div>
      ) : null}

      {showFinalizeCta ? (
        <div className={`${CTA_BAR_POSITION} flex flex-col items-center gap-2`}>
          {finalizeErrorMessage ? (
            <p
              role="alert"
              className="rounded-md border border-border bg-card px-3 py-1.5 font-mono text-xs font-semibold text-destructive shadow"
            >
              {finalizeErrorMessage}
            </p>
          ) : null}
          <button
            type="button"
            onClick={() => finalizeNight.mutate(nightId)}
            disabled={finalizeNight.isPending}
            className="flex items-center gap-2 rounded-full border border-entity-event/40 bg-entity-event px-5 py-2.5 font-display text-sm font-extrabold text-white shadow-lg transition hover:bg-entity-event/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            🏁 Concludi serata
          </button>
        </div>
      ) : null}

      <WinnerPickerModal
        open={winnerPickerOpen}
        candidates={vm.winnerCandidates}
        pending={completeGame.isPending}
        errorMessage={winnerPickerOpen ? completeErrorMessage : null}
        onCancel={() => setWinnerPickerOpen(false)}
        onConfirm={handleCompleteConfirm}
      />

      <BlockedLiveSessionModal
        open={blockedModalOpen}
        onClose={() => setBlockedModalOpen(false)}
        onJumpToLive={liveSessionId ? () => handleJumpToSession(liveSessionId) : undefined}
      />
    </>
  );
}

function NightLiveSkeleton() {
  return (
    <div
      role="status"
      aria-label="Caricamento serata live"
      aria-live="polite"
      className="flex h-full min-h-0 flex-col gap-3 bg-background p-4"
    >
      <div className="h-16 w-full animate-pulse rounded-md bg-muted" />
      <div className="flex flex-1 gap-3">
        <div className="h-full w-[280px] shrink-0 animate-pulse rounded-md bg-muted" />
        <div className="h-full flex-1 animate-pulse rounded-md bg-muted" />
        <div className="h-full w-[320px] shrink-0 animate-pulse rounded-md bg-muted" />
      </div>
      <span className="sr-only">Caricamento serata live…</span>
    </div>
  );
}

/** LD-10: distinct copy per error class; a generic fallback for the rest. */
function NightLiveError({
  error,
  onBack,
  onLogin,
}: {
  error: unknown;
  onBack: () => void;
  onLogin: () => void;
}) {
  let title = 'Qualcosa è andato storto';
  let body = 'Non è stato possibile caricare la serata live. Riprova più tardi.';
  // LD-10: a lapsed session (401) is a dead end without a recovery path — offer
  // an explicit login action so the user is not stuck behind another auth wall.
  let primaryAction: { label: string; onClick: () => void } | undefined;

  if (error instanceof UnauthorizedError) {
    title = 'Sessione scaduta';
    body = 'Effettua di nuovo l’accesso per vedere la serata live.';
    primaryAction = { label: 'Accedi di nuovo', onClick: onLogin };
  } else if (error instanceof ForbiddenError) {
    title = 'Accesso riservato';
    body = 'Solo l’organizzatore o un giocatore invitato può vedere questa serata.';
  } else if (error instanceof NotFoundError) {
    title = 'Serata non trovata';
    body = 'La serata richiesta non esiste o è stata rimossa.';
  } else if (
    error instanceof Error &&
    (error.name === 'NetworkError' || error.name === 'CircuitBreakerError')
  ) {
    title = 'Connessione persa';
    body = 'Controlla la connessione e riprova.';
  }

  return (
    <NightLiveNotice
      title={title}
      body={body}
      onBack={onBack}
      tone="error"
      primaryAction={primaryAction}
    />
  );
}

function NightLiveNotice({
  title,
  body,
  onBack,
  tone = 'neutral',
  primaryAction,
}: {
  title: string;
  body: ReactNode;
  onBack: () => void;
  tone?: 'neutral' | 'error';
  primaryAction?: { label: string; onClick: () => void };
}) {
  return (
    <div className="flex h-full min-h-0 flex-col items-center justify-center gap-3 bg-background p-6 text-center">
      <h1
        className={[
          'font-display text-xl font-extrabold',
          tone === 'error' ? 'text-[hsl(var(--c-danger))]' : 'text-foreground',
        ].join(' ')}
      >
        {title}
      </h1>
      <p className="max-w-sm font-mono text-sm text-muted-foreground">{body}</p>
      <div className="mt-2 flex items-center gap-2">
        {primaryAction ? (
          <button
            type="button"
            onClick={primaryAction.onClick}
            className="rounded-md border border-entity-session/30 bg-entity-session/10 px-4 py-2 font-display text-[13px] font-extrabold text-entity-session hover:bg-entity-session/15"
          >
            {primaryAction.label}
          </button>
        ) : null}
        <button
          type="button"
          onClick={onBack}
          className="rounded-md border border-border bg-card px-4 py-2 font-display text-[13px] font-extrabold text-foreground hover:bg-muted"
        >
          ← Torna alla serata
        </button>
      </div>
    </div>
  );
}
