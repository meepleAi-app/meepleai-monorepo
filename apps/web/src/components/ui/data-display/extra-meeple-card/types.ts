/**
 * ExtraMeepleCard Types
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 *
 * Types for the expanded card component used in session detail views.
 * ~600×900px card with 4-tab system (Overview, Toolkit, Scoreboard, History).
 */

import type {
  SessionStatus,
  SessionPlayerInfo,
  SessionRoundScore,
  SessionScoringConfig,
  SessionTurnInfo,
  SessionTimelineEvent,
  SessionMediaCounts,
  SessionActionHandlers,
} from '../meeple-card-features/session-types';
import type { LucideIcon } from 'lucide-react';

// Re-export session types for convenience
export type {
  SessionStatus,
  SessionPlayerInfo,
  SessionRoundScore,
  SessionScoringConfig,
  SessionTurnInfo,
  SessionTimelineEvent,
  SessionMediaCounts,
  SessionActionHandlers,
};

// ============================================================================
// Tab System
// ============================================================================

/** Available tabs in the ExtraMeepleCard */
export type ExtraMeepleCardTab = 'overview' | 'toolkit' | 'scoreboard' | 'history';

/** Tab configuration */
export interface TabConfig {
  id: ExtraMeepleCardTab;
  label: string;
  icon: LucideIcon;
  badge?: number;
}

// ============================================================================
// Toolkit Types (mirrors backend GameToolkit DTOs)
// ============================================================================

/** Dice tool configuration */
export interface DiceToolInfo {
  name: string;
  diceType: string;
  quantity: number;
  customFaces?: string[];
  isInteractive: boolean;
  color?: string;
}

/** Card tool configuration */
export interface CardToolInfo {
  name: string;
  deckType: string;
  cardCount: number;
  shuffleable: boolean;
  allowDraw: boolean;
  allowDiscard: boolean;
  allowPeek: boolean;
  allowReturnToDeck: boolean;
}

/** Timer tool configuration */
export interface TimerToolInfo {
  name: string;
  durationSeconds: number;
  timerType: string;
  autoStart: boolean;
  color?: string;
  isPerPlayer: boolean;
  warningThresholdSeconds?: number;
}

/** Counter tool configuration */
export interface CounterToolInfo {
  name: string;
  minValue: number;
  maxValue: number;
  defaultValue: number;
  isPerPlayer: boolean;
  icon?: string;
  color?: string;
}

/** Scoring template */
export interface ScoringTemplateInfo {
  dimensions: string[];
  defaultUnit: string;
  scoreType: string;
}

/** Turn template */
export interface TurnTemplateInfo {
  turnOrderType: string;
  phases: string[];
}

/** Complete toolkit data */
export interface ToolkitData {
  id: string;
  name: string;
  version: number;
  isPublished: boolean;
  diceTools: DiceToolInfo[];
  cardTools: CardToolInfo[];
  timerTools: TimerToolInfo[];
  counterTools: CounterToolInfo[];
  scoringTemplate?: ScoringTemplateInfo;
  turnTemplate?: TurnTemplateInfo;
}

// ============================================================================
// Snapshot Types (mirrors backend SessionSnapshot DTOs)
// ============================================================================

/** Session snapshot for history tab */
export interface SnapshotInfo {
  id: string;
  snapshotNumber: number;
  triggerType: string;
  description: string;
  turnNumber?: number;
  createdAt: string;
}

// ============================================================================
// Tab Content Data
// ============================================================================

/** Overview tab data */
export interface OverviewTabData {
  gameName: string;
  gameImageUrl?: string;
  status: SessionStatus;
  startedAt: string;
  completedAt?: string;
  durationMinutes?: number;
  sessionCode?: string;
  players: SessionPlayerInfo[];
  turnInfo?: SessionTurnInfo;
  currentRound?: number;
  totalRounds?: number;
}

/** Scoreboard tab data */
export interface ScoreboardTabData {
  players: SessionPlayerInfo[];
  roundScores: SessionRoundScore[];
  scoringConfig?: SessionScoringConfig;
  totalRounds?: number;
}

/** History tab data */
export interface HistoryTabData {
  snapshots: SnapshotInfo[];
  timeline: SessionTimelineEvent[];
  totalTurns?: number;
}

// ============================================================================
// Component Props
// ============================================================================

/** ExtraMeepleCard component props */
export interface ExtraMeepleCardProps {
  /** Session ID */
  sessionId: string;
  /** Session title (game name or custom) */
  title: string;
  /** Session status */
  status: SessionStatus;
  /** Game cover image URL */
  imageUrl?: string;
  /** Active elapsed time display (mm:ss or hh:mm:ss) */
  elapsedTime?: string;
  /** Player count */
  playerCount: number;
  /** Current tab */
  activeTab?: ExtraMeepleCardTab;
  /** Tab change callback */
  onTabChange?: (tab: ExtraMeepleCardTab) => void;
  /** Action handlers */
  actions?: SessionActionHandlers;
  /** Tab badge counts */
  tabBadges?: Partial<Record<ExtraMeepleCardTab, number>>;

  /** Overview tab data (inline or via hook) */
  overviewData?: OverviewTabData;
  /** Toolkit data */
  toolkitData?: ToolkitData;
  /** Scoreboard data */
  scoreboardData?: ScoreboardTabData;
  /** History data */
  historyData?: HistoryTabData;

  /** Loading state */
  loading?: boolean;
  /** Error state */
  error?: string;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}
