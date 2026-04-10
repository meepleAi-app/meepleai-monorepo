/**
 * Contextual Hand Store — Types (Session Flow v2.1)
 *
 * State and action interfaces for the Zustand store that powers the
 * "Contextual Hand" UI: a persistent floating panel tracking the
 * current active/paused game session.
 */

import type {
  CurrentSessionDto,
  CreateSessionResult,
  DiaryEntryDto,
  KbReadinessDto,
  SetTurnOrderResult,
  AdvanceTurnResult,
  RollSessionDiceResult,
  UpsertScoreResult,
} from '@/lib/api/session-flow/types';

// ─── Hand Context ──────────────────────────────────────────────────────────

/** High-level lifecycle phase of the contextual hand. */
export type HandContext =
  | 'idle' // no active session — show game picker
  | 'setup' // session created, pre-play (turn order, etc.)
  | 'active' // session in Active status — gameplay
  | 'paused'; // session paused — show resume CTA

// ─── State ─────────────────────────────────────────────────────────────────

export interface ContextualHandState {
  /** Current lifecycle phase. */
  context: HandContext;

  /** The active/paused session loaded from the API (null when idle). */
  currentSession: CurrentSessionDto | null;

  /** Extra data returned by createSession (agentId, toolkitId, gameNightId). */
  createResult: CreateSessionResult | null;

  /** True while any primary async action is in-flight. */
  isLoading: boolean;

  /** Last error message (cleared on next action). */
  error: string | null;

  /** Append-only diary entries for the current session. */
  diaryEntries: DiaryEntryDto[];

  /** True while diary is being fetched. */
  isDiaryLoading: boolean;

  /** KB readiness probe result (for game picker). */
  kbReadiness: KbReadinessDto | null;
}

// ─── Actions ───────────────────────────────────────────────────────────────

export interface ContextualHandActions {
  /** Load the caller's current Active/Paused session (orphan recovery). */
  initialize: () => Promise<void>;

  /** Create a new session for a game and transition to active. */
  startSession: (gameId: string, guestNames?: string[], gameNightEventId?: string) => Promise<void>;

  /** Pause the current session. */
  pauseSession: () => Promise<void>;

  /** Resume the current (paused) session. */
  resumeSession: () => Promise<void>;

  /** Set or shuffle the turn order. */
  setTurnOrder: (
    method: 'manual' | 'random',
    order?: string[]
  ) => Promise<SetTurnOrderResult | null>;

  /** Advance the turn to the next participant. */
  advanceTurn: () => Promise<AdvanceTurnResult | null>;

  /** Roll dice for a participant. */
  rollDice: (
    participantId: string,
    formula: string,
    label?: string
  ) => Promise<RollSessionDiceResult | null>;

  /** Upsert a participant's score (with optional round/category). */
  upsertScore: (
    participantId: string,
    newValue: number,
    roundNumber?: number,
    category?: string,
    reason?: string
  ) => Promise<UpsertScoreResult | null>;

  /** Fetch diary entries for the current session. */
  loadDiary: (eventTypes?: string) => Promise<void>;

  /** Probe KB readiness for a game (used in game picker). */
  checkKbReadiness: (gameId: string) => Promise<void>;

  /** Reset the store to idle state and clear localStorage. */
  reset: () => void;
}

// ─── Combined ──────────────────────────────────────────────────────────────

export type ContextualHandStore = ContextualHandState & ContextualHandActions;
