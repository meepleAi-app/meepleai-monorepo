/**
 * LiveScoringPanel — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Read-only scoreboard for all participants.
 * Foundation: score update handlers absent (aria-disabled on inputs).
 * Interactions sub-PR wires Player+Host score delta handlers.
 *
 * Gate C: DIVERGES from MeepleCard — live score panel, not a card.
 */

import type { ReactElement } from 'react';

import type { ParticipantRole } from '@/lib/session-live/sse-events';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface LiveScoringPanelScoreEntry {
  readonly playerId: string;
  readonly playerName: string;
  readonly score: number;
  readonly isWinner: boolean;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface LiveScoringPanelLabels {
  readonly title: string;
  /** Raw template "Punteggio: {score}" — component does .replace(). */
  readonly scoreLabelTemplate: string;
  readonly winnerLabel: string;
  readonly myScoreLabel: string;
  /** Raw template "Aumenta punteggio di {playerName}" — component does .replace(). */
  readonly incrementAriaLabelTemplate: string;
  /** Raw template "Diminuisci punteggio di {playerName}" — component does .replace(). */
  readonly decrementAriaLabelTemplate: string;
  /** Raw template "Inserisci punteggio per {playerName}" — component does .replace(). */
  readonly scoreInputAriaLabelTemplate: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface LiveScoringPanelProps {
  readonly scores: ReadonlyArray<LiveScoringPanelScoreEntry>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onScoreUpdate?: (playerId: string, delta: number) => void;
  readonly emptyTurn?: boolean;
  readonly compact?: boolean;
  readonly labels: LiveScoringPanelLabels;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function LiveScoringPanel({
  scores,
  viewerRole,
  viewerId,
  onScoreUpdate,
  compact = false,
  labels,
}: LiveScoringPanelProps): ReactElement {
  const canEdit = viewerRole === 'Player' || viewerRole === 'Host';

  // Sort by score descending for display
  const sorted = [...scores].sort((a, b) => b.score - a.score);

  return (
    <div data-slot="live-scoring-panel" className="flex flex-col gap-2">
      {!compact && <h3 className="text-sm font-semibold text-slate-200">{labels.title}</h3>}

      <ul role="list" className="flex flex-col gap-1" aria-label={labels.title}>
        {sorted.map((entry, idx) => {
          const isViewer = entry.playerId === viewerId;
          const canEditThis = canEdit && (viewerRole === 'Host' || isViewer); // Player edits own score only

          const scoreLabel = labels.scoreLabelTemplate.replace('{score}', String(entry.score));
          const incrementLabel = labels.incrementAriaLabelTemplate.replace(
            '{playerName}',
            entry.playerName
          );
          const decrementLabel = labels.decrementAriaLabelTemplate.replace(
            '{playerName}',
            entry.playerName
          );

          return (
            <li
              key={entry.playerId}
              data-slot="scoring-entry"
              data-player-id={entry.playerId}
              className={[
                'flex items-center justify-between gap-2 rounded-lg px-2 py-1.5',
                isViewer ? 'bg-[hsl(240,60%,18%)]' : 'bg-slate-800/40',
              ].join(' ')}
            >
              {/* Rank + name */}
              <div className="flex min-w-0 items-center gap-2">
                <span
                  className={[
                    'shrink-0 tabular-nums text-xs font-medium',
                    idx === 0 ? 'text-amber-400' : 'text-slate-500',
                  ].join(' ')}
                  aria-hidden="true"
                >
                  #{idx + 1}
                </span>
                <span className="truncate text-xs font-medium text-slate-200">
                  {entry.playerName}
                  {isViewer && (
                    <span className="ml-1 text-xs text-[hsl(240,60%,70%)]">
                      ({labels.myScoreLabel})
                    </span>
                  )}
                </span>
                {entry.isWinner && (
                  <span
                    className="shrink-0 rounded-full bg-amber-500/20 px-1.5 py-0.5
                    text-xs font-medium text-amber-300"
                  >
                    {labels.winnerLabel}
                  </span>
                )}
              </div>

              {/* Score + edit controls */}
              <div className="flex shrink-0 items-center gap-1">
                {canEditThis && (
                  <button
                    type="button"
                    aria-label={decrementLabel}
                    aria-disabled={onScoreUpdate == null ? 'true' : undefined}
                    onClick={() => onScoreUpdate?.(entry.playerId, -1)}
                    className="flex h-6 w-6 items-center justify-center rounded text-slate-400
                      hover:bg-slate-700 hover:text-slate-200 focus-visible:outline-none
                      focus-visible:ring-1 focus-visible:ring-slate-500
                      aria-disabled:cursor-not-allowed aria-disabled:opacity-40"
                  >
                    –
                  </button>
                )}
                <span
                  aria-label={scoreLabel}
                  className="min-w-[2rem] text-center tabular-nums text-sm font-bold
                    text-slate-100"
                >
                  {entry.score}
                </span>
                {canEditThis && (
                  <button
                    type="button"
                    aria-label={incrementLabel}
                    aria-disabled={onScoreUpdate == null ? 'true' : undefined}
                    onClick={() => onScoreUpdate?.(entry.playerId, +1)}
                    className="flex h-6 w-6 items-center justify-center rounded text-slate-400
                      hover:bg-slate-700 hover:text-slate-200 focus-visible:outline-none
                      focus-visible:ring-1 focus-visible:ring-slate-500
                      aria-disabled:cursor-not-allowed aria-disabled:opacity-40"
                  >
                    +
                  </button>
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
