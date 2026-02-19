/**
 * Session Types for MeepleCard Session Entity
 * Issue #4751 - MeepleCard Session Front + Relationship Links Footer
 *
 * Shared types for session-specific components within the MeepleCard system.
 * Mirrors backend DTOs from GameManagement/Application/DTOs/LiveSessions.
 */

// ============================================================================
// Enums
// ============================================================================

/** Session lifecycle status (mirrors LiveSessionStatus C# enum) */
export type SessionStatus = 'setup' | 'inProgress' | 'paused' | 'completed';

/** Player color (mirrors PlayerColor C# enum) */
export type PlayerColor =
  | 'red'
  | 'blue'
  | 'green'
  | 'yellow'
  | 'purple'
  | 'orange'
  | 'pink'
  | 'teal';

/** Player role within a session (mirrors PlayerRole C# enum) */
export type PlayerRole = 'host' | 'player' | 'spectator';

// ============================================================================
// Data Interfaces
// ============================================================================

/** Player in a live session */
export interface SessionPlayerInfo {
  id: string;
  userId?: string;
  displayName: string;
  avatarUrl?: string;
  color: PlayerColor;
  role: PlayerRole;
  teamId?: string;
  totalScore: number;
  currentRank: number;
  isActive: boolean;
}

/** Round score entry */
export interface SessionRoundScore {
  playerId: string;
  round: number;
  dimension: string;
  value: number;
  unit?: string;
}

/** Scoring configuration */
export interface SessionScoringConfig {
  enabledDimensions: string[];
  dimensionUnits: Record<string, string>;
}

/** Turn state info */
export interface SessionTurnInfo {
  currentIndex: number;
  currentPlayerId?: string;
}

/** Session action handlers (context-sensitive by status) */
export interface SessionActionHandlers {
  onPause?: () => void;
  onResume?: () => void;
  onSave?: () => void;
  onStart?: () => void;
  onAddPlayer?: () => void;
  onViewPlayRecord?: () => void;
  onRematch?: () => void;
  onEditScore?: () => void;
}

// ============================================================================
// Color Mapping
// ============================================================================

/** HSL values for player colors */
export const PLAYER_COLOR_MAP: Record<PlayerColor, string> = {
  red: '0 84% 60%',
  blue: '217 91% 60%',
  green: '142 71% 45%',
  yellow: '48 96% 53%',
  purple: '262 83% 58%',
  orange: '25 95% 53%',
  pink: '330 81% 60%',
  teal: '174 72% 40%',
};

/** Tailwind bg classes for player colors */
export const PLAYER_COLOR_BG: Record<PlayerColor, string> = {
  red: 'bg-red-500',
  blue: 'bg-blue-500',
  green: 'bg-green-600',
  yellow: 'bg-yellow-400',
  purple: 'bg-purple-500',
  orange: 'bg-orange-500',
  pink: 'bg-pink-500',
  teal: 'bg-teal-500',
};

/** Tailwind text classes for player colors */
export const PLAYER_COLOR_TEXT: Record<PlayerColor, string> = {
  red: 'text-red-500',
  blue: 'text-blue-500',
  green: 'text-green-600',
  yellow: 'text-yellow-500',
  purple: 'text-purple-500',
  orange: 'text-orange-500',
  pink: 'text-pink-500',
  teal: 'text-teal-500',
};
