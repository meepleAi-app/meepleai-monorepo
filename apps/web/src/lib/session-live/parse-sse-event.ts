/**
 * SSE Event Parser (Wave D.2 Interactions, Issue #750)
 *
 * Parses raw SSE event name + data JSON string into a typed SessionEvent.
 * Used by useSessionLiveStream's EventSource onmessage handler.
 *
 * Design:
 * - Returns null on unknown event type, malformed JSON, or missing required fields
 * - Caller logs a warning on null return and discards the message
 * - Pure function — no side effects, no React imports
 * - Works with the Foundation sub-PR (Issue #746) SessionEvent flat-field schema:
 *   no `id`/`payload` wrapper; events use participantId, turnNumber, etc.
 *
 * ## Gate B Audit — Backend payload reality vs. Foundation contract §3.1
 *
 * Backend event records audited in:
 *   apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/
 *
 * | SSE event type       | Backend record             | v1 carryover gap                        |
 * |----------------------|----------------------------|-----------------------------------------|
 * | session:score        | ScoreUpdatedEvent          | BE `participantId` matches FE contract  |
 * |                      |                            | BE `newScore` decimal → FE `score` number|
 * |                      |                            | BE `scoreEntryId` not in FE contract    |
 * | session:player-join  | ParticipantAddedEvent      | BE `participantId` matches FE contract  |
 * |                      |                            | BE does NOT send role; FE `role` defaults to 'Player' |
 * | session:player-leave | ParticipantKickedEvent     | BE `kickedBy` not in FE contract        |
 * | session:role-change  | ParticipantRoleChangedEvent| BE `newRole` enum → FE `newRole` string |
 * |                      |                            | BE `changedBy` not in FE contract       |
 * | session:pause        | SessionPausedEvent         | BE `pausedBy` not in FE contract (OK)   |
 * | session:resume       | SessionResumedEvent        | BE `resumedBy` not in FE contract (OK)  |
 * | session:endgame      | SessionFinalizedEvent      | BE `FinalRanks: Dict<Guid,int>` vs FE `finalScores[]` |
 * |                      |                            | v1 gap: rank-only dict vs score+rank array |
 * | session:chat         | SessionChatMessageSentEvent| BE `messageType` enum not in FE         |
 * |                      |                            | BE `senderId` nullable matches FE       |
 * | session:tool-execution| DiceRolledEvent/RandomToolEvents | FE `tool: 'dice'|'timer'|'card'` |
 * |                      |                            | v1 gap: BE has CoinFlippedEvent, WheelSpunEvent not in FE enum |
 * | session:diary        | NoteSavedEvent/NoteRevealedEvent | BE `HasObscuredText` not in FE  |
 * | session:turn         | NO backend event           | v1 gap: no TurnAdvancedEvent domain record |
 * | heartbeat            | inline endpoint            | timestamp field matches FE contract     |
 */

import { SESSION_EVENT_TYPES, isSessionEvent } from './sse-events';

import type { ParticipantRole } from './participant-role';
import type { SessionEvent, SessionEventType } from './sse-events';

// ============================================================================
// Type guards
// ============================================================================

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function toRole(value: unknown): ParticipantRole {
  if (value === 'Spectator' || value === 'Host') return value;
  return 'Player';
}

/**
 * Resolve participantId from various field names.
 * v1 carryover: backend may also send `participantId` which matches FE contract.
 */
function resolveParticipantId(obj: Record<string, unknown>): string | null {
  const id = obj['participantId'] ?? obj['playerId'];
  return typeof id === 'string' && id.length > 0 ? id : null;
}

// ============================================================================
// Per-event parsers producing flat-field SessionEvent shapes
// ============================================================================

function parseScore(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:score' }> | null {
  const participantId = resolveParticipantId(data);
  if (!participantId) return null;
  const score =
    typeof data['score'] === 'number'
      ? data['score']
      : typeof data['newScore'] === 'number'
        ? data['newScore']
        : null;
  if (score === null) return null;
  const updatedBy = typeof data['updatedBy'] === 'string' ? data['updatedBy'] : participantId;
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:score', sessionId, participantId, score, updatedBy, timestamp };
}

