/**
 * ScoringPanelRenderer — G5a polymorphic scoring display dispatcher.
 *
 * Renders one of 4 read-only scoring variants based on the `data.kind`
 * discriminator (mirrors the backend `SessionTracking.ScoreType` enum):
 *
 *   Points     → numeric leaderboard (player row + total score)
 *   Ranking    → ordinal display (1st / 2nd / 3rd pills per player)
 *   BinaryWin  → cooperative outcome banner + win/lose indicator per player
 *   Objectives → checklist of objectives with per-player completion state
 *
 * This is the **read-only renderer**. The mutable counterpart is
 * `components/sessions/PolymorphicScoreEditor` (Asse D follow-up P1 #1899).
 * Types here are *renderer*-specific — they carry only what the display needs,
 * not editor callbacks.
 *
 * Exhaustiveness is enforced by the `assertNever` default — adding a 5th
 * ScoreType on the backend triggers a TypeScript compile error here.
 *
 * @see apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ScoreType.cs
 * @see components/sessions/PolymorphicScoreEditor (editor counterpart)
 * @see Issue #2375 (G5a), parent umbrella #2354
 */

import type { ReactElement } from 'react';

// ─── Renderer-local types ──────────────────────────────────────────────────────

/** Minimal player row for any scoring variant. */
export interface ScoringPlayerEntry {
  readonly id: string;
  readonly displayName: string;
}

/** Points variant: each player has a numeric score. */
export interface PointsScoringData {
  readonly kind: 'Points';
  readonly players: ReadonlyArray<ScoringPlayerEntry & { readonly score: number }>;
}

/** Ranking variant: each player has a 1-indexed ordinal position. */
export interface RankingScoringData {
  readonly kind: 'Ranking';
  readonly players: ReadonlyArray<ScoringPlayerEntry & { readonly position: number }>;
}

/** BinaryWin variant: per-player win/lose flag (cooperative / PvP outcome). */
export interface BinaryWinScoringData {
  readonly kind: 'BinaryWin';
  readonly players: ReadonlyArray<ScoringPlayerEntry & { readonly isWinner: boolean }>;
  /** Optional human-readable label for the in-progress state banner. */
  readonly inProgressLabel?: string;
}

/** Objectives variant: per-player checklist of named objectives. */
export interface ObjectiveScoringItem {
  readonly id: string;
  readonly label: string;
  readonly done: boolean;
}

export interface ObjectivesScoringData {
  readonly kind: 'Objectives';
  readonly players: ReadonlyArray<
    ScoringPlayerEntry & { readonly completedObjectives: ReadonlyArray<string> }
  >;
  /** Full list of objectives for all players to check against. */
  readonly objectives: ReadonlyArray<ObjectiveScoringItem>;
}

/** Discriminated union over all 4 scoring modes. */
export type ScoringPanelData =
  | PointsScoringData
  | RankingScoringData
  | BinaryWinScoringData
  | ObjectivesScoringData;

export interface ScoringPanelRendererLabels {
  readonly points: {
    /** Column header / section label for Points variant. */
    readonly heading: string;
    /** Template "Punteggio di {name}" for aria-label on score cell. */
    readonly scoreAriaTemplate: string;
    /** Accessibility label for the winner badge. */
    readonly leaderBadgeLabel: string;
  };
  readonly ranking: {
    readonly heading: string;
    /** Template "Posizione di {name}" for aria-label on rank pill. */
    readonly rankAriaTemplate: string;
    /** Accessibility label attached to the 1st-place badge. */
    readonly firstPlaceBadgeLabel: string;
  };
  readonly binaryWin: {
    readonly heading: string;
    /** Displayed in the in-progress banner when the game is still live. */
    readonly inProgressLabel: string;
    readonly winLabel: string;
    readonly loseLabel: string;
    /** aria-label for the win/lose badge. Template: "{name}: {result}" */
    readonly outcomeAriaTemplate: string;
  };
  readonly objectives: {
    readonly heading: string;
    /** Template "Completati da {name}" */
    readonly completedAriaTemplate: string;
    /** aria-label for a done checkbox. Template: "{label} (completato)" */
    readonly doneAriaTemplate: string;
    /** aria-label for a pending checkbox. Template: "{label} (non completato)" */
    readonly pendingAriaTemplate: string;
  };
}

export interface ScoringPanelRendererProps {
  readonly data: ScoringPanelData;
  readonly labels: ScoringPanelRendererLabels;
  /** Optional CSS class applied to the outermost wrapper div. */
  readonly className?: string;
}

// ─── Ordinal helper ───────────────────────────────────────────────────────────

/** Returns "1°", "2°", "3°", "4°", etc. (locale-neutral, Italian ordinal). */
function ordinal(n: number): string {
  return `${n}°`;
}

// ─── Points variant ───────────────────────────────────────────────────────────

