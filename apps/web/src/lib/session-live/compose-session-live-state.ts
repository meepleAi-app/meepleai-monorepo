/**
 * Session Live State Compositor (Wave D.2, Issue #750)
 *
 * Pure reducer that composes an initial session DTO with accumulated SSE events
 * into a unified SessionLiveState object consumed by the orchestrator.
 *
 * ## Design principles
 * - Pure function — no side effects, no React imports
 * - Idempotent — caller (useSessionLiveStream) deduplicates events by SSE envelope ID
 *   before passing them here; this function does NOT re-deduplicate
 * - Immutable — returns new state objects, never mutates in place
 * - Assumes events are pre-sorted ascending (backend stream guarantee)
 *
 * ## Foundation schema (Foundation sub-PR Issue #746)
 * SessionEvent is a flat-field discriminated union — NO id/payload wrapper.
 * Field names:
 *   session:score        → participantId, score, updatedBy
 *   session:turn         → turnNumber, activePlayerId
 *   session:player-join  → participantId, playerName, role
 *   session:player-leave → participantId
 *   session:role-change  → participantId, oldRole, newRole, assignedBy
 *   session:pause        → pausedBy, reason?
 *   session:resume       → resumedBy
 *   session:endgame      → finalScores[{participantId,score,winner}]
 *   session:chat         → messageId, senderId, content, visibility
 *   session:tool-execution → tool, outcome, executedBy
 *   session:diary        → entryId, authorId, content
 *   heartbeat            → timestamp only
 *
 * ## Gate B v1 carryover gaps documented inline (see also parse-sse-event.ts):
 *
 * 1. session:turn — no backend domain event exists; turn state comes from initial DTO.
 *    SSE session:turn events are forward-compat placeholders.
 * 2. session:player-join — role defaults to 'Player' (see parse-sse-event.ts).
 * 3. session:endgame — finalScores normalized in parse-sse-event.ts from BE FinalRanks dict.
 * 4. session:tool-execution — BE has CoinFlipped/WheelSpun events not in FE 'dice'|'timer'|'card' enum.
 */

import type { SessionEvent } from './sse-events';

// ============================================================================
// Public state types
// ============================================================================

export interface LivePlayerState {
  readonly id: string;
  readonly name: string;
  readonly role: 'Spectator' | 'Player' | 'Host';
  readonly score: number;
  readonly isOnline: boolean;
}

export interface LiveLogEntry {
  readonly id: string;
  readonly type: 'score' | 'tool' | 'agent' | 'chat' | 'photo' | 'event';
  readonly authorName: string;
  readonly content: string;
  readonly timestamp: string;
}

export interface SessionLiveState {
  readonly status: 'InProgress' | 'Paused' | 'Setup';
  readonly currentTurn: number;
  readonly totalTurns: number;
  readonly activePlayerId: string;
  readonly players: ReadonlyArray<LivePlayerState>;
  readonly actionLog: ReadonlyArray<LiveLogEntry>;
}

// ============================================================================
// Source types (initial data, flexible — DTO or fixture)
// ============================================================================

/**
 * Minimal shape required from the initial data source.
 * Accepts both LiveSessionDto (API) and LiveSessionFixture (visual testing).
 */
export interface InitialSessionData {
  readonly status?: string;
  readonly currentTurnIndex?: number;
  readonly currentTurnPlayerId?: string | null;
  readonly players?: ReadonlyArray<{
    readonly id: string;
    readonly displayName?: string;
    readonly name?: string;
    readonly role?: string;
    readonly totalScore?: number;
    readonly score?: number;
    readonly isActive?: boolean;
    readonly isOnline?: boolean;
  }>;
  // Fixture-compatible extras
  readonly currentTurn?: number;
  readonly totalTurns?: number;
  readonly activePlayerId?: string;
  readonly actionLog?: ReadonlyArray<LiveLogEntry>;
}

// ============================================================================
// Helpers
// ============================================================================

function toStatus(raw: string | undefined): 'InProgress' | 'Paused' | 'Setup' {
  if (raw === 'Paused') return 'Paused';
  if (raw === 'InProgress') return 'InProgress';
  return 'Setup';
}

function toRole(raw: string | undefined): 'Spectator' | 'Player' | 'Host' {
  if (raw === 'Spectator') return 'Spectator';
  if (raw === 'Host') return 'Host';
  return 'Player';
}

/** Convert initial DTO/fixture to base SessionLiveState. */
function toBaseState(initial: InitialSessionData): SessionLiveState {
  const players: LivePlayerState[] = (initial.players ?? []).map(p => ({
    id: p.id,
    name: p.displayName ?? p.name ?? '',
    role: toRole(p.role),
    score: p.totalScore ?? p.score ?? 0,
    isOnline: p.isOnline ?? p.isActive ?? true,
  }));

  return {
    status: toStatus(initial.status),
    currentTurn: initial.currentTurn ?? initial.currentTurnIndex ?? 0,
    totalTurns: initial.totalTurns ?? 0,
    activePlayerId: initial.activePlayerId ?? initial.currentTurnPlayerId ?? players[0]?.id ?? '',
    players,
    actionLog: initial.actionLog ?? [],
  };
}

// ============================================================================
// Event reducers (one per SSE event type)
// ============================================================================

