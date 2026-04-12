'use client';

/**
 * Play Record Detail Page — mobile-first redesign (US-32)
 *
 * - MobileHeader con titolo gioco + edit icon
 * - Hero card dark con stato/durata
 * - Classifica con medaglie (🥇🥈🥉)
 * - Sezione punteggi dettagliati (collapsible, se dimensioni > 1)
 * - Sezione note
 * - Bottom action row (Elimina / Condividi)
 */

import { useState } from 'react';

import { ChevronDown, ChevronUp, Edit } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import type { SessionPlayer, PlayRecordStatus } from '@/lib/api/schemas/play-records.schemas';
import {
  usePlayRecord,
  useStartRecord,
  useCompleteRecord,
} from '@/lib/domain-hooks/usePlayRecords';
import { cn } from '@/lib/utils';

// ── Helpers ──────────────────────────────────────────────────────────────────

function statusLabel(status: PlayRecordStatus): string {
  switch (status) {
    case 'Completed':
      return '✅ Completata';
    case 'InProgress':
      return '🔄 In corso';
    case 'Planned':
      return '📅 Pianificata';
    case 'Archived':
      return '🗄 Archiviata';
  }
}

function statusColor(status: PlayRecordStatus): string {
  switch (status) {
    case 'Completed':
      return 'text-emerald-400 bg-emerald-400/10 border-emerald-400/20';
    case 'InProgress':
      return 'text-blue-400 bg-blue-400/10 border-blue-400/20';
    case 'Planned':
      return 'text-slate-400 bg-white/5 border-white/10';
    case 'Archived':
      return 'text-slate-500 bg-white/5 border-white/10';
  }
}

function formatDuration(duration: string | null): string {
  if (!duration) return '';
  // eslint-disable-next-line security/detect-unsafe-regex
  const dotNetMatch = duration.match(/^(?:(\d+)\.)?(\d+):(\d+):(\d+)$/);
  if (dotNetMatch) {
    const days = dotNetMatch[1] ? parseInt(dotNetMatch[1]) : 0;
    const hours = parseInt(dotNetMatch[2]) + days * 24;
    const minutes = parseInt(dotNetMatch[3]);
    const h = hours > 0 ? `${hours}h` : '';
    const m = minutes > 0 ? `${minutes}min` : '';
    return [h, m].filter(Boolean).join(' ') || '';
  }
  return duration;
}

const RANK_EMOJI = ['🥇', '🥈', '🥉'];

function rankEmoji(i: number): string {
  return RANK_EMOJI[i] ?? `${i + 1}.`;
}

