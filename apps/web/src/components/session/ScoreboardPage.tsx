/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
/**
 * ScoreboardPage — /sessions/{id}/scoreboard
 *
 * #4156: l'id della rotta è quello di una `LiveGameSession` (decisione D4). La classifica legge
 * la live e ordina i giocatori per totale decrescente; i totali sono quelli di `RoundScores`
 * (ADR-089 §1). Prima leggeva la `GameSession` con l'id di una live e riceveva 404, e anche
 * quando la trovava ordinava per ordine di ingresso, perché quella DTO non porta punteggi.
 *
 * I punteggi non si registrano qui: il pulsante porta alla scheda Score della pagina live. Il
 * modulo che stava al suo posto non salvava niente, chiudeva e basta.
 *
 * Task 3 — Sessions Redesign
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Trophy, Users } from 'lucide-react';
import Link from 'next/link';

import { SessionStateRenderer } from '@/components/features/session-live';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { PLAYER_COLOR_HEX } from '@/lib/constants/player-colors';

interface ScoreboardPageProps {
  sessionId: string;
}

type LivePlayer = LiveSessionDto['players'][number];

const STATUS_LABELS: Record<LiveSessionDto['status'], string> = {
  Created: 'In preparazione',
  Setup: 'In preparazione',
  InProgress: 'In corso',
  Paused: 'In pausa',
  Completed: 'Completata',
};

const NEUTRAL_BADGE = 'bg-muted text-foreground dark:bg-card dark:text-muted-foreground';

const STATUS_COLORS: Record<LiveSessionDto['status'], string> = {
  Created: NEUTRAL_BADGE,
  Setup: NEUTRAL_BADGE,
  InProgress: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-400',
  Paused: 'bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-400',
  Completed: NEUTRAL_BADGE,
};

// Avatar colors cycling for players whose color has no mapping
const FALLBACK_COLORS = [
  '#ef4444',
  '#3b82f6',
  '#22c55e',
  '#f97316',
  '#8b5cf6',
  '#ec4899',
  '#06b6d4',
  '#eab308',
];

const RANK_MEDAL: Record<number, string> = { 1: '🥇', 2: '🥈', 3: '🥉' };

interface RankedPlayer {
  player: LivePlayer;
  /** Classifica a competizione: a pari punti, stessa posizione. */
  rank: number;
}

/**
 * Chi gioca, per totale decrescente; a pari punti nell'ordine di ingresso. Gli spettatori non
 * segnano punti e non sono in classifica.
 */
function rankPlayers(session: LiveSessionDto): RankedPlayer[] {
  const ordered = session.players
    .filter(p => p.role !== 'Spectator')
    .map((player, joinIndex) => ({ player, joinIndex }))
    .sort((a, b) => b.player.totalScore - a.player.totalScore || a.joinIndex - b.joinIndex)
    .map(entry => entry.player);
  return ordered.map(player => ({
    player,
    rank: 1 + ordered.filter(other => other.totalScore > player.totalScore).length,
  }));
}

