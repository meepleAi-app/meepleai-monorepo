/**
 * mapScoreDataToPanelData — pure adapter from polymorphic scoreData
 * (editor / store shape) to ScoringPanelData (renderer discriminated union).
 *
 * Issue #2389 Block B (T4) — wires the polymorphic store selector into
 * SessionLiveView's read-only ScoringPanelRenderer.
 *
 * Returns null when scoringType is null OR scoreData is null (no SignalR
 * delivery yet AND no REST hydration). Caller MUST gate the renderer on null.
 *
 * For each variant, `players[]` is the master list: every player appears
 * in the output. Missing scoreData entries are padded with type-specific
 * defaults (Points→0, Ranking→players.length, BinaryWin→false, Objectives→[]).
 * Display name falls back to name per-player when displayName is undefined.
 *
 * Objectives variant: catalogue from `options.availableObjectives ?? []`.
 * Each catalogue entry becomes `{ id: label, label, done: anyPlayerCompleted }`.
 *
 * Pure function: no side effects, no implicit imports, deterministic.
 */

import type { ScoringPanelData } from '@/components/features/session-live';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';

interface AdapterPlayer {
  readonly id: string;
  readonly name: string;
  readonly displayName?: string;
}

export interface MapScoreDataOptions {
  readonly availableObjectives?: ReadonlyArray<string>;
}

export function mapScoreDataToPanelData(
  scoringType: ScoreType | null,
  scoreData: ScoreDataByType[ScoreType] | null,
  players: ReadonlyArray<AdapterPlayer>,
  options?: MapScoreDataOptions
): ScoringPanelData | null {
  if (scoringType === null || scoreData === null) return null;

  switch (scoringType) {
    case 'Points': {
      const data = scoreData as ScoreDataByType['Points'];
      const scoresByPlayer = new Map(data.scores.map(s => [s.playerId, s.points]));
      return {
        kind: 'Points',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          score: scoresByPlayer.get(p.id) ?? 0,
        })),
      };
    }

    case 'BinaryWin': {
      const data = scoreData as ScoreDataByType['BinaryWin'];
      const winnerByPlayer = new Map(data.results.map(r => [r.playerId, r.isWinner]));
      return {
        kind: 'BinaryWin',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          isWinner: winnerByPlayer.get(p.id) ?? false,
        })),
      };
    }

    case 'Ranking': {
      const data = scoreData as ScoreDataByType['Ranking'];
      const positionByPlayer = new Map(data.positions.map(r => [r.playerId, r.position]));
      const lastPosition = players.length;
      return {
        kind: 'Ranking',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          position: positionByPlayer.get(p.id) ?? lastPosition,
        })),
      };
    }

    case 'Objectives': {
      const data = scoreData as ScoreDataByType['Objectives'];
      const objectivesByPlayer = new Map(
        data.completedByPlayer.map(r => [r.playerId, r.objectives])
      );
      const catalogue = options?.availableObjectives ?? [];
      return {
        kind: 'Objectives',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          completedObjectives: objectivesByPlayer.get(p.id) ?? [],
        })),
        objectives: catalogue.map(label => ({
          id: label,
          label,
          done: data.completedByPlayer.some(cb => cb.objectives.includes(label)),
        })),
      };
    }

    default:
      return assertNever(scoringType);
  }
}

function assertNever(value: never): never {
  throw new Error(`mapScoreDataToPanelData: unhandled scoringType "${value as string}"`);
}