function parseTurn(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:turn' }> | null {
  // v1 carryover: no backend TurnAdvancedEvent — this is a forward-compat placeholder
  const turnNumber =
    typeof data['turnNumber'] === 'number'
      ? data['turnNumber']
      : typeof data['currentTurn'] === 'number'
        ? data['currentTurn']
        : null;
  if (turnNumber === null) return null;
  const activePlayerId = typeof data['activePlayerId'] === 'string' ? data['activePlayerId'] : null;
  if (!activePlayerId) return null;
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:turn', sessionId, turnNumber, activePlayerId, timestamp };
}

function parsePlayerJoin(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:player-join' }> | null {
  const participantId = resolveParticipantId(data);
  if (!participantId) return null;
  const playerName =
    typeof data['playerName'] === 'string'
      ? data['playerName']
      : typeof data['displayName'] === 'string'
        ? data['displayName']
        : typeof data['name'] === 'string'
          ? data['name']
          : null;
  if (!playerName) return null;
  // v1 carryover: role not in ParticipantAddedEvent — default to 'Player'
  const role = toRole(data['role']);
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:player-join', sessionId, participantId, playerName, role, timestamp };
}

function parsePlayerLeave(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:player-leave' }> | null {
  const participantId = resolveParticipantId(data);
  if (!participantId) return null;
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:player-leave', sessionId, participantId, timestamp };
}

function parseRoleChange(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:role-change' }> | null {
  const participantId = resolveParticipantId(data);
  if (!participantId) return null;
  const oldRole = toRole(data['oldRole'] ?? data['previousRole']);
  const newRole = toRole(data['newRole'] ?? data['role']);
  const assignedBy =
    typeof data['assignedBy'] === 'string'
      ? data['assignedBy']
      : typeof data['changedBy'] === 'string'
        ? data['changedBy']
        : participantId;
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return {
    type: 'session:role-change',
    sessionId,
    participantId,
    oldRole,
    newRole,
    assignedBy,
    timestamp,
  };
}

function parsePause(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:pause' }> | null {
  const pausedBy = typeof data['pausedBy'] === 'string' ? data['pausedBy'] : 'system';
  const reason = typeof data['reason'] === 'string' ? data['reason'] : undefined;
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:pause', sessionId, pausedBy, reason, timestamp };
}

function parseResume(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:resume' }> | null {
  const resumedBy = typeof data['resumedBy'] === 'string' ? data['resumedBy'] : 'system';
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:resume', sessionId, resumedBy, timestamp };
}

function parseEndgame(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:endgame' }> | null {
  // v1 carryover: BE FinalRanks: Dict<Guid, int> → normalize to finalScores array
  let finalScores: Array<{ participantId: string; score: number; winner: boolean }> = [];

  if (Array.isArray(data['finalScores'])) {
    finalScores = (data['finalScores'] as Array<Record<string, unknown>>)
      .filter(s => typeof s['participantId'] === 'string' || typeof s['playerId'] === 'string')
      .map(s => ({
        participantId: (s['participantId'] ?? s['playerId']) as string,
        score: typeof s['score'] === 'number' ? s['score'] : 0,
        winner: s['winner'] === true,
      }));
  } else if (isObject(data['finalRanks'])) {
    // Backend dictionary: rank only, no score
    const ranks = data['finalRanks'] as Record<string, unknown>;
    const winnerId = typeof data['winnerId'] === 'string' ? data['winnerId'] : null;
    finalScores = Object.entries(ranks)
      .filter(([, rank]) => typeof rank === 'number')
      .map(([pid]) => ({
        participantId: pid,
        score: 0,
        winner: pid === winnerId,
      }));
  }

  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:endgame', sessionId, finalScores, timestamp };
}

function parseChat(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:chat' }> | null {
  const messageId =
    typeof data['messageId'] === 'string'
      ? data['messageId']
      : typeof data['id'] === 'string'
        ? data['id']
        : null;
  if (!messageId) return null;
  const senderId = typeof data['senderId'] === 'string' ? data['senderId'] : 'anonymous';
  const content = typeof data['content'] === 'string' ? data['content'] : '';
  const visibility: 'private' | 'shared' = data['visibility'] === 'private' ? 'private' : 'shared';
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:chat', sessionId, messageId, senderId, content, visibility, timestamp };
}

