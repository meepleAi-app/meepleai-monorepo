'use client';

/**
 * TurnState — Issue #2378 G5b.
 *
 * Discriminated union covering all 7 `TurnOrderType` variants plus shared
 * `PlayerInfo` shape. The renderer dispatcher narrows on `state.type`.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2378-g5b-turn-indicator-renderer-design.md §4
 */

export type TurnOrderType =
  | 'RoundRobin'
  | 'Sequential'
  | 'Simultaneous'
  | 'Realtime'
  | 'None'
  | 'Custom'
  | 'FirstPlayerToken';

export interface PlayerInfo {
  readonly id: string;
  readonly name: string;
  readonly avatarUrl?: string;
}

export type TurnState =
  | {
      readonly type: 'RoundRobin';
      readonly round: number;
      readonly totalRounds: number;
      readonly activePlayerId: string;
      readonly playOrder: ReadonlyArray<string>;
    }
  | {
      readonly type: 'Sequential';
      readonly phases: ReadonlyArray<string>;
      readonly activePhaseIndex: number;
    }
  | {
      readonly type: 'Simultaneous';
      readonly phases?: ReadonlyArray<string>;
      readonly activePhaseIndex?: number;
    }
  | { readonly type: 'Realtime' }
  | { readonly type: 'None' }
  | {
      readonly type: 'Custom';
      readonly phases: ReadonlyArray<string>;
      readonly activePhaseIndex: number;
    }
  | {
      readonly type: 'FirstPlayerToken';
      readonly round: number;
      readonly totalRounds: number;
      readonly tokenHolderId: string;
      readonly playOrder: ReadonlyArray<string>;
    };