function PointsPanel({
  data,
  labels,
}: {
  data: PointsScoringData;
  labels: ScoringPanelRendererLabels['points'];
}): ReactElement {
  const sorted = [...data.players].sort((a, b) => b.score - a.score);
  const leadScore = sorted[0]?.score;

  return (
    <div data-slot="scoring-panel-points" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>

      <ul role="list" className="flex flex-col gap-1" aria-label={labels.heading}>
        {sorted.map((player, idx) => {
          const isLeader = player.score === leadScore && idx === 0;
          const scoreAriaLabel = labels.scoreAriaTemplate.replace('{name}', player.displayName);

          return (
            <li
              key={player.id}
              data-slot="scoring-row"
              className={[
                'flex items-center gap-2 rounded-lg px-2 py-1.5',
                isLeader
                  ? 'border border-entity-session/30 bg-entity-session/10'
                  : 'border border-transparent bg-card',
              ].join(' ')}
            >
              {/* Rank indicator */}
              <span
                aria-hidden="true"
                className={[
                  'shrink-0 w-5 text-center tabular-nums text-xs font-bold',
                  isLeader ? 'text-entity-session' : 'text-muted-foreground',
                ].join(' ')}
              >
                {ordinal(idx + 1)}
              </span>

              {/* Player name */}
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
                {isLeader && <span className="sr-only">, {labels.leaderBadgeLabel}</span>}
              </span>

              {/* Score */}
              <span
                aria-label={scoreAriaLabel}
                className={[
                  'shrink-0 tabular-nums text-sm font-bold',
                  isLeader ? 'text-entity-session' : 'text-foreground',
                ].join(' ')}
              >
                {player.score}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

// ─── Ranking variant ──────────────────────────────────────────────────────────

function RankingPanel({
  data,
  labels,
}: {
  data: RankingScoringData;
  labels: ScoringPanelRendererLabels['ranking'];
}): ReactElement {
  const sorted = [...data.players].sort((a, b) => a.position - b.position);

  return (
    <div data-slot="scoring-panel-ranking" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>

      <ul role="list" className="flex flex-col gap-1" aria-label={labels.heading}>
        {sorted.map(player => {
          const isFirst = player.position === 1;
          const rankAriaLabel = labels.rankAriaTemplate.replace('{name}', player.displayName);

          return (
            <li
              key={player.id}
              data-slot="ranking-row"
              className={[
                'flex items-center gap-2 rounded-lg px-2 py-1.5',
                isFirst
                  ? 'border border-entity-session/30 bg-entity-session/10'
                  : 'border border-transparent bg-card',
              ].join(' ')}
            >
              {/* Rank pill */}
              <span
                aria-label={rankAriaLabel}
                className={[
                  'inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold',
                  isFirst
                    ? 'bg-entity-session text-white'
                    : 'bg-entity-session/15 text-entity-session',
                ].join(' ')}
              >
                {player.position}
                {isFirst && <span className="sr-only">, {labels.firstPlaceBadgeLabel}</span>}
              </span>

              {/* Player name */}
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
              </span>

              {/* Ordinal label */}
              <span
                aria-hidden="true"
                className="shrink-0 text-xs font-medium text-muted-foreground"
              >
                {ordinal(player.position)}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

// ─── BinaryWin variant ────────────────────────────────────────────────────────

function BinaryWinPanel({
  data,
  labels,
}: {
  data: BinaryWinScoringData;
  labels: ScoringPanelRendererLabels['binaryWin'];
}): ReactElement {
  const allUndecided = data.players.every(p => !p.isWinner);

  return (
    <div data-slot="scoring-panel-binary-win" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>

      {/* In-progress banner when no outcome is decided yet */}
      {allUndecided && (
        <div
          role="status"
          aria-live="polite"
          className="rounded-lg border border-entity-session/25 bg-entity-session/8 p-3 text-center"
        >
          <p className="text-xs font-medium text-foreground">
            {data.inProgressLabel ?? labels.inProgressLabel}
          </p>
        </div>
      )}

      <ul role="list" className="flex flex-col gap-1" aria-label={labels.heading}>
        {data.players.map(player => {
          const resultLabel = player.isWinner ? labels.winLabel : labels.loseLabel;
          const outcomeAriaLabel = labels.outcomeAriaTemplate
            .replace('{name}', player.displayName)
            .replace('{result}', resultLabel);

          return (
            <li
              key={player.id}
              data-slot="binary-win-row"
              className={[
                'flex items-center gap-2 rounded-lg px-2 py-1.5',
                player.isWinner
                  ? 'border border-entity-session/30 bg-entity-session/10'
                  : 'border border-transparent bg-card',
              ].join(' ')}
            >
              {/* Win/lose indicator */}
              <span
                aria-label={outcomeAriaLabel}
                data-outcome={player.isWinner ? 'win' : 'lose'}
                className={[
                  'inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-xs font-bold',
                  player.isWinner
                    ? 'bg-entity-session text-white'
                    : 'border border-border bg-muted text-muted-foreground',
                ].join(' ')}
                aria-hidden="false"
              >
                {player.isWinner ? '✓' : '–'}
              </span>

              {/* Player name */}
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
              </span>

              {/* Outcome label */}
              <span
                className={[
                  'shrink-0 text-xs font-medium',
                  player.isWinner ? 'text-entity-session' : 'text-muted-foreground',
                ].join(' ')}
              >
                {resultLabel}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

// ─── Objectives variant ───────────────────────────────────────────────────────

function ObjectivesPanel({
  data,
  labels,
}: {
  data: ObjectivesScoringData;
  labels: ScoringPanelRendererLabels['objectives'];
}): ReactElement {
  const totalObjectives = data.objectives.length;
  const allDone = data.objectives.filter(o => o.done).length;

  return (
    <div data-slot="scoring-panel-objectives" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>

      {/* Per-player completion summary */}
      <ul role="list" className="flex flex-col gap-1" aria-label={labels.heading}>
        {data.players.map(player => {
          const doneCount = data.objectives.filter(
            o => o.done && player.completedObjectives.includes(o.id)
          ).length;
          const completedAriaLabel = labels.completedAriaTemplate.replace(
            '{name}',
            player.displayName
          );

          return (
            <li
              key={player.id}
              data-slot="objectives-player-row"
              className="flex items-center gap-2 rounded-lg border border-transparent bg-card px-2 py-1.5"
            >
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
              </span>
              <span
                aria-label={completedAriaLabel}
                className="shrink-0 tabular-nums text-xs font-bold text-entity-session"
              >
                {doneCount}/{totalObjectives}
              </span>
            </li>
          );
        })}
      </ul>

      {/* Objectives checklist */}
      <ul role="list" className="flex flex-col gap-1" aria-label={`${labels.heading} checklist`}>
        {data.objectives.map(obj => {
          const checkAriaLabel = obj.done
            ? labels.doneAriaTemplate.replace('{label}', obj.label)
            : labels.pendingAriaTemplate.replace('{label}', obj.label);

          return (
            <li
              key={obj.id}
              data-slot="objective-item"
              className={[
                'flex items-center gap-2 rounded-lg px-2 py-1.5',
                obj.done
                  ? 'border border-entity-session/25 bg-entity-session/8'
                  : 'border border-transparent bg-card',
              ].join(' ')}
            >
              {/* Checkbox indicator (read-only) */}
              <span
                role="img"
                aria-label={checkAriaLabel}
                className={[
                  'inline-flex h-4 w-4 shrink-0 items-center justify-center rounded text-xs font-bold',
                  obj.done
                    ? 'bg-entity-session text-white'
                    : 'border-2 border-border bg-transparent text-transparent',
                ].join(' ')}
              >
                {obj.done ? '✓' : ''}
              </span>

              {/* Objective label */}
              <span
                className={[
                  'min-w-0 flex-1 truncate text-xs font-medium',
                  obj.done ? 'text-muted-foreground line-through' : 'text-foreground',
                ].join(' ')}
              >
                {obj.label}
              </span>
            </li>
          );
        })}
      </ul>

      {/* Overall progress summary */}
      {totalObjectives > 0 && (
        <p aria-live="polite" className="text-right text-xs text-muted-foreground">
          {allDone}/{totalObjectives}
        </p>
      )}
    </div>
  );
}

// ─── Dispatcher ───────────────────────────────────────────────────────────────

/**
 * ScoringPanelRenderer — polymorphic read-only scoring display.
 *
 * Dispatches to one of 4 panel-local sub-components based on `data.kind`.
 * Adding a new `ScoreType` backend value requires adding a matching case here;
 * TypeScript will surface a compile error via `assertNever` until then.
 */
export function ScoringPanelRenderer({
  data,
  labels,
  className,
}: ScoringPanelRendererProps): ReactElement {
  const wrapper = (child: ReactElement) => (
    <div data-slot="scoring-panel-renderer" className={className}>
      {child}
    </div>
  );

  switch (data.kind) {
    case 'Points':
      return wrapper(<PointsPanel data={data} labels={labels.points} />);

    case 'Ranking':
      return wrapper(<RankingPanel data={data} labels={labels.ranking} />);

    case 'BinaryWin':
      return wrapper(<BinaryWinPanel data={data} labels={labels.binaryWin} />);

    case 'Objectives':
      return wrapper(<ObjectivesPanel data={data} labels={labels.objectives} />);

    default:
      return assertNever(data);
  }
}

// ─── Exhaustiveness helper ────────────────────────────────────────────────────

function assertNever(value: never): never {
  throw new Error(
    `ScoringPanelRenderer: unhandled scoring kind "${(value as { kind: string }).kind}". ` +
      'Add a case to the switch in ScoringPanelRenderer.'
  );
}
