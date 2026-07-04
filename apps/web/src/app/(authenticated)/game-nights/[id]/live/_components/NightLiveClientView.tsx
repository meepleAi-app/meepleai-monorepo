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

import { BlockedLiveSessionModal, NightLiveHub } from '@/components/features/game-nights/live';
import { ForbiddenError, NotFoundError, UnauthorizedError } from '@/lib/api/core/errors';
import { useGameNightLive } from '@/lib/game-nights/hooks/useGameNightLive';
import { isMaxLiveBlockedError, useStartNextGame } from '@/lib/game-nights/hooks/useStartNextGame';

export interface NightLiveClientViewProps {
  readonly nightId: string;
}

export function NightLiveClientView({ nightId }: NightLiveClientViewProps) {
  const router = useRouter();
  const { data: vm, isLoading, isError, error } = useGameNightLive(nightId);

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

  return (
    <>
      <NightLiveHub
        readOnly
        night={vm.night}
        status={vm.status}
        current={vm.current}
        total={vm.total}
        elapsed={vm.elapsed}
        confirmedPlayers={vm.confirmedPlayers}
        totalPlayers={vm.totalPlayers}
        plannedGames={vm.plannedGames}
        currentGame={vm.currentGame}
        diaryEvents={vm.diaryEvents}
        diaryGames={vm.diaryGames}
        diaryPlayers={vm.diaryPlayers}
        onBack={handleBack}
        onJumpToSession={handleJumpToSession}
      />

      {showStartCta && nextGame ? (
        <div className="fixed inset-x-0 bottom-0 z-40 flex justify-center p-4">
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
