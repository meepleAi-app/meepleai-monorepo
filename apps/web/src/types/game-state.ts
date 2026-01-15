/**
 * Game State Types
 * Issue #2406: Game State Editor UI
 *
 * TypeScript types for game state management aligned with backend GameStateTemplate.
 */

import { JSONSchema7 } from 'json-schema';

/**
 * Generation source for state templates
 */
export type GenerationSource = 'AI' | 'Manual';

/**
 * Game State Template from backend
 */
export interface GameStateTemplate {
  id: string;
  sharedGameId: string;
  name: string;
  schemaJson: string | null;
  version: string;
  isActive: boolean;
  source: GenerationSource;
  confidenceScore: number | null;
  generatedAt: string;
  createdBy: string;
}

/**
 * Parsed JSON Schema from template
 */
export type GameStateSchema = JSONSchema7;

/**
 * Player state in game session
 */
export interface PlayerState {
  playerName: string;
  playerOrder: number;
  color?: string;
  score?: number;
  resources?: Record<string, number>;
  [key: string]: unknown; // Allow additional properties
}

/**
 * Complete game state for a session
 */
export interface GameState {
  sessionId: string;
  gameId: string;
  templateId: string;
  version: string;
  phase?: string;
  currentPlayerIndex?: number;
  roundNumber?: number;
  players: PlayerState[];
  globalResources?: Record<string, number>;
  metadata?: Record<string, unknown>;
  [key: string]: unknown; // Allow additional properties from schema
}

/**
 * State snapshot for history/undo
 */
export interface GameStateSnapshot {
  id: string;
  state: GameState;
  timestamp: string;
  userId: string;
  action: string; // Description of what changed
}

/**
 * State change operation for real-time sync
 */
export interface GameStateChange {
  sessionId: string;
  path: string[]; // JSON path to changed value
  oldValue: unknown;
  newValue: unknown;
  userId: string;
  timestamp: string;
}

/**
 * Conflict detection for concurrent edits
 */
export interface StateConflict {
  path: string[];
  localValue: unknown;
  remoteValue: unknown;
  timestamp: string;
}

/**
 * Undo/Redo stack entry
 */
export interface UndoRedoEntry {
  state: GameState;
  description: string;
  timestamp: string;
}

/**
 * Real-time update message from SignalR
 */
export interface StateUpdateMessage {
  type: 'state-changed' | 'conflict-detected' | 'snapshot-created';
  sessionId: string;
  data: GameState | StateConflict | GameStateSnapshot;
  userId: string;
  timestamp: string;
}
