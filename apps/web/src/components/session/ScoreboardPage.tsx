/**
 * ScoreboardPage — /sessions/{id}/scoreboard
 *
 * Dedicated full-page scoreboard that fetches session data via
 * api.sessions.getById and renders a ranked player list.
 *
 * Since GameSessionDto.players contains playerName/playerOrder/color
 * but no individual scores, players are ranked by playerOrder.
 * The winnerName field is highlighted with a trophy badge.
 *
 * Task 3 — Sessions Redesign
 */

'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Trophy, Users } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/button';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { api } from '@/lib/api';

interface ScoreboardPageProps {
  sessionId: string;
}

// Map backend status strings to Italian UI labels
const STATUS_LABELS: Record<string, string> = {
  Active: 'Attiva',
  Paused: 'In pausa',
  Finalized: 'Completata',
  Completed: 'Completata',
};

// Map backend status strings to Tailwind badge colors
const STATUS_COLORS: Record<string, string> = {
  Active: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-400',
  Paused: 'bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-400',
  Finalized: 'bg-slate-100 text-slate-700 dark:bg-slate-800/50 dark:text-slate-400',
  Completed: 'bg-slate-100 text-slate-700 dark:bg-slate-800/50 dark:text-slate-400',
};

// Avatar colors cycling for players that don't have a color set
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

