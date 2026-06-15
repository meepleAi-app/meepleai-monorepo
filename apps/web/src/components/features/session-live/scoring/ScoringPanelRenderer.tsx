/**
 * ScoringPanelRenderer — polymorphic dispatcher + host/viewer role gate.
 *
 * This is the centerpiece of the G5a feature (issue #2373). It:
 *   1. Selects the read-side variant panel from {Points, Ranking, BinaryWin,
 *      Objectives} based on `data.scoringType`.
 *   2. Composes `PolymorphicScoreEditor` (write-side) when the viewer's role
 *      satisfies the canEdit predicate (§3 D3).
 *
 * Role gate (matches plan §3 D3):
 *   Host                       → always edit (any ScoreType)
 *   Player + Points + in-list  → edit OWN score only (legacy carve-out)
 *   Player + non-Points        → read-only
 *   Spectator                  → read-only
 *
 * Editor composition contract:
 *   - The Host editor receives ALL players from `data`.
 *   - The Player+Points editor receives only the viewer's own player row
 *     (scoping prevents the editor from rendering inputs for other players).
 *   - `availableObjectives` is derived from `data.objectives[].label` when
 *     hosting an Objectives session unless the orchestrator supplies a richer
 *     prop. Required by `PolymorphicScoreEditor` (throws otherwise).
 *
 * Token discipline (CLAUDE.md § Token Canonicalization, DS-15 error level):
 *   - This file is a layout-only composition; the variant panels and editor
 *     own their own styling. No direct color utilities here.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T6).
 * Plan: docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md §4 T6
 */

'use client';

import { type ReactElement, useMemo } from 'react';

import {
  PolymorphicScoreEditor,
  type ScoreChangePayload,
} from '@/components/sessions/PolymorphicScoreEditor';
import type { PlayerOption, ScoreDataByType } from '@/components/sessions/score-strategies/types';
import type { ParticipantRole } from '@/lib/session-live/participant-role';

import { ScoringPanelEmpty, type ScoringPanelEmptyLabels } from './ScoringPanelEmpty';
import { BinaryWinPanel, type BinaryWinPanelLabels } from './variants/BinaryWinPanel';
import { ObjectivesPanel, type ObjectivesPanelLabels } from './variants/ObjectivesPanel';
import { PointsPanel, type PointsPanelLabels } from './variants/PointsPanel';
import { RankingPanel, type RankingPanelLabels } from './variants/RankingPanel';

import type { ScoringPanelData } from './types';

export interface ScoringPanelRendererLabels {
  readonly empty: ScoringPanelEmptyLabels;
  readonly points: PointsPanelLabels;
  readonly ranking: RankingPanelLabels;
  readonly binaryWin: BinaryWinPanelLabels;
  readonly objectives: ObjectivesPanelLabels;
}

export interface ScoringPanelRendererProps {
  readonly data: ScoringPanelData | null;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  /** Required only when the editor is mounted (Host or Player+Points). */
  readonly sessionId?: string;
  /**
   * Required for the Objectives editor when host. When omitted, the renderer
   * derives the list from `data.objectives[].label` so the editor cannot
   * crash on a missing prop.
   */
  readonly availableObjectives?: readonly string[];
  /**
   * Orchestrator-supplied editor roster. REQUIRED when the host edits a
   * `BinaryWin` or `Objectives` session because those variants do NOT carry a
   * player roster on the read-side data shape. For `Points` and `Ranking` the
   * renderer falls back to the read-side roster when this prop is undefined.
   *
   * The Player+Points carve-out always filters to the viewer's own row,
   * regardless of what is passed here.
   */
  readonly editorPlayers?: readonly PlayerOption[];
  /**
   * Forwarded to `PolymorphicScoreEditor`. The orchestrator wires this to
   * `useUpdateSessionScores`; on this layer we are agnostic.
   */
  readonly onScoreChange?: (payload: ScoreChangePayload) => void;
  /** Initial editor data. Shape must match `data.scoringType`. */
  readonly editorInitialData?: ScoreDataByType[keyof ScoreDataByType];
  /** Optional disabled toggle forwarded to the editor (e.g. during a save). */
  readonly editorDisabled?: boolean;
  readonly labels: ScoringPanelRendererLabels;
  readonly className?: string;
  /** Defaults to "scoring-panel". E2E tests can override per page. */
  readonly 'data-testid'?: string;
  /** Defaults to "Scoring panel". Localize via the labels block if needed. */
  readonly 'aria-label'?: string;
}

/**
 * Role-gate predicate. Matches plan §3 D3.
 *
 * Player + non-Points returns FALSE because Ranking/BinaryWin/Objectives are
 * host-resolved at game end (mockup intent).
 */
function canEdit(
  viewerRole: ParticipantRole,
  scoringType: ScoringPanelData['scoringType']
): boolean {
  if (viewerRole === 'Host') return true;
  if (viewerRole === 'Player' && scoringType === 'Points') return true;
  return false;
}

/**
 * Map `ScoringPlayerView` (read-side) → `PlayerOption` (editor write-side).
 * Both share `id` + `displayName`; the editor's optional `avatar` is unused
 * on this branch (mockup omits avatars in the editor stripe).
 */