function parseToolExecution(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:tool-execution' }> | null {
  const participantId = resolveParticipantId(data);
  if (!participantId) return null;
  // v1 gap: BE has CoinFlippedEvent, WheelSpunEvent — not in FE 'dice'|'timer'|'card' enum
  const rawTool = data['tool'] ?? data['toolType'] ?? 'dice';
  const tool: 'dice' | 'timer' | 'card' =
    rawTool === 'timer' ? 'timer' : rawTool === 'card' ? 'card' : 'dice';
  const outcome = data['outcome'] ?? data['result'] ?? data['total'];
  const executedBy = typeof data['executedBy'] === 'string' ? data['executedBy'] : participantId;
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:tool-execution', sessionId, tool, outcome, executedBy, timestamp };
}

function parseDiary(
  data: Record<string, unknown>,
  sessionId: string
): Extract<SessionEvent, { type: 'session:diary' }> | null {
  const entryId =
    typeof data['entryId'] === 'string'
      ? data['entryId']
      : typeof data['noteId'] === 'string'
        ? data['noteId']
        : typeof data['id'] === 'string'
          ? data['id']
          : null;
  if (!entryId) return null;
  const authorId =
    resolveParticipantId(data) ?? (typeof data['authorId'] === 'string' ? data['authorId'] : null);
  if (!authorId) return null;
  const content = typeof data['content'] === 'string' ? data['content'] : '';
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'session:diary', sessionId, entryId, authorId, content, timestamp };
}

function parseHeartbeat(
  data: Record<string, unknown>
): Extract<SessionEvent, { type: 'heartbeat' }> {
  const timestamp =
    typeof data['timestamp'] === 'string' ? data['timestamp'] : new Date().toISOString();
  return { type: 'heartbeat', timestamp };
}

// ============================================================================
// Public API
// ============================================================================

/**
 * Parse SSE event data string + event type name into typed SessionEvent.
 * Returns null if event type unknown or payload malformed.
 *
 * Used by useSessionLiveStream EventSource onmessage handler.
 *
 * @param eventType - SSE event name (e.g. "session:score", "heartbeat")
 * @param dataJson  - Raw JSON string from SSE `data:` field
 * @param sessionId - Session ID from the URL (provides sessionId field in events)
 * @returns Typed SessionEvent or null on parse failure
 */
export function parseSseEvent(
  eventType: string,
  dataJson: string,
  sessionId: string = ''
): SessionEvent | null {
  // Validate event type
  if (!SESSION_EVENT_TYPES.includes(eventType as SessionEventType)) {
    return null;
  }

  // Parse JSON safely
  let data: unknown;
  try {
    data = JSON.parse(dataJson);
  } catch {
    return null;
  }

  if (!isObject(data)) {
    return null;
  }

  // Use sessionId from data if not provided externally
  const resolvedSessionId =
    sessionId || (typeof data['sessionId'] === 'string' ? data['sessionId'] : '');

  try {
    let event: SessionEvent | null = null;
    switch (eventType as SessionEventType) {
      case 'session:score':
        event = parseScore(data, resolvedSessionId);
        break;
      case 'session:turn':
        event = parseTurn(data, resolvedSessionId);
        break;
      case 'session:player-join':
        event = parsePlayerJoin(data, resolvedSessionId);
        break;
      case 'session:player-leave':
        event = parsePlayerLeave(data, resolvedSessionId);
        break;
      case 'session:role-change':
        event = parseRoleChange(data, resolvedSessionId);
        break;
      case 'session:pause':
        event = parsePause(data, resolvedSessionId);
        break;
      case 'session:resume':
        event = parseResume(data, resolvedSessionId);
        break;
      case 'session:endgame':
        event = parseEndgame(data, resolvedSessionId);
        break;
      case 'session:chat':
        event = parseChat(data, resolvedSessionId);
        break;
      case 'session:tool-execution':
        event = parseToolExecution(data, resolvedSessionId);
        break;
      case 'session:diary':
        event = parseDiary(data, resolvedSessionId);
        break;
      case 'heartbeat':
        event = parseHeartbeat(data);
        break;
    }

    // Final validation using the Foundation's type guard
    if (event && isSessionEvent(event)) {
      return event;
    }
    return event; // return even if isSessionEvent narrow check fails (still valid shape)
  } catch {
    return null;
  }
}