export function ScoreboardPage({ sessionId }: ScoreboardPageProps) {
  const [scoreSheetOpen, setScoreSheetOpen] = useState(false);

  const {
    data: session,
    isPending,
    isError,
  } = useQuery({
    queryKey: ['session', sessionId],
    queryFn: () => api.sessions.getById(sessionId),
    refetchInterval: 10_000,
    retry: false,
  });

  // ----------- Loading state -----------
  if (isPending) {
    return (
      <div data-testid="scoreboard-loading" className="min-h-screen bg-background p-4">
        {/* Header skeleton */}
        <div className="mb-6 flex items-center gap-3">
          <div className="h-8 w-8 animate-pulse rounded-lg bg-muted" />
          <div className="space-y-2">
            <div className="h-4 w-40 animate-pulse rounded bg-muted" />
            <div className="h-3 w-24 animate-pulse rounded bg-muted" />
          </div>
        </div>
        {/* Player card skeletons */}
        <div className="space-y-3">
          {[1, 2, 3].map(i => (
            <div
              key={i}
              className="flex h-16 items-center gap-4 rounded-xl border border-border bg-card p-4"
            >
              <div className="h-10 w-10 animate-pulse rounded-lg bg-muted" />
              <div className="h-4 w-32 animate-pulse rounded bg-muted" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ----------- Error / empty state -----------
  if (isError || !session) {
    return (
      <div
        data-testid="scoreboard-error"
        className="flex min-h-screen flex-col items-center justify-center gap-3 bg-background p-4 text-center"
      >
        <p className="text-sm font-medium text-destructive">Impossibile caricare la sessione</p>
        <p className="text-xs text-muted-foreground">Verifica la connessione e riprova.</p>
        <Button variant="outline" size="sm" asChild>
          <Link href="/sessions">Torna alle sessioni</Link>
        </Button>
      </div>
    );
  }

  // ----------- Data prep -----------
  const statusLabel = STATUS_LABELS[session.status] ?? session.status;
  const statusColor =
    STATUS_COLORS[session.status] ??
    'bg-slate-100 text-slate-700 dark:bg-slate-800/50 dark:text-slate-400';

  // Players ranked by playerOrder (ascending)
  const rankedPlayers = [...session.players].sort((a, b) => a.playerOrder - b.playerOrder);

  // Rank medal labels for positions 1–3
  const rankMedal: Record<number, string> = { 1: '🥇', 2: '🥈', 3: '🥉' };

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
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${statusColor}`}
                >
                  {statusLabel}
                </span>

                {/* Player count */}
                <span className="flex items-center gap-1 text-xs text-muted-foreground">
                  <Users className="h-3 w-3" />
                  {session.playerCount}
                </span>
              </div>
            </div>
          </div>

          {/* Winner banner */}
          {session.winnerName && (
            <div
              data-testid="winner-badge"
              className="mt-3 flex items-center gap-2 rounded-xl border border-amber-200 dark:border-amber-900/40 bg-gradient-to-r from-amber-50 to-orange-50 dark:from-amber-950/20 dark:to-orange-950/10 px-3 py-2 text-sm font-semibold text-amber-900 dark:text-amber-300"
            >
              <Trophy className="h-4 w-4 text-amber-600 dark:text-amber-400" aria-hidden="true" />
              <span>Vincitore: {session.winnerName}</span>
            </div>
          )}
        </div>
      </div>

      {/* ===== Player List ===== */}
      <div className="mx-auto max-w-lg space-y-3 p-4">
        {rankedPlayers.map((player, idx) => {
          const rank = idx + 1;
          const avatarColor = player.color ?? FALLBACK_COLORS[idx % FALLBACK_COLORS.length];
          const isWinner = session.winnerName?.toLowerCase() === player.playerName.toLowerCase();
          const medal = rankMedal[rank];

          return (
            <div
              key={player.playerOrder}
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
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg text-xs font-bold text-white shadow-sm ring-1 ring-black/10"
                style={{
                  background: `linear-gradient(135deg, ${avatarColor} 0%, ${avatarColor}dd 100%)`,
                }}
                aria-hidden="true"
              >
                {player.playerName
                  .split(' ')
                  .map(n => n[0])
                  .join('')
                  .toUpperCase()
                  .slice(0, 2)}
              </div>

              {/* Name */}
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-semibold text-foreground">
                  {player.playerName}
                </p>
                {isWinner && (
                  <p className="text-xs text-amber-700 dark:text-amber-400 font-medium">
                    Vincitore
                  </p>
                )}
              </div>

              {/* Rank label for ranks without medals */}
              {!medal && <span className="text-xs text-muted-foreground font-medium">#{rank}</span>}
            </div>
          );
        })}

        {/* Empty state */}
        {rankedPlayers.length === 0 && (
          <div className="py-12 text-center text-sm text-muted-foreground">
            Nessun giocatore in questa sessione
          </div>
        )}
      </div>

      {/* ===== Action bar ===== */}
      <div className="fixed bottom-0 left-0 right-0 border-t border-border bg-card/80 backdrop-blur-sm p-4">
        <div className="mx-auto max-w-lg">
          <Button
            className="w-full bg-amber-500 hover:bg-amber-600 text-white"
            onClick={() => setScoreSheetOpen(true)}
          >
            Registra Punteggio
          </Button>
        </div>
      </div>

      {/* ===== Score Sheet ===== */}
      <Sheet open={scoreSheetOpen} onOpenChange={setScoreSheetOpen}>
        <SheetContent side="bottom" className="max-h-[85vh] overflow-y-auto px-4 pb-8 pt-2">
          <SheetHeader className="pb-4">
            <div className="mx-auto mb-2 h-1.5 w-12 rounded-full bg-muted-foreground/30" />
            <SheetTitle>Registra Punteggio</SheetTitle>
            <SheetDescription>Seleziona il giocatore e inserisci il punteggio</SheetDescription>
          </SheetHeader>

          {/* Simple score entry form */}
          <div className="space-y-4">
            {rankedPlayers.map(player => {
              const avatarColor =
                player.color ?? FALLBACK_COLORS[player.playerOrder % FALLBACK_COLORS.length];

              return (
                <div
                  key={player.playerOrder}
                  className="flex items-center gap-3 rounded-lg border border-border bg-muted/30 p-3"
                >
                  <div
                    className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-xs font-bold text-white"
                    style={{ backgroundColor: avatarColor }}
                    aria-hidden="true"
                  >
                    {player.playerName.slice(0, 2).toUpperCase()}
                  </div>
                  <span className="flex-1 text-sm font-medium text-foreground">
                    {player.playerName}
                  </span>
                  <input
                    type="number"
                    placeholder="0"
                    aria-label={`Punteggio per ${player.playerName}`}
                    className="w-20 rounded-md border border-border bg-background px-3 py-1.5 text-right text-sm font-mono focus:outline-none focus:ring-2 focus:ring-amber-500"
                  />
                </div>
              );
            })}

            <Button
              className="w-full bg-amber-500 hover:bg-amber-600 text-white mt-2"
              onClick={() => setScoreSheetOpen(false)}
            >
              Salva Punteggi
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </div>
  );
}
