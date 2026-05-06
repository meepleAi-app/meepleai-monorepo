/**
 * SSE event type definitions for `/sessions/[id]/live` (Wave D.2).
 *
 * Typed event names enum + payload discriminated unions for the SSE stream
 * at `GET /api/v1/game-sessions/{id}/stream/v2`.
 *
 * SCHEMA REALITY V1 CARRYOVER (Gate B audit, mirror Wave D.1 PR #736 pattern):
 *
 * SSE event payload shapes documented based on Phase 0.5 contract §3.1.
 * Verification against backend event records DEFERRED to Interactions sub-PR
 * implementation (when useSessionLiveStream is wired):
 *   grep -rn "INotification|public sealed record.*Event" \
 *     apps/api/src/Api/BoundedContexts/SessionTracking/Application/Events/
 *
 * If backend payload differs from this contract, document v1 carryover gaps
 * in Interactions sub-PR (similar to D.1 SessionPlayerDto pattern).
 *
 * For Foundation sub-PR: this enum is REFERENCE ONLY (no actual SSE handling).
 *
 * Wave D.2 Foundation sub-PR — Issue #746
 */

import type { ParticipantRole } from './participant-role';

/**
 * All typed event names broadcast by the SSE endpoint.
 *
 * Backend event record mapping (to verify in Interactions sub-PR):
 *   'session:score'          → ScoreUpdatedEvent
 *   'session:turn'           → TurnAdvancedEvent
 *   'session:player-join'    → ParticipantJoinedEvent
 *   'session:player-leave'   → ParticipantLeftEvent
 *   'session:role-change'    → ParticipantRoleChangedEvent
 *   'session:pause'          → SessionPausedEvent
 *   'session:resume'         → SessionResumedEvent
 *   'session:endgame'        → SessionEndedEvent
 *   'session:chat'           → ChatMessageEvent (private + shared visibility)
 *   'session:tool-execution' → ToolExecutedEvent (dice/timer/card)
 *   'session:diary'          → DiaryEntryEvent (note added)
 *   'heartbeat'              → server keep-alive (no payload action required)
 */
export type SessionEventType =
  | 'session:score'
  | 'session:turn'
  | 'session:player-join'
  | 'session:player-leave'
  | 'session:role-change'
  | 'session:pause'
  | 'session:resume'
  | 'session:endgame'
  | 'session:chat'
  | 'session:tool-execution'
  | 'session:diary'
  | 'heartbeat';

/**
 * Array of all valid event type strings.
 * Useful for runtime validation of raw SSE `event:` header values.
 */
export const SESSION_EVENT_TYPES: ReadonlyArray<SessionEventType> = [
  'session:score',
  'session:turn',
  'session:player-join',
  'session:player-leave',
  'session:role-change',
  'session:pause',
  'session:resume',
  'session:endgame',
  'session:chat',
  'session:tool-execution',
  'session:diary',
  'heartbeat',
];

/**
 * Discriminated union of all SSE event payloads.
 * Discriminant: `type` field matching SessionEventType.
 *
 * NOTE: All timestamp fields are ISO-8601 strings from the backend.
 */
export type SessionEvent =
  | {
      type: 'session:score';
      sessionId: string;
      participantId: string;
      score: number;
      updatedBy: string;
      timestamp: string;
    }
  | {
      type: 'session:turn';
      sessionId: string;
      turnNumber: number;
      activePlayerId: string;
      timestamp: string;
    }
  | {
      type: 'session:player-join';
      sessionId: string;
      participantId: string;
      playerName: string;
      role: ParticipantRole;
      timestamp: string;
    }
  | {
      type: 'session:player-leave';
      sessionId: string;
      participantId: string;
      timestamp: string;
    }
  | {
      type: 'session:role-change';
      sessionId: string;
      participantId: string;
      oldRole: ParticipantRole;
      newRole: ParticipantRole;
      assignedBy: string;
      timestamp: string;
    }
  | {
      type: 'session:pause';
      sessionId: string;
      pausedBy: string;
      reason?: string;
      timestamp: string;
    }
  | {
      type: 'session:resume';
      sessionId: string;
      resumedBy: string;
      timestamp: string;
    }
  | {
      type: 'session:endgame';
      sessionId: string;
      finalScores: ReadonlyArray<{
        participantId: string;
        score: number;
        winner: boolean;
      }>;
      timestamp: string;
    }
  | {
      type: 'session:chat';
      sessionId: string;
      messageId: string;
      senderId: string;
      content: string;
      visibility: 'private' | 'shared';
      timestamp: string;
    }
  | {
      type: 'session:tool-execution';
      sessionId: string;
      tool: 'dice' | 'timer' | 'card';
      outcome: unknown;
      executedBy: string;
      timestamp: string;
    }
  | {
      type: 'session:diary';
      sessionId: string;
      entryId: string;
      authorId: string;
      content: string;
      timestamp: string;
    }
  | {
      type: 'heartbeat';
      timestamp: string;
    };

/**
 * Type guard: narrows an unknown value to SessionEvent.
 *
 * Checks that `type` is a known SessionEventType. Full payload shape
 * validation is deferred to the SSE hook implementation (Interactions sub-PR).
 */
export function isSessionEvent(value: unknown): value is SessionEvent {
  if (typeof value !== 'object' || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate['type'] === 'string' &&
    SESSION_EVENT_TYPES.includes(candidate['type'] as SessionEventType)
  );
}
