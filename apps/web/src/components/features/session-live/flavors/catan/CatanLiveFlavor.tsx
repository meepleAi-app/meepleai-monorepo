'use client';

import { type ReactElement } from 'react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import { catanPieceColor } from './catan-palette';

export interface CatanLiveFlavorLabels {
  readonly panelAriaLabel: string;
  readonly roundTemplate: string; // "Round {n}"
  readonly activePlayerTemplate: string; // "Turno di {name}"
  readonly leaderboardHeading: string;
  readonly leaderBadgeLabel: string;
  readonly scoreAriaTemplate: string; // "Punti di {name}: {score}"
  readonly dimensionsHeading: string;
  readonly emptyLabel: string;
}

export interface CatanLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly labels: CatanLiveFlavorLabels;
  readonly className?: string;
}

function sumDimension(
  roundScores: LiveSessionDto['roundScores'],
  playerId: string,
  dimension: string
): number {
  return roundScores
    .filter(rs => rs.playerId === playerId && rs.dimension === dimension)
    .reduce((sum, rs) => sum + rs.value, 0);
}

export function CatanLiveFlavor({
  session,
  labels,
  className,
}: CatanLiveFlavorProps): ReactElement {
  const { players, roundScores, scoringConfig, currentTurnIndex, currentTurnPlayerId } = session;

  if (players.length === 0) {
    return (
      <div
        role="status"
        aria-live="polite"
        data-slot="catan-flavor-empty"
        className={`${className ?? ''} text-xs text-muted-foreground`.trim()}
      >
        {labels.emptyLabel}
      </div>
    );
  }

  const sorted = [...players].sort((a, b) => b.totalScore - a.totalScore);
  const leadScore = sorted[0]?.totalScore;
  const activePlayer = players.find(p => p.id === currentTurnPlayerId) ?? null;
  const dimensions = scoringConfig.enabledDimensions;

  return (
    <section
      aria-label={labels.panelAriaLabel}
      data-slot="catan-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Turn / phase header */}
      <header
        data-slot="catan-flavor-turn"
        aria-live="polite"
        className="flex flex-col gap-0.5 rounded-lg border border-entity-session/25 bg-entity-session/8 px-3 py-2"
      >
        <span className="text-sm font-semibold text-foreground">
          {labels.roundTemplate.replace('{n}', String(currentTurnIndex + 1))}
        </span>
        {activePlayer && (
          <span className="text-xs text-muted-foreground">
            {labels.activePlayerTemplate.replace('{name}', activePlayer.displayName)}
          </span>
        )}
      </header>

      {/* Leaderboard */}
      <div data-slot="catan-flavor-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {labels.leaderboardHeading}
        </h3>
        <ul role="list" className="flex flex-col gap-1" aria-label={labels.leaderboardHeading}>
          {sorted.map((player, idx) => {
            const isLeader = player.totalScore === leadScore && idx === 0;
            const scoreAria = labels.scoreAriaTemplate
              .replace('{name}', player.displayName)
              .replace('{score}', String(player.totalScore));
            return (
              <li
                key={player.id}
                data-slot="catan-flavor-row"
                data-active={player.isActive ? 'true' : 'false'}
                className={[
                  'flex items-center gap-2 rounded-lg px-2 py-1.5',
                  player.isActive
                    ? 'border border-entity-session/40 bg-entity-session/10'
                    : 'border border-transparent bg-card',
                ].join(' ')}
              >
                <span
                  aria-hidden="true"
                  data-slot="catan-flavor-swatch"
                  className="h-3.5 w-3.5 shrink-0 rounded-full border border-border-strong"
                  style={{ backgroundColor: catanPieceColor(player.color) }}
                />
                <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                  {player.displayName}
                  {isLeader && <span className="sr-only">, {labels.leaderBadgeLabel}</span>}
                  {isLeader && <span aria-hidden="true"> 👑</span>}
                </span>
                <span
                  aria-label={scoreAria}
                  className={[
                    'shrink-0 tabular-nums text-sm font-bold',
                    isLeader ? 'text-entity-session' : 'text-foreground',
                  ].join(' ')}
                >
                  {player.totalScore}
                </span>
              </li>
            );
          })}
        </ul>
      </div>

      {/* Optional per-dimension breakdown (real roundScores only) */}
      {dimensions.length > 0 && (
        <div data-slot="catan-flavor-dimensions" className="flex flex-col gap-2">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {labels.dimensionsHeading}
          </h3>
          <ul role="list" className="flex flex-col gap-1">
            {dimensions.map(dim => (
              <li key={dim} className="flex flex-col gap-0.5 rounded-lg bg-card px-2 py-1.5">
                <span className="text-xs font-medium text-foreground">{dim}</span>
                <span className="flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                  {players.map(p => (
                    <span key={p.id} className="tabular-nums">
                      {p.displayName}:{' '}
                      <span data-player={p.id} className="font-semibold text-foreground">
                        {sumDimension(roundScores, p.id, dim)}
                      </span>
                    </span>
                  ))}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
