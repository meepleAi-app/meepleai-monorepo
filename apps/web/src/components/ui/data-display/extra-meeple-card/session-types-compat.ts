/**
 * Session types compatibility shim.
 * Previously imported from the deleted meeple-card-features/session-types module.
 * Extended to cover all field access patterns used by the extra-meeple-card tabs.
 */

export type SessionStatus =
  | 'Setup'
  | 'setup'
  | 'InProgress'
  | 'inProgress'
  | 'Paused'
  | 'paused'
  | 'Completed'
  | 'completed'
  | 'Cancelled';

export interface SessionPlayerInfo {
  id: string;
  displayName: string;
  avatarUrl?: string;
  color: string;
  isWinner?: boolean;
  totalScore?: number;
  position?: number;
  /** Player role in session */
  role?: string;
  /** Current rank during active session */
  currentRank?: number;
  /** Whether the player is currently active/taking a turn */
  isActive?: boolean;
}

export interface SessionRoundScore {
  roundNumber: number;
  scores: Record<string, number>;
  /** Round identifier (alias for roundNumber) */
  round?: number;
  /** Player identifier for this score entry */
  playerId?: string;
  /** Score value */
  value?: number;
}

export interface SessionScoringConfig {
  dimensions: string[];
  defaultUnit: string;
  scoreType: string;
  /** Active scoring dimensions */
  enabledDimensions?: string[];
}

export interface SessionTurnInfo {
  currentTurn: number;
  currentPlayerId?: string;
}

export interface SessionTimelineEvent {
  id: string;
  type: string;
  description: string;
  timestamp: string;
  turnNumber?: number;
  /** Human-readable event label */
  label?: string;
}

export interface SessionMediaCounts {
  photos: number;
  notes: number;
}

export interface SessionActionHandlers {
  onPause?: () => void;
  onResume?: () => void;
  onEnd?: () => void;
  onSave?: () => void;
  onAddPlayer?: () => void;
  /** Start session (from setup state) */
  onStart?: () => void;
  /** Start a rematch (from completed state) */
  onRematch?: () => void;
  /** View play record (from completed state) */
  onViewPlayRecord?: () => void;
}

/** Background color classes for session players */
export const PLAYER_COLOR_BG: Record<string, string> = {
  red: 'bg-red-400',
  blue: 'bg-blue-400',
  green: 'bg-green-400',
  yellow: 'bg-yellow-400',
  purple: 'bg-purple-400',
  orange: 'bg-orange-400',
  pink: 'bg-pink-400',
  teal: 'bg-teal-400',
};

/** Text color classes for session players */
export const PLAYER_COLOR_TEXT: Record<string, string> = {
  red: 'text-red-600',
  blue: 'text-blue-600',
  green: 'text-green-600',
  yellow: 'text-yellow-600',
  purple: 'text-purple-600',
  orange: 'text-orange-600',
  pink: 'text-pink-600',
  teal: 'text-teal-600',
};
