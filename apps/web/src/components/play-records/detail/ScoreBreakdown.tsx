/**
 * ScoreBreakdown — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Accordion expandable showing per-dimension score breakdown table.
 * Only rendered when `dimensions.length > 1` (EC-10 multi-dim).
 *
 * AC-2.6: ScoreBreakdown accordion expandable se scores[].length > 1
 * A11y: aria-expanded on trigger, aria-controls references panel id, role="region" on panel.
 *
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx` ScoringBreakdown
 */
'use client';

import type { ReactElement } from 'react';
import { useId, useState } from 'react';

import { ChevronDown, ChevronUp } from 'lucide-react';

import { entityHsl } from '@/components/ui/data-display/meeple-card';

export interface ScoreBreakdownScore {
  readonly dimension: string;
  readonly value: number;
}

export interface ScoreBreakdownRow {
  readonly playerId: string;
  readonly name: string;
  readonly scores: ReadonlyArray<ScoreBreakdownScore>;
  readonly totalScore: number | null;
}

export interface ScoreBreakdownProps {
  readonly rows: ReadonlyArray<ScoreBreakdownRow>;
  readonly dimensions: ReadonlyArray<string>;
  readonly className?: string;
}

/** Returns null (renders nothing) when single dimension — no accordion needed. */
export function ScoreBreakdown({
  rows,
  dimensions,
  className,
}: ScoreBreakdownProps): ReactElement | null {
  const [open, setOpen] = useState(false);
  const panelId = useId();

  // EC-10: only render when >1 dimension
  if (dimensions.length <= 1) {
    return null;
  }

  return (
    <section
      data-slot="score-breakdown"
      className={`overflow-hidden rounded-xl border border-border bg-card ${className ?? ''}`}
      role="region"
      aria-label="Dettaglio punteggi"
    >
      {/* Accordion trigger */}
      <button
        type="button"
        data-slot="score-breakdown-trigger"
        aria-expanded={open}
        aria-controls={panelId}
        aria-label={open ? 'Chiudi dettaglio punteggi' : 'Mostra dettaglio punteggi'}
        onClick={() => setOpen(v => !v)}
        className="flex w-full items-center justify-between px-4 py-3 text-left"
      >
        <h2 className="flex items-center gap-1.5 font-display text-sm font-extrabold text-foreground">
          <span aria-hidden="true">🔢</span>
          Dettaglio punteggi
        </h2>
        {open ? (
          <ChevronUp className="h-4 w-4 text-muted-foreground" />
        ) : (
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        )}
      </button>

      {/* Accordion panel */}
      <div
        id={panelId}
        data-slot="score-breakdown-panel"
        data-state={open ? 'open' : 'closed'}
        hidden={!open}
        role="region"
        aria-label="Tabella dettaglio punteggi per dimensione"
      >
        <div className="overflow-x-auto border-t border-border">
          <table className="w-full min-w-[400px] border-collapse font-mono text-xs">
            <thead>
              <tr className="border-b border-border">
                <th className="px-4 py-2.5 text-left font-mono text-[9px] font-extrabold uppercase tracking-wider text-muted-foreground">
                  Giocatore
                </th>
                {dimensions.map(dim => (
                  <th
                    key={dim}
                    className="px-2 py-2.5 text-center font-mono text-[9px] font-extrabold uppercase tracking-wider text-muted-foreground"
                  >
                    {dim}
                  </th>
                ))}
                <th
                  className="px-4 py-2.5 text-center font-mono text-[9px] font-extrabold uppercase tracking-wider"
                  style={{ color: entityHsl('session') }}
                >
                  Tot
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, i) => {
                const scoreMap = Object.fromEntries(row.scores.map(s => [s.dimension, s.value]));
                return (
                  <tr
                    key={row.playerId}
                    className={i < rows.length - 1 ? 'border-b border-border' : ''}
                  >
                    <td className="px-4 py-2.5 font-display text-[12.5px] font-extrabold text-foreground">
                      {row.name}
                    </td>
                    {dimensions.map(dim => (
                      <td
                        key={dim}
                        className="px-2 py-2.5 text-center tabular-nums text-muted-foreground"
                      >
                        {scoreMap[dim] !== undefined ? scoreMap[dim] : '—'}
                      </td>
                    ))}
                    <td className="px-4 py-2.5 text-center font-extrabold tabular-nums text-foreground">
                      {row.totalScore !== null ? row.totalScore : '—'}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
