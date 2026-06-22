/**
 * mapTurnDataToTurnState — pure adapter from BE turnOrderType string to FE
 * TurnState discriminated union (renderer input).
 *
 * Issue #2483 Task 3 — wires the static turnOrderType from useLiveSessionStore
 * into SessionLiveView's TurnIndicatorRenderer.
 *
 * ## BE → FE mapping table
 *
 * | BE string       | FE TurnState.type  | Notes                               |
 * |-----------------|--------------------|-------------------------------------|
 * | "RoundRobin"    | 'RoundRobin'       | 1:1                                  |
 * | "Sequential"    | 'Sequential'       | 1:1                                  |
 * | "Simultaneous"  | 'Simultaneous'     | 1:1                                  |
 * | "Realtime"      | 'Realtime'         | 1:1                                  |
 * | "None"          | 'None'             | 1:1                                  |
 * | "Custom"        | 'Custom'           | 1:1                                  |
 * | "Free"          | 'None'             | BE legacy alias; normalised to None  |
 * | null            | 'None'             | Safe fallback (no toolkit wired)     |
 * | unknown string  | 'None'             | Safe fallback (future BE enum value) |
 *
 * Note: "FirstPlayerToken" is a FE-only variant (not produced by the current BE
 * path B). The type exists in TurnOrderType for future compatibility; the mapping
 * table includes it for completeness (if the BE ever sends it, it passes through
 * 1:1). The fallback for truly unknown values uses 'None' — an explicit, neutral
 * variant that renders a readable "no active turn" UI rather than silently
 * impersonating a different turn mode.
 *
 * ## Per-variant fields
 *
 * Fields are populated from the sessionData object where available. When
 * sessionData is null (no REST data yet), sensible zero defaults are used.
 * The adapter never throws — it is defensive by design.
 *
 * Pure function: no side effects, no implicit imports, deterministic.
 */

import type { TurnState } from './turn-state';

// ─── Session data subset ──────────────────────────────────────────────────────

/**
 * Minimal session data shape consumed by the adapter.
 * Compatible with both `LiveSessionFixture` and the composed liveState.
 */
export interface TurnSessionData {
  readonly currentTurn?: number;
  readonly totalTurns?: number;
  readonly activePlayerId?: string;
  readonly players?: ReadonlyArray<{ readonly id: string; readonly name: string }>;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const SAFE_FALLBACK: TurnState = { type: 'None' };

function extractPlayOrder(sessionData: TurnSessionData | null | undefined): ReadonlyArray<string> {
  return sessionData?.players?.map(p => p.id) ?? [];
}

// ─── Exported adapter ────────────────────────────────────────────────────────

/**
 * Map a BE-supplied turnOrderType string (or null) into a concrete `TurnState`
 * that TurnIndicatorRenderer can dispatch on.
 *
 * @param turnOrderType - Raw string from SessionDto.turnOrderType or store field. May be null.
 * @param sessionData   - Current session data for per-variant fields (round, playOrder, etc.).
 *                        Pass null/undefined when the session data is not yet available.
 * @returns A valid `TurnState` — never null/undefined.
 */
export function mapTurnDataToTurnState(
  turnOrderType: string | null | undefined,
  sessionData: TurnSessionData | null | undefined
): TurnState {
  const currentTurn = sessionData?.currentTurn ?? 0;
  const totalTurns = sessionData?.totalTurns ?? 0;
  const activePlayerId = sessionData?.activePlayerId ?? '';
  const playOrder = extractPlayOrder(sessionData);

  // Normalise BE-only alias before the switch.
  const normalised = turnOrderType === 'Free' ? 'None' : turnOrderType;

  switch (normalised) {
    case 'RoundRobin':
      return {
        type: 'RoundRobin',
        round: currentTurn,
        totalRounds: totalTurns,
        activePlayerId,
        playOrder,
      };

    case 'Sequential':
      // Sequential phases come from the toolkit; not available in the live session
      // data at this level. Provide a single synthetic phase keyed on currentTurn
      // so the renderer has a non-empty array to display.
      return {
        type: 'Sequential',
        phases: [`Fase ${currentTurn > 0 ? currentTurn : 1}`],
        activePhaseIndex: 0,
      };

    case 'Simultaneous':
      return { type: 'Simultaneous' };

    case 'Realtime':
      return { type: 'Realtime' };

    case 'None':
      return { type: 'None' };

    case 'Custom':
      return {
        type: 'Custom',
        phases: [`Fase ${currentTurn > 0 ? currentTurn : 1}`],
        activePhaseIndex: 0,
      };

    case 'FirstPlayerToken':
      // FE-only type; pass through if BE ever produces it.
      return {
        type: 'FirstPlayerToken',
        round: currentTurn,
        totalRounds: totalTurns,
        tokenHolderId: activePlayerId,
        playOrder,
      };

    default:
      // Unknown BE string or null — safe neutral fallback.
      // 'None' renders an explicit "no active turn" UI (not a silent fake type).
      return SAFE_FALLBACK;
  }
}
