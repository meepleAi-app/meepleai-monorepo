/**
 * ScoreTabContent — the Score tab of the live page, on `RoundScores` (#4156 fetta 2, ADR-089).
 *
 * An editor player × dimension × round: the players are those who score (active, not spectators),
 * the dimensions are the ones the wizard configured (`ScoringConfig`), and the round is the derived
 * round of the game (D6), which the host can change. The host writes a cell on blur or Enter; the
 * others read. The totals are the backend's (`totalScore`, recalculated on every write).
 *
 * Before #4156 the tab read a polymorphic `scoringType` that nothing ever set — it stayed on
 * "Caricamento punteggi…" — and wrote to `/game-sessions/{id}/scores-polymorphic` with the id of a
 * live session. The polymorphic scoring is retired from the live path, as ADR-089 provides.
 */

'use client';

import { useCallback, useEffect, useRef, useState, type ReactElement } from 'react';

import { ChevronLeft, ChevronRight } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { useRecordRoundScore } from '@/hooks/queries/useRecordRoundScore';
import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import {
  canRecordScores,
  cellValue,
  deriveRound,
  scoringPlayers,
} from '@/lib/session-live/round-scores';

export interface ScoreTabContentProps {
  readonly sessionId: string;
  /** The live session; `null`/`undefined` while it loads. */
  readonly session: LiveSessionDto | null | undefined;
  readonly viewerRole: 'Host' | 'Player' | 'Spectator';
  readonly className?: string;
}

/** A cell of the editor, and the value it holds (`undefined` when nobody has scored it). */
interface Cell {
  readonly playerId: string;
  readonly round: number;
  readonly dimension: string;
  readonly unit?: string;
  readonly current?: number;
}

function cellKey(playerId: string, round: number, dimension: string): string {
  return `${playerId}|${round}|${dimension.toLowerCase()}`;
}