function toPlayerOption(
  view: ScoringPanelData extends infer T
    ? T extends { players: ReadonlyArray<infer P> }
      ? P
      : T extends { ranking: ReadonlyArray<infer R> }
        ? R
        : never
    : never
): PlayerOption {
  return {
    id: (view as { id: string }).id,
    displayName: (view as { displayName: string }).displayName,
  };
}

/**
 * Resolve the player list passed to the editor.
 *
 * - Host: ALL players from the read-side data.
 * - Player + Points: ONLY the viewer's own player row (carve-out).
 *
 * Falls back to an empty array when the read-side variant carries no
 * player list (BinaryWin / Objectives are not player-keyed for the editor
 * in the same way, but the editor still asks for `players` — the
 * orchestrator typically passes session participants in those cases; in
 * unit tests we lean on the data fixtures).
 */
function resolveEditorPlayers(
  data: ScoringPanelData,
  viewerRole: ParticipantRole,
  viewerId: string,
  editorPlayersOverride: readonly PlayerOption[] | undefined
): readonly PlayerOption[] {
  // Extract a native roster from variant data where one exists.
  let nativeRoster: ReadonlyArray<{ readonly id: string; readonly displayName: string }> = [];
  if (data.scoringType === 'Points') {
    nativeRoster = data.players;
  } else if (data.scoringType === 'Ranking') {
    nativeRoster = data.ranking;
  }
  // BinaryWin / Objectives: no native roster on the read-side data.

  if (viewerRole === 'Host') {
    // Prefer orchestrator override; fall back to native roster for
    // Points/Ranking; otherwise the editor cannot mount.
    if (editorPlayersOverride !== undefined) return editorPlayersOverride;
    return nativeRoster.map(toPlayerOption);
  }

  if (viewerRole === 'Player' && data.scoringType === 'Points') {
    const own = nativeRoster.find(p => p.id === viewerId);
    return own ? [toPlayerOption(own)] : [];
  }

  return [];
}

/**
 * Derive `availableObjectives` for the editor when scoringType is 'Objectives'.
 * Returns undefined for other variants (editor ignores it).
 */
function resolveAvailableObjectives(
  data: ScoringPanelData,
  override: readonly string[] | undefined
): readonly string[] | undefined {
  if (data.scoringType !== 'Objectives') return undefined;
  if (override) return override;
  return data.objectives.map(o => o.label);
}

export function ScoringPanelRenderer({
  data,
  viewerRole,
  viewerId,
  sessionId: _sessionId,
  availableObjectives,
  editorPlayers,
  onScoreChange,
  editorInitialData,
  editorDisabled,
  labels,
  className,
  'data-testid': dataTestId = 'scoring-panel',
  'aria-label': ariaLabel = 'Scoring panel',
}: ScoringPanelRendererProps): ReactElement {
  // ── 1. null data → render the shared empty primitive ─────────────────────
  if (data === null) {
    return (
      <section
        data-testid={dataTestId}
        data-score-type="unknown"
        aria-label={ariaLabel}
        className={`flex flex-col gap-3 ${className ?? ''}`}
      >
        <ScoringPanelEmpty labels={labels.empty} />
      </section>
    );
  }

  // ── 2. Read-side panel selection ──────────────────────────────────────────
  const readPanel = ((): ReactElement => {
    switch (data.scoringType) {
      case 'Points':
        return <PointsPanel data={data} labels={labels.points} />;
      case 'Ranking':
        return <RankingPanel data={data} labels={labels.ranking} />;
      case 'BinaryWin':
        return <BinaryWinPanel data={data} labels={labels.binaryWin} />;
      case 'Objectives':
        return <ObjectivesPanel data={data} labels={labels.objectives} />;
      default: {
        // Exhaustive check — TS narrows `data` to never if a new variant
        // lands without a case here. At runtime this branch is unreachable
        // but we surface an empty as a safety net.
        const _exhaustive: never = data;
        void _exhaustive;
        return <ScoringPanelEmpty labels={labels.empty} />;
      }
    }
  })();

  // ── 3. Role gate + editor composition ─────────────────────────────────────
  const gateOpen = canEdit(viewerRole, data.scoringType);
  const resolvedEditorPlayers = useMemo(
    () => (gateOpen ? resolveEditorPlayers(data, viewerRole, viewerId, editorPlayers) : []),
    [gateOpen, data, viewerRole, viewerId, editorPlayers]
  );
  const objectivesList = resolveAvailableObjectives(data, availableObjectives);
  const mountEditor = gateOpen && resolvedEditorPlayers.length > 0;

  return (
    <section
      data-testid={dataTestId}
      data-score-type={data.scoringType}
      aria-label={ariaLabel}
      className={`flex flex-col gap-3 ${className ?? ''}`}
    >
      {readPanel}
      {mountEditor && (
        <PolymorphicScoreEditor
          scoringType={data.scoringType}
          players={resolvedEditorPlayers}
          initialData={editorInitialData}
          availableObjectives={objectivesList}
          onChange={onScoreChange ?? (() => undefined)}
          disabled={editorDisabled}
        />
      )}
    </section>
  );
}