function applyScore(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:score' }>
): SessionLiveState {
  const players = state.players.map(p =>
    p.id === event.participantId ? { ...p, score: event.score } : p
  );
  return { ...state, players };
}

function applyTurn(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:turn' }>
): SessionLiveState {
  return {
    ...state,
    currentTurn: event.turnNumber,
    // Preserve existing totalTurns when event sends 0 (v1 carryover: BE lacks totalTurns)
    totalTurns:
      event.turnNumber > 0 && state.totalTurns === 0 ? state.totalTurns : state.totalTurns,
    activePlayerId: event.activePlayerId,
  };
}

function applyPlayerJoin(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:player-join' }>
): SessionLiveState {
  // Guard: if player already exists, mark online only
  const exists = state.players.some(p => p.id === event.participantId);
  if (exists) {
    return {
      ...state,
      players: state.players.map(p =>
        p.id === event.participantId ? { ...p, isOnline: true } : p
      ),
    };
  }
  const newPlayer: LivePlayerState = {
    id: event.participantId,
    name: event.playerName,
    role: event.role,
    score: 0,
    isOnline: true,
  };
  return { ...state, players: [...state.players, newPlayer] };
}

function applyPlayerLeave(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:player-leave' }>
): SessionLiveState {
  // Mark offline, do NOT remove — preserves score history in UI
  return {
    ...state,
    players: state.players.map(p => (p.id === event.participantId ? { ...p, isOnline: false } : p)),
  };
}

function applyRoleChange(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:role-change' }>
): SessionLiveState {
  return {
    ...state,
    players: state.players.map(p =>
      p.id === event.participantId ? { ...p, role: event.newRole } : p
    ),
  };
}

function applyPause(state: SessionLiveState): SessionLiveState {
  return { ...state, status: 'Paused' };
}

function applyResume(state: SessionLiveState): SessionLiveState {
  return { ...state, status: 'InProgress' };
}

function applyEndgame(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:endgame' }>
): SessionLiveState {
  let players = state.players;
  if (event.finalScores.length > 0) {
    players = state.players.map(p => {
      const final = event.finalScores.find(s => s.participantId === p.id);
      return final ? { ...p, score: final.score } : p;
    });
  }
  return { ...state, status: 'Paused', players };
}

function applyChat(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:chat' }>
): SessionLiveState {
  const entry: LiveLogEntry = {
    id: event.messageId,
    type: 'chat',
    authorName: event.senderId,
    content: event.content,
    timestamp: event.timestamp,
  };
  return { ...state, actionLog: [...state.actionLog, entry] };
}

function applyToolExecution(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:tool-execution' }>
): SessionLiveState {
  const entry: LiveLogEntry = {
    id: `${event.executedBy}-${event.timestamp}`,
    type: 'tool',
    authorName: event.executedBy,
    content: typeof event.outcome === 'string' ? event.outcome : JSON.stringify(event.outcome),
    timestamp: event.timestamp,
  };
  return { ...state, actionLog: [...state.actionLog, entry] };
}

function applyDiary(
  state: SessionLiveState,
  event: Extract<SessionEvent, { type: 'session:diary' }>
): SessionLiveState {
  const entry: LiveLogEntry = {
    id: event.entryId,
    type: 'event',
    authorName: event.authorId,
    content: event.content,
    timestamp: event.timestamp,
  };
  return { ...state, actionLog: [...state.actionLog, entry] };
}

// ============================================================================
// Core reducer
// ============================================================================

function applyEvent(state: SessionLiveState, event: SessionEvent): SessionLiveState {
  switch (event.type) {
    case 'session:score':
      return applyScore(state, event);
    case 'session:turn':
      return applyTurn(state, event);
    case 'session:player-join':
      return applyPlayerJoin(state, event);
    case 'session:player-leave':
      return applyPlayerLeave(state, event);
    case 'session:role-change':
      return applyRoleChange(state, event);
    case 'session:pause':
      return applyPause(state);
    case 'session:resume':
      return applyResume(state);
    case 'session:endgame':
      return applyEndgame(state, event);
    case 'session:chat':
      return applyChat(state, event);
    case 'session:tool-execution':
      return applyToolExecution(state, event);
    case 'session:diary':
      return applyDiary(state, event);
    case 'heartbeat':
      return state; // no-op
    default:
      return state;
  }
}

// ============================================================================
// Public API
// ============================================================================

/**
 * Compose live state from initial DTO/fixture + accumulated SSE events.
 *
 * Idempotency guarantee: caller (useSessionLiveStream) deduplicates events by
 * SSE envelope Last-Event-ID before accumulating. This function assumes the
 * events array has already been deduplicated.
 *
 * Pure: no side effects.
 *
 * @param initial - Initial session data from REST query (LiveSessionDto) or fixture
 * @param events  - Accumulated SSE events (pre-deduplicated, assumed sorted ascending)
 * @returns Derived SessionLiveState ready for component consumption
 */
export function composeSessionLiveState(
  initial: InitialSessionData,
  events: ReadonlyArray<SessionEvent>
): SessionLiveState {
  const base = toBaseState(initial);
  let state = base;

  for (const event of events) {
    state = applyEvent(state, event);
  }

  return state;
}