export function ScoreTabContent({
  sessionId,
  session,
  viewerRole,
  className,
}: ScoreTabContentProps): ReactElement {
  const { t } = useTranslation();
  const mutation = useRecordRoundScore(sessionId);

  /** The round the host picked; `null` follows the derived round of the game. */
  const [chosenRound, setChosenRound] = useState<number | null>(null);
  /** What the host is typing, per cell, until it is written. */
  const [drafts, setDrafts] = useState<Readonly<Record<string, string>>>({});

  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  // Writes go one at a time: two writes on the same live session at once collide on its
  // optimistic concurrency, and one of them would fail.
  const queueRef = useRef<Promise<void>>(Promise.resolve());
  const { mutateAsync } = mutation;

  const dropDraft = useCallback((key: string) => {
    if (!isMountedRef.current) return;
    setDrafts(current => {
      const next = { ...current };
      delete next[key];
      return next;
    });
  }, []);

  const commit = useCallback(
    (key: string, cell: Cell) => {
      const raw = drafts[key];
      if (raw === undefined) return;
      const trimmed = raw.trim();
      const value = Number(trimmed);
      if (trimmed === '' || value === cell.current) {
        dropDraft(key);
        return;
      }
      if (!Number.isInteger(value)) {
        toast.error(t('pages.sessionLive.scoring.rounds.notAnInteger'), {
          id: 'round-score-integer',
        });
        dropDraft(key);
        return;
      }
      queueRef.current = queueRef.current.then(async () => {
        try {
          await mutateAsync({
            request: {
              playerId: cell.playerId,
              round: cell.round,
              dimension: cell.dimension,
              value,
              ...(cell.unit ? { unit: cell.unit } : {}),
            },
            correction: cell.current !== undefined,
          });
        } catch {
          if (isMountedRef.current) {
            toast.error(t('pages.sessionLive.scoring.rounds.saveError'), {
              id: 'round-score-save',
            });
          }
        } finally {
          dropDraft(key);
        }
      });
    },
    [drafts, dropDraft, mutateAsync, t]
  );

  if (session == null) {
    return (
      <div
        role="status"
        aria-live="polite"
        data-slot="scoring-panel-empty"
        className={`${className ?? ''} text-xs text-muted-foreground`.trim()}
      >
        {t('pages.sessionLive.scoring.loadingLabel')}
      </div>
    );
  }

  const players = scoringPlayers(session);
  const dimensions = session.scoringConfig.enabledDimensions;
  const units = session.scoringConfig.dimensionUnits;

  if (dimensions.length === 0 || players.length === 0) {
    return (
      <div
        role="status"
        data-slot="scoring-panel-empty"
        className={`${className ?? ''} text-xs text-muted-foreground`.trim()}
      >
        {t(
          dimensions.length === 0
            ? 'pages.sessionLive.scoring.rounds.noDimensions'
            : 'pages.sessionLive.scoring.rounds.noPlayers'
        )}
      </div>
    );
  }

  const derivedRound = deriveRound(session.currentTurnIndex, players.length);
  const round = chosenRound ?? derivedRound;
  const editable = viewerRole === 'Host' && canRecordScores(session);

  return (
    <div data-slot="round-score-editor" className={className}>
      <div className="mb-3 flex items-center gap-1" data-slot="round-score-round">
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={t('pages.sessionLive.scoring.rounds.previousRound')}
          disabled={round <= 1}
          onClick={() => setChosenRound(Math.max(1, round - 1))}
        >
          <ChevronLeft className="h-4 w-4" aria-hidden="true" />
        </Button>
        <span
          className="min-w-[5rem] text-center text-sm font-semibold text-foreground"
          aria-live="polite"
          data-slot="round-score-round-label"
        >
          {t('pages.sessionLive.scoring.rounds.roundLabel', { round })}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={t('pages.sessionLive.scoring.rounds.nextRound')}
          onClick={() => setChosenRound(round + 1)}
        >
          <ChevronRight className="h-4 w-4" aria-hidden="true" />
        </Button>
        {round !== derivedRound && (
          <Button type="button" variant="link" size="sm" onClick={() => setChosenRound(null)}>
            {t('pages.sessionLive.scoring.rounds.currentRound')}
          </Button>
        )}
      </div>

      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-border text-left text-xs text-muted-foreground">
            <th scope="col" className="py-1 pr-2 font-medium">
              {t('pages.sessionLive.scoring.rounds.player')}
            </th>
            {dimensions.map(dimension => (
              <th key={dimension} scope="col" className="px-1 py-1 font-medium">
                {units[dimension] ? `${dimension} (${units[dimension]})` : dimension}
              </th>
            ))}
            <th scope="col" className="py-1 pl-2 text-right font-medium">
              {t('pages.sessionLive.scoring.rounds.total')}
            </th>
          </tr>
        </thead>
        <tbody>
          {players.map(player => (
            <tr key={player.id} className="border-b border-border/60">
              <th scope="row" className="py-1.5 pr-2 text-left font-medium text-foreground">
                {player.displayName}
              </th>
              {dimensions.map(dimension => {
                const key = cellKey(player.id, round, dimension);
                const current = cellValue(session, player.id, round, dimension);
                const label = t('pages.sessionLive.scoring.rounds.cellAriaTemplate', {
                  dimension,
                  player: player.displayName,
                  round,
                });
                return (
                  <td key={dimension} className="px-1 py-1">
                    {editable ? (
                      <input
                        type="number"
                        inputMode="numeric"
                        step={1}
                        aria-label={label}
                        data-slot="round-score-cell"
                        value={drafts[key] ?? (current === undefined ? '' : String(current))}
                        onChange={event =>
                          setDrafts(previous => ({ ...previous, [key]: event.target.value }))
                        }
                        onBlur={() =>
                          commit(key, {
                            playerId: player.id,
                            round,
                            dimension,
                            unit: units[dimension],
                            current,
                          })
                        }
                        onKeyDown={event => {
                          if (event.key === 'Enter') event.currentTarget.blur();
                        }}
                        className="w-16 rounded-md border border-border bg-background px-2 py-1 text-right text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      />
                    ) : (
                      <span className="block text-right text-foreground">{current ?? '—'}</span>
                    )}
                  </td>
                );
              })}
              <td
                className="py-1.5 pl-2 text-right font-semibold text-foreground"
                data-slot="round-score-total"
              >
                {player.totalScore}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