export function ScoreboardPage({ sessionId }: ScoreboardPageProps) {
  const {
    data: session,
    isPending,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: ['live-session-scoreboard', sessionId],
    queryFn: () => api.liveSessions.getSession(sessionId),
    refetchInterval: query => (query.state.status === 'error' ? false : 10_000),
    retry: false,
  });

  // ----------- Loading state (G7 SessionStateRenderer pilot — Issue #2356) -----------
  if (isPending) {
    return (
      <div data-testid="scoreboard-loading" className="min-h-screen bg-background p-4">
        <SessionStateRenderer
          state={{
            kind: 'loading',
            loadingAriaLabel: 'Caricamento classifica in corso',
          }}
        />
      </div>
    );
  }

  // ----------- Error state (G7 SessionStateRenderer pilot — Issue #2356) -----------
  if (isError || !session) {
    const fallbackError =
      error instanceof Error ? error : new Error('Impossibile caricare la sessione');
    return (
      <div
        data-testid="scoreboard-error"
        className="flex min-h-screen flex-col items-center justify-center gap-3 bg-background p-4 text-center"
      >
        <SessionStateRenderer
          state={{
            kind: 'error',
            error: fallbackError,
            onRetry: () => {
              void refetch();
            },
            errorTitle: 'Impossibile caricare la sessione',
            retryLabel: 'Riprova',
          }}
        />
        <Button variant="outline" size="sm" asChild>
          <Link href="/sessions">Torna alle sessioni</Link>
        </Button>
      </div>
    );
  }

  // ----------- Data prep -----------
  const ranked = rankPlayers(session);
  const isCompleted = session.status === 'Completed';
  const leaders = ranked.filter(entry => entry.rank === 1);
  // Un vincitore solo a partita chiusa e con il primo posto non condiviso.
  const winnerName = isCompleted && leaders.length === 1 ? leaders[0].player.displayName : null;

  // ----------- Render -----------
  return (
    <div className="min-h-screen bg-background">
      {/* ===== Header ===== */}
      <div className="border-b border-border bg-card/60 backdrop-blur-sm px-4 py-3">
        <div className="mx-auto max-w-lg">
          {/* Back + title row */}
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="icon" className="shrink-0" asChild>
              <Link href={`/sessions/${sessionId}`} data-testid="back-link">
                <ArrowLeft className="h-4 w-4" />
                <span className="sr-only">Torna alla sessione</span>
              </Link>
            </Button>

            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <Trophy className="h-4 w-4 shrink-0 text-amber-600 dark:text-amber-400" />
                <h1 className="truncate text-base font-bold text-foreground">Classifica</h1>
              </div>

              <div className="mt-0.5 flex items-center gap-2">
                {/* Status badge */}
                <span
                  data-testid="status-badge"
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[session.status]}`}
                >
                  {STATUS_LABELS[session.status]}
                </span>

                {/* Player count */}
                <span
                  data-testid="player-count"
                  className="flex items-center gap-1 text-xs text-muted-foreground"
                >
                  <Users className="h-3 w-3" />
                  {ranked.length}
                </span>
              </div>
            </div>
          </div>

          {/* Winner banner */}
          {winnerName && (
            <div
              data-testid="winner-badge"
              className="mt-3 flex items-center gap-2 rounded-xl border border-amber-200 dark:border-amber-900/40 bg-gradient-to-r from-amber-50 to-orange-50 dark:from-amber-950/20 dark:to-orange-950/10 px-3 py-2 text-sm font-semibold text-amber-900 dark:text-amber-300"
            >
              <Trophy className="h-4 w-4 text-amber-600 dark:text-amber-400" aria-hidden="true" />
              <span>Vincitore: {winnerName}</span>
            </div>
          )}
        </div>
      </div>

      {/* ===== Player List ===== */}
      <div className="mx-auto max-w-lg space-y-3 p-4">
        {ranked.map(({ player, rank }, idx) => {
          const avatarColor =
            PLAYER_COLOR_HEX[player.color] ?? FALLBACK_COLORS[idx % FALLBACK_COLORS.length];
          const isWinner = winnerName !== null && player.displayName === winnerName;
          const medal = RANK_MEDAL[rank];

          return (
            <div
              key={player.id}
              data-testid="scoreboard-row"
              data-rank={rank}
              className={`flex items-center gap-4 rounded-xl border p-4 transition-colors ${
                isWinner
                  ? 'border-amber-200 dark:border-amber-900/40 bg-amber-50/60 dark:bg-amber-950/10'
                  : 'border-border bg-card'
              }`}
            >
              {/* Rank */}
              <div className="w-8 shrink-0 text-center">
                {medal ? (
                  <span className="text-lg" aria-label={`Posizione ${rank}`}>
                    {medal}
                  </span>
                ) : (
                  <span className="text-sm font-bold text-muted-foreground">#{rank}</span>
                )}
              </div>

              {/* Avatar */}
              <div
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg text-xs font-bold text-white shadow-sm ring-1 ring-border"
                style={{
                  background: `linear-gradient(135deg, ${avatarColor} 0%, ${avatarColor}dd 100%)`,
                }}
                aria-hidden="true"
              >
                {player.displayName
                  .split(' ')
                  .map(n => n[0])
                  .join('')
                  .toUpperCase()
                  .slice(0, 2)}
              </div>

              {/* Name */}
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-semibold text-foreground">
                  {player.displayName}
                </p>
                {isWinner && (
                  <p className="text-xs text-amber-700 dark:text-amber-400 font-medium">
                    Vincitore
                  </p>
                )}
              </div>

              {/* Total */}
              <p
                data-testid="player-total"
                className="shrink-0 text-lg font-bold tabular-nums text-foreground"
              >
                {player.totalScore}
              </p>
            </div>
          );
        })}

        {/* Empty state */}
        {ranked.length === 0 && (
          <div className="py-12 text-center text-sm text-muted-foreground">
            Nessun giocatore in questa sessione
          </div>
        )}
      </div>

      {/* ===== Action bar: i punteggi si registrano nella scheda Score della pagina live ===== */}
      {!isCompleted && (
        <div className="fixed bottom-0 left-0 right-0 border-t border-border bg-card/80 backdrop-blur-sm p-4">
          <div className="mx-auto max-w-lg">
            <Button className="w-full bg-amber-500 hover:bg-amber-600 text-white" asChild>
              <Link href={`/sessions/${sessionId}/live?tab=score`} data-testid="record-score-link">
                Registra Punteggio
              </Link>
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
