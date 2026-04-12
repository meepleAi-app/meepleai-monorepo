/**
 * Toolkit Drawer — Shared Types
 *
 * Type definitions and constants for the Default Game Toolkit drawer.
 * Used across the store, API client, provider, and tab components.
 */

// ============================================================================
// Tab Navigation
// ============================================================================

export type ToolkitTab = 'dice' | 'timer' | 'notes' | 'diary' | 'scores';

// ============================================================================
// Diary Event Types
// ============================================================================

export type DiaryEventType =
  | 'dice_roll'
  | 'score_change'
  | 'turn_change'
  | 'note_added'
  | 'manual_entry'
  | 'player_joined'
  | 'round_advance'
  | 'score_reset'
  | 'timer_end';

// ============================================================================
// Player
// ============================================================================

export interface LocalPlayer {
  id: string;
  name: string;
  color: string;
  avatarUrl?: string;
}

// ============================================================================
// Diary
// ============================================================================

export interface DiaryEvent {
  id: string;
  type: DiaryEventType;
  timestamp: number;
  playerId?: string;
  playerName?: string;
  round?: number;
  payload: Record<string, unknown>;
}

// ============================================================================
// Dice
// ============================================================================

export interface DiceResult {
  formula: string;
  rolls: number[];
  modifier: number;
  total: number;
}

export interface DicePreset {
  name: string;
  formula: string;
  source: 'universal' | 'ai' | 'custom';
  icon?: string;
}

// ============================================================================
// Notes
// ============================================================================

export interface LocalNote {
  id: string;
  content: string;
  type: 'shared' | 'private';
  playerId?: string;
  playerName?: string;
  pinned: boolean;
  createdAt: number;
  updatedAt: number;
}

// ============================================================================
// Drawer Props
// ============================================================================

export interface ToolkitDrawerProps {
  gameId: string;
  sessionId?: string;
  onClose: () => void;
  defaultTab?: ToolkitTab;
}

// ============================================================================
// Constants
// ============================================================================

/** 8 distinct player colors for the toolkit */
export const PLAYER_COLORS = [
  '#E67E22', // orange
  '#9B59B6', // purple
  '#2ECC71', // green
  '#3498DB', // blue
  '#E74C3C', // red
  '#F1C40F', // yellow
  '#1ABC9C', // teal
  '#E84393', // pink
] as const;

/** Built-in dice presets available to all users */
export const UNIVERSAL_DICE_PRESETS: DicePreset[] = [
  { name: '1d6', formula: '1d6', source: 'universal' },
  { name: '2d6', formula: '2d6', source: 'universal' },
  { name: '1d20', formula: '1d20', source: 'universal' },
  { name: '3d6', formula: '3d6', source: 'universal' },
  { name: '1d100', formula: '1d100', source: 'universal' },
];