function getPlayerTotalScore(player: SessionPlayer): number | null {
  if (player.scores.length === 0) return null;
  return player.scores.reduce((sum, s) => sum + s.value, 0);
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function PlayRecordDetailsPage() {
  const params = useParams();
  const router = useRouter();
  const [scoresOpen, setScoresOpen] = useState(false);
  const [notesOpen, setNotesOpen] = useState(true);

  const recordId = typeof params?.id === 'string' ? params.id : '';
  const { data: record, isLoading, error } = usePlayRecord(recordId);
  const startRecord = useStartRecord(recordId);
  const completeRecord = useCompleteRecord(recordId);

  const handleStart = async () => {
    try {
      await startRecord.mutateAsync();
      toast.success('Partita avviata');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Errore');
    }
  };

  const handleComplete = async () => {
    try {
      await completeRecord.mutateAsync();
      toast.success('Partita completata');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Errore');
    }
  };

  // ── Loading ────────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div
        className="flex flex-col min-h-full bg-[var(--gaming-bg-base)]"
        data-testid="play-record-detail-loading"
      >
        <MobileHeader title="" onBack={() => router.back()} />
        <div className="px-4 pt-3 flex flex-col gap-3">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-20 animate-pulse rounded-xl bg-white/5" />
          ))}
        </div>
      </div>
    );
  }

  if (error || !record) {
    return (
      <div
        className="flex flex-col min-h-full bg-[var(--gaming-bg-base)]"
        data-testid="play-record-detail-error"
      >
        <MobileHeader title="Errore" onBack={() => router.back()} />
        <div className="px-4 pt-4">
          <div className="rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400">
            {error instanceof Error ? error.message : 'Partita non trovata'}
          </div>
          <button
            type="button"
            onClick={() => router.push('/play-records')}
            className="mt-3 text-sm text-white/50 underline"
          >
            Torna alle partite
          </button>
        </div>
      </div>
    );
  }

  // Classifica
  const sortedPlayers = [...record.players].sort((a, b) => {
    const sa = getPlayerTotalScore(a);
    const sb = getPlayerTotalScore(b);
    if (sa === null && sb === null) return 0;
    if (sa === null) return 1;
    if (sb === null) return -1;
    return sb - sa;
  });

  const hasDimensions = record.scoringConfig.enabledDimensions.length > 1;
  const dur = formatDuration(record.duration);
  const dateStr = new Date(record.sessionDate).toLocaleDateString('it-IT', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });

  const canStart = record.status === 'Planned';
  const canComplete = record.status === 'InProgress';

  return (
    <div
      className="flex flex-col min-h-full bg-[var(--gaming-bg-base)]"
      data-testid="play-record-detail"
    >
      <MobileHeader
        title={record.gameName}
        onBack={() => router.back()}
        rightActions={
          record.status !== 'Archived' ? (
            <button
              type="button"
              aria-label="Modifica partita"
              onClick={() => router.push(`/play-records/${recordId}/edit`)}
              className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
            >
              <Edit className="h-4 w-4" />
            </button>
          ) : undefined
        }
      />

      <div className="flex-1 px-4 pt-3 pb-28 flex flex-col gap-4">
        {/* Hero card */}
        <div className="rounded-2xl bg-white/5 border border-white/8 overflow-hidden">
          <div className="px-4 pt-4 pb-3">
            <div className="flex items-start justify-between gap-2 mb-3">
              <div>
                <p className="text-[10px] font-semibold uppercase tracking-wider text-white/30 mb-0.5">
                  Partita
                </p>
                <h1 className="text-lg font-bold text-white font-quicksand leading-tight">
                  {record.gameName}
                </h1>
                <p className="text-sm text-white/50 mt-0.5">{dateStr}</p>
              </div>
              <span
                className={cn(
                  'flex-shrink-0 rounded-lg border px-2.5 py-1 text-[11px] font-semibold',
                  statusColor(record.status)
                )}
                data-testid="record-status-badge"
              >
                {statusLabel(record.status)}
              </span>
            </div>

            <div className="flex items-center gap-3 text-xs text-white/40">
              <span>
                {record.players.length} {record.players.length === 1 ? 'giocatore' : 'giocatori'}
              </span>
              {dur && (
                <>
                  <span>·</span>
                  <span>⏱ {dur}</span>
                </>
              )}
              {record.location && (
                <>
                  <span>·</span>
                  <span>📍 {record.location}</span>
                </>
              )}
            </div>
          </div>

          {/* Quick actions se la partita è in uno stato modificabile */}
          {(canStart || canComplete) && (
            <div className="border-t border-white/8 px-4 py-3 flex gap-2">
              {canStart && (
                <button
                  type="button"
                  onClick={handleStart}
                  disabled={startRecord.isPending}
                  className="flex-1 rounded-xl border border-blue-500/30 bg-blue-500/10 py-2 text-sm font-semibold text-blue-400 hover:bg-blue-500/20 disabled:opacity-50"
                  data-testid="start-record-btn"
                >
                  ▶ Avvia partita
                </button>
              )}
              {canComplete && (
                <button
                  type="button"
                  onClick={handleComplete}
                  disabled={completeRecord.isPending}
                  className="flex-1 rounded-xl border border-emerald-500/30 bg-emerald-500/10 py-2 text-sm font-semibold text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50"
                  data-testid="complete-record-btn"
                >
                  ✅ Completa
                </button>
              )}
            </div>
          )}
        </div>

        {/* Classifica */}
        {sortedPlayers.length > 0 && (
          <div className="flex flex-col gap-2" data-testid="classifica">
            <p className="text-[10px] font-semibold uppercase tracking-wider text-white/30 px-1">
              Classifica
            </p>
            {sortedPlayers.map((player, i) => {
              const total = getPlayerTotalScore(player);
              return (
                <div
                  key={player.id}
                  className={cn(
                    'flex items-center justify-between rounded-xl px-3 py-3 border',
                    i === 0 ? 'bg-amber-500/10 border-amber-500/20' : 'bg-white/5 border-white/8'
                  )}
                  data-testid={`rank-row-${player.displayName}`}
                >
                  <div className="flex items-center gap-3">
                    <span className="text-xl">{rankEmoji(i)}</span>
                    <div>
                      <p
                        className={cn(
                          'text-sm font-bold',
                          i === 0 ? 'text-amber-300' : 'text-white'
                        )}
                      >
                        {player.displayName}
                      </p>
                      {player.userId && (
                        <p className="text-[10px] text-white/30">utente registrato</p>
                      )}
                    </div>
                  </div>
                  {total !== null && (
                    <span
                      className={cn(
                        'text-sm font-bold',
                        i === 0 ? 'text-amber-400' : 'text-white/60'
                      )}
                    >
                      {total} pts
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        )}

        {/* Dettagli punteggio (collapsible) */}
        {hasDimensions && record.players.some(p => p.scores.length > 0) && (
          <div
            className="rounded-xl bg-white/5 border border-white/8 overflow-hidden"
            data-testid="score-details"
          >
            <button
              type="button"
              onClick={() => setScoresOpen(v => !v)}
              className="w-full flex items-center justify-between px-4 py-3 text-sm font-semibold text-white"
            >
              <span>Dettagli punteggio</span>
              {scoresOpen ? (
                <ChevronUp className="h-4 w-4 text-white/40" />
              ) : (
                <ChevronDown className="h-4 w-4 text-white/40" />
              )}
            </button>

            {scoresOpen && (
              <div className="border-t border-white/8 px-4 pb-4 pt-3 overflow-x-auto">
                <table className="w-full text-xs">
                  <thead>
                    <tr>
                      <th className="text-left text-white/40 font-semibold pb-2 pr-3">Giocatore</th>
                      {record.scoringConfig.enabledDimensions.map(dim => (
                        <th key={dim} className="text-center text-white/40 font-semibold pb-2 px-2">
                          {dim}
                        </th>
                      ))}
                      <th className="text-right text-white/40 font-semibold pb-2 pl-3">Tot</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedPlayers.map(player => {
                      const scoreMap = Object.fromEntries(
                        player.scores.map(s => [s.dimension, s.value])
                      );
                      const total = getPlayerTotalScore(player);
                      return (
                        <tr key={player.id} className="border-t border-white/5">
                          <td className="text-white font-semibold py-2 pr-3 whitespace-nowrap">
                            {player.displayName}
                          </td>
                          {record.scoringConfig.enabledDimensions.map(dim => (
                            <td key={dim} className="text-center text-white/70 py-2 px-2">
                              {scoreMap[dim] ?? '—'}
                            </td>
                          ))}
                          <td className="text-right text-white font-bold py-2 pl-3">
                            {total ?? '—'}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Note */}
        {record.notes && (
          <div
            className="rounded-xl bg-white/5 border border-white/8 overflow-hidden"
            data-testid="notes-section"
          >
            <button
              type="button"
              onClick={() => setNotesOpen(v => !v)}
              className="w-full flex items-center justify-between px-4 py-3 text-sm font-semibold text-white"
            >
              <span>📝 Note</span>
              {notesOpen ? (
                <ChevronUp className="h-4 w-4 text-white/40" />
              ) : (
                <ChevronDown className="h-4 w-4 text-white/40" />
              )}
            </button>
            {notesOpen && (
              <div className="border-t border-white/8 px-4 pb-4 pt-3">
                <p className="text-sm text-white/70 whitespace-pre-wrap">{record.notes}</p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
