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
export type ExtraMeepleCardTab = 'overview' | 'toolkit' | 'scoreboard' | 'history' | 'media' | 'ai';

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
// Media Tab Types (Issue #4762)
// ============================================================================

/** Media item (photo or note) */
export interface MediaItem {
  id: string;
  type: 'photo' | 'note';
  url?: string;
  thumbnailUrl?: string;
  title?: string;
  content?: string;
  turnNumber?: number;
  createdAt: string;
  createdBy?: string;
}

/** Media tab data */
export interface MediaTabData {
  items: MediaItem[];
  totalPhotos: number;
  totalNotes: number;
}

// ============================================================================
// AI Tab Types (Issue #4762)
// ============================================================================

/** Chat message in the AI tab */
export interface AIChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  sources?: AISource[];
}

/** Source citation from RAG agent */
export interface AISource {
  title: string;
  snippet: string;
  documentId?: string;
}

/** Quick action for the AI tab */
export interface AIQuickAction {
  label: string;
  prompt: string;
  icon?: LucideIcon;
}

/** AI tab data */
export interface AITabData {
  messages: AIChatMessage[];
  sessionContext?: string;
  quickActions: AIQuickAction[];
  isLoading?: boolean;
  agentName?: string;
  agentModel?: string;
}

// ============================================================================
// Entity Variant Types (Issue #4762)
// ============================================================================

/** Entity types supported by ExtraMeepleCard variants */
export type ExtraMeepleCardEntity = 'session' | 'game' | 'player' | 'collection';

/** Game entity detail data */
export interface GameDetailData {
  id: string;
  title: string;
  imageUrl?: string;
  publisher?: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playTimeMinutes?: number;
  description?: string;
  averageRating?: number;
  totalPlays?: number;
  faqCount?: number;
  rulesDocumentCount?: number;
}

/** Player entity detail data */
export interface PlayerDetailData {
  id: string;
  displayName: string;
  avatarUrl?: string;
  gamesPlayed: number;
  winRate: number;
  totalSessions: number;
  favoriteGame?: string;
  achievements: { id: string; name: string; icon: string }[];
  recentGames: { name: string; date: string; result: 'win' | 'loss' | 'draw' }[];
}

/** Collection entity detail data */
export interface CollectionDetailData {
  id: string;
  name: string;
  description?: string;
  imageUrl?: string;
  gameCount: number;
  ownerName: string;
  isShared: boolean;
  games: { id: string; title: string; imageUrl?: string }[];
}

// ============================================================================
// Interactive Card Deck Types (Issue #4763)
// ============================================================================

/** Card zone in the deck system */
export type CardZone = 'deck' | 'hand' | 'discard' | 'play';

/** Single card in the deck */
export interface CardEntry {
  id: string;
  name: string;
  zone: CardZone;
  faceUp: boolean;
  order: number;
}

/** Card deck runtime state */
export interface CardDeckState {
  toolName: string;
  deckType: string;
  cards: CardEntry[];
  deckCount: number;
  handCount: number;
  discardCount: number;
  shuffleable: boolean;
  allowDraw: boolean;
  allowDiscard: boolean;
  allowPeek: boolean;
  allowReturnToDeck: boolean;
}

/** Card deck action handlers */
export interface CardDeckActions {
  onDraw?: () => void;
  onDiscard?: (cardId: string) => void;
  onShuffle?: () => void;
  onPeek?: () => void;
  onReturnToDeck?: (cardId: string) => void;
}

// ============================================================================
// Interactive Timer Types (Issue #4763)
// ============================================================================

/** Timer running state */
export type TimerStatus = 'idle' | 'running' | 'paused' | 'warning' | 'expired';

/** Single timer runtime state */
export interface TimerState {
  toolName: string;
  timerType: string;
  totalSeconds: number;
  remainingSeconds: number;
  status: TimerStatus;
  color?: string;
  isPerPlayer: boolean;
  warningThresholdSeconds?: number;
  /** Per-player remaining seconds (keyed by playerId) */
  playerTimers?: Record<string, { remainingSeconds: number; status: TimerStatus }>;
  activePlayerId?: string;
}

/** Timer action handlers */
export interface TimerActions {
  onStart?: () => void;
  onPause?: () => void;
  onResume?: () => void;
  onReset?: () => void;
}

// ============================================================================
// Events Timeline Types (Issue #4763)
// ============================================================================

/** Extended event types for timeline filtering */
export type SessionEventType = 'system' | 'turn' | 'score' | 'action' | 'media' | 'phase' | 'snapshot' | 'chat';

/** Enhanced timeline event with expanded details */
export interface EnhancedTimelineEvent {
  id: string;
  timestamp: string;
  type: SessionEventType;
  label: string;
  description?: string;
  playerId?: string;
  playerName?: string;
  turnNumber?: number;
  snapshotId?: string;
}

/** Events timeline data */
export interface EventsTimelineData {
  events: EnhancedTimelineEvent[];
  totalEvents: number;
}

/** Events timeline action handlers */
export interface EventsTimelineActions {
  onEventClick?: (event: EnhancedTimelineEvent) => void;
  onNavigateToSnapshot?: (snapshotId: string) => void;
}

// ============================================================================
// Turn Phase Indicator Types (Issue #4763)
// ============================================================================

/** Turn phase state */
export interface TurnPhaseState {
  phases: string[];
  currentPhaseIndex: number;
  currentTurnNumber: number;
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
  /** Media tab data (Issue #4762) */
  mediaData?: MediaTabData;
  /** AI tab data (Issue #4762) */
  aiData?: AITabData;
  /** Callback when user sends an AI message */
  onAISendMessage?: (message: string) => void;

  /** Loading state */
  loading?: boolean;
  /** Error state */
  error?: string;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}
