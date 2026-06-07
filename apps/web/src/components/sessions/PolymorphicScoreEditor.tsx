/**
 * PolymorphicScoreEditor — strategy dispatcher.
 *
 * Mirrors the asse A backend `ScoringStrategyFactory`: routes the runtime
 * `scoringType` discriminator to the matching editor primitive and lifts the
 * structured `onChange` payload back to the caller alongside its tag. The
 * exhaustive `switch` will surface a compile-time error if a new `ScoreType`
 * is added to the enum but not wired here.
 *
 * Asse D follow-up P1 (#1899) T4.
 *
 * @module components/sessions/PolymorphicScoreEditor
 */

'use client';

import { BinaryWinEditor } from './score-strategies/BinaryWinEditor';
import { ObjectivesEditor } from './score-strategies/ObjectivesEditor';
import { PointsEditor } from './score-strategies/PointsEditor';
import { RankingEditor } from './score-strategies/RankingEditor';

import type {
  BinaryWinScoreData,
  ObjectivesScoreData,
  PlayerOption,
  PointsScoreData,
  RankingScoreData,
  ScoreDataByType,
  ScoreType,
} from './score-strategies/types';

/**
 * Tagged discriminated union for the dispatcher's `onChange` callback.
 *
 * Callers can switch on `scoringType` to narrow `data` without unsafe casts:
 *
 * ```ts
 * if (payload.scoringType === 'Points') {
 *   payload.data.scores.forEach(...);
 * }
 * ```
 */
export type ScoreChangePayload =
  | { scoringType: 'Points'; data: PointsScoreData }
  | { scoringType: 'BinaryWin'; data: BinaryWinScoreData }
  | { scoringType: 'Objectives'; data: ObjectivesScoreData }
  | { scoringType: 'Ranking'; data: RankingScoreData };

export interface PolymorphicScoreEditorProps {
  scoringType: ScoreType;
  players: readonly PlayerOption[];
  /**
   * Optional pre-filled score data. The exact shape MUST match `scoringType`;
   * callers are responsible for passing a fresh / undefined value when the
   * scoring type changes, otherwise the underlying strategy may discard the
   * mismatched payload silently.
   */
  initialData?: ScoreDataByType[ScoreType];
  /**
   * Required when `scoringType === 'Objectives'`. Ignored for other types.
   */
  availableObjectives?: readonly string[];
  /**
   * Tagged dispatch. The implementation forwards each strategy's specific
   * payload as a discriminated `ScoreChangePayload`.
   */
  onChange: (payload: ScoreChangePayload) => void;
  disabled?: boolean;
}

export function PolymorphicScoreEditor({
  scoringType,
  players,
  initialData,
  availableObjectives,
  onChange,
  disabled,
}: PolymorphicScoreEditorProps) {
  switch (scoringType) {
    case 'Points':
      return (
        <PointsEditor
          players={players}
          initialData={initialData as PointsScoreData | undefined}
          onChange={data => onChange({ scoringType: 'Points', data })}
          disabled={disabled}
        />
      );
    case 'BinaryWin':
      return (
        <BinaryWinEditor
          players={players}
          initialData={initialData as BinaryWinScoreData | undefined}
          onChange={data => onChange({ scoringType: 'BinaryWin', data })}
          disabled={disabled}
        />
      );
    case 'Objectives': {
      if (!availableObjectives) {
        throw new Error(
          'PolymorphicScoreEditor: `availableObjectives` is required when scoringType="Objectives"'
        );
      }
      return (
        <ObjectivesEditor
          players={players}
          availableObjectives={availableObjectives}
          initialData={initialData as ObjectivesScoreData | undefined}
          onChange={data => onChange({ scoringType: 'Objectives', data })}
          disabled={disabled}
        />
      );
    }
    case 'Ranking':
      return (
        <RankingEditor
          players={players}
          initialData={initialData as RankingScoreData | undefined}
          onChange={data => onChange({ scoringType: 'Ranking', data })}
          disabled={disabled}
        />
      );
    default: {
      const exhaustive: never = scoringType;
      throw new Error(`PolymorphicScoreEditor: unknown scoring type ${exhaustive as string}`);
    }
  }
}
